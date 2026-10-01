using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>What one version changed, as the changelog tells it.</summary>
    internal sealed record ChangelogEntry(string Version, string Notes);

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

        private static readonly Regex Heading = new(
            @"^##\s+v?(?<version>\d+(?:\.\d+){1,3}(?:-[\w.]+)?)\s*$",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

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
        ///  Not sorted by number: the numbering restarted at 1.0.0 after 3.0.0-alpha.1, and sorting
        ///  would put that old alpha above every real release. The file is written newest first.
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

                found.Add(new ChangelogEntry(version, Plain(text[start..end])));
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
