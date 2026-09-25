using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The handoff to the elevated helper. Nothing here starts a process - that would raise a UAC
    ///  prompt in the middle of a test run - so what is covered is the wire format: the bit mask
    ///  the exit code carries, and the step list it decodes back to.
    /// </summary>
    public class ElevationTests
    {
        [Fact]
        public void EachStepGetsItsOwnBit()
        {
            int[] bits = MemoryClean.Order.Select(Elevation.Bit).ToArray();

            Assert.Equal(bits.Length, bits.Distinct().Count());
            Assert.All(bits, bit => Assert.True(bit > 0));
        }

        /// <summary>
        ///  The mask travels as a process exit code, so it has to stay inside a positive int - and
        ///  well clear of -1, which means "the prompt was declined".
        /// </summary>
        [Fact]
        public void TheWholeMaskFitsInAnExitCode()
        {
            int all = Elevation.MaskOf(MemoryClean.Order);

            Assert.True(all > 0);
            Assert.True(all < short.MaxValue);
            Assert.NotEqual(Elevation.Declined, all);
        }

        [Fact]
        public void MaskRoundTripsBackToTheSameSteps()
        {
            MemoryStep[] wanted =
            [
                MemoryStep.PurgeStandbyList,
                MemoryStep.TrimFileCache,
                MemoryStep.EmptySystemWorkingSets,
            ];

            IReadOnlyList<MemoryStep> decoded = Elevation.StepsIn(Elevation.MaskOf(wanted));

            Assert.Equal(wanted.OrderBy(s => s), decoded.OrderBy(s => s));
        }

        [Fact]
        public void DecodedStepsComeBackInRunOrder()
        {
            IReadOnlyList<MemoryStep> decoded = Elevation.StepsIn(Elevation.MaskOf(MemoryClean.Order));

            Assert.Equal(MemoryClean.Order, decoded);
        }

        [Fact]
        public void AnEmptyMaskIsNoSteps()
        {
            Assert.Empty(Elevation.StepsIn(0));
            Assert.Equal(0, Elevation.MaskOf([]));
        }

        [Fact]
        public async Task AskingForNothingStartsNothing()
        {
            Assert.Empty(await Elevation.RunAsync([], CancellationToken.None));
        }

        /// <summary>
        ///  Away from Windows there is no prompt to raise, and the steps have to come back reported
        ///  rather than silently missing from the run.
        /// </summary>
        [Fact]
        public async Task WithoutWindowsTheStepsAreReportedNotDropped()
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }

            IReadOnlyList<StepOutcome> outcomes =
                await Elevation.RunAsync([MemoryStep.PurgeStandbyList], CancellationToken.None);

            StepOutcome only = Assert.Single(outcomes);

            Assert.Equal(StepStatus.NeedsAdmin, only.Status);
            Assert.False(string.IsNullOrWhiteSpace(only.Detail));
        }

        /// <summary>The helper refuses a step it cannot do rather than reporting a false success.</summary>
        [Fact]
        public void TheHelperWillNotClaimAnUnsupportedStep()
        {
            MemoryStep step = OperatingSystem.IsWindows()
                ? MemoryStep.DropPageCache
                : MemoryStep.PurgeStandbyList;

            StepOutcome outcome = MemoryClean.RunOneElevated(step);

            Assert.Equal(StepStatus.Unsupported, outcome.Status);
        }

        [Fact]
        public void ElevationIsOnByDefaultButCanBeRefused()
        {
            Assert.True(new CleanOptions().AllowElevation);
            Assert.False(new CleanOptions { AllowElevation = false }.AllowElevation);
        }
    }
}
