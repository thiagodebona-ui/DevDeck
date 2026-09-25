using System.Globalization;

namespace DevDeck.Core
{
    /// <summary>
    ///  One language the app can be read in.
    /// </summary>
    /// <remarks>
    ///  <see cref="Name"/> is written in the language itself - "Português (Brasil)" rather than
    ///  "Brazilian Portuguese". Someone who has landed in a language they cannot read needs to find
    ///  their own in the list, and the one thing they can be relied on to recognise is its name as
    ///  they would write it themselves.
    /// </remarks>
    internal sealed record LanguageChoice(string Id, string Name)
    {
        /// <summary>Shown in the picker; ToString is what a bare ComboBox binds to.</summary>
        public override string ToString() => Name;
    }

    /// <summary>
    ///  Every language the app ships, and how to read a stored one back.
    /// </summary>
    /// <remarks>
    ///  The same shape as <see cref="AppTheme"/> deliberately: a stored id, an All list for the
    ///  picker, and a Parse that never throws and never lands on nothing. A settings file carrying
    ///  a language this build does not have is a file written by a newer build, and the right
    ///  answer to that is English rather than a crash on startup.
    ///
    ///  Following the system is the default, and it is the reason <see cref="System"/> exists as an
    ///  entry rather than as an empty string: a user in Brazil should not have to find this setting
    ///  at all, and one who has deliberately chosen English should not be moved off it when they
    ///  next log into a Portuguese desktop.
    /// </remarks>
    internal static class AppLanguage
    {
        /// <summary>Follows the desktop, resolving to <see cref="English"/> or <see cref="Portuguese"/>.</summary>
        public const string SystemId = "System";

        public static readonly LanguageChoice English = new("en", "English");

        public static readonly LanguageChoice Portuguese = new("pt-BR", "Português (Brasil)");

        /// <summary>The picker's contents.</summary>
        public static readonly IReadOnlyList<LanguageChoice> All =
        [
            new(SystemId, "Follow the system"),
            English,
            Portuguese,
        ];

        public static bool IsSystem(LanguageChoice language) =>
            string.Equals(language.Id, SystemId, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///  Turns a stored language back into a choice, falling back to following the system.
        /// </summary>
        public static LanguageChoice Parse(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return All[0];
            }

            LanguageChoice? known = All.FirstOrDefault(
                language => string.Equals(language.Id, id, StringComparison.OrdinalIgnoreCase));

            if (known is not null)
            {
                return known;
            }

            // A regional variant this build does not carry: pt-PT, or en-GB. The language part is
            // what matters, and landing a Portuguese speaker on Portuguese is better than landing
            // them on English over a country code.
            return FromCulture(id);
        }

        /// <summary>
        ///  The language to actually read in, given what the desktop is set to.
        /// </summary>
        /// <remarks>
        ///  Only System needs resolving; every other choice is already an answer.
        /// </remarks>
        public static LanguageChoice Resolve(LanguageChoice language) =>
            IsSystem(language) ? FromCulture(CultureInfo.CurrentUICulture.Name) : language;

        /// <summary>
        ///  The nearest shipped language to a culture name, which is English unless it is Portuguese.
        /// </summary>
        /// <remarks>
        ///  Matched on the language part alone. Every Portuguese-speaking desktop gets the
        ///  Brazilian translation rather than English: it is not the right dialect for Lisbon, but
        ///  it is far closer to readable than the alternative.
        /// </remarks>
        public static LanguageChoice FromCulture(string? culture)
        {
            if (string.IsNullOrWhiteSpace(culture))
            {
                return English;
            }

            int dash = culture.IndexOf('-');
            string language = dash > 0 ? culture[..dash] : culture;

            return string.Equals(language, "pt", StringComparison.OrdinalIgnoreCase)
                ? Portuguese
                : English;
        }
    }
}
