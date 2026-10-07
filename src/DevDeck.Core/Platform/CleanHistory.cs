using System.Globalization;

namespace DevDeck.Core
{
    /// <summary>One clean as the history keeps it: when, and what it bought.</summary>
    internal readonly record struct CleanRecord(DateTime When, long Freed);

    /// <summary>
    ///  The last few cleans, read from and written to <see cref="AppSettings.MemoryHistory"/>.
    /// </summary>
    /// <remarks>
    ///  Stored as "when|freed bytes" lines, the format the setting was declared with, so a file
    ///  written by an older build reads back. A line that does not parse is dropped rather than
    ///  failing the lot: this is a convenience, not a record anyone depends on.
    /// </remarks>
    internal static class CleanHistory
    {
        /// <summary>How many cleans are kept. Enough for a row of bars, not enough to matter.</summary>
        public const int Keep = 24;

        public static List<CleanRecord> Parse(IEnumerable<string>? lines)
        {
            List<CleanRecord> records = [];

            foreach (string line in lines ?? [])
            {
                string[] parts = line.Split('|');

                if (parts.Length == 2
                    && DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime when)
                    && long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out long freed))
                {
                    records.Add(new CleanRecord(when.ToUniversalTime(), freed));
                }
            }

            return records;
        }

        public static string Format(CleanRecord record) =>
            string.Create(CultureInfo.InvariantCulture, $"{record.When.ToUniversalTime():o}|{record.Freed}");

        /// <summary>The stored lines with one more clean on the end, oldest dropped past <see cref="Keep"/>.</summary>
        public static List<string> Append(IEnumerable<string>? lines, CleanRecord record)
        {
            List<string> kept = [.. Parse(lines).Select(Format), Format(record)];

            return kept.Count > Keep ? kept[^Keep..] : kept;
        }

        /// <summary>"just now", "12 min ago", "3 h ago", "2 d ago".</summary>
        public static string Ago(DateTime when, DateTime now)
        {
            TimeSpan gone = now - when;

            if (gone < TimeSpan.FromMinutes(1))
            {
                return Strings.Text("MemAgoNow");
            }

            if (gone < TimeSpan.FromHours(1))
            {
                return Strings.Format("MemAgoMinutes", (int)gone.TotalMinutes);
            }

            return gone < TimeSpan.FromDays(1)
                ? Strings.Format("MemAgoHours", (int)gone.TotalHours)
                : Strings.Format("MemAgoDays", (int)gone.TotalDays);
        }
    }
}
