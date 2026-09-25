using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  How a response body is split into coloured runs.
    /// </summary>
    /// <remarks>
    ///  The contract the pane depends on is tested first and hardest: every character of the body
    ///  appears in exactly one token, in order, with no gaps. The pane builds its text by
    ///  concatenating those runs, so a lexer that drops a span does not merely mis-colour - it
    ///  shows the user a body that is not the one that arrived, which is worse than no colour.
    ///
    ///  After that, the distinctions that are the point of the exercise: a field name apart from a
    ///  value, a tag apart from the words between tags, and a half-written document surviving both.
    /// </remarks>
    public class MarkupTests
    {
        #region Nothing is lost

        /// <summary>
        ///  The rule the pane relies on. A dropped run would silently rewrite the reply.
        /// </summary>
        /// <remarks>
        ///  The kind arrives as a name rather than as the enum: BodyKind is internal, and a public
        ///  test signature cannot take one. Named by the theory so a failure still says which.
        /// </remarks>
        [Theory]
        [InlineData("Json", "{\"id\":1,\"name\":\"a\",\"ok\":true,\"sub\":{\"x\":[1,2,null]}}")]
        [InlineData("Html", "<html><body class=\"x\">hello <b>there</b></body></html>")]
        [InlineData("Xml", "<?xml version=\"1.0\"?><r a=\"1\"><a>text</a><!-- note --></r>")]
        public void EveryCharacterIsAccountedFor(string kind, string body)
        {
            Assert.Equal(body, Rebuild(body, Kind(kind)));
        }

        [Theory]
        [InlineData("Json", "{\"a\":1}")]
        [InlineData("Html", "<p>x</p>")]
        public void TheRunsAreInOrderAndDoNotOverlap(string kind, string body)
        {
            int at = 0;

            foreach (Token token in Markup.Tokenize(body, Kind(kind)))
            {
                Assert.Equal(at, token.Start);
                Assert.True(token.Length > 0);

                at += token.Length;
            }

            Assert.Equal(body.Length, at);
        }

        /// <summary>
        ///  A body that is not one of the three still paints. An empty list here would show a
        ///  blank pane over a reply that did arrive.
        /// </summary>
        [Fact]
        public void APlainBodyIsStillOneWholeRun()
        {
            IReadOnlyList<Token> tokens = Markup.Tokenize("just some words", BodyKind.Plain);

            Assert.Single(tokens);
            Assert.Equal(TokenKind.Plain, tokens[0].Kind);
            Assert.Equal("just some words".Length, tokens[0].Length);
        }

        [Fact]
        public void AnEmptyBodyHasNothingToPaint()
        {
            Assert.Empty(Markup.Tokenize(string.Empty, BodyKind.Json));
        }

        /// <summary>Only the three with structure are coloured; the rest are drawn flat.</summary>
        [Fact]
        public void OnlyStructuredBodiesAreHandled()
        {
            Assert.True(Markup.Handles(BodyKind.Json));
            Assert.True(Markup.Handles(BodyKind.Xml));
            Assert.True(Markup.Handles(BodyKind.Html));

            Assert.False(Markup.Handles(BodyKind.Plain));
            Assert.False(Markup.Handles(BodyKind.Binary));
        }

        #endregion

        #region JSON

        /// <summary>
        ///  The distinction that makes a JSON body readable: the name on the left is not the same
        ///  as a string on the right, and only the colon tells them apart.
        /// </summary>
        [Fact]
        public void AFieldNameIsNotTheSameAsAStringValue()
        {
            const string body = "{\"name\":\"value\"}";

            Assert.Equal(TokenKind.Variable, KindOf(body, BodyKind.Json, "\"name\""));
            Assert.Equal(TokenKind.Text, KindOf(body, BodyKind.Json, "\"value\""));
        }

        [Fact]
        public void NumbersAndTheThreeKeywordsAreTheirOwnColours()
        {
            const string body = "{\"a\":12.5,\"b\":true,\"c\":null,\"d\":-3e10}";

            Assert.Equal(TokenKind.Number, KindOf(body, BodyKind.Json, "12.5"));
            Assert.Equal(TokenKind.Keyword, KindOf(body, BodyKind.Json, "true"));
            Assert.Equal(TokenKind.Keyword, KindOf(body, BodyKind.Json, "null"));
            Assert.Equal(TokenKind.Number, KindOf(body, BodyKind.Json, "-3e10"));
        }

        /// <summary>
        ///  A quote inside a string does not end it. Without the escape clause a value ending in a
        ///  backslash swallows the remainder of the document into one colour.
        /// </summary>
        [Fact]
        public void AnEscapedQuoteDoesNotEndTheString()
        {
            const string body = "{\"a\":\"he said \\\"no\\\"\",\"b\":1}";

            Assert.Equal(body, Rebuild(body, BodyKind.Json));
            Assert.Equal(TokenKind.Number, KindOf(body, BodyKind.Json, "1"));
        }

        /// <summary>
        ///  A truncated reply is the common case, not an edge one: a timeout cuts the body
        ///  mid-string and the pane still has to show what arrived.
        /// </summary>
        [Fact]
        public void ATruncatedBodyStillPaints()
        {
            const string body = "{\"a\":1,\"b\":\"unterminated";

            Assert.Equal(body, Rebuild(body, BodyKind.Json));
        }

        #endregion

        #region Markup

        /// <summary>
        ///  The words between the tags are the page. Colouring those as markup is what makes an
        ///  HTML reply unreadable.
        /// </summary>
        [Fact]
        public void TextBetweenTagsIsLeftAlone()
        {
            const string body = "<p>hello there</p>";

            Assert.Equal(TokenKind.Plain, KindOf(body, BodyKind.Html, "hello there"));
        }

        [Fact]
        public void AnAttributeNameIsApartFromItsValue()
        {
            const string body = "<a href=\"/x\">go</a>";

            Assert.Equal(TokenKind.Variable, KindOf(body, BodyKind.Html, "href"));
            Assert.Equal(TokenKind.Text, KindOf(body, BodyKind.Html, "\"/x\""));
        }

        /// <summary>Markup quoted inside a comment is prose, not markup.</summary>
        [Fact]
        public void MarkupInsideACommentStaysAComment()
        {
            const string body = "<!-- <b>not bold</b> --><i>italic</i>";

            Assert.Equal(TokenKind.Comment, KindOf(body, BodyKind.Html, "<!-- <b>not bold</b> -->"));
            Assert.Equal(body, Rebuild(body, BodyKind.Html));
        }

        [Fact]
        public void ADoctypeAndADeclarationAreFurniture()
        {
            Assert.Equal(
                TokenKind.Comment,
                KindOf("<!DOCTYPE html><html></html>", BodyKind.Html, "<!DOCTYPE html>"));

            Assert.Equal(
                TokenKind.Comment,
                KindOf("<?xml version=\"1.0\"?><r/>", BodyKind.Xml, "<?xml version=\"1.0\"?>"));
        }

        /// <summary>
        ///  A page cut off inside a tag is what a proxy timeout produces, and it still has to show.
        /// </summary>
        [Fact]
        public void AnUnclosedTagStillPaints()
        {
            const string body = "<html><body><div class=\"x";

            Assert.Equal(body, Rebuild(body, BodyKind.Html));
        }

        #endregion

        /// <summary>A kind by name, so the theories above can stay public.</summary>
        private static BodyKind Kind(string name) => Enum.Parse<BodyKind>(name);

        /// <summary>The body as the pane would rebuild it, run by run.</summary>
        private static string Rebuild(string body, BodyKind kind) =>
            string.Concat(Markup.Tokenize(body, kind)
                .Select(token => body.Substring(token.Start, token.Length)));

        /// <summary>The kind of the run covering <paramref name="piece"/>.</summary>
        private static TokenKind KindOf(string body, BodyKind kind, string piece)
        {
            int where = body.IndexOf(piece, StringComparison.Ordinal);

            Assert.True(where >= 0, $"'{piece}' is not in the body under test.");

            return Markup.Tokenize(body, kind)
                .First(token => token.Start <= where && token.Start + token.Length >= where + piece.Length)
                .Kind;
        }
    }
}
