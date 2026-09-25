using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DevDeck.Core
{
    /// <summary>
    ///  Stops the machine sleeping, blanking its screen or locking itself while the deck is open.
    /// </summary>
    /// <remarks>
    ///  V2 had this and V3 shipped the setting without the code behind it, so the tick box saved a
    ///  flag that nothing ever read. This is the missing half.
    ///
    ///  There are two separate problems here and only one of them is about power:
    ///  <list type="bullet">
    ///   <item><b>Sleeping and blanking</b> are a power policy, and every platform has a documented
    ///    way to ask for a reprieve - <c>SetThreadExecutionState</c>, <c>caffeinate</c>,
    ///    <c>systemd-inhibit</c>.</item>
    ///   <item><b>Locking</b> very often is not. A managed Windows machine locks on the
    ///    "machine inactivity limit" policy, which reads how long since the OS last saw a keypress
    ///    and does not consult the power request at all. The same clock is what Teams and Slack
    ///    read to decide you are "Away". A power request alone therefore looks like it is doing
    ///    nothing, which is exactly the symptom this was reported as.</item>
    ///  </list>
    ///
    ///  So the second half exists too: while the machine has been genuinely quiet, one F15 is
    ///  injected every minute. F15 is chosen because no keyboard has shipped with one for decades
    ///  and nothing maps it - the cursor never moves and no application sees a key it reacts to.
    ///  It only runs when the user has already been idle for <see cref="Quiet"/>, so it never
    ///  competes for the keyboard while they are actually typing.
    ///
    ///  <b>Thread affinity is the reason this owns a thread.</b> <c>SetThreadExecutionState</c>
    ///  sets state for the calling thread and that state dies with the thread. Called from a pool
    ///  thread it would be silently dropped seconds later, which is a far worse bug than the one
    ///  being fixed because it would appear to work. One long-lived thread makes both calls, so
    ///  what was asserted stays asserted.
    /// </remarks>
    internal static class KeepAwake
    {
        #region Windows imports

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern ExecutionState SetThreadExecutionState(ExecutionState flags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LastInputInfo info);

        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;

        /// <summary>F15. Nothing maps it, so nothing reacts to it.</summary>
        private const ushort VkF15 = 0x7E;

        [StructLayout(LayoutKind.Sequential)]
        private struct LastInputInfo
        {
            public uint Size;
            public uint Time;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int X;
            public int Y;
            public uint Data;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeyboardInput
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HardwareInput
        {
            public uint Message;
            public ushort ParamLow;
            public ushort ParamHigh;
        }

        // SendInput's INPUT is a tagged union. Only the keyboard arm is ever filled in, but the
        // whole union has to be laid out or the struct is the wrong size and SendInput rejects it.
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeyboardInput Keyboard;
            [FieldOffset(0)] public HardwareInput Hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion Union;
        }

        [Flags]
        private enum ExecutionState : uint
        {
            None = 0,
            SystemRequired = 0x00000001,
            DisplayRequired = 0x00000002,
            Continuous = 0x80000000,
        }

        #endregion

        /// <summary>How quiet the machine must be before an injected key is appropriate.</summary>
        /// <remarks>
        ///  Shorter than any lock policy worth defeating and longer than a pause for thought. While
        ///  the user is working their own keystrokes keep resetting this and nothing is sent.
        /// </remarks>
        public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(45);

        /// <summary>How often the request is re-asserted and the idle clock checked.</summary>
        private static readonly TimeSpan Beat = TimeSpan.FromSeconds(30);

        private static readonly object Gate = new();
        private static readonly AutoResetEvent Wake = new(false);

        private static Thread? keeper;
        private static bool wanted;
        private static bool presence;
        private static Process? helper;

        /// <summary>Whether anything on this platform can hold the machine awake.</summary>
        public static bool IsAvailable =>
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();

        /// <summary>Whether the request is currently in force.</summary>
        public static bool Active { get; private set; }

        /// <summary>
        ///  Whether the idle clock is also being held back.
        /// </summary>
        /// <remarks>
        ///  Windows only. macOS and Linux have no injection call that works without the user first
        ///  granting the app the right to type on their behalf, and asking for that in order to
        ///  press a key nobody has is a trade not worth offering.
        /// </remarks>
        public static bool IsPresenceAvailable => OperatingSystem.IsWindows();

        /// <summary>What actually happened, in one line, for the settings page and the log.</summary>
        public static string Detail { get; private set; } = "Off.";

        /// <summary>
        ///  Turns the request on or off.
        /// </summary>
        /// <param name="enabled">Whether to hold the machine awake at all.</param>
        /// <param name="stayAvailable">
        ///  Whether to also hold back the idle clock, which is the half that defeats an inactivity
        ///  lock policy and keeps chat apps showing you as available. Ignored when
        ///  <paramref name="enabled"/> is false, because it is the same feature seen from the other
        ///  end and a machine allowed to sleep should not be poked awake.
        /// </param>
        /// <remarks>
        ///  Never throws. A request the OS refuses is reported through <see cref="Detail"/> and
        ///  logged - a settings tick box is the wrong place to raise an exception, and the last
        ///  version of this bug was precisely a failure nobody was told about.
        /// </remarks>
        public static void Set(bool enabled, bool stayAvailable)
        {
            lock (Gate)
            {
                wanted = enabled && IsAvailable;
                presence = wanted && stayAvailable && IsPresenceAvailable;

                if (wanted && keeper is null)
                {
                    // Background, so a forgotten thread can never hold the process open, and named
                    // so it is identifiable in a debugger next to the pool.
                    keeper = new Thread(Hold)
                    {
                        IsBackground = true,
                        Name = "DevDeck keep-awake",
                    };

                    keeper.Start();
                }
            }

            Wake.Set();
        }

        /// <summary>Releases the request. Called on the way out.</summary>
        /// <remarks>
        ///  Windows would drop the request when the process ends anyway, but the external helpers
        ///  on macOS and Linux are separate processes and would outlive it. Left behind, they would
        ///  hold the machine awake with nothing on screen to explain why.
        /// </remarks>
        public static void Release()
        {
            Set(false, false);

            Stop();
        }

        /// <summary>
        ///  How long since the OS last saw input from anyone.
        /// </summary>
        /// <remarks>
        ///  Fails open. Reporting zero on an error would read as "the user is busy" and switch the
        ///  nudge off for good - the same silent nothing being fixed here. An unnecessary F15 costs
        ///  nothing at all.
        /// </remarks>
        public static TimeSpan Idle
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return TimeSpan.MaxValue;
                }

                LastInputInfo info = new() { Size = (uint)Marshal.SizeOf<LastInputInfo>() };

                if (!GetLastInputInfo(ref info))
                {
                    return TimeSpan.MaxValue;
                }

                // Both clocks wrap every 49.7 days. Unchecked unsigned subtraction is right across
                // the wrap; signed arithmetic would briefly report about 49 days of idleness.
                return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.Time));
            }
        }

        /// <summary>
        ///  Whether a nudge is due, given how long the machine has been quiet.
        /// </summary>
        /// <remarks>
        ///  Pulled out as a plain function so the rule can be tested without a desktop: the
        ///  interesting part of this feature is when it decides to act, and that decision should
        ///  not only be exercised by leaving a machine alone for a minute and watching.
        /// </remarks>
        public static bool ShouldNudge(bool enabled, bool stayAvailable, TimeSpan idle) =>
            enabled && stayAvailable && idle >= Quiet;

        /// <summary>
        ///  Says what the feature is doing, for the settings page.
        /// </summary>
        /// <remarks>
        ///  Always says something, including when it is off. A readout that goes blank leaves the
        ///  user with the same question they started with, which is how this was reported: the
        ///  switch was on and there was no way to tell it was doing nothing.
        /// </remarks>
        public static string Describe(bool enabled, bool applied, bool presenceOn) =>
            !enabled ? "Off. The machine follows its own power and lock settings."
            : !applied ? "Asked for, but this system refused it. The log has the detail."
            : presenceOn ? "On. The machine will not sleep, blank or lock itself, and chat apps will keep showing you as available."
            : "On. The machine will not sleep or blank its screen.";

        /// <summary>
        ///  The thread that owns the request.
        /// </summary>
        /// <remarks>
        ///  It re-asserts on every beat rather than once at the start. The call is cheap and
        ///  idempotent, and something else on the machine - a power plan change, another app
        ///  clearing its own request badly - can leave the state not saying what we think it says.
        ///  Re-asserting means the worst case is half a minute of drift instead of a feature that
        ///  quietly stopped hours ago.
        /// </remarks>
        private static void Hold()
        {
            bool held = false;

            while (true)
            {
                bool on;
                bool nudging;

                lock (Gate)
                {
                    on = wanted;
                    nudging = presence;
                }

                if (on)
                {
                    bool applied = Assert();

                    if (!held || !applied)
                    {
                        Detail = Describe(true, applied, nudging);

                        if (!held && applied)
                        {
                            AppLog.Instance.Good("awake", Detail);
                        }
                    }

                    held = true;
                    Active = applied;

                    if (ShouldNudge(true, nudging, Idle))
                    {
                        Nudge();
                    }
                }
                else if (held)
                {
                    Clear();

                    held = false;
                    Active = false;
                    Detail = Describe(false, false, false);

                    AppLog.Instance.Info("awake", "Keep-awake is off; the machine follows its own settings again.");
                }

                Wake.WaitOne(Beat);
            }
        }

        /// <summary>Asks this platform to stay awake, by whatever means it offers.</summary>
        private static bool Assert()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Continuous makes it a standing request rather than a one-off reset of the
                    // idle timers, which is the difference between "do not sleep" and "not yet".
                    return SetThreadExecutionState(
                        ExecutionState.Continuous
                        | ExecutionState.SystemRequired
                        | ExecutionState.DisplayRequired) != ExecutionState.None;
                }

                // macOS and Linux both answer this with a process that holds the reprieve for as
                // long as it lives, so the assertion is "is ours still running" rather than a call.
                if (helper is { HasExited: false })
                {
                    return true;
                }

                helper?.Dispose();
                helper = Spawn();

                return helper is not null;
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("awake", "Could not hold the machine awake", exception);

                return false;
            }
        }

        /// <summary>
        ///  Starts the platform's own inhibitor.
        /// </summary>
        /// <remarks>
        ///  Both are shipped with their systems rather than installed, but neither is guaranteed -
        ///  <c>caffeinate</c> is absent from some minimal images and <c>systemd-inhibit</c> means
        ///  nothing on a machine that does not run systemd. A missing binary is reported as a
        ///  refusal rather than thrown, because it is an ordinary property of the host.
        /// </remarks>
        private static Process? Spawn()
        {
            ProcessStartInfo start = OperatingSystem.IsMacOS()
                // -d display, -i idle sleep, -m disk, -s system. Held until the process is killed.
                ? new ProcessStartInfo("caffeinate", "-dims")
                : new ProcessStartInfo("systemd-inhibit",
                    "--what=idle:sleep --who=DevDeck --why=\"DevDeck is open\" --mode=block "
                    + "sleep infinity");

            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;

            Process? started = Process.Start(start);

            if (started is null)
            {
                AppLog.Instance.Warn("awake", $"{start.FileName} would not start; the machine will follow its own settings.");
            }

            return started;
        }

        /// <summary>Gives the machine back its own power settings.</summary>
        private static void Clear()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    // Continuous on its own, with neither requirement: the standing request is
                    // withdrawn without also counting as one last "stay up".
                    SetThreadExecutionState(ExecutionState.Continuous);

                    return;
                }

                Stop();
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("awake", "Could not release the machine", exception);
            }
        }

        private static void Stop()
        {
            try
            {
                if (helper is { HasExited: false } running)
                {
                    running.Kill(entireProcessTree: true);
                    running.WaitForExit(2000);
                }

                helper?.Dispose();
                helper = null;
            }
            catch (Exception)
            {
                // A helper that has already gone, or that we cannot signal, is not worth a message
                // on the way out of the app.
                helper = null;
            }
        }

        /// <summary>
        ///  Resets the idle clock with one key nobody is listening for.
        /// </summary>
        /// <remarks>
        ///  Cannot work while the workstation is locked - Windows refuses injected input into the
        ///  secure desktop - which is why this holds a lock off rather than undoing one.
        /// </remarks>
        private static bool Nudge()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            try
            {
                Input[] inputs =
                [
                    new()
                    {
                        Type = InputKeyboard,
                        Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = VkF15 } },
                    },
                    new()
                    {
                        Type = InputKeyboard,
                        Union = new InputUnion
                        {
                            Keyboard = new KeyboardInput { VirtualKey = VkF15, Flags = KeyEventKeyUp },
                        },
                    },
                ];

                return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("awake", "Could not hold back the idle clock", exception);

                return false;
            }
        }
    }
}
