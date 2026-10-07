using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The new halves of the cleaner: presets, the clean history, and the disk sweep's rules.
    ///  Nothing here deletes a file or sends a memory command.
    /// </summary>
    [Collection("Strings")]
    public class SpaceCleanTests
    {
        /// <summary>The number is the step's bit in the elevated helper's exit code.</summary>
        [Fact]
        public void NewStepsAreNumberedOnFromTheEnd()
        {
            Assert.Equal(8, (int)MemoryStep.CombineMemoryPages);
            Assert.Equal(9, (int)MemoryStep.FlushRegistryCache);
        }

        /// <summary>Combining has to see pages while they are still resident, before any trim.</summary>
        [Fact]
        public void CombiningComesBeforeTheTrims()
        {
            List<MemoryStep> order = MemoryClean.Order.ToList();

            Assert.True(order.IndexOf(MemoryStep.CombineMemoryPages) < order.IndexOf(MemoryStep.TrimProcessWorkingSets));
            Assert.True(order.IndexOf(MemoryStep.FlushRegistryCache) < order.IndexOf(MemoryStep.PurgeStandbyList));
        }

        [Fact]
        public void QuickNeverNeedsAnAdministrator()
        {
            Assert.NotEmpty(MemoryClean.StepsFor(CleanPreset.Quick));
            Assert.All(MemoryClean.StepsFor(CleanPreset.Quick), step => Assert.False(MemoryClean.NeedsAdmin(step)));
        }

        [Fact]
        public void DeepIsEverythingThisPlatformHas()
        {
            Assert.Equal(MemoryClean.Available, MemoryClean.StepsFor(CleanPreset.Deep));
        }

        [Fact]
        public void EachPresetIsRecognisedFromItsOwnTicks()
        {
            Assert.Equal(CleanPreset.Quick, MemoryClean.PresetOf(MemoryClean.StepsFor(CleanPreset.Quick)));
            Assert.Equal(CleanPreset.Deep, MemoryClean.PresetOf(MemoryClean.StepsFor(CleanPreset.Deep)));

            if (OperatingSystem.IsWindows())
            {
                Assert.Equal(CleanPreset.Recommended, MemoryClean.PresetOf(MemoryClean.StepsFor(CleanPreset.Recommended)));
            }
        }

        [Fact]
        public void AnOddSelectionIsNoPreset()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.Null(MemoryClean.PresetOf([MemoryStep.FlushModifiedPages]));
            }
        }

        [Fact]
        public void HistoryRoundTripsAndDropsGarbage()
        {
            DateTime when = new(2026, 10, 7, 12, 30, 0, DateTimeKind.Utc);

            List<string> lines = CleanHistory.Append(["not a record", "x|y"], new CleanRecord(when, 412_000_000));
            List<CleanRecord> records = CleanHistory.Parse(lines);

            Assert.Equal([new CleanRecord(when, 412_000_000)], records);
        }

        [Fact]
        public void HistoryIsCapped()
        {
            List<string> lines = [];

            for (int i = 0; i < CleanHistory.Keep + 10; i++)
            {
                lines = CleanHistory.Append(lines, new CleanRecord(DateTime.UtcNow, i));
            }

            Assert.Equal(CleanHistory.Keep, lines.Count);

            // The newest survive, not the oldest.
            Assert.Equal(CleanHistory.Keep + 9, CleanHistory.Parse(lines)[^1].Freed);
        }

        [Fact]
        public void AgoReadsInWholeUnits()
        {
            Strings.Use(AppLanguage.English);

            DateTime now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

            Assert.Equal("just now", CleanHistory.Ago(now.AddSeconds(-20), now));
            Assert.Equal("5 min ago", CleanHistory.Ago(now.AddMinutes(-5), now));
            Assert.Equal("3 h ago", CleanHistory.Ago(now.AddHours(-3), now));
            Assert.Equal("2 d ago", CleanHistory.Ago(now.AddDays(-2), now));
        }

        /// <summary>The recycle bin holds things the user put there on purpose.</summary>
        [Fact]
        public void TheRecycleBinIsNeverTickedByDefault()
        {
            Assert.DoesNotContain(SpaceTarget.RecycleBin, SpaceClean.Defaults);
            Assert.DoesNotContain(SpaceTarget.RecycleBin, SpaceClean.ParseTargets(null));
        }

        [Fact]
        public void StoredTargetsReadBackInOrderAndIgnoreUnknownNames()
        {
            List<SpaceTarget> parsed = SpaceClean.ParseTargets(
                [nameof(SpaceTarget.PipCache), "SomethingFromTheFuture", nameof(SpaceTarget.NpmCache), nameof(SpaceTarget.NpmCache)]);

            Assert.Equal([SpaceTarget.NpmCache, SpaceTarget.PipCache], parsed);
        }

        [Fact]
        public void EveryTargetSaysWhatItIs()
        {
            foreach (SpaceTarget target in SpaceClean.Order)
            {
                Assert.NotEqual("Space" + target, SpaceClean.Title(target));
                Assert.NotEqual("Space" + target + "Why", SpaceClean.Explain(target));
            }
        }

        /// <summary>A TEMP pointed at the top of a drive must never become a sweep of the drive.</summary>
        [Fact]
        public void TopLevelFoldersAreNeverSwept()
        {
            string root = Path.GetPathRoot(Path.GetTempPath())!;

            Assert.False(SpaceClean.IsSafeRoot(root));
            Assert.False(SpaceClean.IsSafeRoot(Path.Combine(root, "Users")));
            Assert.False(SpaceClean.IsSafeRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
            Assert.False(SpaceClean.IsSafeRoot("relative\\path"));
            Assert.False(SpaceClean.IsSafeRoot(string.Empty));

            Assert.True(SpaceClean.IsSafeRoot(Path.Combine(root, "Users", "someone", "cache")));
        }

        [Fact]
        public void EveryFolderATargetOffersIsSafe()
        {
            foreach (SpaceTarget target in SpaceClean.Order)
            {
                Assert.All(SpaceClean.Folders(target), folder => Assert.True(SpaceClean.IsSafeRoot(folder)));
            }
        }
    }
}
