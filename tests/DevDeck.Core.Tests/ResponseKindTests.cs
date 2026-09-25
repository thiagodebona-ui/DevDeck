using System.Text;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  How a response body is recognised, which decides how it is drawn.
    /// </summary>
    /// <remarks>
    ///  The rule under test throughout: the content type chooses what to try, and the body decides.
    ///  Both halves matter and both are things real servers get wrong - a JSON API that labels its
    ///  output <c>text/plain</c>, and an endpoint that swears it returns JSON and hands back an
    ///  HTML error page from the proxy in front of it.
    /// </remarks>
    public class ResponseKindTests
    {
        #region What the header says

        [Theory]
        [InlineData("application/json")]
        [InlineData("application/vnd.api+json")]
        [InlineData("APPLICATION/JSON")]
        public void JsonIsRecognisedFromItsContentType(string media)
        {
            Assert.Equal(BodyKind.Json, Http.Classify("{\"ok\":true}", media));
        }

        [Fact]
        public void XmlIsRecognisedFromItsContentType()
        {
            Assert.Equal(BodyKind.Xml, Http.Classify("<?xml version=\"1.0\"?><r><a>1</a></r>", "application/xml"));
        }

        [Fact]
        public void HtmlIsRecognisedFromItsContentType()
        {
            Assert.Equal(BodyKind.Html, Http.Classify("<html><body>hello</body></html>", "text/html"));
        }

        [Fact]
        public void PlainTextStaysPlain()
        {
            Assert.Equal(BodyKind.Plain, Http.Classify("just some words", "text/plain"));
        }

        #endregion

        #region What the body actually is

        /// <summary>
        ///  The case that matters most: an API that labels JSON as text/plain still gets it
        ///  formatted, because the body parses and that is the better evidence.
        /// </summary>
        [Theory]
        [InlineData("text/plain")]
        [InlineData("")]
        [InlineData("application/octet-stream")]
        public void JsonIsRecognisedEvenWhenMislabelled(string media)
        {
            Assert.Equal(BodyKind.Json, Http.Classify("{\"id\":1,\"name\":\"a\"}", media));
            Assert.Equal(BodyKind.Json, Http.Classify("[1, 2, 3]", media));
        }

        /// <summary>
        ///  The mirror of it: a body announced as JSON that does not parse is not JSON. This is the
        ///  proxy error page, and showing it as flat text is what lets the user see what they got.
        /// </summary>
        [Fact]
        public void SomethingAnnouncedAsJsonThatDoesNotParseIsNot()
        {
            Assert.NotEqual(BodyKind.Json, Http.Classify("<html><body>502 Bad Gateway</body></html>", "application/json"));
            Assert.Equal(BodyKind.Plain, Http.Classify("not json at all", "application/json"));
        }

        [Fact]
        public void AnHtmlErrorPageUnderAJsonLabelIsStillHtml()
        {
            Assert.Equal(
                BodyKind.Html,
                Http.Classify("<!DOCTYPE html><html><body>504</body></html>", "application/json"));
        }

        [Fact]
        public void XmlIsRecognisedFromItsDeclaration()
        {
            Assert.Equal(BodyKind.Xml, Http.Classify("<?xml version=\"1.0\"?><a>1</a>", "text/plain"));
        }

        /// <summary>XHTML is both; a page is more usefully read as a page.</summary>
        [Fact]
        public void HtmlWinsOverXmlWhenItIsBoth()
        {
            Assert.Equal(BodyKind.Html, Http.Classify("<html xmlns=\"http://www.w3.org/1999/xhtml\"></html>", "application/xhtml+xml"));
        }

        [Fact]
        public void AnEmptyBodyIsPlain()
        {
            Assert.Equal(BodyKind.Plain, Http.Classify(string.Empty, "application/json"));
            Assert.Equal(BodyKind.Plain, Http.Classify("   ", "application/json"));
        }

        #endregion

        #region Binaries

        /// <summary>
        ///  A null byte, whatever the label. An image rendered into a text box is a screenful of
        ///  noise that can lock the UI up while it lays out.
        /// </summary>
        /// <remarks>
        ///  image/png used to be in this list and is not any more. The pane draws images now, so a
        ///  PNG has somewhere better to go than the binary placeholder - and the property this test
        ///  exists to protect, that raw bytes never reach a text box, is still true either way.
        /// </remarks>
        [Theory]
        [InlineData("application/octet-stream")]
        [InlineData("application/json")]
        public void BytesThatAreNotTextAreBinaryWhateverTheLabelSays(string media)
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x00, 0x1A, 0x0A];

            Assert.Equal(BodyKind.Binary, Http.Classify(Encoding.UTF8.GetString(png), media, png));
        }

        /// <summary>An image is an image, and is drawn rather than described.</summary>
        [Theory]
        [InlineData("image/png")]
        [InlineData("image/jpeg")]
        [InlineData("image/gif")]
        [InlineData("image/webp")]
        public void AnImageIsRenderedRatherThanCalledBinary(string media)
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x00, 0x1A, 0x0A];

            Assert.Equal(BodyKind.Image, Http.Classify(Encoding.UTF8.GetString(png), media, png));
        }

        [Fact]
        public void TextIsNotMistakenForABinary()
        {
            Assert.NotEqual(BodyKind.Binary, Http.Classify("{\"ok\":true}", "application/json"));
        }

        #endregion

        #region What the pane says it is

        [Fact]
        public void TheLabelNamesTheFormat()
        {
            Assert.Equal("JSON", (Result() with { Kind = BodyKind.Json }).BodyLabel);
            Assert.Equal("XML", (Result() with { Kind = BodyKind.Xml }).BodyLabel);
            Assert.Equal("HTML", (Result() with { Kind = BodyKind.Html }).BodyLabel);
        }

        /// <summary>A binary has nothing to say for itself except what it claimed to be.</summary>
        [Fact]
        public void ABinaryIsLabelledWithItsMediaType()
        {
            HttpResult result = Result() with { Kind = BodyKind.Binary, MediaType = "image/png" };

            Assert.Equal("image/png", result.BodyLabel);
        }

        [Fact]
        public void AnUnlabelledBinaryStillSaysSomething()
        {
            Assert.Equal("binary", (Result() with { Kind = BodyKind.Binary }).BodyLabel);
        }

        [Fact]
        public void PlainTextIsLabelledWithItsMediaTypeOrJustText()
        {
            Assert.Equal("text/csv", (Result() with { MediaType = "text/csv" }).BodyLabel);
            Assert.Equal("text", Result().BodyLabel);
        }

        /// <summary>Only the three with structure are worth drawing with colour.</summary>
        [Fact]
        public void OnlyStructuredBodiesSaySoAreStructured()
        {
            Assert.True((Result() with { Kind = BodyKind.Json }).IsStructured);
            Assert.True((Result() with { Kind = BodyKind.Xml }).IsStructured);
            Assert.True((Result() with { Kind = BodyKind.Html }).IsStructured);

            Assert.False((Result() with { Kind = BodyKind.Plain }).IsStructured);
            Assert.False((Result() with { Kind = BodyKind.Binary }).IsStructured);
        }

        private static HttpResult Result() =>
            new(200, "OK", string.Empty, [], 1, 0, null);

        #endregion
    }
}
