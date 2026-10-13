using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>JavaScript as a Run with language, and a group keeping each of its processes.</summary>
    public class JavaScriptKindTests
    {
        /// <summary>The number is what a settings file stores, so the older kinds must keep theirs.</summary>
        [Fact]
        public void JavaScriptComesAfterEveryOlderKind()
        {
            Assert.Equal(0, (int)CommandKind.Shell);
            Assert.Equal(3, (int)CommandKind.Bash);
            Assert.Equal(4, (int)CommandKind.JavaScript);
        }

        [Theory]
        [InlineData("javascript")]
        [InlineData("js")]
        [InlineData("node")]
        [InlineData("JS")]
        public void AFencedBlockLabelledJavaScriptRunsWithNode(string label)
        {
            // Even a single line, which every other language would run as a plain command line.
            Assert.Equal(CommandKind.JavaScript, CodeBlock.KindFor(label, "console.log(1)"));
        }

        [Theory]
        [InlineData("javascript")]
        [InlineData("js")]
        [InlineData("node")]
        public void AProjectDeckCanAskForJavaScript(string kind)
        {
            Assert.Equal(CommandKind.JavaScript, new DeckEntry { Kind = kind }.Parsed);
        }

        [Fact]
        public void JavaScriptIsColoured()
        {
            IReadOnlyList<Token> tokens = Syntax.Tokenize(
                "// say hello\nconst name = 'world';\nconsole.log(`hi ${name}`, 42);", CommandKind.JavaScript);

            Assert.Contains(tokens, token => token.Kind == TokenKind.Comment);
            Assert.Contains(tokens, token => token.Kind == TokenKind.Keyword);
            Assert.Contains(tokens, token => token.Kind == TokenKind.Text);
            Assert.Contains(tokens, token => token.Kind == TokenKind.Number);
        }

        [Fact]
        public void TheAssistantIsToldWhatJavaScriptMeans()
        {
            Assert.Equal("javascript", ScriptConversion.Fence(CommandKind.JavaScript));
            Assert.Contains("Node", ScriptConversion.Describe(CommandKind.JavaScript));
        }

        /// <summary>
        ///  Without the fence in the briefing, a model asked for JavaScript wraps it in PowerShell.
        /// </summary>
        [Fact]
        public void TheBriefingOffersAJavaScriptFenceWhenNodeIsInstalled()
        {
            string briefing = AiBriefing.For(string.Empty);

            Assert.Equal(ScriptFile.FindNode() is not null, briefing.Contains("```javascript", StringComparison.Ordinal));
            Assert.Contains("Never wrap it in another script", briefing);
        }

        [Fact]
        public void AGroupKeepsEachProcessWithItsOwnFigures()
        {
            ProcessUsage group = ProcessSampler.Group(
            [
                new(10, "node", 300, 1),
                new(11, "node", 500, 2),
            ]).Single();

            Assert.Equal([11, 10], group.Members.Select(member => member.Id));
            Assert.Equal(500, group.Members[0].WorkingSet);
            Assert.Equal(2, group.Members[0].CpuPercent);
        }

        [Fact]
        public void ALoneProcessHasNoMembersToOpen()
        {
            Assert.Empty(ProcessSampler.Group([new ProcessUsage(20, "chrome", 700)]).Single().Members);
        }
    }
}
