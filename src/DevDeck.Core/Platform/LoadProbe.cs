using System.Runtime.InteropServices;

namespace DevDeck.Core
{
    /// <summary>
    ///  Disk activity and GPU load, read from the performance counters Task Manager shows.
    /// </summary>
    /// <remarks>
    ///  <para>
    ///   Disk is "% Idle Time" across every physical disk, turned round into busy time. That is
    ///   Task Manager's "Disk" figure - how much of the time the disks were doing something - and
    ///   it is the number that explains a slow machine, where free space would not.
    ///  </para>
    ///  <para>
    ///   GPU is the 3D engine's utilisation, summed over every process using it, which is also
    ///   what Task Manager's GPU column adds up. It is capped at 100: the per-process figures are
    ///   sampled separately and can overshoot a little when summed.
    ///  </para>
    ///  <para>
    ///   Through pdh.dll directly rather than the PerformanceCounter package, which would be a
    ///   dependency for two numbers. Windows only; everywhere else both readings are unknown and
    ///   the gauges say so rather than showing a confident zero. Like <see cref="CpuProbe"/>, each
    ///   figure is a rate between two calls, so the first read after construction is unknown.
    ///  </para>
    /// </remarks>
    internal sealed class LoadProbe : IDisposable
    {
        private const uint FormatDouble = 0x00000200;
        private const uint FormatNoCap = 0x00008000;
        private const uint MoreData = 0x800007D2;

        private readonly IntPtr query;
        private readonly IntPtr disk;
        private readonly IntPtr gpu;
        private readonly bool open;
        private bool primed;

        public LoadProbe()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                if (PdhOpenQuery(null, IntPtr.Zero, out query) != 0)
                {
                    return;
                }

                open = true;

                if (PdhAddEnglishCounter(query, @"\PhysicalDisk(_Total)\% Idle Time", IntPtr.Zero, out disk) != 0)
                {
                    disk = IntPtr.Zero;
                }

                // Missing on machines without a WDDM 2 driver, and on some VMs. The disk reading
                // still works without it.
                if (PdhAddEnglishCounter(query, @"\GPU Engine(*engtype_3D)\Utilization Percentage", IntPtr.Zero, out gpu) != 0)
                {
                    gpu = IntPtr.Zero;
                }

                PdhCollectQueryData(query);
            }
            catch (Exception)
            {
                // A missing or broken pdh.dll is "unknown", not a crash on a two-second tick.
                open = false;
            }
        }

        /// <summary>
        ///  Busy percentages since the last call, or null for a reading that is not available.
        /// </summary>
        public (double? Disk, double? Gpu) Read()
        {
            if (!open)
            {
                return (null, null);
            }

            try
            {
                if (PdhCollectQueryData(query) != 0)
                {
                    return (null, null);
                }

                // The first collect only sets the baseline a rate counter needs.
                if (!primed)
                {
                    primed = true;

                    return (null, null);
                }

                double? busy = disk != IntPtr.Zero && Single(disk) is { } idle
                    ? Math.Clamp(100 - idle, 0, 100)
                    : null;

                double? graphics = gpu != IntPtr.Zero && Sum(gpu) is { } sum
                    ? Math.Clamp(sum, 0, 100)
                    : null;

                return (busy, graphics);
            }
            catch (Exception)
            {
                return (null, null);
            }
        }

        private static double? Single(IntPtr counter) =>
            PdhGetFormattedCounterValue(counter, FormatDouble | FormatNoCap, out _, out CounterValue value) == 0
            && value.Status is 0 or 1
                ? value.Double
                : null;

        /// <summary>Adds up every instance a wildcard counter expanded to.</summary>
        private static double? Sum(IntPtr counter)
        {
            uint size = 0;

            uint result = PdhGetFormattedCounterArray(counter, FormatDouble | FormatNoCap, ref size, out uint count, IntPtr.Zero);

            if (result == 0 && count == 0)
            {
                return 0;
            }

            if (result != MoreData || size == 0)
            {
                return null;
            }

            IntPtr buffer = Marshal.AllocHGlobal((int)size);

            try
            {
                if (PdhGetFormattedCounterArray(counter, FormatDouble | FormatNoCap, ref size, out count, buffer) != 0)
                {
                    return null;
                }

                int stride = Marshal.SizeOf<CounterItem>();
                double total = 0;

                for (int at = 0; at < count; at++)
                {
                    CounterItem item = Marshal.PtrToStructure<CounterItem>(buffer + (at * stride));

                    if (item.Value.Status is 0 or 1)
                    {
                        total += item.Value.Double;
                    }
                }

                return total;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public void Dispose()
        {
            if (open)
            {
                PdhCloseQuery(query);
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct CounterValue
        {
            [FieldOffset(0)]
            public uint Status;

            [FieldOffset(8)]
            public double Double;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CounterItem
        {
            public IntPtr Name;

            public CounterValue Value;
        }

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounter(IntPtr query, string path, IntPtr userData, out IntPtr counter);

        [DllImport("pdh.dll")]
        private static extern uint PdhCollectQueryData(IntPtr query);

        [DllImport("pdh.dll")]
        private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out CounterValue value);

        [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
        private static extern uint PdhGetFormattedCounterArray(IntPtr counter, uint format, ref uint size, out uint count, IntPtr buffer);

        [DllImport("pdh.dll")]
        private static extern uint PdhCloseQuery(IntPtr query);
    }
}
