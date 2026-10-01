using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>The folder a chain run keeps every step's output in.</summary>
    public class ChainOutputsTests
    {
        [Fact]
        public void EachStepIsSavedUnderItsPositionAndTheFolderGoesAtTheEnd()
        {
            string root = Path.Combine(Path.GetTempPath(), "DevDeck.Tests", Guid.NewGuid().ToString("N"));

            string folder;

            using (ChainOutputs outputs = Assert.IsType<ChainOutputs>(ChainOutputs.Create(root)))
            {
                folder = outputs.Folder;

                outputs.Write(1, "42\n");
                outputs.Write(3, "main\n");

                Assert.Equal("42\n", File.ReadAllText(Path.Combine(folder, "step1.txt")));
                Assert.Equal("main\n", File.ReadAllText(Path.Combine(folder, "step3.txt")));
                Assert.Equal(outputs.PathFor(3), Path.Combine(folder, "step3.txt"));
            }

            Assert.False(Directory.Exists(folder));

            Directory.Delete(root, recursive: true);
        }

        /// <summary>Two runs at once never share a folder, or one would read the other's step2.txt.</summary>
        [Fact]
        public void TwoRunsGetTwoFolders()
        {
            string root = Path.Combine(Path.GetTempPath(), "DevDeck.Tests", Guid.NewGuid().ToString("N"));

            using ChainOutputs first = Assert.IsType<ChainOutputs>(ChainOutputs.Create(root));
            using ChainOutputs second = Assert.IsType<ChainOutputs>(ChainOutputs.Create(root));

            Assert.NotEqual(first.Folder, second.Folder);
        }

        [Fact]
        public void AStepIsToldWhereTheOutputsAre()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain("Report", 3, 3, outputs: @"C:\temp\run");

            Assert.Equal(@"C:\temp\run", given[RunContext.Outputs]);
            Assert.Equal("DEVDECK_CHAIN_OUTPUTS", RunContext.Outputs);
        }

        /// <summary>Absent rather than empty when there is no folder, as the previous-step variables are.</summary>
        [Fact]
        public void NoFolderMeansNoVariable()
        {
            Assert.False(RunContext.Chain("Report", 1, 1).ContainsKey(RunContext.Outputs));
        }
    }
}
