namespace DevDeck.Core
{
    /// <summary>
    ///  One named palette the app can wear.
    /// </summary>
    /// <remarks>
    ///  <see cref="IsDark"/> is not decoration. Avalonia's Fluent theme draws everything we do not
    ///  draw ourselves - scrollbars, combo popups, the caret, tick marks - and it picks those from
    ///  its own variant rather than from our brushes. A palette therefore has to say which variant
    ///  it belongs to, or a light palette ends up with dark scrollbars down the side of it.
    /// </remarks>
    internal sealed record ThemePalette(string Id, string Name, bool IsDark)
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
            new("SolarizedDark", "Solarized Dark", IsDark: true),
            new("Contrast", "High Contrast", IsDark: true),
            Light,
            new("SolarizedLight", "Solarized Light", IsDark: false),
            new("Paper", "Paper", IsDark: false),
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
