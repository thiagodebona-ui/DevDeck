using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>What one version changed, as the changelog tells it.</summary>
    /// <param name="Released">When it was released, from its heading. Null for a heading without a date.</param>
    /// <param name="Items">The changes one by one, for the page to lay out; <paramref name="Notes"/> is the same as text.</param>
    internal sealed record ChangelogEntry(
        string Version,
        string Notes,
        DateTime? Released = null,
        IReadOnlyList<ChangelogItem>? Items = null);

    /// <summary>
    ///  One change: the bold lead-in the changelog gives it, and the sentence after.
    /// </summary>
    /// <remarks>
    ///  Both keep their inline markdown - `code` and **bold** - for the page to draw, rather than
    ///  having it stripped as the plain notes do. A change written without a bold lead-in has an
    ///  empty title and is all body.
    /// </remarks>
    internal sealed record ChangelogItem(string Title, string Body)
    {
        private static readonly Regex Repair = new(
            @"\b(no more|no longer|fix(ed|es)?|crash(es)?|freez\w*|glitch\w*|bug|works?|starts clean|lines up)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        ///  Whether this reads as a repair rather than something new, for the mark beside it.
        /// </summary>
        /// <remarks>
        ///  Read off the title's wording, because the changelog does not file its entries under
        ///  headings. A guess, and only ever used for the icon: getting it wrong costs a star where
        ///  a tick should be, and the words beside it still say what happened.
        /// </remarks>
        public bool IsFix => Repair.IsMatch(Title.Length > 0 ? Title : Body);
    }

    /// <summary>One run of text in a changelog line, and how it is set.</summary>
    internal sealed record InlineRun(string Text, bool IsCode, bool IsBold);

    /// <summary>
    ///  What changed between the running build and a newer one, read from CHANGELOG.md.
    /// </summary>
    /// <remarks>
    ///  The changelog rather than the release body. The release notes GitHub generates are a list
    ///  of commit titles; the changelog is written for the person deciding whether to upgrade, and
    ///  it covers every version in between - someone two releases behind wants both.
    ///
    ///  Read at the newer release's own tag, so what is shown is what that build says about itself
    ///  rather than whatever is on the main branch today. Only fetched after the user has pressed
    ///  "Check now", for the same reason the check itself is never automatic.
    /// </remarks>
    internal static class Changelog
    {
        /// <summary>The file as it was at a given tag.</summary>
        public static string Source(string tag) =>
            $"https://raw.githubusercontent.com/thiagodebona-ui/DevDeck/{Uri.EscapeDataString(tag)}/CHANGELOG.md";

        /// <summary>
        ///  "## 1.2.7", or with its release time: "## 1.2.7 - 2026-10-01 18:47".
        /// </summary>
        /// <remarks>
        ///  The date is optional so that every heading written before dates were kept still reads,
        ///  and the release workflow matches a heading by its version alone for the same reason.
        /// </remarks>
        private static readonly Regex Heading = new(
            @"^##\s+v?(?<version>\d+(?:\.\d+){1,3}(?:-[\w.]+)?)(?:\s+[-\u2013\u2014]\s+(?<date>\d{4}-\d{2}-\d{2}(?:[ T]\d{2}:\d{2})?))?\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        private static readonly Regex Inline = new(
            @"`(?<code>[^`]+)`|\*\*(?<bold>.+?)\*\*",
            RegexOptions.CultureInvariant);

        /// <summary>
        ///  Every version in the text newer than <paramref name="running"/> and no newer than
        ///  <paramref name="upTo"/>, newest first.
        /// </summary>
        /// <remarks>
        ///  The upper bound matters because the file at a tag can already carry a heading for the
        ///  version after it, written ahead of time. That one is not out yet and is not offered.
        /// </remarks>
        public static IReadOnlyList<ChangelogEntry> Between(string markdown, Version running, Version upTo) =>
            All(markdown)
                .Where(entry => TryParse(entry.Version, out Version? parsed) && parsed > running && parsed <= upTo)
                .ToList();

        /// <summary>Every version the text describes, in the file's own order - newest first.</summary>
        /// <remarks>
        ///  Not sorted by number: the file is written newest first, and its order is the one the
        ///  author meant - a renumbered or out-of-sequence heading must not jump to the top.
        /// </remarks>
        public static IReadOnlyList<ChangelogEntry> All(string markdown)
        {
            List<ChangelogEntry> found = [];
            string text = markdown.Replace("\r\n", "\n");
            MatchCollection headings = Heading.Matches(text);

            for (int i = 0; i < headings.Count; i++)
            {
                Match heading = headings[i];
                string version = heading.Groups["version"].Value;

                int start = heading.Index + heading.Length;
                int end = i + 1 < headings.Count ? headings[i + 1].Index : text.Length;
                string section = text[start..end];

                found.Add(new ChangelogEntry(version, Plain(section), Date(heading.Groups["date"].Value), Items(section)));
            }

            return found;
        }

        /// <summary>
        ///  The changelog this build was made with, newest first.
        /// </summary>
        /// <remarks>
        ///  Embedded at build time, so the page works offline and always describes the build that
        ///  is actually running. Empty, rather than an exception, in a build that somehow lacks it.
        /// </remarks>
        public static IReadOnlyList<ChangelogEntry> Bundled { get; } = ReadBundled();

        private static IReadOnlyList<ChangelogEntry> ReadBundled()
        {
            using Stream? stream = typeof(Changelog).Assembly.GetManifestResourceStream("CHANGELOG.md");

            if (stream is null)
            {
                return [];
            }

            using StreamReader reader = new(stream);

            return All(reader.ReadToEnd());
        }

        /// <summary>
        ///  Fetches the changelog at <paramref name="tag"/> and returns what is newer than this build.
        /// </summary>
        /// <returns>Empty when the file could not be read, so the caller can fall back.</returns>
        public static async Task<IReadOnlyList<ChangelogEntry>> SinceAsync(string tag, CancellationToken token)
        {
            if (!TryParse(tag, out Version? upTo))
            {
                return [];
            }

            try
            {
                using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };

                client.DefaultRequestHeaders.Add("User-Agent", $"DevDeck/{AppVersion.Number}");

                string markdown = await client.GetStringAsync(Source(tag), token);

                return Between(markdown, AppVersion.Current, upTo);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                AppLog.Instance.Failure("update", "Could not read the changelog", exception);

                return [];
            }
        }

        /// <summary>A heading's date, if it has one that reads.</summary>
        private static DateTime? Date(string text) =>
            DateTime.TryParseExact(
                text,
                ["yyyy-MM-dd HH:mm", "yyyy-MM-ddTHH:mm", "yyyy-MM-dd"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out DateTime date)
                ? date
                : null;

        /// <summary>
        ///  A section's changes, one per bullet.
        /// </summary>
        /// <remarks>
        ///  A bullet's wrapped lines are joined back into one, since the line breaks in the file
        ///  are only there to keep it readable at eighty columns. A paragraph that is not a bullet
        ///  is a change of its own.
        /// </remarks>
        public static IReadOnlyList<ChangelogItem> Items(string section)
        {
            List<string> bullets = [];
            System.Text.StringBuilder current = new();

            void Close()
            {
                if (current.Length > 0)
                {
                    bullets.Add(current.ToString());
                    current.Clear();
                }
            }

            foreach (string raw in section.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();

                if (line.Length == 0)
                {
                    Close();

                    continue;
                }

                if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal))
                {
                    Close();
                    current.Append(line[2..].Trim());

                    continue;
                }

                current.Append(current.Length > 0 ? " " : string.Empty).Append(line);
            }

            Close();

            return [.. bullets.Select(Item)];
        }

        /// <summary>Splits a change into its bold lead-in and the rest.</summary>
        private static ChangelogItem Item(string text)
        {
            if (text.StartsWith("**", StringComparison.Ordinal))
            {
                int close = text.IndexOf("**", 2, StringComparison.Ordinal);

                if (close > 2)
                {
                    return new ChangelogItem(text[2..close].Trim(), text[(close + 2)..].Trim());
                }
            }

            return new ChangelogItem(string.Empty, text);
        }

        /// <summary>
        ///  A line split into its plain, `code` and **bold** runs, for the page to set each its own way.
        /// </summary>
        public static IReadOnlyList<InlineRun> Runs(string text)
        {
            List<InlineRun> runs = [];
            int at = 0;

            foreach (Match match in Inline.Matches(text))
            {
                if (match.Index > at)
                {
                    runs.Add(new InlineRun(text[at..match.Index], false, false));
                }

                runs.Add(match.Groups["code"].Success
                    ? new InlineRun(match.Groups["code"].Value, true, false)
                    : new InlineRun(match.Groups["bold"].Value, false, true));

                at = match.Index + match.Length;
            }

            if (at < text.Length)
            {
                runs.Add(new InlineRun(text[at..], false, false));
            }

            return runs;
        }

        /// <summary>
        ///  Markdown made readable as plain text: bold markers and code ticks go, the words stay.
        /// </summary>
        public static string Plain(string markdown) =>
            markdown.Replace("**", string.Empty).Replace("`", string.Empty).Trim();

        /// <summary>A version or tag, with any v prefix and prerelease suffix ignored.</summary>
        private static bool TryParse(string text, [NotNullWhen(true)] out Version? version)
        {
            string trimmed = text.TrimStart('v', 'V');
            int dash = trimmed.IndexOf('-');

            return Version.TryParse(dash > 0 ? trimmed[..dash] : trimmed, out version);
        }
    }
}
