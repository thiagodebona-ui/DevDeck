namespace DevDeck.Core
{
    /// <summary>
    ///  What to call a response when it is saved to disk.
    /// </summary>
    /// <remarks>
    ///  Its own class because the question has two halves that come from different places and are
    ///  wrong in different ways. The stem comes from the URL, which is what makes a folder of saved
    ///  responses readable a week later; the extension comes from the content type, which is the
    ///  only thing that knows what the bytes actually are.
    ///
    ///  Taking the extension from the URL instead is the obvious shortcut and it is wrong in both
    ///  directions: <c>/api/users/42</c> has none and would be saved as a file the desktop cannot
    ///  open, and <c>/report.pdf?format=json</c> has one that lies. The server has just said what
    ///  it sent; that is the answer.
    ///
    ///  The table is deliberately short. It covers what an API returns and what a developer asks a
    ///  browser-shaped tool for, and everything else falls through to <c>.bin</c> - which the
    ///  desktop will at least not open with the wrong application.
    /// </remarks>
    internal static class Names
    {
        /// <summary>
        ///  Media type to extension, dot included.
        /// </summary>
        /// <remarks>
        ///  Keyed on the full type rather than matched by prefix, so that a subtype nobody
        ///  anticipated does not borrow the extension of its family - <c>image/avif</c> saved as
        ///  <c>.png</c> would be a file that opens to an error. The prefix families that do have a
        ///  safe general answer are handled in <see cref="Extension"/> below.
        /// </remarks>
        private static readonly Dictionary<string, string> Known = new(StringComparer.OrdinalIgnoreCase)
        {
            ["application/json"] = ".json",
            ["application/problem+json"] = ".json",
            ["application/ld+json"] = ".json",
            ["text/json"] = ".json",
            ["application/xml"] = ".xml",
            ["text/xml"] = ".xml",
            ["application/soap+xml"] = ".xml",
            ["application/xhtml+xml"] = ".html",
            ["text/html"] = ".html",
            ["text/plain"] = ".txt",
            ["text/csv"] = ".csv",
            ["text/markdown"] = ".md",
            ["text/css"] = ".css",
            ["text/javascript"] = ".js",
            ["application/javascript"] = ".js",
            ["application/x-javascript"] = ".js",
            ["application/ecmascript"] = ".js",
            ["application/pdf"] = ".pdf",
            ["application/zip"] = ".zip",
            ["application/gzip"] = ".gz",
            ["application/x-tar"] = ".tar",
            ["application/octet-stream"] = ".bin",
            ["application/x-www-form-urlencoded"] = ".txt",
            ["image/png"] = ".png",
            ["image/jpeg"] = ".jpg",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp",
            ["image/bmp"] = ".bmp",
            ["image/avif"] = ".avif",
            ["image/tiff"] = ".tiff",
            ["image/x-icon"] = ".ico",
            ["image/vnd.microsoft.icon"] = ".ico",
            ["image/svg+xml"] = ".svg",
            ["audio/mpeg"] = ".mp3",
            ["audio/ogg"] = ".ogg",
            ["audio/wav"] = ".wav",
            ["audio/x-wav"] = ".wav",
            ["audio/webm"] = ".weba",
            ["audio/aac"] = ".aac",
            ["audio/flac"] = ".flac",
            ["video/mp4"] = ".mp4",
            ["video/webm"] = ".webm",
            ["video/ogg"] = ".ogv",
            ["video/quicktime"] = ".mov",
            ["video/x-msvideo"] = ".avi",
            ["font/woff2"] = ".woff2",
            ["font/woff"] = ".woff",
            ["font/ttf"] = ".ttf",
        };

        /// <summary>The extension for a media type, falling back by family and then to .bin.</summary>
        public static string Extension(string media, BodyKind kind)
        {
            string type = media.Trim();

            int parameters = type.IndexOf(';');

            if (parameters >= 0)
            {
                type = type[..parameters].Trim();
            }

            if (Known.TryGetValue(type, out string? known))
            {
                return known;
            }

            // A subtype that is not in the table but whose shape says what it is. The +json and
            // +xml suffixes are a convention the whole API world follows, so an unrecognised
            // application/vnd.acme.thing+json is still JSON and still worth saving as such.
            if (type.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
            {
                return ".json";
            }

            if (type.EndsWith("+xml", StringComparison.OrdinalIgnoreCase))
            {
                return ".xml";
            }

            // Nothing useful in the header - which happens - so fall back to what the body was
            // read as, since that was worked out from the bytes.
            return kind switch
            {
                BodyKind.Json => ".json",
                BodyKind.Xml => ".xml",
                BodyKind.Html => ".html",
                BodyKind.JavaScript => ".js",
                BodyKind.Plain => ".txt",
                _ => type.StartsWith("text/", StringComparison.OrdinalIgnoreCase) ? ".txt" : ".bin",
            };
        }

        /// <summary>
        ///  A file name for a response: the tail of the request path, then the right extension.
        /// </summary>
        /// <remarks>
        ///  Everything the filesystem would refuse is replaced rather than dropped, so that two
        ///  different paths do not collapse onto the same name. A path that leaves nothing usable -
        ///  a bare host, a URL that is only a query string - falls back to "response", which is at
        ///  least honest about having nothing better to offer.
        /// </remarks>
        public static string For(string url, string media, BodyKind kind)
        {
            string stem = Stem(url);
            string extension = Extension(media, kind);

            // A path that already ends in the right extension should not gain a second one.
            return stem.EndsWith(extension, StringComparison.OrdinalIgnoreCase)
                ? stem
                : stem + extension;
        }

        private static string Stem(string url)
        {
            string path = url;

            int query = path.IndexOfAny(['?', '#']);

            if (query >= 0)
            {
                path = path[..query];
            }

            // The scheme's own slashes are not path separators, and leaving them in makes the host
            // the last segment of every URL that has no path.
            int scheme = path.IndexOf("://", StringComparison.Ordinal);

            if (scheme >= 0)
            {
                path = path[(scheme + 3)..];
            }

            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            // The last segment that is not a variable reference. A trailing {{id}} names nothing a
            // person would recognise, and the segment before it usually does.
            // Enumerable.Reverse rather than the array's own: Array.Reverse sorts in place and
            // returns nothing, and the two are one keystroke apart.
            string chosen = Enumerable.Reverse(segments)
                .FirstOrDefault(segment => segment.Length > 0 && !segment.Contains("{{", StringComparison.Ordinal))
                ?? string.Empty;

            // Only one segment, and it is the host: better than nothing, and it is what a request
            // to a bare domain is actually about.
            if (chosen.Length == 0 && segments.Length > 0)
            {
                chosen = segments[0];
            }

            chosen = Safe(chosen);

            return chosen.Length > 0 ? chosen : "response";
        }

        /// <summary>Strips what a file name may not contain, on any of the platforms shipped to.</summary>
        private static string Safe(string name)
        {
            char[] cleaned = new char[name.Length];
            int length = 0;

            foreach (char c in name)
            {
                // The union of what Windows, macOS and Linux object to, rather than
                // Path.GetInvalidFileNameChars - that answers for the machine this is running on,
                // and a name saved onto a share is read from the others.
                bool bad = char.IsControl(c)
                    || c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*';

                cleaned[length++] = bad ? '-' : c;
            }

            // Trailing dots and spaces are legal to create on Linux and impossible to open on
            // Windows, which is the worst of the two outcomes.
            return new string(cleaned, 0, length).Trim().TrimEnd('.');
        }
    }
}
