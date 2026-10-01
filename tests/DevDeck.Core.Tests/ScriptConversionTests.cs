using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>Rewriting a command in another language with the assistant's model.</summary>
    public class ScriptConversionTests
    {
        [Fact]
        public void TheScriptIsTheLastFencedBlock()
        {
            string? script = ScriptConversion.Extract("""
                Here you go:

                ```bash
                # count the files
                find . -type f | wc -l
                ```
                """);

            // The comment survives: CodeBlock would have read "# " as a prompt and stripped it.
            Assert.Equal($"# count the files{Environment.NewLine}find . -type f | wc -l", script);
        }

        [Fact]
        public void AReplyCutOffInsideItsBlockIsNotUsed()
        {
            Assert.Null(ScriptConversion.Extract("```bash\necho half"));
            Assert.Null(ScriptConversion.Extract("   "));
        }

        [Fact]
        public void AReplyWithNoFenceIsTakenWhole()
        {
            Assert.Equal("echo hi", ScriptConversion.Extract("echo hi\n"));
        }

        /// <summary>The request names both languages, the fence to answer in, and what must be kept.</summary>
        [Fact]
        public void TheRequestCarriesTheRulesThatMatter()
        {
            IReadOnlyList<ChatMessage> messages = ScriptConversion.Messages(
                "\"Hello, {{Name:world}}\"", CommandKind.PowerShell, CommandKind.Bash);

            Assert.Equal("system", messages[0].Role);
            Assert.Contains("labelled\nbash", messages[0].Content.ReplaceLineEndings("\n"));
            Assert.Contains("{{secret:NAME}}", messages[0].Content);
            Assert.Contains("DEVDECK_ARG_NAME", messages[0].Content);

            Assert.Contains("from a PowerShell script", messages[1].Content);
            Assert.Contains("to a bash script", messages[1].Content);
            Assert.Contains("{{Name:world}}", messages[1].Content);
        }
    }
}
