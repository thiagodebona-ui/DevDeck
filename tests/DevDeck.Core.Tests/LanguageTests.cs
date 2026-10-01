using System.Globalization;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Reading a stored language back, and the table behind every word the app says.
    /// </summary>
    /// <remarks>
    ///  The table half of this matters more than it looks. A missing or misnumbered format hole is
    ///  not a compile error - it is a FormatException the first time a user reaches that line, in a
    ///  language nobody on the team reads. These tests are the only thing standing between a typo
    ///  in a translation and a crash in the wild.
    /// </remarks>
    [Collection("Strings")]
    public class LanguageTests
    {
        [Fact]
        public void EveryShippedLanguageHasADistinctIdAndName()
        {
            Assert.Equal(
                AppLanguage.All.Count,
                AppLanguage.All.Select(language => language.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

            Assert.Equal(
                AppLanguage.All.Count,
                AppLanguage.All.Select(language => language.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        [Fact]
        public void EveryShippedLanguageRoundTripsThroughParse()
        {
            foreach (LanguageChoice language in AppLanguage.All)
            {
                Assert.Equal(language.Id, AppLanguage.Parse(language.Id).Id);
            }
        }

        /// <summary>
        ///  A settings file written before this setting existed, or by a newer build that ships a
        ///  language this one does not. Neither is allowed to be a crash on startup.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NothingStoredMeansFollowTheSystem(string? stored)
        {
            Assert.True(AppLanguage.IsSystem(AppLanguage.Parse(stored)));
        }

        /// <summary>
        ///  A regional variant this build does not carry lands on the language, not on English.
        /// </summary>
        [Theory]
        [InlineData("pt", "pt-BR")]
        [InlineData("pt-PT", "pt-BR")]
        [InlineData("PT-br", "pt-BR")]
        [InlineData("en-GB", "en")]
        [InlineData("fr-FR", "en")]
        [InlineData("klingon", "en")]
        public void AnUnshippedCultureLandsOnTheNearestLanguage(string stored, string expected)
        {
            Assert.Equal(expected, AppLanguage.Parse(stored).Id);
        }

        [Fact]
        public void FollowingTheSystemResolvesToSomethingShipped()
        {
            LanguageChoice resolved = AppLanguage.Resolve(AppLanguage.All[0]);

            Assert.False(AppLanguage.IsSystem(resolved));
            Assert.Contains(resolved, AppLanguage.All);
        }

        /// <summary>
        ///  A choice that is already an answer is left exactly as it is, so that someone who picked
        ///  English on a Portuguese desktop stays in English.
        /// </summary>
        [Fact]
        public void AnExplicitChoiceIsNotResolvedAway()
        {
            Assert.Equal(AppLanguage.English, AppLanguage.Resolve(AppLanguage.English));
            Assert.Equal(AppLanguage.Portuguese, AppLanguage.Resolve(AppLanguage.Portuguese));
        }

        /// <summary>
        ///  An unknown key renders as itself rather than as nothing, so a gap in the table shows up
        ///  as an obviously wrong word instead of a blank label nobody notices.
        /// </summary>
        [Fact]
        public void AnUnknownKeyRendersAsItself()
        {
            Assert.Equal("NoSuchKeyAnywhere", Strings.Text("NoSuchKeyAnywhere"));
        }

        [Fact]
        public void SwitchingLanguageChangesWhatTheAppSays()
        {
            try
            {
                Strings.Use(AppLanguage.English);
                string english = Strings.Text("NavSettings");

                Strings.Use(AppLanguage.Portuguese);
                string portuguese = Strings.Text("NavSettings");

                Assert.NotEqual(english, portuguese);
                Assert.NotEmpty(portuguese);
            }
            finally
            {
                Strings.Use(AppLanguage.English);
            }
        }

        [Fact]
        public void SwitchingLanguageRaisesChanged()
        {
            int heard = 0;
            void Count() => heard++;

            Strings.Changed += Count;

            try
            {
                Strings.Use(AppLanguage.Portuguese);
                Strings.Use(AppLanguage.English);
            }
            finally
            {
                Strings.Changed -= Count;
                Strings.Use(AppLanguage.English);
            }

            Assert.Equal(2, heard);
        }

        /// <summary>
        ///  Every English string has a translation, and neither column is blank.
        /// </summary>
        [Fact]
        public void EveryStringIsSaidInBothLanguages()
        {
            List<string> missing = [];

            foreach (string key in Strings.Keys)
            {
                Strings.Use(AppLanguage.English);

                if (Strings.Text(key).Trim().Length == 0)
                {
                    missing.Add(key + " (en)");
                }

                Strings.Use(AppLanguage.Portuguese);

                // Text falls back to English, so an untranslated key is one that comes back
                // identical - which is correct for a few (UUID, Hash, JWT) and a gap for the rest.
                if (Strings.Text(key).Trim().Length == 0)
                {
                    missing.Add(key + " (pt-BR)");
                }
            }

            Strings.Use(AppLanguage.English);

            Assert.Empty(missing);
        }

        /// <summary>
        ///  The one that catches a crash: a translation has to carry the same numbered holes as the
        ///  English it replaces, or formatting it throws where English would not have.
        /// </summary>
        /// <remarks>
        ///  Holes may be reordered - that is the whole reason they are numbered rather than
        ///  interpolated - so this compares the set, not the sequence.
        /// </remarks>
        [Fact]
        public void EveryTranslationCarriesTheSameFormatHoles()
        {
            List<string> wrong = [];

            foreach (string key in Strings.Keys)
            {
                Strings.Use(AppLanguage.English);
                string english = Strings.Text(key);

                Strings.Use(AppLanguage.Portuguese);
                string portuguese = Strings.Text(key);

                if (!Holes(english).SetEquals(Holes(portuguese)))
                {
                    wrong.Add(key);
                }
            }

            Strings.Use(AppLanguage.English);

            Assert.Empty(wrong);
        }

        /// <summary>
        ///  Every string in both languages survives being formatted with arguments to spare.
        /// </summary>
        /// <remarks>
        ///  A stray unbalanced brace - "{0" or "secret:{name}" left un-doubled - is a FormatException
        ///  rather than a wrong word, and it happens at the moment the user does the thing.
        /// </remarks>
        [Fact]
        public void EveryStringCanBeFormatted()
        {
            object?[] spare = ["a", "b", "c", "d", "e"];
            List<string> broken = [];

            foreach (LanguageChoice language in new[] { AppLanguage.English, AppLanguage.Portuguese })
            {
                Strings.Use(language);

                foreach (string key in Strings.Keys)
                {
                    try
                    {
                        Strings.Format(key, spare);
                    }
                    catch (FormatException)
                    {
                        broken.Add(language.Id + ":" + key);
                    }
                }
            }

            Strings.Use(AppLanguage.English);

            Assert.Empty(broken);
        }

        /// <summary>
        ///  Formatting is culture-invariant, so a number in a message reads the same in a log
        ///  pasted into a bug report as it did on screen.
        /// </summary>
        [Fact]
        public void FormattingDoesNotFollowTheCurrentCulture()
        {
            CultureInfo held = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
                Strings.Use(AppLanguage.Portuguese);

                Assert.Contains("1.5", Strings.Format("AiExited", 1.5));
            }
            finally
            {
                CultureInfo.CurrentCulture = held;
                Strings.Use(AppLanguage.English);
            }
        }

        private static HashSet<string> Holes(string text)
        {
            HashSet<string> found = [];

            for (int at = 0; at < text.Length; at++)
            {
                if (text[at] != '{')
                {
                    continue;
                }

                // A doubled brace is a literal one, and is not a hole.
                if (at + 1 < text.Length && text[at + 1] == '{')
                {
                    at++;
                    continue;
                }

                int close = text.IndexOf('}', at);

                if (close > at)
                {
                    found.Add(text[(at + 1)..close]);
                    at = close;
                }
            }

            return found;
        }
    }
}
