using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace DevDeck.Core
{
    /// <summary>What the app and everything it started are using, at one moment.</summary>
    /// <param name="Memory">Working set, in bytes, added up over every process.</param>
    /// <param name="CpuPercent">Share of all cores since the last reading.</param>
    /// <param name="DiskPerSecond">Bytes read plus written per second since the last reading.</param>
    /// <param name="Processes">How many processes that was: the app, and its children.</param>
    internal readonly record struct AppUsageReading(long Memory, double CpuPercent, double DiskPerSecond, int Processes)
    {
        /// <summary>"312 MB", "1.4 GB".</summary>
        public string MemoryText => Size(Memory);

        /// <summary>"4%", or "0.4%" below one, where a whole number would round to nothing.</summary>
        public string CpuText => CpuPercent < 1 && CpuPercent > 0
            ? CpuPercent.ToString("0.0", CultureInfo.CurrentCulture) + "%"
            : CpuPercent.ToString("0", CultureInfo.CurrentCulture) + "%";

        /// <summary>"0 KB", "640 KB", "12.3 MB" - per second, said by whoever shows it.</summary>
        public string DiskText => Size((long)DiskPerSecond);

        public static string Size(long bytes) => bytes switch
        {
            >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.0", CultureInfo.CurrentCulture) + " GB",
            >= 10L << 20 => (bytes / (double)(1L << 20)).ToString("0", CultureInfo.CurrentCulture) + " MB",
            >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0.0", CultureInfo.CurrentCulture) + " MB",
            _ => (bytes / 1024.0).ToString("0", CultureInfo.CurrentCulture) + " KB",
        };
    }

    /// <summary>
    ///  Measures the app as the user experiences it: this process and every process under it.
    /// </summary>
    /// <remarks>
    ///  The app on its own is rarely the heavy part. A build, a dev server or a model run from the
    ///  deck is a child process, and a readout that counted only DevDeck itself would say "80 MB"
    ///  while the machine fan said otherwise. So the tree is walked on every reading: commands
    ///  come and go between two ticks.
    ///
    ///  Processor time and bytes moved are both running totals, so - like <see cref="ProcessSampler"/>
    ///  - the figure is a difference against the previous reading, and the first one reads zero.
    ///  A process that appears between readings counts from its first sighting; one that exits
    ///  takes its share with it, which errs low for a tick rather than inventing a spike.
    ///
    ///  Disk is the I/O the kernel counts per process: on Windows that is every read and write,
    ///  pipes included, which is what a child writing its output to us looks like; on Linux it is
    ///  storage only. macOS gives no per-process figure without privileges and reads zero.
    ///
    ///  Called off the UI thread: walking every process on the machine is cheap but not free, and
    ///  it must never be what makes the window stutter.
    /// </remarks>
    internal sealed class AppUsage
    {
        private readonly int self = Environment.ProcessId;

        private readonly int cores = Environment.ProcessorCount;

        private Dictionary<int, (TimeSpan Cpu, long Io)> previous = [];

        private DateTime sampledAt;

        public AppUsageReading Read()
        {
            DateTime now = DateTime.UtcNow;
            double seconds = (now - sampledAt).TotalSeconds;
            bool canCompare = sampledAt != default && seconds > 0.05;

            Dictionary<int, (TimeSpan Cpu, long Io)> current = [];
            long memory = 0;
            double cpuMs = 0;
            long io = 0;

            foreach (int id in Tree())
            {
                try
                {
                    using Process process = Process.GetProcessById(id);

                    TimeSpan cpu = process.TotalProcessorTime;
                    long moved = IoOf(process);

                    memory += process.WorkingSet64;
                    current[id] = (cpu, moved);

                    if (canCompare && previous.TryGetValue(id, out (TimeSpan Cpu, long Io) before))
                    {
                        cpuMs += Math.Max(0, (cpu - before.Cpu).TotalMilliseconds);
                        io += Math.Max(0, moved - before.Io);
                    }
                }
                catch (Exception)
                {
                    // Exited between the walk and the read, or not ours to look at.
                }
            }

            previous = current;
            sampledAt = now;

            return canCompare
                ? new AppUsageReading(
                    memory,
                    Math.Clamp(cpuMs * 100.0 / (seconds * 1000 * cores), 0, 100),
                    io / seconds,
                    current.Count)
                : new AppUsageReading(memory, 0, 0, current.Count);
        }

        /// <summary>This process and all of its descendants.</summary>
        private List<int> Tree()
        {
            Dictionary<int, List<int>> children = [];

            try
            {
                foreach ((int id, int parent) in OperatingSystem.IsWindows() ? WindowsParents() : LinuxParents())
                {
                    // The idle process on Windows names itself as its parent, and following that
                    // would loop.
                    if (id == parent)
                    {
                        continue;
                    }

                    if (!children.TryGetValue(parent, out List<int>? list))
                    {
                        children[parent] = list = [];
                    }

                    list.Add(id);
                }
            }
            catch (Exception)
            {
                // No way to see the tree here, so the app alone is still worth showing.
            }

            List<int> found = [self];
            HashSet<int> seen = [self];

            for (int at = 0; at < found.Count; at++)
            {
                if (children.TryGetValue(found[at], out List<int>? under))
                {
                    foreach (int child in under)
                    {
                        if (seen.Add(child))
                        {
                            found.Add(child);
                        }
                    }
                }
            }

            return found;
        }

        private static long IoOf(Process process)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return GetProcessIoCounters(process.Handle, out IoCounters counters)
                        ? (long)(counters.ReadTransferCount + counters.WriteTransferCount)
                        : 0;
                }

                if (OperatingSystem.IsLinux())
                {
                    long total = 0;

                    foreach (string line in File.ReadLines($"/proc/{process.Id}/io"))
                    {
                        if (line.StartsWith("read_bytes:", StringComparison.Ordinal)
                            || line.StartsWith("write_bytes:", StringComparison.Ordinal))
                        {
                            total += long.Parse(line[(line.IndexOf(':') + 1)..].Trim(), CultureInfo.InvariantCulture);
                        }
                    }

                    return total;
                }
            }
            catch (Exception)
            {
                // Another user's process, or a kernel without the file.
            }

            return 0;
        }

        /// <summary>Every process on Linux with its parent, from the fourth field of /proc/n/stat.</summary>
        private static IEnumerable<(int Id, int Parent)> LinuxParents()
        {
            if (!OperatingSystem.IsLinux())
            {
                yield break;
            }

            foreach (string folder in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(folder), out int id))
                {
                    continue;
                }

                string stat;

                try
                {
                    stat = File.ReadAllText(Path.Combine(folder, "stat"));
                }
                catch (Exception)
                {
                    continue;
                }

                // The name is in parentheses and may itself contain spaces or parentheses, so the
                // fields are read from after the last closing one.
                string[] fields = stat[(stat.LastIndexOf(')') + 2)..].Split(' ');

                if (fields.Length > 1 && int.TryParse(fields[1], out int parent))
                {
                    yield return (id, parent);
                }
            }
        }

        /// <summary>Every process on Windows with its parent, from one Toolhelp snapshot.</summary>
        private static List<(int Id, int Parent)> WindowsParents()
        {
            List<(int, int)> found = [];

            IntPtr snapshot = CreateToolhelp32Snapshot(SnapProcess, 0);

            if (snapshot == InvalidHandle)
            {
                return found;
            }

            try
            {
                ProcessEntry entry = new() { Size = (uint)Marshal.SizeOf<ProcessEntry>() };

                for (bool more = Process32FirstW(snapshot, ref entry); more; more = Process32NextW(snapshot, ref entry))
                {
                    found.Add(((int)entry.ProcessId, (int)entry.ParentProcessId));
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            return found;
        }

        private const uint SnapProcess = 0x00000002;

        private static readonly IntPtr InvalidHandle = new(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            public uint Size;
            public uint Usage;
            public uint ProcessId;
            public IntPtr DefaultHeapId;
            public uint ModuleId;
            public uint Threads;
            public uint ParentProcessId;
            public int PriorityClassBase;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string ExeFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IoCounters
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32FirstW(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool Process32NextW(IntPtr snapshot, ref ProcessEntry entry);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessIoCounters(IntPtr process, out IoCounters counters);
    }
}
