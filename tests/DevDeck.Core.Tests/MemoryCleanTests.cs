using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The cleaner's bookkeeping: which steps exist, which this platform can do, and how a stored
    ///  list of them is read back. Nothing here runs a step - that would trim the test runner's own
    ///  working set and, on Windows, ask for an administrator.
    /// </summary>
    public class MemoryCleanTests
    {
        [Fact]
        public void EveryStepAppearsInTheRunOrderExactlyOnce()
        {
            MemoryStep[] all = Enum.GetValues<MemoryStep>();

            Assert.Equal(all.Length, MemoryClean.Order.Count);
            Assert.Equal(all.Length, MemoryClean.Order.Distinct().Count());
        }

        /// <summary>
        ///  Trimming moves pages to the standby cache, so every purge has to come after every trim
        ///  or it purges a cache that is about to be refilled.
        /// </summary>
        [Fact]
        public void PurgesComeAfterTrims()
        {
            int trim = MemoryClean.Order.ToList().IndexOf(MemoryStep.TrimProcessWorkingSets);
            int emptyAll = MemoryClean.Order.ToList().IndexOf(MemoryStep.EmptySystemWorkingSets);
            int purgeLow = MemoryClean.Order.ToList().IndexOf(MemoryStep.PurgeLowPriorityStandby);
            int purge = MemoryClean.Order.ToList().IndexOf(MemoryStep.PurgeStandbyList);

            Assert.True(trim < purge);
            Assert.True(emptyAll < purge);
            Assert.True(purgeLow < purge);
        }

        /// <summary>Flushing dirty pages has to precede freeing them.</summary>
        [Fact]
        public void FlushComesBeforeThePurges()
        {
            List<MemoryStep> order = MemoryClean.Order.ToList();

            Assert.True(order.IndexOf(MemoryStep.FlushModifiedPages) < order.IndexOf(MemoryStep.PurgeStandbyList));
        }

        [Fact]
        public void DefaultsAreAllRealStepsAndLeaveOutTheExpensiveOnes()
        {
            Assert.All(MemoryClean.Defaults, step => Assert.Contains(step, MemoryClean.Order));

            // Costs disk writes and can stall for seconds; a poor trade for a button people press
            // casually. Left off by default in V2 for the same reason.
            Assert.DoesNotContain(MemoryStep.FlushModifiedPages, MemoryClean.Defaults);

            // Needs root and drops a cache that is not waste.
            Assert.DoesNotContain(MemoryStep.DropPageCache, MemoryClean.Defaults);
        }

        [Fact]
        public void EveryStepSaysWhatItIsAndWhatItDoes()
        {
            foreach (MemoryStep step in MemoryClean.Order)
            {
                Assert.False(string.IsNullOrWhiteSpace(MemoryClean.Title(step)));
                Assert.False(string.IsNullOrWhiteSpace(MemoryClean.Explain(step)));
            }
        }

        /// <summary>
        ///  A step the platform cannot do has to say why, because the UI shows it disabled with
        ///  that sentence beside it rather than hiding it.
        /// </summary>
        [Fact]
        public void UnsupportedStepsCarryAReason()
        {
            foreach (MemoryStep step in MemoryClean.Order)
            {
                if (MemoryClean.Supported(step))
                {
                    Assert.Equal(string.Empty, MemoryClean.WhyUnsupported(step));
                }
                else
                {
                    Assert.False(string.IsNullOrWhiteSpace(MemoryClean.WhyUnsupported(step)));
                }
            }
        }

        [Fact]
        public void CollectingOurOwnGarbageWorksEverywhere()
        {
            Assert.True(MemoryClean.Supported(MemoryStep.CollectOwnGarbage));
        }

        [Fact]
        public void PlatformDecidesWhichStepsExist()
        {
            if (OperatingSystem.IsWindows())
            {
                Assert.True(MemoryClean.Supported(MemoryStep.PurgeStandbyList));
                Assert.False(MemoryClean.Supported(MemoryStep.DropPageCache));
            }
            else
            {
                Assert.False(MemoryClean.Supported(MemoryStep.PurgeStandbyList));
                Assert.True(MemoryClean.Supported(MemoryStep.DropPageCache));
            }
        }

        [Fact]
        public void TheTwoUnprivilegedStepsAreTheOnlyOnesThatDoNotNeedAdmin()
        {
            Assert.False(MemoryClean.NeedsAdmin(MemoryStep.CollectOwnGarbage));
            Assert.False(MemoryClean.NeedsAdmin(MemoryStep.TrimProcessWorkingSets));

            Assert.True(MemoryClean.NeedsAdmin(MemoryStep.PurgeStandbyList));
            Assert.True(MemoryClean.NeedsAdmin(MemoryStep.TrimFileCache));
        }

        [Fact]
        public void ParsingStoredStepsKeepsRunOrderRatherThanStoredOrder()
        {
            List<string> stored =
            [
                nameof(MemoryStep.PurgeStandbyList),
                nameof(MemoryStep.CollectOwnGarbage),
            ];

            List<MemoryStep> parsed = MemoryClean.ParseSteps(stored);

            Assert.Equal([MemoryStep.CollectOwnGarbage, MemoryStep.PurgeStandbyList], parsed);
        }

        [Fact]
        public void ParsingIgnoresNamesThisBuildNoLongerHas()
        {
            List<MemoryStep> parsed = MemoryClean.ParseSteps(
                [nameof(MemoryStep.CollectOwnGarbage), "SomeStepFromAFutureBuild"]);

            Assert.Equal([MemoryStep.CollectOwnGarbage], parsed);
        }

        [Fact]
        public void ParsingDropsDuplicates()
        {
            List<MemoryStep> parsed = MemoryClean.ParseSteps(
                [nameof(MemoryStep.CollectOwnGarbage), nameof(MemoryStep.CollectOwnGarbage)]);

            Assert.Single(parsed);
        }

        /// <summary>No stored list at all is a first run, not an empty selection.</summary>
        [Fact]
        public void NoStoredListMeansTheDefaults()
        {
            List<MemoryStep> parsed = MemoryClean.ParseSteps(null);

            Assert.NotEmpty(parsed);
            Assert.All(parsed, step => Assert.True(MemoryClean.Supported(step)));
        }

        /// <summary>
        ///  The numbers are what a V2 settings file stores, so they are part of the file format.
        /// </summary>
        [Fact]
        public void StepNumbersMatchV2()
        {
            Assert.Equal(0, (int)MemoryStep.CollectOwnGarbage);
            Assert.Equal(1, (int)MemoryStep.TrimProcessWorkingSets);
            Assert.Equal(2, (int)MemoryStep.EmptySystemWorkingSets);
            Assert.Equal(3, (int)MemoryStep.FlushModifiedPages);
            Assert.Equal(4, (int)MemoryStep.PurgeStandbyList);
            Assert.Equal(5, (int)MemoryStep.PurgeLowPriorityStandby);
            Assert.Equal(6, (int)MemoryStep.TrimFileCache);
        }

        [Fact]
        public void FreedIsMeasuredFromAvailableRatherThanSummedPerProcess()
        {
            CleanReport report = new(
                new MemoryStatus(Total: 16_000, Available: 4_000),
                new MemoryStatus(Total: 16_000, Available: 6_000),
                []);

            Assert.Equal(2_000, report.Freed);
        }

        /// <summary>A clean can leave less available than before, and saying so beats hiding it.</summary>
        [Fact]
        public void FreedCanBeNegative()
        {
            CleanReport report = new(
                new MemoryStatus(Total: 16_000, Available: 6_000),
                new MemoryStatus(Total: 16_000, Available: 5_000),
                []);

            Assert.Equal(-1_000, report.Freed);
            Assert.False(report.Worthwhile);
        }
    }
}
