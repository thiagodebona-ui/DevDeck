using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The parts of the HTTP panel that can be checked without a server.
    /// </summary>
    /// <remarks>
    ///  Which is all the parts worth checking. Sending is the framework's job; what belongs to this
    ///  app is the curl round trip, and that is pure text in and text out.
    /// </remarks>
    public class HttpTests
    {
        [Fact]
        public void ReadsAPlainCurlCommand()
        {
            HttpRequest request = Http.FromCurl("curl https://api.example.com/users")!;

            Assert.Equal("GET", request.Method);
            Assert.Equal("https://api.example.com/users", request.Url);
            Assert.Empty(request.Headers);
        }

        [Fact]
        public void ABodyWithoutAMethodIsAPost()
        {
            // curl's own rule, and the reason a documentation snippet rarely spells out -X POST.
            HttpRequest request = Http.FromCurl("""curl https://example.com -d '{"a":1}'""")!;

            Assert.Equal("POST", request.Method);
            Assert.Equal("""{"a":1}""", request.Body);
        }

        [Fact]
        public void ReadsTheFormDevtoolsHandsOut()
        {
            HttpRequest request = Http.FromCurl("""
                curl -X PUT 'https://api.example.com/v1/items/42' \
                  -H 'Content-Type: application/json' \
                  -H 'Authorization: Bearer abc.def' \
                  --data-raw '{"name":"a thing","tags":["x","y"]}'
                """)!;

            Assert.Equal("PUT", request.Method);
            Assert.Equal("https://api.example.com/v1/items/42", request.Url);
            Assert.Equal(2, request.Headers.Count);
            Assert.Equal("Authorization", request.Headers[1].Name);
            Assert.Equal("Bearer abc.def", request.Headers[1].Value);
            Assert.Contains("a thing", request.Body);
        }

        [Fact]
        public void IgnoresFlagsItDoesNotUnderstand()
        {
            // --compressed and -s ride along on most copied commands and must not derail the parse
            // or be mistaken for the URL.
            HttpRequest request = Http.FromCurl("curl -s --compressed https://example.com/a")!;

            Assert.Equal("https://example.com/a", request.Url);
        }

        [Fact]
        public void RejectsSomethingThatIsNotCurlAtAll()
        {
            Assert.Null(Http.FromCurl("wget https://example.com"));
            Assert.Null(Http.FromCurl("curl -X POST"));
            Assert.Null(Http.FromCurl(string.Empty));
        }

        [Fact]
        public void SurvivesTheRoundTrip()
        {
            HttpRequest original = new()
            {
                Method = "POST",
                Url = "https://example.com/hook",
                Body = """{"message":"it's here","quote":"\"x\""}""",
                Headers = [new HeaderLine { Name = "X-Token", Value = "abc 123" }],
            };

            HttpRequest back = Http.FromCurl(Http.AsCurl(original))!;

            Assert.Equal(original.Method, back.Method);
            Assert.Equal(original.Url, back.Url);
            Assert.Equal(original.Body, back.Body);
            Assert.Contains(back.Headers, header => header is { Name: "X-Token", Value: "abc 123" });
        }

        [Fact]
        public void QuotesABodyContainingASingleQuote()
        {
            // The '\'' dance. If this is wrong the command is not merely ugly, it is a different
            // command - and this body shape (an apostrophe in JSON text) is entirely ordinary.
            string curl = Http.AsCurl(new HttpRequest
            {
                Method = "POST",
                Url = "https://example.com",
                Body = "it's",
            });

            Assert.Contains("""'it'\''s'""", curl);
            Assert.Equal("it's", Http.FromCurl(curl)!.Body);
        }

        [Fact]
        public void GuessesJsonFromTheBodyWhenNoHeaderSaysSo()
        {
            string curl = Http.AsCurl(new HttpRequest
            {
                Method = "POST",
                Url = "https://example.com",
                Body = """{"a":1}""",
            });

            Assert.Contains("Content-Type: application/json", curl);
        }

        [Fact]
        public void DoesNotAddAContentTypeTheUserAlreadySet()
        {
            string curl = Http.AsCurl(new HttpRequest
            {
                Method = "POST",
                Url = "https://example.com",
                Body = """{"a":1}""",
                Headers = [new HeaderLine { Name = "Content-Type", Value = "application/ld+json" }],
            });

            Assert.Contains("application/ld+json", curl);
            Assert.DoesNotContain("application/json'", curl);
        }

        [Fact]
        public void ADisabledHeaderIsNotSent()
        {
            string curl = Http.AsCurl(new HttpRequest
            {
                Url = "https://example.com",
                Headers =
                [
                    new HeaderLine { Name = "Kept", Value = "1" },
                    new HeaderLine { Name = "Dropped", Value = "2", Enabled = false },
                ],
            });

            Assert.Contains("Kept", curl);
            Assert.DoesNotContain("Dropped", curl);
        }

        [Fact]
        public void NamesARequestAfterItsPath()
        {
            Assert.Equal("v1/items", Http.Name("https://api.example.com/v1/items"));
            Assert.Equal("api.example.com", Http.Name("https://api.example.com"));
            Assert.Equal("health", Http.Name("localhost:5000/health"));
        }

        [Fact]
        public async Task AnEmptyUrlFailsWithoutThrowing()
        {
            HttpResult result = await Http.Send(new HttpRequest());

            Assert.True(result.Failed);
            Assert.Equal(0, result.Status);
        }

        [Fact]
        public void FormatsTheSizesAHumanReads()
        {
            Assert.Equal("512 B", new HttpResult(200, "OK", "", [], 1, 512, null).Size);
            Assert.Equal("2.0 KB", new HttpResult(200, "OK", "", [], 1, 2048, null).Size);
            Assert.Equal("1.5 MB", new HttpResult(200, "OK", "", [], 1, 1_572_864, null).Size);
        }

        [Fact]
        public void AFourOhFourIsAnAnswerRatherThanAFailure()
        {
            HttpResult missing = new(404, "Not Found", "", [], 12, 0, null);

            Assert.False(missing.Failed);
            Assert.Equal("warn", missing.Tone);
            Assert.Equal("bad", new HttpResult(500, "Server Error", "", [], 1, 0, null).Tone);
            Assert.Equal("good", new HttpResult(204, "No Content", "", [], 1, 0, null).Tone);
        }
    }
}
