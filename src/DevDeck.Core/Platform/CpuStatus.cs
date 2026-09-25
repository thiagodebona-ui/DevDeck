using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DevDeck.Core
{
    /// <summary>A reading of processor load, as a percentage of all cores.</summary>
    internal readonly record struct CpuStatus(double UsedPercent, int Cores, bool IsKnown)
    {
        public static readonly CpuStatus Unknown = new(0, Environment.ProcessorCount, false);
    }

    /// <summary>
    ///  Reads processor load on Windows, Linux and macOS.
    /// </summary>
    /// <remarks>
    ///  Unlike memory, processor load is not a number any kernel will simply hand you: it only
    ///  exists as a difference between two samples. So this is an object with a memory of the last
    ///  sample rather than a static Read, and the first call after construction has nothing to
    ///  compare against and reports unknown.
    ///
    ///  Three ways in, in order of preference:
    ///    Windows - GetSystemTimes, which gives idle, kernel and user tick counts machine-wide.
    ///    Linux   - the aggregate "cpu" line of /proc/stat.
    ///    macOS   - no cheap equivalent that is stable across releases, so it falls back to adding
    ///              up every process's processor time. That undercounts the kernel's own work, and
    ///              is documented here rather than presented as the same measurement.
    ///
    ///  Every path returns <see cref="CpuStatus.Unknown"/> rather than throwing, for the reason
    ///  <see cref="MemoryProbe"/> gives: a widget on a two-second tick must not be able to take the
    ///  app down on an unexpected kernel.
    /// </remarks>
    internal sealed class CpuProbe
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        private long lastIdle;
        private long lastBusy;
        private bool primed;

        /// <summary>For the macOS fallback, which measures against wall-clock rather than ticks.</summary>
        private TimeSpan lastProcessTime;
        private DateTime lastSampledAt;

        public int Cores => Environment.ProcessorCount;

        /// <summary>
        ///  Load since the previous call. Unknown on the first one.
        /// </summary>
        public CpuStatus Read()
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return ReadWindows();
                }

                return OperatingSystem.IsMacOS() ? ReadMac() : ReadLinux();
            }
            catch (Exception)
            {
                return CpuStatus.Unknown;
            }
        }

        /// <summary>
        ///  Idle, kernel and user tick counts, machine-wide.
        /// </summary>
        /// <remarks>
        ///  The kernel figure already includes the idle one - that is the documented shape of
        ///  GetSystemTimes, and treating them as separate is the classic way to get a reading that
        ///  never rises above fifty percent. Busy is therefore kernel + user - idle.
        /// </remarks>
        private CpuStatus ReadWindows()
        {
            if (!GetSystemTimes(out long idle, out long kernel, out long user))
            {
                return CpuStatus.Unknown;
            }

            long busy = kernel + user - idle;

            return Compare(idle, busy);
        }

        /// <summary>The aggregate "cpu" line of /proc/stat.</summary>
        /// <remarks>
        ///  iowait counts as idle: the processor is not doing anything during it, and counting it
        ///  as load is why some monitors show a machine pinned at 100% while it copies a file.
        /// </remarks>
        private CpuStatus ReadLinux()
        {
            const string path = "/proc/stat";

            if (!File.Exists(path))
            {
                return CpuStatus.Unknown;
            }

            string? line = File.ReadLines(path).FirstOrDefault(
                l => l.StartsWith("cpu ", StringComparison.Ordinal));

            if (line is null)
            {
                return CpuStatus.Unknown;
            }

            long[] fields = line
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Skip(1)
                .Select(field => long.TryParse(field, out long value) ? value : 0)
                .ToArray();

            if (fields.Length < 5)
            {
                return CpuStatus.Unknown;
            }

            // user nice system idle iowait irq softirq steal
            long idle = fields[3] + fields[4];
            long busy = fields.Sum() - idle;

            return Compare(idle, busy);
        }

        /// <summary>
        ///  Every process's processor time against the wall clock.
        /// </summary>
        /// <remarks>
        ///  A real approximation, not the same measurement: work the kernel does on nobody's behalf
        ///  is invisible to it, so this reads a little low. It is here because the alternative on
        ///  macOS is host_statistics through P/Invoke, whose struct has changed shape across
        ///  releases - and a misread struct gives a confidently wrong number rather than no number.
        /// </remarks>
        private CpuStatus ReadMac()
        {
            TimeSpan total = TimeSpan.Zero;

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    total += process.TotalProcessorTime;
                }
                catch (Exception)
                {
                    // Exited, or not ours to look at.
                }
                finally
                {
                    process.Dispose();
                }
            }

            DateTime now = DateTime.UtcNow;

            if (!primed)
            {
                lastProcessTime = total;
                lastSampledAt = now;
                primed = true;

                return CpuStatus.Unknown;
            }

            double elapsed = (now - lastSampledAt).TotalMilliseconds;
            double used = (total - lastProcessTime).TotalMilliseconds;

            lastProcessTime = total;
            lastSampledAt = now;

            if (elapsed <= 0)
            {
                return CpuStatus.Unknown;
            }

            return new CpuStatus(Clamp(used * 100.0 / (elapsed * Cores)), Cores, true);
        }

        /// <summary>Turns two tick counters into a percentage, holding them for next time.</summary>
        private CpuStatus Compare(long idle, long busy)
        {
            if (!primed)
            {
                lastIdle = idle;
                lastBusy = busy;
                primed = true;

                return CpuStatus.Unknown;
            }

            long idleDelta = idle - lastIdle;
            long busyDelta = busy - lastBusy;

            lastIdle = idle;
            lastBusy = busy;

            long total = idleDelta + busyDelta;

            // Two reads inside the same tick, or a counter that wrapped. Neither is worth a number.
            return total <= 0
                ? CpuStatus.Unknown
                : new CpuStatus(Clamp(busyDelta * 100.0 / total), Cores, true);
        }

        private static double Clamp(double percent) => Math.Clamp(percent, 0, 100);

        /// <summary>"7%", "64%" - sized for a readout that updates every couple of seconds.</summary>
        public static string Describe(double percent) =>
            percent.ToString("0", CultureInfo.InvariantCulture) + "%";
    }
}
