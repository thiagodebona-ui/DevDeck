namespace DevDeck.Core
{
    /// <summary>
    ///  The subset of System.Windows.Forms.Keys that a hotkey needs, with the original values.
    /// </summary>
    /// <remarks>
    ///  AppSettings stores a hotkey as a single int, and V2 built that int out of the WinForms
    ///  Keys enum. Keeping the same numbers means a settings.json is read the same way by both
    ///  versions, and it lets AppSettings.cs come across from V2 untouched.
    ///
    ///  Avalonia has its own Key and KeyModifiers enums with different values; converting between
    ///  those and this belongs in the UI project, not here.
    /// </remarks>
    [Flags]
    internal enum Keys
    {
        None = 0,

        // Virtual-key codes, which occupy the low word.
        D0 = 48, D1 = 49, D2 = 50, D3 = 51, D4 = 52,
        D5 = 53, D6 = 54, D7 = 55, D8 = 56, D9 = 57,

        A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73,
        J = 74, K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82,
        S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,

        F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117,
        F7 = 118, F8 = 119, F9 = 120, F10 = 121, F11 = 122, F12 = 123,

        Space = 32,
        Insert = 45,
        Delete = 46,
        Home = 36,
        End = 35,
        PageUp = 33,
        PageDown = 34,
        Escape = 27,
        Pause = 19,

        /// <summary>Everything below this is a modifier bit; everything above is the key itself.</summary>
        KeyCode = 0xFFFF,

        Shift = 0x10000,
        Control = 0x20000,
        Alt = 0x40000,

        Modifiers = Shift | Control | Alt,
    }
}
