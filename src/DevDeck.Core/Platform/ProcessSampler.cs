using System.Diagnostics;

namespace DevDeck.Core
{
    /// <summary>
    ///  The heaviest processes, with a processor share worked out across successive calls.
    /// </summary>
    /// <remarks>
    ///  Same shape and same reason as <see cref="CpuProbe"/>: per-process CPU is a difference
    ///  between two samples, so something has to remember the last one. V2 took its sample by
    ///  reading every process twice around a 700ms sleep, which is accurate but blocks; here the
    ///  previous tick is the earlier sample, so a readout already refreshing every two seconds
    ///  gets the figure for free.
    ///
    ///  The first call reports zero CPU for everything, which is honest - there is nothing to
    ///  compare against yet.
    /// </remarks>
    internal sealed class ProcessSampler
    {
        private readonly Dictionary<int, TimeSpan> previous = [];

        private DateTime sampledAt;

        private readonly int cores = Environment.ProcessorCount;

        /// <summary>
        ///  The processes holding the most memory, ordered by working set.
        /// </summary>
        /// <remarks>
        ///  Process.WorkingSet64 and TotalProcessorTime both mean roughly the same thing on all
        ///  three platforms, so this needs no per-OS branch. Processes that exit mid-enumeration
        ///  throw on access and are skipped.
        /// </remarks>
        public IReadOnlyList<ProcessUsage> Top(int count, bool group = false)
        {
            DateTime now = DateTime.UtcNow;
            double elapsed = (now - sampledAt).TotalMilliseconds;

            // A first call, or two calls in the same instant: report memory and leave CPU at zero
            // rather than dividing by something near enough to nothing to produce nonsense.
            bool canCompare = sampledAt != default && elapsed > 1;

            List<ProcessUsage> found = [];
            Dictionary<int, TimeSpan> current = [];

            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    TimeSpan used = process.TotalProcessorTime;
                    current[process.Id] = used;

                    double percent = 0;

                    if (canCompare && previous.TryGetValue(process.Id, out TimeSpan before))
                    {
                        // Against every core, so a process pinning one core of eight reads as 12%
                        // rather than 100% - the same basis the memory figures use.
                        percent = Math.Clamp(
                            (used - before).TotalMilliseconds * 100.0 / (elapsed * cores), 0, 100);
                    }

                    found.Add(new ProcessUsage(
                        process.Id, process.ProcessName, process.WorkingSet64, percent));
                }
                catch (Exception)
                {
                    // Exited, or not ours to look at. On Linux and macOS this is also every process
                    // belonging to another user, which is the normal case rather than a fault.
                }
                finally
                {
                    process.Dispose();
                }
            }

            previous.Clear();

            foreach ((int id, TimeSpan used) in current)
            {
                previous[id] = used;
            }

            sampledAt = now;

            return (group ? Group(found) : found)
                .OrderByDescending(usage => usage.WorkingSet)
                .Take(count)
                .ToList();
        }

        /// <summary>
        ///  Folds every copy of one executable into a single row: memory and processor summed.
        /// </summary>
        /// <remarks>
        ///  A browser, an editor or a Node toolchain runs as dozens of processes with one name, and
        ///  listed one by one they push everything else off the list while each looks harmless. As
        ///  one row the answer to "what is using my memory" is the program, which is the question.
        ///
        ///  By name rather than by full path: reading each process's image path costs a handle per
        ///  process every two seconds, and two different executables with the same name are rare
        ///  enough that a list for the eye can live with it. The row keeps the id of its heaviest
        ///  member, so a group of one is exactly the row it used to be.
        /// </remarks>
        public static IEnumerable<ProcessUsage> Group(IEnumerable<ProcessUsage> found) =>
            found
                .GroupBy(usage => usage.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    List<ProcessUsage> members = [.. group.OrderByDescending(usage => usage.WorkingSet)];

                    return new ProcessUsage(
                        members[0].Id,
                        members[0].Name,
                        members.Sum(usage => usage.WorkingSet),
                        Math.Min(100, members.Sum(usage => usage.CpuPercent)))
                    {
                        Ids = [.. members.Select(usage => usage.Id)],
                    };
                });
    }
}
