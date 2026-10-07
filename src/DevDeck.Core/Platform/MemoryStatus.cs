using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DevDeck.Core
{
    /// <summary>A reading of physical memory, in bytes.</summary>
    internal sealed record MemoryStatus(long Total, long Available)
    {
        public long Used => Total - Available;

        /// <summary>0 to 100. Zero when the reading failed, rather than a divide by zero.</summary>
        public double UsedPercent => Total > 0 ? Used * 100.0 / Total : 0;

        public static readonly MemoryStatus Unknown = new(0, 0);

        public bool IsKnown => Total > 0;
    }

    /// <summary>One process worth reporting, with the memory it is holding and how hard it is working.</summary>
    /// <remarks>
    ///  CPU is zero unless the row came from a <see cref="ProcessSampler"/>, because a percentage
    ///  needs two readings and a static call only ever has one.
    /// </remarks>
    internal sealed record ProcessUsage(int Id, string Name, long WorkingSet, double CpuPercent = 0)
    {
        /// <summary>
        ///  Every process this row stands for: just <see cref="Id"/>, or each copy of one
        ///  executable when the list is grouped.
        /// </summary>
        public IReadOnlyList<int> Ids { get; init; } = [Id];

        public int Count => Ids.Count;

        public bool IsGroup => Ids.Count > 1;

        /// <summary>"836 MB", for a list that shows the size beside the name.</summary>
        public string Size => MemoryProbe.Describe(WorkingSet);
    }

    /// <summary>
    ///  Reads physical memory on Windows, Linux and macOS.
    /// </summary>
    /// <remarks>
    ///  Monitoring is the part of V2's memory tooling that ports cleanly: every one of the three
    ///  kernels will tell you how much memory exists and how much is available, they just do it
    ///  through three unrelated interfaces. Reclaiming is the part that does not port - see
    ///  <see cref="MemoryReclaim"/>.
    ///
    ///  Every path returns <see cref="MemoryStatus.Unknown"/> rather than throwing. A widget that
    ///  reads memory once a second must not be able to take the app down on an unexpected kernel.
    /// </remarks>
    internal static class MemoryProbe
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatusEx
        {
            public uint Length;
            public uint MemoryLoad;
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong TotalPageFile;
            public ulong AvailPageFile;
            public ulong TotalVirtual;
            public ulong AvailVirtual;
            public ulong AvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

        [DllImport("libc", SetLastError = true)]
        private static extern int sysctlbyname(
            string name, out long value, ref nint size, IntPtr newValue, nint newLength);

        public static MemoryStatus Read()
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
                // An unreadable /proc, a sandboxed sysctl, a kernel that moved something. The
                // widget shows "unknown" and the app carries on.
                return MemoryStatus.Unknown;
            }
        }

        private static MemoryStatus ReadWindows()
        {
            MemoryStatusEx buffer = new();
            buffer.Length = (uint)Marshal.SizeOf<MemoryStatusEx>();

            return GlobalMemoryStatusEx(ref buffer)
                ? new MemoryStatus((long)buffer.TotalPhys, (long)buffer.AvailPhys)
                : MemoryStatus.Unknown;
        }

        /// <summary>
        ///  Reads /proc/meminfo.
        /// </summary>
        /// <remarks>
        ///  MemAvailable, not MemFree: free memory on Linux is nearly always small because the
        ///  kernel spends the rest on cache it will hand back on demand. Reporting MemFree is how
        ///  a monitor ends up claiming a healthy machine is out of memory.
        /// </remarks>
        private static MemoryStatus ReadLinux()
        {
            const string path = "/proc/meminfo";

            if (!File.Exists(path))
            {
                return MemoryStatus.Unknown;
            }

            long total = 0;
            long available = 0;

            foreach (string line in File.ReadLines(path))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    total = Kilobytes(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    available = Kilobytes(line);
                }

                if (total > 0 && available > 0)
                {
                    break;
                }
            }

            return total > 0 ? new MemoryStatus(total, available) : MemoryStatus.Unknown;
        }

        /// <summary>The value out of a "MemTotal:  16316456 kB" line, in bytes.</summary>
        private static long Kilobytes(string line)
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return parts.Length >= 2 && long.TryParse(parts[1], out long value) ? value * 1024 : 0;
        }

        /// <summary>
        ///  Total from sysctl, available from vm_stat.
        /// </summary>
        /// <remarks>
        ///  hw.memsize is a plain sysctl. Free memory is not: it lives in the mach VM statistics,
        ///  and "available" on macOS is a judgement rather than a number - pages that are free,
        ///  speculative or purgeable can all be handed back. vm_stat is parsed rather than calling
        ///  host_statistics64 through P/Invoke because the struct it returns has changed shape
        ///  across releases, and a misread struct is worse than a spawned process once a second.
        /// </remarks>
        private static MemoryStatus ReadMac()
        {
            nint size = sizeof(long);

            if (sysctlbyname("hw.memsize", out long total, ref size, IntPtr.Zero, 0) != 0 || total <= 0)
            {
                return MemoryStatus.Unknown;
            }

            long pageSize = 4096;
            long free = 0;
            long speculative = 0;
            long purgeable = 0;

            using Process probe = new();
            probe.StartInfo.FileName = "/usr/bin/vm_stat";
            probe.StartInfo.RedirectStandardOutput = true;
            probe.StartInfo.UseShellExecute = false;
            probe.StartInfo.CreateNoWindow = true;
            probe.Start();

            string output = probe.StandardOutput.ReadToEnd();
            probe.WaitForExit(2000);

            foreach (string line in output.Split('\n'))
            {
                if (line.Contains("page size of", StringComparison.Ordinal))
                {
                    pageSize = FirstNumber(line) is > 0 and var read ? read : pageSize;
                }
                else if (line.StartsWith("Pages free:", StringComparison.Ordinal))
                {
                    free = FirstNumber(line);
                }
                else if (line.StartsWith("Pages speculative:", StringComparison.Ordinal))
                {
                    speculative = FirstNumber(line);
                }
                else if (line.StartsWith("Pages purgeable:", StringComparison.Ordinal))
                {
                    purgeable = FirstNumber(line);
                }
            }

            long available = (free + speculative + purgeable) * pageSize;

            return available > 0 ? new MemoryStatus(total, available) : MemoryStatus.Unknown;
        }

        private static long FirstNumber(string line)
        {
            string digits = new(line.Where(char.IsDigit).ToArray());

            return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)
                ? value
                : 0;
        }

        /// <summary>
        ///  The processes holding the most memory.
        /// </summary>
        /// <remarks>
        ///  Process.WorkingSet64 is one of the few things that means roughly the same on all three
        ///  platforms, so this needs no per-OS branch. Processes that exit mid-enumeration throw on
        ///  access and are skipped.
        /// </remarks>
        public static IReadOnlyList<ProcessUsage> TopProcesses(int count)
        {
            List<ProcessUsage> found = [];

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    found.Add(new ProcessUsage(process.Id, process.ProcessName, process.WorkingSet64));
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

            return found
                .OrderByDescending(usage => usage.WorkingSet)
                .Take(count)
                .ToList();
        }

        /// <summary>
        ///  Ends a process and the tree under it, and says what happened.
        /// </summary>
        /// <remarks>
        ///  The tree, because the process a developer wants gone is usually a launcher - npm, dotnet
        ///  watch, a shell - and ending only the parent leaves the server it started holding the
        ///  port. Never throws: a process that has already exited, or is not ours to end, is an
        ///  answer to show, not a crash.
        /// </remarks>
        public static string End(int id, string name)
        {
            if (id == Environment.ProcessId)
            {
                return Strings.Format("RunKillFailed", name, "that is DevDeck itself");
            }

            try
            {
                using Process process = Process.GetProcessById(id);

                process.Kill(entireProcessTree: true);

                return Strings.Format("RunKilled", name, id);
            }
            catch (Exception exception)
            {
                return Strings.Format("RunKillFailed", name, exception.Message);
            }
        }

        /// <summary>
        ///  Ends one process and its tree, quietly, for ending a whole group: whether it went is
        ///  all the caller counts. A member that already exited because its parent was ended a
        ///  moment ago counts as not ended, which is honest - it was not this call that did it.
        /// </summary>
        public static bool TryEnd(int id)
        {
            if (id == Environment.ProcessId)
            {
                return false;
            }

            try
            {
                using Process process = Process.GetProcessById(id);

                process.Kill(entireProcessTree: true);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>"12.4 GB", "836 MB" - sized for a readout that updates every second.</summary>
        public static string Describe(long bytes)
        {
            const double Gigabyte = 1024d * 1024 * 1024;
            const double Megabyte = 1024d * 1024;

            return bytes >= Gigabyte
                ? $"{bytes / Gigabyte:0.0} GB"
                : $"{bytes / Megabyte:0} MB";
        }
    }
}
