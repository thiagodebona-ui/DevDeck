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

        /// <summary>A heading may carry its release time; one without still reads.</summary>
        [Fact]
        public void AHeadingsReleaseTimeIsRead()
        {
            IReadOnlyList<ChangelogEntry> found = Changelog.All(
                "## 1.2.8 - 2026-10-01 19:30\n\n- One.\n\n## 1.2.7 - 2026-09-30\n\n- Two.\n\n## 1.2.6\n\n- Three.\n");

            Assert.Equal(["1.2.8", "1.2.7", "1.2.6"], found.Select(entry => entry.Version));
            Assert.Equal(new DateTime(2026, 10, 1, 19, 30, 0), found[0].Released);
            Assert.Equal(new DateTime(2026, 9, 30), found[1].Released);
            Assert.Null(found[2].Released);
        }

        /// <summary>Each bullet is a change, its wrapped lines joined, its bold lead-in its title.</summary>
        [Fact]
        public void EachBulletBecomesAChangeWithItsTitle()
        {
            IReadOnlyList<ChangelogItem> items = Changelog.Items(
                "\n- **Run chain in the Assistant.** A chain the Assistant writes\n  now has `Run chain`.\n- No more crash at shutdown.\n");

            Assert.Equal(2, items.Count);
            Assert.Equal("Run chain in the Assistant.", items[0].Title);
            Assert.Equal("A chain the Assistant writes now has `Run chain`.", items[0].Body);
            Assert.False(items[0].IsFix);

            Assert.Equal(string.Empty, items[1].Title);
            Assert.True(items[1].IsFix);
        }

        [Fact]
        public void ALineSplitsIntoPlainCodeAndBoldRuns()
        {
            IReadOnlyList<InlineRun> runs = Changelog.Runs("Press **Run** to start `build.ps1` now");

            Assert.Equal(
                [("Press ", false, false), ("Run", false, true), (" to start ", false, false), ("build.ps1", true, false), (" now", false, false)],
                runs.Select(run => (run.Text, run.IsCode, run.IsBold)));
        }

        /// <summary>Every version this file describes says when it came out, so the page can show it.</summary>
        [Fact]
        public void EveryBundledVersionIsDated()
        {
            Assert.All(Changelog.Bundled, entry => Assert.NotNull(entry.Released));
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
