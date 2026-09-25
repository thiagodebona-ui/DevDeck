using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The two live readouts. These run against the real machine, so they assert the shape of the
    ///  answer rather than a particular number - "between 0 and 100" is the useful invariant, and
    ///  it is exactly the one that a units or sign mistake breaks.
    /// </summary>
    public class ProbeTests
    {
        [Fact]
        public void MemoryReadsBackSomethingCoherent()
        {
            MemoryStatus status = MemoryProbe.Read();

            Assert.True(status.IsKnown, "every supported platform can read physical memory");
            Assert.True(status.Total > 0);
            Assert.InRange(status.Available, 0, status.Total);
            Assert.Equal(status.Total - status.Available, status.Used);
            Assert.InRange(status.UsedPercent, 0, 100);
        }

        [Fact]
        public void UnknownMemoryDoesNotDivideByZero()
        {
            Assert.Equal(0, MemoryStatus.Unknown.UsedPercent);
            Assert.False(MemoryStatus.Unknown.IsKnown);
        }

        [Theory]
        [InlineData(0, "0 MB")]
        [InlineData(512L * 1024 * 1024, "512 MB")]
        public void SmallSizesReadInMegabytes(long bytes, string expected)
        {
            Assert.Equal(expected, MemoryProbe.Describe(bytes));
        }

        [Fact]
        public void LargeSizesReadInGigabytes()
        {
            string described = MemoryProbe.Describe(8L * 1024 * 1024 * 1024);

            Assert.EndsWith("GB", described);
            Assert.StartsWith("8", described);
        }

        /// <summary>
        ///  Processor load only exists as a difference between two samples, so the first call has
        ///  nothing to compare against and must say so rather than inventing a number.
        /// </summary>
        [Fact]
        public void FirstCpuReadingIsUnknown()
        {
            Assert.False(new CpuProbe().Read().IsKnown);
        }

        [Fact]
        public void SecondCpuReadingIsAPercentage()
        {
            CpuProbe probe = new();

            probe.Read();

            // Long enough for the tick counters to move; short enough not to slow the suite down.
            Thread.Sleep(250);

            CpuStatus status = probe.Read();

            Assert.True(status.IsKnown);
            Assert.InRange(status.UsedPercent, 0, 100);
            Assert.True(status.Cores > 0);
        }

        [Fact]
        public void CpuIsDescribedAsAWholePercentage()
        {
            Assert.Equal("7%", CpuProbe.Describe(6.6));
            Assert.Equal("100%", CpuProbe.Describe(100));
            Assert.Equal("0%", CpuProbe.Describe(0));
        }

        [Fact]
        public void ProcessSamplerReportsMemoryImmediatelyAndCpuOnTheSecondPass()
        {
            ProcessSampler sampler = new();

            IReadOnlyList<ProcessUsage> first = sampler.Top(10);

            Assert.NotEmpty(first);
            Assert.True(first.Count <= 10);

            // Nothing to compare against yet, so every row is honest about knowing no CPU.
            Assert.All(first, usage => Assert.Equal(0, usage.CpuPercent));

            Thread.Sleep(250);

            IReadOnlyList<ProcessUsage> second = sampler.Top(10);

            Assert.All(second, usage => Assert.InRange(usage.CpuPercent, 0, 100));
        }

        [Fact]
        public void ProcessesComeBackHeaviestFirst()
        {
            IReadOnlyList<ProcessUsage> rows = new ProcessSampler().Top(12);

            Assert.Equal(
                rows.OrderByDescending(row => row.WorkingSet).Select(row => row.Id),
                rows.Select(row => row.Id));
        }
    }
}
