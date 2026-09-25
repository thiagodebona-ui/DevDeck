using System.Runtime.InteropServices;

namespace DevDeck.Core
{
    /// <summary>
    ///  Makes a window's taskbar button ask for attention, on Windows.
    /// </summary>
    /// <remarks>
    ///  The fallback for the platform where <see cref="Notifier"/> has nothing: a real Windows
    ///  toast needs a registered application identity, which is a per-install arrangement this app
    ///  does not make, but flashing a taskbar button needs nothing at all.
    ///
    ///  Deliberately not activation. Pulling focus away from whatever the user moved on to is a
    ///  worse interruption than the one being announced - the point is to be noticeable from where
    ///  they are, not to drag them back.
    /// </remarks>
    internal static class TaskbarFlash
    {
        private const uint FlashTray = 0x00000002;

        /// <summary>Keep flashing until the window is brought to the front.</summary>
        private const uint UntilForeground = 0x0000000C;

        [StructLayout(LayoutKind.Sequential)]
        private struct FlashInfo
        {
            public uint Size;
            public IntPtr Window;
            public uint Flags;
            public uint Count;
            public uint Timeout;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FlashInfo info);

        /// <summary>Flashes the given window, and says whether it could.</summary>
        public static bool Ask(IntPtr window)
        {
            if (!OperatingSystem.IsWindows() || window == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                FlashInfo info = new()
                {
                    Size = (uint)Marshal.SizeOf<FlashInfo>(),
                    Window = window,
                    Flags = FlashTray | UntilForeground,

                    // Zero with UntilForeground means "for as long as it takes", which is what is
                    // wanted: the user is elsewhere and there is no knowing for how long.
                    Count = 0,
                    Timeout = 0,
                };

                return FlashWindowEx(ref info);
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
