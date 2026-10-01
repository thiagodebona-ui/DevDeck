using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Reading a chain out of a model's reply.
    /// </summary>
    /// <remarks>
    ///  The other side of this format is whatever a language model writes, which drifts: quotes or
    ///  none, a numbered list or a dashed one, "Stop on failure" or "stop-on-failure". These pin
    ///  down the spellings the parser forgives, so a stricter rewrite cannot quietly stop reading
    ///  answers that work today.
    /// </remarks>
    public class ChainPlanTests
    {
        private const string Reply = """
            Here are three commands and a chain that reports on them.

            ```powershell name="Count files"
            (Get-ChildItem -Recurse -File).Count
            ```

            ```shell name=Branch
            git branch --show-current
            ```

            ```powershell name='Report'
            $folder = $env:DEVDECK_CHAIN_OUTPUTS
            Get-Content (Join-Path $folder 'step1.txt')
            ```

            ```devdeck-chain
            name: Repository report
            stop-on-failure: false
            steps:
            - Count files
            - "Branch"
            - `Report`
            ```
            """;

        [Fact]
        public void TheChainCarriesItsNameStepsAndFailureRule()
        {
            ChainPlan plan = Assert.IsType<ChainPlan>(ChainPlan.Parse(Reply));

            Assert.Equal("Repository report", plan.Name);
            Assert.False(plan.StopOnFailure);
            Assert.Equal(["Count files", "Branch", "Report"], plan.Steps);
        }

        /// <summary>Each name spelling a model uses - double, single or no quotes - is read.</summary>
        [Fact]
        public void EveryNamedBlockBecomesACommandOfTheRightKind()
        {
            ChainPlan plan = Assert.IsType<ChainPlan>(ChainPlan.Parse(Reply));

            Assert.Equal(["Count files", "Branch", "Report"], plan.Commands.Select(command => command.Name));
            Assert.Equal(CommandKind.PowerShell, plan.Commands[0].Kind);
            Assert.Equal(CommandKind.Shell, plan.Commands[1].Kind);
            Assert.Equal(CommandKind.PowerShell, plan.Commands[2].Kind);
            Assert.StartsWith("$folder = $env:DEVDECK_CHAIN_OUTPUTS", plan.Commands[2].Body);
        }

        [Fact]
        public void NoChainBlockMeansNoChain()
        {
            Assert.Null(ChainPlan.Parse("```powershell name=\"Count\"\nGet-Date\n```"));
            Assert.Null(ChainPlan.Parse("Just prose."));
        }

        /// <summary>A step that names nothing in the reply is kept: it may be a command already in the deck.</summary>
        [Fact]
        public void AStepCanNameACommandTheReplyDidNotWrite()
        {
            ChainPlan plan = Assert.IsType<ChainPlan>(ChainPlan.Parse("""
                ```devdeck-chain
                name: Morning
                steps:
                1. Where am I
                2) Today's commits
                ```
                """));

            Assert.Equal(["Where am I", "Today's commits"], plan.Steps);
            Assert.Empty(plan.Commands);

            // Stop on failure is the safe default, as it is for a chain made by hand.
            Assert.True(plan.StopOnFailure);
        }

        /// <summary>A step is spelled the way its block is, so the chain finds the command.</summary>
        [Fact]
        public void AStepMatchesItsBlockWhateverTheCase()
        {
            ChainPlan plan = Assert.IsType<ChainPlan>(ChainPlan.Parse("""
                ```shell name="Build It"
                dotnet build
                ```

                ```devdeck-chain
                Name: Ship
                Stop on failure: yes
                Steps:
                - build it
                ```
                """));

            Assert.Equal(["Build It"], plan.Steps);
            Assert.Equal("Ship", plan.Name);
            Assert.True(plan.StopOnFailure);
        }

        /// <summary>A command used twice in the chain is still only one command.</summary>
        [Fact]
        public void ACommandUsedTwiceIsDefinedOnce()
        {
            ChainPlan plan = Assert.IsType<ChainPlan>(ChainPlan.Parse("""
                ```shell name="Build"
                dotnet build
                ```

                ```devdeck-chain
                steps:
                - Build
                - Build
                ```
                """));

            Assert.Single(plan.Commands);
            Assert.Equal(2, plan.Steps.Count);
            Assert.Equal("New chain", plan.Name);
        }

        [Fact]
        public void TheFenceLineSplitsIntoLanguageAndName()
        {
            Assert.Equal("powershell", ChainPlan.LanguageOf("powershell name=\"Count files\""));
            Assert.Equal("Count files", ChainPlan.NameOf("powershell name=\"Count files\""));
            Assert.Equal("bash", ChainPlan.LanguageOf("bash"));
            Assert.Equal(string.Empty, ChainPlan.NameOf("bash"));
            Assert.True(ChainPlan.IsChainFence("devdeck-chain"));
            Assert.False(ChainPlan.IsChainFence("powershell name=\"devdeck-chain\""));
        }

        /// <summary>A named block is still saved as the language its fence says, not guessed from its body.</summary>
        [Fact]
        public void SavingANamedBlockReadsItsLanguage()
        {
            (string command, CommandKind kind) = CodeBlock.Last("```powershell name=\"Date\"\nGet-Date\n```");

            Assert.Equal("Get-Date", command);
            Assert.Equal(CommandKind.PowerShell, kind);
        }

        /// <summary>The briefing teaches the format this parser reads, and the variables a step gets.</summary>
        [Fact]
        public void TheBriefingTeachesTheChainFormat()
        {
            string briefing = AiBriefing.For(string.Empty);

            Assert.Contains("```" + ChainPlan.Fence, briefing);
            Assert.Contains(RunContext.Outputs, briefing);
            Assert.Contains(RunContext.Previous, briefing);

            // The worked example in the briefing has to be something this parser accepts.
            int start = briefing.IndexOf("For example:", StringComparison.Ordinal);
            ChainPlan plan = Assert.IsType<ChainPlan>(ChainPlan.Parse(briefing[start..]));

            Assert.Equal(3, plan.Commands.Count);
            Assert.Equal(["Count files", "Count TODOs", "Report"], plan.Steps);
        }
    }
}
