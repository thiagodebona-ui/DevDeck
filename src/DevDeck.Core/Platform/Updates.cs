using System.Net.Http;
using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>One published build.</summary>
    internal sealed record Release(string Version, string Url, string Notes)
    {
        /// <summary>
        ///  Whether this release is newer than the build that is running.
        /// </summary>
        /// <remarks>
        ///  Compared as version numbers rather than as strings, because "1.10.0" sorts before
        ///  "1.9.0" as text and an update check that goes backwards is worse than none. Anything
        ///  unparseable is treated as not newer: a tag someone typed by hand is not a reason to
        ///  tell the user to upgrade.
        /// </remarks>
        public bool IsNewerThan(Version running) =>
            System.Version.TryParse(Version.TrimStart('v', 'V'), out Version? published)
            && published > running;
    }

    /// <summary>
    ///  Asks whether there is a newer build, and never does anything about it on its own.
    /// </summary>
    /// <remarks>
    ///  Checking is offered; installing is not. A tool that replaces its own executable while the
    ///  user is in the middle of something is a tool that needs signing, rollback, a service and a
    ///  great deal of trust - and this is an app that runs commands with the user's account, which
    ///  makes silently swapping its own binary precisely the wrong habit to teach. The check ends
    ///  at a link.
    ///
    ///  It is also never automatic on startup: an application that phones home the moment it opens
    ///  has made a decision on the user's behalf about a network request their firewall may well
    ///  care about. The button is in Settings, and pressing it is the consent.
    /// </remarks>
    internal static class Updates
    {
        /// <summary>Where releases are published.</summary>
        /// <remarks>
        ///  The API rather than the web page: the JSON has the version and the link in it, and
        ///  scraping HTML for a version number breaks the first time the page is restyled.
        /// </remarks>
        public const string Endpoint = "https://api.github.com/repos/thiagodebona-ui/DevDeck/releases/latest";

        /// <summary>The page a person would read, for the link in Settings.</summary>
        public const string Page = "https://github.com/thiagodebona-ui/DevDeck/releases";

        /// <summary>
        ///  Asks the release feed what the newest build is.
        /// </summary>
        /// <remarks>
        ///  A short timeout on purpose. This is a convenience, and a convenience that makes the
        ///  settings page sit still for two minutes behind a corporate proxy has become an
        ///  annoyance instead.
        /// </remarks>
        public static async Task<Release?> LatestAsync(CancellationToken token)
        {
            using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(10) };

            // Required by the API, and a good citizen everywhere else: a request with no user agent
            // is refused outright by GitHub.
            client.DefaultRequestHeaders.Add("User-Agent", $"DevDeck/{AppVersion.Number}");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

            string body = await client.GetStringAsync(Endpoint, token);

            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;

            string tag = Text(root, "tag_name");

            if (tag.Length == 0)
            {
                return null;
            }

            string url = Text(root, "html_url");

            return new Release(tag, url.Length > 0 ? url : Page, Text(root, "body"));
        }

        private static string Text(JsonElement element, string name) =>
            element.TryGetProperty(name, out JsonElement found) && found.ValueKind == JsonValueKind.String
                ? found.GetString() ?? string.Empty
                : string.Empty;
    }
}
