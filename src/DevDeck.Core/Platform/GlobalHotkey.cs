using System.Runtime.InteropServices;

namespace DevDeck.Core
{
    /// <summary>
    ///  A key combination that works when the app is not in front.
    /// </summary>
    /// <remarks>
    ///  The deck's whole proposition is that a command is one keystroke away, and a keystroke that
    ///  only works once you have found the window is not one keystroke. This is the difference
    ///  between an app you open and a tool that is simply there.
    ///
    ///  Only Windows registers a key here, and that is an honest limit rather than an oversight:
    ///  <list type="bullet">
    ///   <item>Windows has one call for it, <c>RegisterHotKey</c>, and it works from any process.</item>
    ///   <item>macOS requires either a Carbon event handler from a bundled app or accessibility
    ///    permission the user must grant in System Settings, neither of which a running process can
    ///    arrange for itself.</item>
    ///   <item>Linux has no such thing at all: hotkeys belong to the desktop environment, and the
    ///    supported way to add one is to tell that environment which command to run.</item>
    ///  </list>
    ///
    ///  Where a key cannot be registered the app says so and points at the thing that does work
    ///  everywhere - <c>devdeck show</c> and the <c>devdeck://</c> scheme, which every desktop
    ///  environment's own keyboard settings can bind. That is a better answer than a half-working
    ///  hotkey, because the user ends up with a binding their system knows about.
    /// </remarks>
    internal static class GlobalHotkey
    {
        /// <summary>The id this app uses. Any value will do; it only has to be ours.</summary>
        private const int Id = 0xD3CC;

        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;
        private const uint ModShift = 0x0004;
        private const uint ModWin = 0x0008;

        /// <summary>Stops Windows repeating the hotkey while the keys are held down.</summary>
        private const uint ModNoRepeat = 0x4000;

        private const int WmHotkey = 0x0312;

        private static Thread? pump;
        private static uint pumpThread;
        private static Action? pressed;

        /// <summary>Whether a key can be registered on this system at all.</summary>
        public static bool IsAvailable => OperatingSystem.IsWindows();

        /// <summary>What the app says when it cannot register one.</summary>
        public static string Unavailable => OperatingSystem.IsMacOS()
            ? "macOS does not let a running app claim a system-wide key without accessibility "
                + "permission. Bind a shortcut to \"devdeck show\" in System Settings instead - "
                + "write the terminal launcher above first."
            : "Linux keeps hotkeys in the desktop environment rather than in applications. Bind a "
                + "shortcut to \"devdeck show\" in your keyboard settings - write the terminal "
                + "launcher above first.";

        /// <summary>The combination in force, or empty.</summary>
        public static string Current { get; private set; } = string.Empty;

        /// <summary>
        ///  Claims a key combination, replacing whatever was claimed before.
        /// </summary>
        /// <remarks>
        ///  Returns false rather than throwing when the combination is already taken by something
        ///  else, because that is an ordinary outcome and not a fault: every desktop has a handful
        ///  of combinations already spoken for, and the answer is to pick another one.
        /// </remarks>
        public static bool Register(string combination, Action onPressed)
        {
            Unregister();

            if (!IsAvailable || !Parse(combination, out uint modifiers, out uint key))
            {
                return false;
            }

            pressed = onPressed;

            // The registration and the message loop have to be on the same thread: WM_HOTKEY is
            // posted to the thread that registered, not to a window, so the thread that asks for
            // the key is the only one that can ever hear it.
            using ManualResetEventSlim ready = new(false);

            bool claimed = false;

            pump = new Thread(() =>
            {
                claimed = RegisterHotKey(IntPtr.Zero, Id, modifiers | ModNoRepeat, key);

                pumpThread = GetCurrentThreadId();

                ready.Set();

                if (!claimed)
                {
                    return;
                }

                while (GetMessage(out Message message, IntPtr.Zero, 0, 0) > 0)
                {
                    if (message.Value == WmHotkey && message.WParam.ToInt32() == Id)
                    {
                        // Straight through. The handler posts to the UI thread itself; doing that
                        // here would put a UI dependency in Core.
                        pressed?.Invoke();
                    }
                }

                UnregisterHotKey(IntPtr.Zero, Id);
            })
            {
                IsBackground = true,
                Name = "devdeck-hotkey",
            };

            pump.Start();

            // Waited on rather than assumed: the caller needs to know whether the key is theirs
            // before it tells the user it is.
            ready.Wait(TimeSpan.FromSeconds(2));

            if (claimed)
            {
                Current = combination;

                AppLog.Instance.Info("hotkey", $"{combination} is registered.");
            }
            else
            {
                pump = null;
                pressed = null;

                AppLog.Instance.Warn("hotkey", $"Something else already has {combination}.");
            }

            return claimed;
        }

        /// <summary>Gives the key back.</summary>
        public static void Unregister()
        {
            if (pump is null)
            {
                return;
            }

            // Ends GetMessage, which lets the thread run its own UnregisterHotKey and exit. Posting
            // a quit is the only way to stop a message loop from outside it.
            if (pumpThread != 0)
            {
                PostThreadMessage(pumpThread, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
            }

            pump = null;
            pumpThread = 0;
            pressed = null;
            Current = string.Empty;
        }

        /// <summary>
        ///  Reads "Ctrl+Shift+D" into the pair Windows wants.
        /// </summary>
        /// <remarks>
        ///  Deliberately strict about needing a modifier. A bare letter as a global hotkey would
        ///  swallow that letter in every other application on the machine, which is a thing the
        ///  user would experience as their keyboard being broken.
        /// </remarks>
        public static bool Parse(string combination, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;

            if (string.IsNullOrWhiteSpace(combination))
            {
                return false;
            }

            string[] parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (string part in parts)
            {
                switch (part.ToLowerInvariant())
                {
                    case "ctrl" or "control": modifiers |= ModControl; break;
                    case "alt": modifiers |= ModAlt; break;
                    case "shift": modifiers |= ModShift; break;
                    case "win" or "cmd" or "super": modifiers |= ModWin; break;

                    default:
                        if (key != 0 || !Key(part, out key))
                        {
                            return false;
                        }

                        break;
                }
            }

            return modifiers != 0 && key != 0;
        }

        /// <summary>The virtual-key code for a named key.</summary>
        private static bool Key(string name, out uint code)
        {
            code = 0;

            string key = name.ToUpperInvariant();

            if (key.Length == 1 && (char.IsAsciiLetter(key[0]) || char.IsAsciiDigit(key[0])))
            {
                // Virtual-key codes for letters and digits are their ASCII values, which is the one
                // convenient thing about this API.
                code = key[0];

                return true;
            }

            if (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key[1..], out int number)
                && number is >= 1 and <= 24)
            {
                code = (uint)(0x70 + number - 1);

                return true;
            }

            code = key switch
            {
                "SPACE" => 0x20,
                "ENTER" or "RETURN" => 0x0D,
                "TAB" => 0x09,
                "ESC" or "ESCAPE" => 0x1B,
                "INSERT" => 0x2D,
                "DELETE" => 0x2E,
                "HOME" => 0x24,
                "END" => 0x23,
                "PAGEUP" => 0x21,
                "PAGEDOWN" => 0x22,
                "`" or "BACKTICK" => 0xC0,
                _ => 0,
            };

            return code != 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Message
        {
            public IntPtr Window;
            public uint Value;
            public IntPtr WParam;
            public IntPtr LParam;
            public uint Time;
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr window, int id);

        [DllImport("user32.dll")]
        private static extern int GetMessage(out Message message, IntPtr window, uint first, uint last);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
    }
}
