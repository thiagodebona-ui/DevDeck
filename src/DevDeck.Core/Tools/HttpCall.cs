using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevDeck.Core
{
    /// <summary>One header, as the user typed it.</summary>
    internal sealed class HeaderLine
    {
        public string Name { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public bool Enabled { get; set; } = true;
    }

    /// <summary>A request, saved between sessions.</summary>
    /// <remarks>
    ///  Raises PropertyChanged for the few things the request list shows live - whether it is being
    ///  sent, and what it last answered - and for nothing else. The rest of the request is edited
    ///  through the panel, which already redraws the row when it saves.
    /// </remarks>
    internal sealed class HttpRequest : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private int lastStatus;

        private long lastMilliseconds;

        private bool inFlight;

        /// <summary>
        ///  The status code this request last came back with: 0 for never sent, -1 for no answer.
        /// </summary>
        /// <remarks>
        ///  Saved, so the list still says which requests were failing after a restart. -1 rather
        ///  than a separate flag, because "no answer" is a status for the purposes of the badge -
        ///  a request that timed out is as broken as one that came back 500.
        /// </remarks>
        public int LastStatus
        {
            get => lastStatus;
            set
            {
                if (lastStatus == value)
                {
                    return;
                }

                lastStatus = value;
                Changed(nameof(LastStatus), nameof(HasLast), nameof(LastOk), nameof(LastWarn), nameof(LastBad), nameof(LastText), nameof(LastTip));
            }
        }

        /// <summary>How long the last answer took, for the badge's tooltip.</summary>
        public long LastMilliseconds
        {
            get => lastMilliseconds;
            set
            {
                lastMilliseconds = value;
                Changed(nameof(LastMilliseconds), nameof(LastTip));
            }
        }

        /// <summary>Whether this request is being sent right now. Never saved.</summary>
        [JsonIgnore]
        public bool InFlight
        {
            get => inFlight;
            set
            {
                if (inFlight == value)
                {
                    return;
                }

                inFlight = value;
                Changed(nameof(InFlight), nameof(HasLast));
            }
        }

        /// <summary>Whether there is a last answer to show - hidden while a new one is on its way.</summary>
        [JsonIgnore]
        public bool HasLast => LastStatus != 0 && !InFlight;

        [JsonIgnore]
        public bool LastOk => LastStatus is >= 200 and < 400;

        [JsonIgnore]
        public bool LastWarn => LastStatus is >= 400 and < 500;

        [JsonIgnore]
        public bool LastBad => LastStatus < 0 || LastStatus >= 500;

        [JsonIgnore]
        public string LastText => LastStatus < 0 ? "ERR" : LastStatus.ToString(System.Globalization.CultureInfo.InvariantCulture);

        [JsonIgnore]
        public string LastTip => LastStatus < 0
            ? "No answer"
            : $"{LastStatus} · {LastMilliseconds} ms";

        private void Changed(params string[] names)
        {
            foreach (string name in names)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        private string name = "New request";

        private string method = "GET";

        /// <summary>
        ///  What the row is called.
        /// </summary>
        /// <remarks>
        ///  Raises its change, unlike most of the fields below. The row in the list binds to this,
        ///  and without the notification a rename was saved but the tree went on showing the old
        ///  name until something rebuilt it - most visibly inside a group, which the old trick of
        ///  re-inserting the row into the ungrouped list never reached.
        /// </remarks>
        public string Name
        {
            get => name;
            set
            {
                if (name == value)
                {
                    return;
                }

                name = value;
                Changed(nameof(Name));
            }
        }

        /// <summary>Drawn under the name in the list, so it raises its change too.</summary>
        public string Method
        {
            get => method;
            set
            {
                if (method == value)
                {
                    return;
                }

                method = value;
                Changed(nameof(Method));
            }
        }

        public string Url { get; set; } = string.Empty;

        public List<HeaderLine> Headers { get; set; } = [];

        public string Body { get; set; } = string.Empty;

        /// <summary>
        ///  A colour flag, or empty for none.
        /// </summary>
        /// <remarks>
        ///  Held as one of a small set of names rather than as a hex value, so the swatch follows
        ///  the theme: a colour picked to read well on the dark theme is often invisible on the
        ///  light one, and a request list is exactly where that matters. See
        ///  <see cref="RequestColour"/> for the set and what each is for.
        /// </remarks>
        public string Colour { get; set; } = string.Empty;

        /// <summary>
        ///  The name of an icon to draw beside this request, or empty for none.
        /// </summary>
        /// <remarks>
        ///  A name - "database", "rocket" - and not path data, for the same reason
        ///  <see cref="Colour"/> is a name: the drawing is the app's business and the choice is the
        ///  user's, so an icon can be redrawn or re-weighted in a later build without touching a
        ///  single saved request. It also means a hand-edited settings.json can say
        ///  <c>"Icon": "database"</c>, which nobody could do with a path string.
        ///
        ///  A name the app does not know draws nothing rather than a placeholder glyph. A question
        ///  mark in the list would report a defect to the one person who cannot fix it.
        /// </remarks>
        public string Icon { get; set; } = string.Empty;

        /// <summary>
        ///  The group this request is filed under, or empty for none.
        /// </summary>
        /// <remarks>
        ///  A name rather than an id, and a flat one rather than a path. A saved list that is
        ///  worth grouping is a few dozen entries, and at that size folders-within-folders cost
        ///  more in navigation than they save in tidiness. Renaming a group is therefore a
        ///  find-and-replace across the requests that carry it, which is the honest cost of
        ///  keeping the model this simple.
        ///
        ///  Requests with no group are not in a nameless group: they sit at the top level, which
        ///  is where a brand-new request belongs and where it stays until it is filed.
        /// </remarks>
        public string Group { get; set; } = string.Empty;
    }

    /// <summary>
    ///  The colours a request can be flagged with.
    /// </summary>
    /// <remarks>
    ///  A short list on purpose. The flag is there to make one request findable in a list of
    ///  thirty at a glance, and a palette large enough that two entries look alike defeats that.
    ///  Six reads as six; twelve reads as "some colours".
    ///
    ///  The names are stored, and each resolves to a theme resource at the point of drawing, so
    ///  both themes get a swatch that is actually visible against their own background.
    /// </remarks>
    internal static class RequestColour
    {
        public const string None = "";

        public const string Red = "red";

        public const string Amber = "amber";

        public const string Green = "green";

        public const string Blue = "blue";

        public const string Purple = "purple";

        public const string Grey = "grey";

        /// <summary>Every flag, with none first, for a menu to be built from.</summary>
        public static IReadOnlyList<string> All => [None, Red, Amber, Green, Blue, Purple, Grey];

        /// <summary>Whether a stored value is one this build knows how to draw.</summary>
        /// <remarks>
        ///  Guards against a settings file written by a later version, or edited by hand: an
        ///  unrecognised colour is treated as no colour rather than drawn as a blank swatch.
        /// </remarks>
        public static bool Known(string colour) => All.Contains(colour, StringComparer.Ordinal);
    }

    /// <summary>What came back.</summary>
    /// <summary>What a response body is, as far as showing it goes.</summary>
    internal enum BodyKind
    {
        /// <summary>Text with no structure worth drawing - or no body at all.</summary>
        Plain,

        Json,

        Xml,

        Html,

        /// <summary>JavaScript, and the JSONP and source maps that arrive labelled as it.</summary>
        JavaScript,

        /// <summary>An image the pane can draw rather than describe.</summary>
        Image,

        /// <summary>Audio or video, which the pane offers to play.</summary>
        Media,

        /// <summary>Something that is not text. Shown as a description rather than as mojibake.</summary>
        Binary,
    }

    internal sealed record HttpResult(
        int Status,
        string Reason,
        string Body,
        IReadOnlyList<HeaderLine> Headers,
        long Milliseconds,
        long Bytes,
        string? Error)
    {
        /// <summary>The media type the server said it was sending, without its parameters.</summary>
        public string MediaType { get; init; } = string.Empty;

        /// <summary>
        ///  What the body turned out to be.
        /// </summary>
        /// <remarks>
        ///  Set from the content type and then confirmed against the bytes, because the header is
        ///  wrong often enough to matter: plenty of servers label JSON as <c>text/plain</c>, and a
        ///  misconfigured one will label an HTML error page as JSON. What the body actually parses
        ///  as is the truth; the header only decides what to try first.
        /// </remarks>
        public BodyKind Kind { get; init; } = BodyKind.Plain;

        public bool Failed => Error is not null;

        /// <summary>Whether the reply is worth drawing with colour rather than as flat text.</summary>
        public bool IsStructured =>
            Kind is BodyKind.Json or BodyKind.Xml or BodyKind.Html or BodyKind.JavaScript;

        /// <summary>Whether the reply is something to look at rather than to read.</summary>
        public bool IsViewable => Kind is BodyKind.Image or BodyKind.Media;

        /// <summary>"JSON · application/json" - what the response pane labels itself with.</summary>
        public string BodyLabel => Preview == ResponsePreview.Svg
            ? "SVG"
            : Kind switch
        {
            BodyKind.Json => "JSON",
            BodyKind.Xml => "XML",
            BodyKind.Html => "HTML",
            BodyKind.JavaScript => "JavaScript",
            BodyKind.Image or BodyKind.Media or BodyKind.Binary =>
                MediaType.Length > 0 ? MediaType : "binary",
            _ => MediaType.Length > 0 ? MediaType : "text",
        };

        /// <summary>
        ///  The bytes exactly as they arrived, for a reply that is drawn rather than read.
        /// </summary>
        /// <remarks>
        ///  Only carried for an image or a playable media type - the one case where the text in
        ///  <see cref="Body"/> is a description rather than the reply itself, so there would
        ///  otherwise be nothing left to draw from. Every other kind leaves this null rather than
        ///  keeping a second copy of a body that is already here as text.
        /// </remarks>
        public byte[]? Raw { get; init; }

        /// <summary>The other way this reply can be shown besides its source, if there is one.</summary>
        public ResponsePreview Preview { get; init; } = ResponsePreview.None;

        /// <summary>
        ///  A file name to offer when saving this response, extension included.
        /// </summary>
        /// <remarks>
        ///  Named after the last useful segment of the request path rather than "response", because
        ///  a folder of downloads called response, response(1), response(2) is a folder nobody can
        ///  read. The extension comes from the media type and not from the URL: a path ending in
        ///  <c>/users/42</c> that answers with JSON should be saved as <c>.json</c>, and a
        ///  <c>/download?id=7</c> that answers with a PNG should be saved as <c>.png</c>.
        ///
        ///  Set by the sender, which is the only place that still has the request.
        /// </remarks>
        public string SuggestedName { get; init; } = "response.txt";

        public bool Ok => Status is >= 200 and < 300;

        /// <summary>The line a developer reads first: "200 OK · 142 ms · 3.1 KB".</summary>
        public string Summary => Failed
            ? Error!
            : $"{Status} {Reason} · {Milliseconds} ms · {Size}";

        public string Size => Bytes < 1024
            ? $"{Bytes} B"
            : Bytes < 1024 * 1024
                ? $"{Bytes / 1024.0:F1} KB"
                : $"{Bytes / (1024.0 * 1024):F1} MB";

        /// <summary>
        ///  Which of three states to colour the status with.
        /// </summary>
        /// <remarks>
        ///  A 404 is not an error in this panel - it is an answer, and often the expected one. Only
        ///  a request that never got a reply is a failure.
        /// </remarks>
        public string Tone => Failed ? "bad" : Ok ? "good" : Status >= 500 ? "bad" : "warn";
    }

    /// <summary>
    ///  Sending a request and reading the reply, without leaving the app.
    /// </summary>
    /// <remarks>
    ///  Scoped deliberately below Postman and at about Hoppscotch's level: a method, a URL, headers,
    ///  a body, and the response pretty-printed. No environments, no test scripts, no collections
    ///  shared with a team - those are a product, and this is the thing you reach for when a command
    ///  in the deck has just started a server and you want to know whether it answers.
    ///
    ///  What it does have that curl does not is the response formatted, the timing measured, and the
    ///  request still there tomorrow. What curl has that it does not is everything else, which is
    ///  why <see cref="AsCurl"/> exists - the request can leave as a command and be pasted anywhere.
    /// </remarks>
    internal static class Http
    {
        public static IReadOnlyList<string> Methods { get; } =
            ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"];

        /// <summary>
        ///  One client for the app, as the type is designed to be used.
        /// </summary>
        /// <remarks>
        ///  Redirects are followed, because the alternative is a panel that shows a 301 and makes
        ///  the user do the hop by hand. Automatic decompression is on for the same reason - a body
        ///  displayed as gzip bytes answers nothing.
        ///
        ///  Cookie handling is off, which sounds backwards and is not. With it on, the handler owns
        ///  the Cookie header: it replaces whatever was set by hand with the contents of its own
        ///  container, which is empty, so a session pasted in from a browser's curl is parsed,
        ///  displayed in the header list, and then quietly dropped on the way out. Off, the header
        ///  is sent exactly as it reads on screen. Nothing here wants a cookie jar anyway - a
        ///  request that needs one is a request whose session came from somewhere else.
        /// </remarks>
        private static readonly HttpClient Client = new(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            UseCookies = false,
        })
        {
            Timeout = TimeSpan.FromSeconds(60),
        };

        /// <summary>Bodies past this are truncated rather than rendered - see the note in Send.</summary>
        private const int MaxBody = 2 * 1024 * 1024;

        /// <summary>Sends a request and describes the reply, never throwing.</summary>
        public static async Task<HttpResult> Send(HttpRequest request, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(request.Url))
            {
                return Fail("Enter a URL first.");
            }

            string url = request.Url.Trim();

            // A URL typed without a scheme is what everyone types, and it always means http here -
            // the thing being poked is nearly always a server that just started on localhost.
            if (!url.Contains("://", StringComparison.Ordinal))
            {
                url = "http://" + url;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return Fail("That is not a URL this can send to.");
            }

            Stopwatch clock = Stopwatch.StartNew();

            try
            {
                using HttpRequestMessage message = new(new HttpMethod(request.Method), uri);

                if (request.Body.Length > 0 && request.Method is not ("GET" or "HEAD"))
                {
                    message.Content = new StringContent(request.Body, Encoding.UTF8);
                    message.Content.Headers.Remove("Content-Type");
                    message.Content.Headers.TryAddWithoutValidation("Content-Type", ContentType(request));
                }

                foreach (HeaderLine header in request.Headers.Where(Usable))
                {
                    if (header.Name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        // Already applied to the content above; adding it to the request headers as
                        // well is rejected by the type as a content header in the wrong place.
                        continue;
                    }

                    message.Headers.TryAddWithoutValidation(header.Name.Trim(), header.Value);
                }

                using HttpResponseMessage response = await Client
                    .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, token)
                    .ConfigureAwait(false);

                byte[] bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);

                clock.Stop();

                string body = bytes.Length > MaxBody
                    // A response that large is a download, not something to read in a text box, and
                    // rendering it would freeze the UI for seconds to no purpose.
                    ? Encoding.UTF8.GetString(bytes, 0, MaxBody)
                        + $"\n\n… truncated. The response was {bytes.Length:N0} bytes."
                    : Encoding.UTF8.GetString(bytes);

                // What the server said, unless it said nothing useful and the bytes say more: a
                // bucket that serves a PNG as application/octet-stream still gets it drawn, and
                // saved as .png.
                string media = ResponseFormats.Effective(
                    response.Content.Headers.ContentType?.MediaType ?? string.Empty, bytes);
                BodyKind kind = Classify(body, media, bytes);
                ResponsePreview preview = ResponseFormats.PreviewFor(media, kind, body, bytes);

                string offered = ResponseFormats.DispositionName(
                    response.Content.Headers.ContentDisposition?.FileNameStar,
                    response.Content.Headers.ContentDisposition?.FileName);

                return new HttpResult(
                    (int)response.StatusCode,
                    response.ReasonPhrase ?? string.Empty,
                    kind is BodyKind.Binary or BodyKind.Image or BodyKind.Media
                        ? Describe(media, bytes.Length)
                        : Pretty(body, kind),
                    Headers(response),
                    clock.ElapsedMilliseconds,
                    bytes.Length,
                    null)
                {
                    MediaType = media,
                    Kind = kind,
                    Preview = preview,
                    SuggestedName = Names.For(request.Url, media, kind, offered),

                    // Kept for the three kinds whose Body is a description rather than the reply.
                    // Every other kind already has all of itself as text, and keeping the bytes as
                    // well would double what a response costs for nothing - saving one of those
                    // writes the text back out instead.
                    // An SVG as well: its Body is the XML re-indented, and it is drawn - and saved -
                    // from what actually arrived.
                    Raw = kind is BodyKind.Image or BodyKind.Media or BodyKind.Binary
                        || preview == ResponsePreview.Svg

                        // And a reply too large to show whole: the text here is cut short, and
                        // Save and Open must still have all of it.
                        || bytes.Length > MaxBody
                        ? bytes
                        : null,
                };
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return Fail("Cancelled.");
            }
            catch (TaskCanceledException)
            {
                return Fail($"No reply within {Client.Timeout.TotalSeconds:F0} seconds.");
            }
            catch (HttpRequestException exception)
            {
                // The inner message is the useful half - "connection refused", "no such host" -
                // where the outer one is a generic wrapper.
                return Fail((exception.InnerException ?? exception).Message);
            }
            catch (UriFormatException)
            {
                return Fail("That is not a URL this can send to.");
            }
            catch (InvalidOperationException exception)
            {
                return Fail(exception.Message);
            }
        }

        private static HttpResult Fail(string why) => new(0, string.Empty, string.Empty, [], 0, 0, why);

        private static bool Usable(HeaderLine header) =>
            header.Enabled && !string.IsNullOrWhiteSpace(header.Name);

        /// <summary>
        ///  The content type to send a body as.
        /// </summary>
        /// <remarks>
        ///  Taken from the headers when the user set one, and guessed from the body otherwise -
        ///  because a body that begins with a brace is JSON every time, and having to add the header
        ///  by hand to make the common case work is the kind of friction this panel exists to
        ///  remove.
        /// </remarks>
        private static string ContentType(HttpRequest request)
        {
            foreach (HeaderLine header in request.Headers.Where(Usable))
            {
                if (header.Name.Trim().Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    return header.Value.Trim();
                }
            }

            string body = request.Body.TrimStart();

            return body.StartsWith('{') || body.StartsWith('[')
                ? "application/json"
                : body.StartsWith('<')
                    ? "application/xml"
                    : "text/plain; charset=utf-8";
        }

        private static IReadOnlyList<HeaderLine> Headers(HttpResponseMessage response) =>
        [
            .. response.Headers.Concat(response.Content.Headers)
                .Select(header => new HeaderLine
                {
                    Name = header.Key,
                    Value = string.Join(", ", header.Value),
                })
                .OrderBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
        ];

        /// <summary>
        ///  Indents a response when it is something indentable.
        /// </summary>
        /// <remarks>
        ///  Minified JSON is the normal shape of an API reply and the least readable one. Reusing
        ///  the toolbox's formatter keeps one implementation of that; a body that will not parse is
        ///  shown exactly as it arrived, because a malformed response is usually the thing being
        ///  investigated.
        /// </remarks>
        /// <summary>
        ///  Works out what came back.
        /// </summary>
        /// <remarks>
        ///  The content type is a hint and not an answer. Servers label JSON as <c>text/plain</c>
        ///  all the time, an error page comes back as HTML from an endpoint that swore it returned
        ///  JSON, and an API behind a proxy can return the proxy's own content type. So the header
        ///  chooses what to attempt and the body decides: something that parses as JSON is JSON
        ///  whatever it was announced as, and something announced as JSON that does not parse is
        ///  not - which is the case where showing it as flat text is what lets the user see the
        ///  HTML error page they actually got.
        ///
        ///  Sniffed before any of that: bytes that are not text at all. An image or a zip rendered
        ///  into a text box is a screenful of noise that can lock the UI while it lays out.
        /// </remarks>
        public static BodyKind Classify(string body, string media, byte[] bytes)
        {
            // Asked before the byte sniff, and from the content type rather than from the bytes:
            // these are the two kinds where "not text" is the beginning of the answer rather than
            // the end of it, and a server saying image/png is what says which of them it is. Put
            // after the sniff they would never be reached - a PNG is full of null bytes.
            if (IsImage(media, bytes))
            {
                return BodyKind.Image;
            }

            if (IsMedia(media) && bytes.Length > 0)
            {
                return BodyKind.Media;
            }

            if (Binary(bytes))
            {
                return BodyKind.Binary;
            }

            string trimmed = body.TrimStart();

            if (trimmed.Length == 0)
            {
                return BodyKind.Plain;
            }

            bool saysJson = media.Contains("json", StringComparison.OrdinalIgnoreCase);
            bool saysXml = media.Contains("xml", StringComparison.OrdinalIgnoreCase);
            bool saysHtml = media.Contains("html", StringComparison.OrdinalIgnoreCase);

            // HTML first among the angle-bracket formats: XHTML is both, and the useful reading of
            // a page is as a page.
            if (saysHtml || trimmed.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            {
                return BodyKind.Html;
            }

            if ((saysJson || trimmed[0] is '{' or '[') && !TextTools.FormatJson(body).Failed)
            {
                return BodyKind.Json;
            }

            if ((saysXml || trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
                && !TextTools.FormatXml(body).Failed)
            {
                return BodyKind.Xml;
            }

            // Last of the text kinds, and from the content type alone. There is no cheap shape test
            // for JavaScript the way there is for the others - it is not required to begin with
            // anything in particular - so guessing from the body would mean colouring prose as code
            // on the strength of a stray brace.
            if (IsJavaScript(media))
            {
                return BodyKind.JavaScript;
            }

            return BodyKind.Plain;
        }

        /// <summary>
        ///  Whether the reply is an image the pane can draw.
        /// </summary>
        /// <remarks>
        ///  SVG is deliberately excluded. It is an image, but it is also text and also XML, and what
        ///  a developer wants from an SVG response is nearly always to read it - so it carries on to
        ///  the XML path and is coloured rather than rendered.
        /// </remarks>
        public static bool IsImage(string media, byte[] bytes) =>
            bytes.Length > 0
            && media.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            && !media.Contains("svg", StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether the reply is audio or video, which the pane offers to play.</summary>
        public static bool IsMedia(string media) =>
            media.StartsWith("video/", StringComparison.OrdinalIgnoreCase)
            || media.StartsWith("audio/", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///  Whether the content type says JavaScript, in any of the spellings still in use.
        /// </summary>
        /// <remarks>
        ///  Three of them matter: the standard <c>text/javascript</c>, the widespread
        ///  <c>application/javascript</c>, and the <c>application/x-javascript</c> older servers
        ///  still send. JSONP arrives as one of these and lands here rather than on the JSON path,
        ///  which is right - it is a function call, and parsing it as JSON fails.
        /// </remarks>
        public static bool IsJavaScript(string media) =>
            media.EndsWith("/javascript", StringComparison.OrdinalIgnoreCase)
            || media.EndsWith("/x-javascript", StringComparison.OrdinalIgnoreCase)
            || media.EndsWith("/ecmascript", StringComparison.OrdinalIgnoreCase);

        /// <summary>Classifies a body given as text, for callers that have no raw bytes.</summary>
        public static BodyKind Classify(string body, string media) =>
            Classify(body, media, Encoding.UTF8.GetBytes(body));

        /// <summary>
        ///  Whether the reply is not text.
        /// </summary>
        /// <remarks>
        ///  A null byte in the first kilobyte, the same test git and grep use, rather than trusting
        ///  the content type - a server that sends a PNG as <c>application/octet-stream</c> and one
        ///  that sends it as <c>image/png</c> should both end up here.
        /// </remarks>
        private static bool Binary(byte[] bytes)
        {
            int checkable = Math.Min(bytes.Length, 1024);

            for (int index = 0; index < checkable; index++)
            {
                if (bytes[index] == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>What to put in the body pane instead of a binary.</summary>
        private static string Describe(string media, long length) =>
            $"{(media.Length > 0 ? media : "A binary response")} · {length:N0} bytes."
                + "\n\nNot shown, because it is not text. The headers are on the other tab.";

        private static string Pretty(string body, BodyKind kind)
        {
            if (body.Length == 0 || body.Length > MaxBody)
            {
                return body;
            }

            // Only the three that have a formatter. HTML is left exactly as it came: re-indenting
            // it would change what is inside a <pre>, and a page is usually being read for the
            // error message in it rather than for its structure.
            ToolResult formatted = kind switch
            {
                BodyKind.Json => TextTools.FormatJson(body),
                BodyKind.Xml => TextTools.FormatXml(body),
                BodyKind.JavaScript => TextTools.FormatJavaScript(body),
                _ => ToolResult.Fail("nothing to do"),
            };

            return formatted.Failed ? body : formatted.Text;
        }

        /// <summary>
        ///  The same request as a curl command.
        /// </summary>
        /// <remarks>
        ///  The escape hatch, and the answer to everything this panel deliberately does not do. It
        ///  also means a request worked out here can be pasted into a script, a ticket or a command
        ///  in the deck itself.
        /// </remarks>
        public static string AsCurl(HttpRequest request)
        {
            StringBuilder curl = new();

            curl.Append("curl");

            if (!request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                curl.Append(" -X ").Append(request.Method);
            }

            curl.Append(' ').Append(Quote(request.Url));

            foreach (HeaderLine header in request.Headers.Where(Usable))
            {
                string name = header.Name.Trim();

                // A cookie goes out as -b, which is where curl's own reader expects it and where
                // anyone reading the command looks for it. -H would work identically; it just
                // reads as though the session were an afterthought rather than the point.
                if (name.Equals("Cookie", StringComparison.OrdinalIgnoreCase))
                {
                    curl.Append(" \\\n  -b ").Append(Quote(header.Value));
                }
                else
                {
                    curl.Append(" \\\n  -H ").Append(Quote($"{name}: {header.Value}"));
                }
            }

            if (request.Body.Length > 0 && request.Method is not ("GET" or "HEAD"))
            {
                if (!request.Headers.Where(Usable).Any(header =>
                        header.Name.Trim().Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
                {
                    curl.Append(" \\\n  -H ").Append(Quote($"Content-Type: {ContentType(request)}"));
                }

                curl.Append(" \\\n  -d ").Append(Quote(request.Body));
            }

            return curl.ToString();
        }

        /// <summary>
        ///  Single quotes, in the form every POSIX shell accepts.
        /// </summary>
        /// <remarks>
        ///  A single quote cannot be escaped inside single quotes, so the string is closed, an
        ///  escaped quote is emitted, and it is reopened - the standard '\'' dance. Worth doing
        ///  properly: a JSON body full of quotes is the normal input here.
        /// </remarks>
        private static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";

        /// <summary>
        ///  Flags that swallow the word after them.
        /// </summary>
        /// <remarks>
        ///  Listed so that an option this does not implement cannot eat the URL. A command carrying
        ///  <c>-o out.json https://…</c> would otherwise import with "out.json" as its address,
        ///  which is a worse failure than ignoring the flag: it looks like it worked.
        /// </remarks>
        private static readonly HashSet<string> Takes = new(StringComparer.Ordinal)
        {
            "-o", "--output", "-w", "--write-out", "-m", "--max-time", "--connect-timeout",
            "--retry", "--retry-delay", "--retry-max-time", "-x", "--proxy", "--proxy-user",
            "--cacert", "--capath", "--cert", "--cert-type", "--key", "--key-type", "--pass",
            "-E", "--resolve", "--limit-rate", "-c", "--cookie-jar", "--interface",
            "--max-filesize", "--max-redirs", "-y", "--speed-time", "-Y", "--speed-limit",
            "-K", "--config", "--unix-socket", "--dns-servers", "--local-port", "--ciphers",
            "-C", "--continue-at", "-r", "--range", "--tls-max", "--http-version",
        };

        /// <summary>
        ///  Short flags that carry a value, for unbundling <c>-sSL</c> from <c>-XPOST</c>.
        /// </summary>
        private const string ValueShorts = "XHdbAeuowmxTEcFKrC";

        /// <summary>
        ///  Headers the client owns, which are dropped on import.
        /// </summary>
        /// <remarks>
        ///  Every one of these describes the transport rather than the request. Keeping them is not
        ///  merely redundant - an Accept-Encoding copied from a browser asks for zstd, which this
        ///  handler did not negotiate and cannot decode, so the reply arrives as binary noise and
        ///  the panel gets blamed for it. Content-Length would be wrong the moment the body is
        ///  edited, and Host contradicts the URL as soon as either changes.
        /// </remarks>
        private static readonly HashSet<string> Owned = new(StringComparer.OrdinalIgnoreCase)
        {
            "content-length", "accept-encoding", "host", "connection", "transfer-encoding",
            "keep-alive", "upgrade", "proxy-connection",
        };

        /// <summary>
        ///  Turns a pasted curl command into a request.
        /// </summary>
        /// <remarks>
        ///  The other half of the escape hatch, and the one that gets used more: API documentation
        ///  and browser devtools both hand out curl, and retyping one into fields is the tax this
        ///  removes. The target is specifically the "Copy as cURL" output of a browser's network
        ///  tab, which is not the tidy two-flag command the documentation pages print - it arrives
        ///  with twenty headers, the address behind <c>--url</c>, the session in <c>-b</c> rather
        ///  than a Cookie header, line continuations, and quoting from whichever shell the browser
        ///  decided to target.
        ///
        ///  Flags that carry part of the request are folded into it; flags that describe how curl
        ///  itself should behave are skipped, but skipped knowingly - see <see cref="Takes"/> for
        ///  why the difference matters. Nothing here rejects a command it does not fully
        ///  understand, because a request that imports with one option missing is still most of the
        ///  work done.
        /// </remarks>
        public static HttpRequest? FromCurl(string text)
        {
            List<string> parts = Unbundle(Split(text));

            int at = 0;

            // A command copied out of a terminal often brings its prompt or its sudo with it.
            while (at < parts.Count && parts[at] is "$" or ">" or "sudo")
            {
                at++;
            }

            if (at >= parts.Count
                || !(parts[at].Equals("curl", StringComparison.OrdinalIgnoreCase)
                    || parts[at].Equals("curl.exe", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            HttpRequest request = new() { Method = string.Empty };

            string? url = null;
            string? method = null;
            string? cookie = null;
            string? agent = null;
            string? referer = null;
            string? user = null;
            string? bearer = null;
            bool head = false;
            bool query = false;
            bool upload = false;

            List<string> data = [];
            List<string> form = [];

            for (at++; at < parts.Count; at++)
            {
                string flag = parts[at];
                string? next = at + 1 < parts.Count ? parts[at + 1] : null;

                switch (flag)
                {
                    case "--url" when next is not null:
                        url = next;
                        at++;

                        break;

                    case "-X" or "--request" when next is not null:
                        method = next;
                        at++;

                        break;

                    case "-H" or "--header" when next is not null:
                        Header(request, next);
                        at++;

                        break;

                    case "-b" or "--cookie" when next is not null:
                        // curl reads the value as a file when it has no '=' in it. There is no file
                        // to read here, so that form is dropped rather than sent as a cookie.
                        if (next.Contains('='))
                        {
                            cookie = cookie is null ? next : cookie + "; " + next;
                        }

                        at++;

                        break;

                    case "-A" or "--user-agent" when next is not null:
                        agent = next;
                        at++;

                        break;

                    case "-e" or "--referer" when next is not null:
                        // "-e URL;auto" asks curl to keep sending the referer across redirects,
                        // which is not something the header itself says.
                        referer = next.EndsWith(";auto", StringComparison.OrdinalIgnoreCase)
                            ? next[..^5]
                            : next;
                        at++;

                        break;

                    case "-u" or "--user" when next is not null:
                        user = next;
                        at++;

                        break;

                    case "--oauth2-bearer" when next is not null:
                        bearer = next;
                        at++;

                        break;

                    case "-d" or "--data" or "--data-raw" or "--data-ascii" or "--data-binary"
                        when next is not null:
                        // A leading '@' means "read this file", which cannot be honoured here; the
                        // text is kept as typed so the request is at least editable.
                        data.Add(next);
                        at++;

                        break;

                    case "--data-urlencode" when next is not null:
                        data.Add(Encoded(next));
                        at++;

                        break;

                    case "--json" when next is not null:
                        data.Add(next);
                        Fill(request, "Content-Type", "application/json");
                        Fill(request, "Accept", "application/json");
                        at++;

                        break;

                    case "-F" or "--form" or "--form-string" when next is not null:
                        form.Add(next);
                        at++;

                        break;

                    case "-T" or "--upload-file" when next is not null:
                        upload = true;
                        at++;

                        break;

                    case "-I" or "--head":
                        head = true;

                        break;

                    case "-G" or "--get":
                        query = true;

                        break;

                    default:
                        if (Takes.Contains(flag) && next is not null)
                        {
                            at++;
                        }
                        else if (!flag.StartsWith('-') && url is null)
                        {
                            url = flag;
                        }

                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            request.Url = url.Trim();

            // Only the headers the command did not already state: an explicit -H wins over the
            // shorthand for the same thing, which is curl's own precedence.
            if (cookie is not null)
            {
                Fill(request, "Cookie", cookie);
            }

            if (agent is not null)
            {
                Fill(request, "User-Agent", agent);
            }

            if (referer is not null)
            {
                Fill(request, "Referer", referer);
            }

            if (bearer is not null)
            {
                Fill(request, "Authorization", "Bearer " + bearer);
            }

            if (user is not null)
            {
                Fill(request, "Authorization",
                    "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes(user)));
            }

            string body = string.Join("&", data);

            if (body.Length == 0 && form.Count > 0)
            {
                // -F is multipart, which this panel does not build. Fields that are plain text
                // survive as a form-encoded body, which most endpoints accept and which is at least
                // visible and editable; a field naming a file (@ or <) cannot, and is dropped
                // rather than sent as its own literal path.
                body = string.Join("&", form
                    .Where(field => !field.Contains("=@") && !field.Contains("=<"))
                    .Select(Encoded));

                if (body.Length > 0)
                {
                    Fill(request, "Content-Type", "application/x-www-form-urlencoded");
                }
            }

            if (query && body.Length > 0)
            {
                request.Url += (request.Url.Contains('?') ? "&" : "?") + body;
                body = string.Empty;
            }

            request.Body = body;

            request.Method = method is not null
                ? method.ToUpperInvariant()
                : head
                    ? "HEAD"
                    : upload
                        ? "PUT"
                        // curl's own rule: a body without an explicit method is a POST.
                        : body.Length > 0
                            ? "POST"
                            : "GET";

            request.Name = Name(request.Url);

            return request;
        }

        /// <summary>Adds a header from one "-H" value, unless the client owns that header.</summary>
        private static void Header(HttpRequest request, string text)
        {
            int colon = text.IndexOf(':');

            if (colon <= 0)
            {
                return;
            }

            string name = text[..colon].Trim();

            if (name.Length == 0 || Owned.Contains(name))
            {
                return;
            }

            request.Headers.Add(new HeaderLine
            {
                Name = name,
                Value = text[(colon + 1)..].Trim(),
            });
        }

        /// <summary>Adds a header only if the command did not already set one by that name.</summary>
        private static void Fill(HttpRequest request, string name, string value)
        {
            if (request.Headers.Any(header =>
                    header.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            request.Headers.Add(new HeaderLine { Name = name, Value = value });
        }

        /// <summary>Percent-encodes the value half of a "name=value" pair, leaving the name alone.</summary>
        private static string Encoded(string pair)
        {
            int equals = pair.IndexOf('=');

            return equals < 0
                ? Uri.EscapeDataString(pair)
                : pair[..(equals + 1)] + Uri.EscapeDataString(pair[(equals + 1)..]);
        }

        /// <summary>
        ///  Splits bundled short flags apart, and a value off the flag it is stuck to.
        /// </summary>
        /// <remarks>
        ///  Documentation writes <c>-sSL</c> and <c>-XPOST</c>; both are one token to a splitter and
        ///  neither matches anything the parser looks for. Unbundling here means the loop above only
        ///  ever sees one flag at a time, which is the only reason it can be a flat switch.
        /// </remarks>
        private static List<string> Unbundle(IReadOnlyList<string> parts)
        {
            List<string> tokens = [];

            foreach (string part in parts)
            {
                if (part.StartsWith("--", StringComparison.Ordinal))
                {
                    // --header=value, the other form every long option accepts.
                    int equals = part.IndexOf('=');

                    if (equals > 2)
                    {
                        tokens.Add(part[..equals]);
                        tokens.Add(part[(equals + 1)..]);
                    }
                    else
                    {
                        tokens.Add(part);
                    }

                    continue;
                }

                if (part.Length <= 2 || part[0] != '-')
                {
                    tokens.Add(part);

                    continue;
                }

                for (int at = 1; at < part.Length; at++)
                {
                    tokens.Add("-" + part[at]);

                    if (!ValueShorts.Contains(part[at]))
                    {
                        continue;
                    }

                    // Everything left in the token belongs to this flag, not to another one.
                    if (at + 1 < part.Length)
                    {
                        tokens.Add(part[(at + 1)..]);
                    }

                    break;
                }
            }

            return tokens;
        }

        /// <summary>
        ///  Splits a command line the way a shell would, minus the parts that do not apply.
        /// </summary>
        /// <remarks>
        ///  Quotes group, and a line continuation joins - in bash's backslash, cmd's caret and
        ///  PowerShell's backtick, because the browser picks which of those to emit and the person
        ///  pasting it neither knows nor should have to.
        ///
        ///  Single quotes are literal, which is what makes a header like
        ///  <c>-H 'sec-ch-ua: "Chromium";v="153"'</c> survive intact. The exception is bash's
        ///  <c>$'…'</c>, which a browser reaches for the moment a header value contains anything it
        ///  would rather escape than paste - there the escapes have to be read back out again, or
        ///  the request goes out with a literal backslash-u in it.
        /// </remarks>
        private static IReadOnlyList<string> Split(string text)
        {
            List<string> parts = [];
            StringBuilder current = new();

            char quote = '\0';
            bool ansi = false;
            bool any = false;

            for (int at = 0; at < text.Length; at++)
            {
                char c = text[at];
                char after = at + 1 < text.Length ? text[at + 1] : '\0';

                // A line continuation, in any of the three shells this text gets copied out of.
                // Only outside quotes: inside them, all three characters are ordinary data.
                if (quote == '\0' && c is '\\' or '^' or '`' && after is '\n' or '\r')
                {
                    at++;

                    continue;
                }

                // $'…' - bash's ANSI-C quoting.
                if (quote == '\0' && c == '$' && after == '\'')
                {
                    quote = '\'';
                    ansi = true;
                    any = true;
                    at++;

                    continue;
                }

                if (c == '\\' && after != '\0' && (ansi || quote is '"' or '\0'))
                {
                    if (ansi)
                    {
                        current.Append(Escape(text, ref at));
                    }
                    else
                    {
                        current.Append(after);
                        at++;
                    }

                    any = true;

                    continue;
                }

                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                        ansi = false;
                    }
                    else
                    {
                        current.Append(c);
                    }

                    continue;
                }

                switch (c)
                {
                    case '\'' or '"':
                        quote = c;
                        // An empty quoted string is still an argument, so the flag is set here as
                        // well as on the first character appended.
                        any = true;

                        break;

                    case ' ' or '\t' or '\n' or '\r':
                        if (any)
                        {
                            parts.Add(current.ToString());
                            current.Clear();
                            any = false;
                        }

                        break;

                    default:
                        current.Append(c);
                        any = true;

                        break;
                }
            }

            if (any)
            {
                parts.Add(current.ToString());
            }

            return parts;
        }

        /// <summary>
        ///  Reads one backslash escape out of a <c>$'…'</c> string.
        /// </summary>
        /// <remarks>
        ///  Advances <paramref name="at"/> past everything it consumed, so the caller's loop can
        ///  carry on without knowing how wide the escape was - \n is two characters where
        ///  \U0001F600 is ten.
        /// </remarks>
        private static string Escape(string text, ref int at)
        {
            char c = text[++at];

            switch (c)
            {
                case 'n': return "\n";
                case 't': return "\t";
                case 'r': return "\r";
                case 'a': return "\a";
                case 'b': return "\b";
                case 'f': return "\f";
                case 'v': return "\v";
                case 'e': return "\u001b";

                case 'x' or 'u' or 'U':
                {
                    int want = c == 'x' ? 2 : c == 'u' ? 4 : 8;
                    int from = at + 1;
                    int count = 0;

                    while (count < want && from + count < text.Length && Uri.IsHexDigit(text[from + count]))
                    {
                        count++;
                    }

                    if (count == 0)
                    {
                        return c.ToString();
                    }

                    at += count;

                    int value = int.Parse(
                        text.AsSpan(from, count), System.Globalization.NumberStyles.HexNumber);

                    // A value past the basic plane needs surrogate pairs; one that is not a
                    // character at all is written back as the char it fits in rather than thrown
                    // over, because a malformed escape is not worth failing an import for.
                    return value is > 0xFFFF and <= 0x10FFFF
                        ? char.ConvertFromUtf32(value)
                        : ((char)(value & 0xFFFF)).ToString();
                }

                default: return c.ToString();
            }
        }

        /// <summary>A readable name for a saved request, from its path.</summary>
        public static string Name(string url)
        {
            if (!Uri.TryCreate(url.Contains("://", StringComparison.Ordinal) ? url : "http://" + url,
                    UriKind.Absolute, out Uri? uri))
            {
                return "New request";
            }

            string path = uri.AbsolutePath.Trim('/');

            return path.Length > 0 ? path : uri.Host;
        }
    }
}
