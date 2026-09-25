using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Importing the curl a browser actually produces.
    /// </summary>
    /// <remarks>
    ///  The plain cases live in <see cref="HttpTests"/>; these are the awkward ones. Every shape
    ///  here was taken from real "Copy as cURL" output rather than from the curl manual, because
    ///  the manual's examples are two flags long and the browser's are twenty - and the parts that
    ///  break are the parts only the browser emits: the address behind --url, the session in -b,
    ///  double quotes nested inside single ones, and whichever line continuation the host shell
    ///  uses.
    ///
    ///  The credentials are placeholders. The shape is what is under test, and a token in a test
    ///  file is a token in the repository.
    /// </remarks>
    public class CurlImportTests
    {
        /// <summary>
        ///  Chromium's copy of a real authenticated GET, reduced to the parts that matter.
        /// </summary>
        /// <remarks>
        ///  Kept as one string rather than assembled, because the thing being tested is precisely
        ///  the text as it lands on the clipboard.
        /// </remarks>
        private const string FromDevtools =
            """
            curl --url 'https://www.example.com/api/dicomweb/HangingProtocolV2?JsonQuery=%7B%22where%22%3A%7B%22modality%22%3A%22CT%22%7D%7D&pageSize=50' \
              -H 'accept: */*' \
              -H 'accept-language: en-US,en;q=0.9,pt-BR;q=0.8' \
              -H 'authorization: Bearer header.payload.signature' \
              -b 'first=one; second=two; third=three' \
              -H 'priority: u=1, i' \
              -H 'sec-ch-ua: "Microsoft Edge";v="153", "Not_A Brand";v="8", "Chromium";v="153"' \
              -H 'sec-ch-ua-mobile: ?0' \
              -H 'sec-ch-ua-platform: "Windows"' \
              -H 'sec-fetch-dest: empty' \
              -H 'sec-fetch-mode: cors' \
              -H 'sec-fetch-site: same-origin' \
              -H 'accept-encoding: gzip, deflate, br, zstd' \
              -H 'user-agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36' \
              -H 'x-timezone: America/Sao_Paulo'
            """;

        [Fact]
        public void TakesTheUrlFromTheUrlFlag()
        {
            HttpRequest request = Http.FromCurl(FromDevtools)!;

            Assert.Equal("GET", request.Method);
            Assert.StartsWith(
                "https://www.example.com/api/dicomweb/HangingProtocolV2?JsonQuery=",
                request.Url);

            // The percent-encoding is the query, not an accident of quoting: decoding it here would
            // send a different request from the one that was copied.
            Assert.Contains("%7B%22where%22", request.Url);
            Assert.Contains("&pageSize=50", request.Url);
        }

        [Fact]
        public void FoldsTheCookieFlagIntoAHeader()
        {
            // The one that matters most in practice: -b is where a browser puts the session, and a
            // request that imports without it is a request that quietly stops being authenticated.
            HttpRequest request = Http.FromCurl(FromDevtools)!;

            HeaderLine cookie = Assert.Single(request.Headers,
                header => header.Name.Equals("Cookie", StringComparison.OrdinalIgnoreCase));

            Assert.Equal("first=one; second=two; third=three", cookie.Value);
        }

        [Fact]
        public void KeepsDoubleQuotesInsideSingleQuotedHeaders()
        {
            // sec-ch-ua is the header that catches a splitter treating all quotes alike.
            HttpRequest request = Http.FromCurl(FromDevtools)!;

            HeaderLine ua = Assert.Single(request.Headers, header => header.Name == "sec-ch-ua");

            Assert.Equal("""
                "Microsoft Edge";v="153", "Not_A Brand";v="8", "Chromium";v="153"
                """.Trim(), ua.Value);
        }

        [Fact]
        public void DropsTheHeadersTheClientOwns()
        {
            // accept-encoding asks for zstd, which this handler never negotiated; sending it back
            // means a reply nothing can decode.
            HttpRequest request = Http.FromCurl(FromDevtools)!;

            Assert.DoesNotContain(request.Headers,
                header => header.Name.Equals("accept-encoding", StringComparison.OrdinalIgnoreCase));

            Assert.Contains(request.Headers, header => header.Name == "authorization");
        }

        [Fact]
        public void ReadsEveryHeaderThatWasThere()
        {
            HttpRequest request = Http.FromCurl(FromDevtools)!;

            // Thirteen -H lines, less the dropped accept-encoding, plus the folded Cookie.
            Assert.Equal(13, request.Headers.Count);
            Assert.All(request.Headers, header => Assert.NotEqual(string.Empty, header.Name));
            Assert.All(request.Headers, header => Assert.True(header.Enabled));
        }

        [Fact]
        public void NamesItAfterTheEndpoint()
        {
            Assert.Equal("api/dicomweb/HangingProtocolV2", Http.FromCurl(FromDevtools)!.Name);
        }

        [Fact]
        public void AnUnknownFlagWithAValueDoesNotBecomeTheUrl()
        {
            // The failure this prevents looks like success: -o's filename lands in the address bar
            // and the panel reports a perfectly ordinary connection error for it.
            HttpRequest request = Http.FromCurl("curl -o out.json https://example.com/a")!;

            Assert.Equal("https://example.com/a", request.Url);
        }

        [Fact]
        public void UnbundlesShortFlagsWrittenTogether()
        {
            HttpRequest post = Http.FromCurl("curl -sSL -XPOST https://example.com/a")!;

            Assert.Equal("POST", post.Method);
            Assert.Equal("https://example.com/a", post.Url);

            HttpRequest header = Http.FromCurl("curl -H'X-A: 1' https://example.com/b")!;

            Assert.Contains(header.Headers, line => line is { Name: "X-A", Value: "1" });
        }

        [Fact]
        public void ReadsLongFlagsWrittenWithAnEquals()
        {
            HttpRequest request = Http.FromCurl(
                "curl --request=DELETE --url=https://example.com/a --header='X-B: 2'")!;

            Assert.Equal("DELETE", request.Method);
            Assert.Equal("https://example.com/a", request.Url);
            Assert.Contains(request.Headers, line => line is { Name: "X-B", Value: "2" });
        }

        [Fact]
        public void ReadsTheShorthandsForCommonHeaders()
        {
            HttpRequest request = Http.FromCurl(
                "curl https://example.com -A 'Mozilla/5.0' -e 'https://ref.example.com;auto' "
                + "-u 'user:pass'")!;

            Assert.Contains(request.Headers, line => line is { Name: "User-Agent", Value: "Mozilla/5.0" });
            Assert.Contains(request.Headers,
                line => line is { Name: "Referer", Value: "https://ref.example.com" });

            // "user:pass" in base64, which is the whole of what basic auth is.
            Assert.Contains(request.Headers,
                line => line is { Name: "Authorization", Value: "Basic dXNlcjpwYXNz" });
        }

        [Fact]
        public void AnExplicitHeaderBeatsTheShorthandForTheSameThing()
        {
            HttpRequest request = Http.FromCurl(
                "curl https://example.com -A 'from-the-flag' -H 'User-Agent: from-the-header'")!;

            HeaderLine agent = Assert.Single(request.Headers,
                header => header.Name.Equals("User-Agent", StringComparison.OrdinalIgnoreCase));

            Assert.Equal("from-the-header", agent.Value);
        }

        [Fact]
        public void HeadAndUploadPickTheirOwnMethods()
        {
            Assert.Equal("HEAD", Http.FromCurl("curl -I https://example.com")!.Method);
            Assert.Equal("PUT", Http.FromCurl("curl -T file.txt https://example.com")!.Method);
        }

        [Fact]
        public void TheGetFlagMovesTheBodyIntoTheQuery()
        {
            // -G is what turns a -d into a query string, and the reason it cannot simply be ignored
            // is that the request would otherwise go out as a POST to an endpoint expecting a GET.
            HttpRequest request = Http.FromCurl("curl -G https://example.com/s -d 'q=a b' -d 'n=2'")!;

            Assert.Equal("GET", request.Method);
            Assert.Equal("https://example.com/s?q=a b&n=2", request.Url);
            Assert.Equal(string.Empty, request.Body);
        }

        [Fact]
        public void JoinsRepeatedDataFlagsTheWayCurlDoes()
        {
            HttpRequest request = Http.FromCurl("curl https://example.com -d 'a=1' -d 'b=2'")!;

            Assert.Equal("POST", request.Method);
            Assert.Equal("a=1&b=2", request.Body);
        }

        [Fact]
        public void TheJsonFlagBringsItsHeadersWithIt()
        {
            HttpRequest request = Http.FromCurl("""curl --json '{"a":1}' https://example.com""")!;

            Assert.Equal("POST", request.Method);
            Assert.Equal("""{"a":1}""", request.Body);
            Assert.Contains(request.Headers,
                line => line is { Name: "Content-Type", Value: "application/json" });
            Assert.Contains(request.Headers, line => line.Name == "Accept");
        }

        [Fact]
        public void UrlEncodesWhatTheEncodingFlagAsksItTo()
        {
            HttpRequest request = Http.FromCurl(
                "curl https://example.com --data-urlencode 'q=a b&c'")!;

            Assert.Equal("q=a%20b%26c", request.Body);
        }

        [Fact]
        public void ReadsAnsiQuotedStrings()
        {
            // Chromium switches to $'…' as soon as a value holds something it would rather escape.
            // Left unread, the tab in this header goes out as a literal backslash-t.
            HttpRequest request = Http.FromCurl(
                """curl $'https://example.com/a' -H $'X-C: one\ttwoé'""")!;

            Assert.Equal("https://example.com/a", request.Url);
            Assert.Equal("one\ttwoé", Assert.Single(request.Headers).Value);
        }

        [Fact]
        public void HandlesTheContinuationOfWhicheverShellCopiedIt()
        {
            // cmd's caret and PowerShell's backtick, which "Copy as cURL (cmd)" emits and which a
            // bash-only splitter reads as part of the URL.
            HttpRequest caret = Http.FromCurl("curl ^\n  \"https://example.com/a\" ^\n  -H \"X-D: 4\"")!;

            Assert.Equal("https://example.com/a", caret.Url);
            Assert.Contains(caret.Headers, line => line is { Name: "X-D", Value: "4" });

            HttpRequest tick = Http.FromCurl("curl `\n  'https://example.com/b'")!;

            Assert.Equal("https://example.com/b", tick.Url);
        }

        [Fact]
        public void IgnoresAPromptCopiedAlongWithTheCommand()
        {
            Assert.Equal("https://example.com/a", Http.FromCurl("$ curl https://example.com/a")!.Url);
            Assert.Equal("https://example.com/a", Http.FromCurl("sudo curl https://example.com/a")!.Url);
        }

        [Fact]
        public void StillRefusesWhatIsNotACurlCommand()
        {
            Assert.Null(Http.FromCurl("curl -o only-a-file.json"));
            Assert.Null(Http.FromCurl("https://example.com"));
            Assert.Null(Http.FromCurl("   "));
        }

        [Fact]
        public void ExportsCookiesBackTheWayItReadThem()
        {
            // The round trip matters because the export is what gets pasted into a ticket or a
            // script: a Cookie header written as -H is valid curl, but -b is what anyone reading it
            // expects to see, and it is what comes back in on the next import.
            HttpRequest request = Http.FromCurl(FromDevtools)!;
            HttpRequest back = Http.FromCurl(Http.AsCurl(request))!;

            Assert.Equal(request.Url, back.Url);
            Assert.Equal(request.Headers.Count, back.Headers.Count);

            HeaderLine cookie = Assert.Single(back.Headers,
                header => header.Name.Equals("Cookie", StringComparison.OrdinalIgnoreCase));

            Assert.Equal("first=one; second=two; third=three", cookie.Value);
        }
    }
}
