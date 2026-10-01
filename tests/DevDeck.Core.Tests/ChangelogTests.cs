using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Which parts of the changelog an update offers, and the switch a sign-in start carries.
    /// </summary>
    public sealed class ChangelogTests
    {
        private const string Sample =
            "# Changelog\r\n\r\n## 1.3.0\r\n\r\n- Not out yet.\r\n\r\n## 1.2.0\r\n\r\n- **Bold** and `code`.\r\n\r\n"
            + "## 1.1.0\r\n\r\n- Middle.\r\n\r\n## 1.0.1\r\n\r\n- Already running this.\r\n";

        [Fact]
        public void EveryVersionBetweenTheRunningOneAndTheReleaseIsOfferedNewestFirst()
        {
            IReadOnlyList<ChangelogEntry> found =
                Changelog.Between(Sample, new Version(1, 0, 1), new Version(1, 2, 0));

            Assert.Equal(["1.2.0", "1.1.0"], found.Select(entry => entry.Version));
        }

        [Fact]
        public void TheNotesAreReadableAsPlainText()
        {
            ChangelogEntry entry = Changelog.Between(Sample, new Version(1, 1, 0), new Version(1, 2, 0)).Single();

            Assert.Equal("- Bold and code.", entry.Notes);
        }

        [Fact]
        public void NothingIsOfferedWhenAlreadyUpToDate()
        {
            Assert.Empty(Changelog.Between(Sample, new Version(1, 2, 0), new Version(1, 2, 0)));
        }

        /// <summary>
        ///  A release cannot ship without saying what it brought: the Changelog page opens on the
        ///  running version after every update, and an empty page there is the failure.
        /// </summary>
        [Fact]
        public void TheBundledChangelogDescribesThisVersionFirst()
        {
            Assert.NotEmpty(Changelog.Bundled);
            Assert.Equal(AppVersion.Number, Changelog.Bundled[0].Version);
        }

        [Fact]
        public void TheSignInSwitchIsTakenOffAndReported()
        {
            string[] rest = AutoStart.Strip(["--autostart", "run", "Build"], out bool atSignIn);

            Assert.True(atSignIn);
            Assert.Equal(["run", "Build"], rest);
        }

        [Fact]
        public void AnOrdinaryStartIsNotASignIn()
        {
            string[] rest = AutoStart.Strip(["run", "Build"], out bool atSignIn);

            Assert.False(atSignIn);
            Assert.Equal(["run", "Build"], rest);
        }
    }
}
