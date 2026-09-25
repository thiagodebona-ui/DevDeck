using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The highlighter's contract, which the editor depends on and nothing else enforces.
    /// </summary>
    /// <remarks>
    ///  The first test is the important one: the editor draws its coloured layer by walking these
    ///  tokens in order and appending each one's text. If the tokens ever skip a character or
    ///  overlap, the text under the caret stops matching the text in the box, and the two layers
    ///  drift apart in a way that looks like a rendering bug rather than a lexer bug.
    /// </remarks>
    public class SyntaxTests
    {
        // The kind is passed by name because xunit needs a public signature and CommandKind is
        // internal, like everything else in Core.
        [Theory]
        [InlineData(nameof(CommandKind.PowerShell))]
        [InlineData(nameof(CommandKind.Batch))]
        [InlineData(nameof(CommandKind.Bash))]
        [InlineData(nameof(CommandKind.Shell))]
        public void TokensCoverEveryCharacterExactlyOnce(string kindName)
        {
            CommandKind kind = Enum.Parse<CommandKind>(kindName);

            const string source = """
                # a comment with a "quote" and a $variable in it
                $name = "hello world"
                if ($count -gt 42) { Write-Host 'done' }
                REM trailing
                """;

            IReadOnlyList<Token> tokens = Syntax.Tokenize(source, kind);

            int at = 0;

            foreach (Token token in tokens)
            {
                Assert.Equal(at, token.Start);
                Assert.True(token.Length > 0, "a zero-length token would draw nothing and hide text");

                at += token.Length;
            }

            Assert.Equal(source.Length, at);
        }

        [Fact]
        public void EmptySourceProducesNoTokens()
        {
            Assert.Empty(Syntax.Tokenize(string.Empty, CommandKind.PowerShell));
        }

        [Fact]
        public void ReassemblingTheTokensGivesBackTheSource()
        {
            const string source = "git commit -m \"a message\" # and a note\n$x = 1";

            string rebuilt = string.Concat(
                Syntax.Tokenize(source, CommandKind.PowerShell)
                    .Select(token => source.Substring(token.Start, token.Length)));

            Assert.Equal(source, rebuilt);
        }

        /// <summary>A keyword inside a comment is comment-coloured, which is the ordering rule.</summary>
        [Fact]
        public void CommentsSwallowKeywords()
        {
            const string source = "# if else for while";

            IReadOnlyList<Token> tokens = Syntax.Tokenize(source, CommandKind.Bash);

            Assert.All(tokens, token => Assert.Equal(TokenKind.Comment, token.Kind));
        }

        [Fact]
        public void StringsSwallowVariables()
        {
            const string source = "\"$notAVariable\"";

            IReadOnlyList<Token> tokens = Syntax.Tokenize(source, CommandKind.PowerShell);

            Assert.All(tokens, token => Assert.Equal(TokenKind.Text, token.Kind));
        }

        [Fact]
        public void PowerShellVariablesAreFound()
        {
            IReadOnlyList<Token> tokens = Syntax.Tokenize("$total = 1", CommandKind.PowerShell);

            Assert.Contains(tokens, token => token.Kind == TokenKind.Variable);
        }

        /// <summary>
        ///  Half-written code is the normal case in an editor, so it must not throw or lose text.
        /// </summary>
        [Theory]
        [InlineData("\"unterminated string")]
        [InlineData("'")]
        [InlineData("<# unterminated block comment")]
        [InlineData("$")]
        [InlineData("%")]
        public void UnterminatedInputStillCoversEverything(string source)
        {
            int at = 0;

            foreach (Token token in Syntax.Tokenize(source, CommandKind.PowerShell))
            {
                Assert.Equal(at, token.Start);
                at += token.Length;
            }

            Assert.Equal(source.Length, at);
        }
    }
}
