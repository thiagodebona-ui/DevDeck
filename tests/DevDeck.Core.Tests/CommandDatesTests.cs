using System.Globalization;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>The line under each command in the deck.</summary>
    /// <remarks>
    ///  In the same collection as the language tests, which switch the app's language while they
    ///  run: the words here are read from the table in force, and must be read in English.
    /// </remarks>
    [Collection("Strings")]
    public class CommandDatesTests
    {
        public CommandDatesTests() => Strings.Use(AppLanguage.English);

        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

        private static readonly DateTime Now = new(2026, 10, 1, 17, 30, 0);

        [Fact]
        public void TodayIsATimeThisYearADayAndOlderCarriesItsYear()
        {
            Assert.Equal("09:05", CommandDates.When(new DateTime(2026, 10, 1, 9, 5, 0), Now, English));
            Assert.Equal("14 Mar", CommandDates.When(new DateTime(2026, 3, 14, 9, 5, 0), Now, English));
            Assert.Equal("2 Dec 2025", CommandDates.When(new DateTime(2025, 12, 2, 9, 5, 0), Now, English));
        }

        [Fact]
        public void BothDatesAreShownWhenBothAreKnown()
        {
            string line = CommandDates.Describe(
                new DateTime(2026, 9, 20), new DateTime(2026, 10, 1, 8, 15, 0), Now, English);

            Assert.Equal("created 20 Sep · ran 08:15", line);
        }

        /// <summary>A command from before dates were kept says nothing about its age rather than guessing.</summary>
        [Fact]
        public void AnUnknownCreationDateIsLeftOut()
        {
            Assert.Equal("never run", CommandDates.Describe(null, null, Now, English));
            Assert.Equal("ran 08:15", CommandDates.Describe(null, new DateTime(2026, 10, 1, 8, 15, 0), Now, English));
        }
    }
}
