using System.Globalization;

namespace DevDeck.Core
{
    /// <summary>
    ///  The small line under a command in the deck: when it was made, and when it last ran.
    /// </summary>
    /// <remarks>
    ///  As short as it can be and still be read, because it sits in a column 250 pixels wide
    ///  beside the name. A time alone means today, a day and month means this year, and only an
    ///  older date carries its year - the same convention a mail client uses, and for the same
    ///  reason: the recent ones are the ones being looked for.
    ///
    ///  A command saved before dates were kept has no creation date, and says nothing about it
    ///  rather than guessing one. Its last run is still known, from the run history.
    /// </remarks>
    internal static class CommandDates
    {
        public static string Describe(DateTime? created, DateTime? lastRun, DateTime now, CultureInfo culture)
        {
            List<string> parts = [];

            if (created is { } made)
            {
                parts.Add(Strings.Format("CmdCreated", When(made, now, culture)));
            }

            parts.Add(lastRun is { } ran
                ? Strings.Format("CmdLastRan", When(ran, now, culture))
                : Strings.Text("CmdNeverRun"));

            return string.Join(" · ", parts);
        }

        /// <summary>A moment, as briefly as its distance from now allows.</summary>
        public static string When(DateTime at, DateTime now, CultureInfo culture)
        {
            if (at.Date == now.Date)
            {
                return at.ToString("HH:mm", culture);
            }

            return at.Year == now.Year
                ? at.ToString("d MMM", culture)
                : at.ToString("d MMM yyyy", culture);
        }
    }
}
