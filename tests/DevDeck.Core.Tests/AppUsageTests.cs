using System.Diagnostics;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>The readout over the version: the app and everything it started.</summary>
    public class AppUsageTests
    {
        [Fact]
        public void TheFirstReadingHasMemoryAndNoRates()
        {
            AppUsageReading reading = new AppUsage().Read();

            Assert.True(reading.Processes >= 1);
            Assert.True(reading.Memory > 0);
            Assert.Equal(0, reading.CpuPercent);
            Assert.Equal(0, reading.DiskPerSecond);
        }

        [Fact]
        public void AChildProcessIsCounted()
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
            {
                return;
            }

            AppUsage usage = new();
            int before = usage.Read().Processes;

            using Process child = Process.Start(new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "sleep",
                Arguments = OperatingSystem.IsWindows() ? "/c ping -n 5 127.0.0.1 >nul" : "5",
                CreateNoWindow = true,
                UseShellExecute = false,
            })!;

            try
            {
                Thread.Sleep(300);

                Assert.True(usage.Read().Processes > before);
            }
            finally
            {
                child.Kill(entireProcessTree: true);
            }
        }

        [Theory]
        [InlineData(512L, "1 KB")]
        [InlineData(3L * 1024 * 1024 / 2, "1.5 MB")]
        [InlineData(312L * 1024 * 1024, "312 MB")]
        public void SizesReadAsAPersonWouldSayThem(long bytes, string expected)
        {
            Assert.Equal(expected, AppUsageReading.Size(bytes).Replace(',', '.'));
        }
    }
}
