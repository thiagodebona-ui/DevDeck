namespace DevDeck.Core
{
    /// <summary>
    ///  A living layer drawn behind the whole window, under every panel.
    /// </summary>
    /// <remarks>
    ///  Stored by name. Each one paints with the palette's own brushes - Accent, the syntax
    ///  colours, Text - rather than colours of its own, so an effect picked separately from the
    ///  theme still belongs to it.
    /// </remarks>
    internal enum ThemeBackdrop
    {
        None,

        /// <summary>Slow drifting curtains of colour.</summary>
        Aurora,

        /// <summary>Columns of falling glyphs.</summary>
        Rain,

        /// <summary>Three layers of stars drifting past, the odd shooting star.</summary>
        Stars,

        /// <summary>A neon grid rolling towards a striped sun on the horizon.</summary>
        Grid,

        /// <summary>Sparks rising and flickering out.</summary>
        Embers,

        /// <summary>Snow drifting down and swaying.</summary>
        Snow,

        /// <summary>Petals tumbling across the window.</summary>
        Petals,
    }

    /// <summary>The background-effect choice, which is either "whatever the theme brings" or one effect.</summary>
    internal static class ThemeEffect
    {
        /// <summary>What the setting holds when the theme decides.</summary>
        public const string MatchTheme = "Theme";

        public static IReadOnlyList<string> Choices { get; } =
            [MatchTheme, .. Enum.GetNames<ThemeBackdrop>()];

        /// <summary>The effect to draw, given the setting and the theme being worn.</summary>
        public static ThemeBackdrop Resolve(string? setting, ThemePalette palette) =>
            Enum.TryParse(setting, ignoreCase: true, out ThemeBackdrop chosen)
                ? chosen
                : palette.Backdrop;
    }

    /// <summary>
    ///  One named palette the app can wear.
    /// </summary>
    /// <remarks>
    ///  <see cref="IsDark"/> is not decoration. Avalonia's Fluent theme draws everything we do not
    ///  draw ourselves - scrollbars, combo popups, the caret, tick marks - and it picks those from
    ///  its own variant rather than from our brushes. A palette therefore has to say which variant
    ///  it belongs to, or a light palette ends up with dark scrollbars down the side of it.
    /// </remarks>
    internal sealed record ThemePalette(
        string Id, string Name, bool IsDark, ThemeBackdrop Backdrop = ThemeBackdrop.None, bool Glow = false)
    {
        /// <summary>The resource dictionary behind this palette, under Themes/.</summary>
        public string Source => $"avares://DevDeck/Themes/{Id}.axaml";

        /// <summary>Shown in the picker; ToString is what a bare ComboBox binds to.</summary>
        public override string ToString() => Name;
    }

    /// <summary>
    ///  Every theme the app ships, and how to read a stored one back.
    /// </summary>
    /// <remarks>
    ///  V2 named around forty Krypton palettes. V3 draws its own surfaces instead, so a theme here
    ///  is a short list of brushes rather than a control library's palette - which is what makes it
    ///  cheap to ship several.
    ///
    ///  The stored value is the <see cref="ThemePalette.Id"/>. <see cref="Parse"/> still understands
    ///  V2's palette names, and V3's earlier Light/Dark/System values, so upgrading lands on the
    ///  nearest theme instead of silently resetting.
    /// </remarks>
    internal static class AppTheme
    {
        /// <summary>Follows the desktop, resolving to <see cref="Dark"/> or <see cref="Light"/>.</summary>
        public const string SystemId = "System";

        public static readonly ThemePalette Dark = new("SteelDark", "Steel Dark", IsDark: true);

        public static readonly ThemePalette Light = new("SteelLight", "Steel Light", IsDark: false);

        /// <summary>
        ///  The picker's contents, dark first.
        /// </summary>
        /// <remarks>
        ///  System is a palette like any other so the picker stays one flat list. It carries the
        ///  dark flag only as a starting guess; <see cref="Resolve"/> is what actually decides.
        /// </remarks>
        public static readonly IReadOnlyList<ThemePalette> All =
        [
            new(SystemId, "Follow the system", IsDark: true),
            Dark,
            new("Midnight", "Midnight", IsDark: true),
            new("Nord", "Nord", IsDark: true),
            new("Gruvbox", "Gruvbox Dark", IsDark: true),
            new("Darcula", "Darcula", IsDark: true),
            new("SolarizedDark", "Solarized Dark", IsDark: true),
            new("Contrast", "High Contrast", IsDark: true),

            // The living themes: a palette, an effect behind the window, and for the neon ones a
            // glow on cards and the main buttons. Panels are slightly translucent in these, so the
            // effect shows through the rail and the cards without fighting the text on them.
            new("Aurora", "Aurora", IsDark: true, ThemeBackdrop.Aurora, Glow: true),
            new("Matrix", "Matrix", IsDark: true, ThemeBackdrop.Rain, Glow: true),
            new("Cosmos", "Cosmos", IsDark: true, ThemeBackdrop.Stars),
            new("Synthwave", "Synthwave", IsDark: true, ThemeBackdrop.Grid, Glow: true),
            new("Ember", "Ember", IsDark: true, ThemeBackdrop.Embers),
            new("Frost", "Frost", IsDark: true, ThemeBackdrop.Snow),
            Light,
            new("Sakura", "Sakura", IsDark: false, ThemeBackdrop.Petals),
            new("SolarizedLight", "Solarized Light", IsDark: false),
            new("Paper", "Paper", IsDark: false),
            new("OneLight", "One Light", IsDark: false),
            new("NordLight", "Nord Light", IsDark: false),
            new("GruvboxLight", "Gruvbox Light", IsDark: false),
        ];

        public static bool IsSystem(ThemePalette palette) =>
            string.Equals(palette.Id, SystemId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///  Turns a stored theme name back into a palette, falling back to <see cref="Dark"/>.
        /// </summary>
        public static ThemePalette Parse(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Dark;
            }

            ThemePalette? known = All.FirstOrDefault(
                palette => string.Equals(palette.Id, name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(palette.Name, name, StringComparison.OrdinalIgnoreCase));

            if (known is not null)
            {
                return known;
            }

            // V3's first cut stored the three Avalonia variants by name.
            if (string.Equals(name, "Light", StringComparison.OrdinalIgnoreCase))
            {
                return Light;
            }

            if (string.Equals(name, "Dark", StringComparison.OrdinalIgnoreCase))
            {
                return Dark;
            }

            // A V2 Krypton palette name. Krypton had no "is this dark" flag either, so V2 read it
            // off the name, and the same test is what keeps an upgrade from flipping the user.
            //
            // Only applied to strings that actually look like one. The heuristic answers "light"
            // for anything without "dark" in it, so letting it see arbitrary text meant a
            // corrupted or hand-edited value silently turned the app light rather than landing on
            // the documented default.
            if (LooksLikeKryptonPalette(name))
            {
                return IsDarkPaletteName(name) ? Dark : Light;
            }

            return Dark;
        }

        /// <summary>The families Krypton named its palettes after.</summary>
        private static bool LooksLikeKryptonPalette(string name)
        {
            string[] families =
            [
                "office", "sparkle", "professional", "microsoft365", "krypton", "visualstudio",
            ];

            return families.Any(family => name.Contains(family, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        ///  The palette to actually draw with, given what the desktop is set to.
        /// </summary>
        /// <remarks>
        ///  Only System needs resolving; every other palette is already an answer.
        /// </remarks>
        public static ThemePalette Resolve(ThemePalette palette, bool desktopIsDark) =>
            IsSystem(palette) ? (desktopIsDark ? Dark : Light) : palette;

        /// <summary>Every dark Krypton palette is named for it, one way or the other.</summary>
        private static bool IsDarkPaletteName(string name)
        {
            return name.Contains("Dark", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Black", StringComparison.OrdinalIgnoreCase);
        }
    }
}
