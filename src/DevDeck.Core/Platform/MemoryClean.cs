using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace DevDeck.Core
{
    /// <summary>One thing a clean can do. Not all of them exist on all platforms.</summary>
    /// <remarks>
    ///  The first seven are V2's, with V2's numbering kept so a settings file listing steps by name
    ///  carries over unchanged. <see cref="DropPageCache"/> is new, and is the only step Linux and
    ///  macOS have at all.
    /// </remarks>
    internal enum MemoryStep
    {
        CollectOwnGarbage = 0,
        TrimProcessWorkingSets = 1,
        EmptySystemWorkingSets = 2,
        FlushModifiedPages = 3,
        PurgeStandbyList = 4,
        PurgeLowPriorityStandby = 5,
        TrimFileCache = 6,
        DropPageCache = 7,
    }

    internal enum StepStatus
    {
        Done,
        NeedsAdmin,
        Failed,
        Skipped,

        /// <summary>This platform has no equivalent, which is not a failure.</summary>
        Unsupported,
    }

    /// <summary>How one step went.</summary>
    internal readonly record struct StepOutcome(MemoryStep Step, StepStatus Status, string Detail)
    {
        public string Title => MemoryClean.Title(Step);

        public bool IsDone => Status == StepStatus.Done;

        public bool IsBad => Status is StepStatus.Failed or StepStatus.NeedsAdmin;
    }

    /// <summary>What to run, and what to leave alone while running it.</summary>
    internal sealed record CleanOptions
    {
        public IReadOnlyList<MemoryStep> Steps { get; init; } = MemoryClean.Defaults;

        /// <summary>Skip processes that used processor time during a short sample.</summary>
        public bool SkipBusy { get; init; } = true;

        /// <summary>Process names, without path or extension, never to trim.</summary>
        public IReadOnlyCollection<string> Excluded { get; init; } = [];

        /// <summary>
        ///  Whether steps this process is not allowed to run may be handed to an elevated copy.
        /// </summary>
        /// <remarks>
        ///  On by default, because the steps that need it are the ones worth having - but it costs
        ///  a UAC prompt, so it has to be refusable.
        /// </remarks>
        public bool AllowElevation { get; init; } = true;

        public bool Excludes(string name) =>
            Excluded.Any(entry => string.Equals(entry, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Everything one clean did, and what it actually bought.</summary>
    internal sealed record CleanReport(
        MemoryStatus Before, MemoryStatus After, IReadOnlyList<StepOutcome> Steps)
    {
        /// <summary>
        ///  Physical memory that became available across the whole run.
        /// </summary>
        /// <remarks>
        ///  Measured from the system's own "available" figure before and after, not by adding up
        ///  what each process's working set lost. Working sets share pages: totalling their
        ///  shrinkage counts the same physical page once per process holding it, which is where the
        ///  wildly inflated "freed 14 GB" numbers in the cheaper cleaners come from.
        /// </remarks>
        public long Freed => After.Available - Before.Available;

        public bool Worthwhile => Freed >= 1024 * 1024;
    }

    /// <summary>
    ///  V2's memory cleaner, as far as each platform allows.
    /// </summary>
    /// <remarks>
    ///  V2 ran seven steps, all of them Windows-only, and V3 shipped with one button that trimmed
    ///  working sets. This brings the rest across without giving up the cross-platform build.
    ///
    ///  How that works: the Windows implementations live behind
    ///  <see cref="OperatingSystem.IsWindows"/> and are marked <see cref="SupportedOSPlatformAttribute"/>,
    ///  so they compile everywhere and are only ever called where they exist. A P/Invoke
    ///  declaration costs nothing on a platform that never reaches it - it is resolved at the call,
    ///  not at load - so psapi and ntdll can be named here and the assembly still runs on Linux.
    ///
    ///  Steps a platform does not have report <see cref="StepStatus.Unsupported"/> with a reason,
    ///  rather than being hidden or quietly reporting success. <see cref="Supported"/> is what the
    ///  UI uses to avoid offering them in the first place.
    /// </remarks>
    internal static class MemoryClean
    {
        #region Windows imports
        [StructLayout(LayoutKind.Sequential)]
        private struct Luid
        {
            public uint Low;
            public int High;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LuidAndAttributes
        {
            public Luid Value;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TokenPrivileges
        {
            public int Count;
            public LuidAndAttributes Privilege;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumProcesses([Out] int[] ids, int size, out int needed);

        // The documented way to trim one process. It needs only PROCESS_SET_QUOTA, rather than the
        // all-access handle Process.Handle hands out - which is what lets this reach several times
        // as many processes, and reach them without being an administrator.
        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyWorkingSet(IntPtr process);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetSystemFileCacheSize(IntPtr minimum, IntPtr maximum, int flags);

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int infoClass, ref int information, int length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessTimes(
            IntPtr process, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(
            IntPtr process, int flags, System.Text.StringBuilder name, ref int size);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool LookupPrivilegeValue(string? system, string name, out Luid value);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AdjustTokenPrivileges(
            IntPtr token,
            [MarshalAs(UnmanagedType.Bool)] bool disableAll,
            ref TokenPrivileges state,
            int length,
            IntPtr previous,
            IntPtr returnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint ProcessSetQuota = 0x0100;

        private const uint TokenAdjustPrivileges = 0x0020;
        private const uint TokenQuery = 0x0008;
        private const uint PrivilegeEnabled = 0x0002;
        private const int ErrorNotAllAssigned = 1300;

        // Needed by the memory-list commands; without it NtSetSystemInformation answers
        // STATUS_PRIVILEGE_NOT_HELD and nothing is purged.
        private const string ProfileSingleProcessPrivilege = "SeProfileSingleProcessPrivilege";

        // Needed by SetSystemFileCacheSize.
        private const string IncreaseQuotaPrivilege = "SeIncreaseQuotaPrivilege";

        private const int SystemMemoryListInformation = 80;

        // SYSTEM_MEMORY_LIST_COMMAND. The same commands RAMMap's Empty menu sends.
        private const int MemoryEmptyWorkingSets = 2;
        private const int MemoryFlushModifiedList = 3;
        private const int MemoryPurgeStandbyList = 4;
        private const int MemoryPurgeLowPriorityStandbyList = 5;

        private const int StatusSuccess = 0;
        private const int StatusPrivilegeNotHeld = unchecked((int)0xC0000061);
        private const int ErrorAccessDenied = 5;
        private const int ErrorPrivilegeNotHeld = 1314;
        #endregion

        /// <summary>The order steps run in, which is not the order they are declared in.</summary>
        /// <remarks>
        ///  Trimming working sets moves pages to the standby cache, so every purge has to come
        ///  after every trim or it purges a cache that is about to be refilled. Flushing modified
        ///  pages comes before the purges for the same reason: a dirty page cannot be freed until
        ///  it has been written out.
        /// </remarks>
        public static readonly IReadOnlyList<MemoryStep> Order =
        [
            MemoryStep.CollectOwnGarbage,
            MemoryStep.TrimProcessWorkingSets,
            MemoryStep.EmptySystemWorkingSets,
            MemoryStep.FlushModifiedPages,
            MemoryStep.TrimFileCache,
            MemoryStep.PurgeLowPriorityStandby,
            MemoryStep.PurgeStandbyList,
            MemoryStep.DropPageCache,
        ];

        /// <summary>
        ///  What is ticked on a first run: everything that pays for itself.
        /// </summary>
        /// <remarks>
        ///  FlushModifiedPages is left out, as in V2 - it costs disk writes and can stall for
        ///  seconds, which is a poor trade for a button people press casually. DropPageCache is
        ///  left out for the matching reason on the other platforms: it needs root and drops a
        ///  cache that is not waste.
        /// </remarks>
        public static readonly IReadOnlyList<MemoryStep> Defaults =
        [
            MemoryStep.CollectOwnGarbage,
            MemoryStep.TrimProcessWorkingSets,
            MemoryStep.EmptySystemWorkingSets,
            MemoryStep.TrimFileCache,
            MemoryStep.PurgeLowPriorityStandby,
            MemoryStep.PurgeStandbyList,
        ];

        /// <summary>The steps worth offering here, in run order.</summary>
        public static IReadOnlyList<MemoryStep> Available =>
            Order.Where(Supported).ToList();

        /// <summary>Whether this platform can do anything at all for this step.</summary>
        public static bool Supported(MemoryStep step) => step switch
        {
            MemoryStep.CollectOwnGarbage => true,
            MemoryStep.DropPageCache => !OperatingSystem.IsWindows(),
            _ => OperatingSystem.IsWindows(),
        };

        /// <summary>Whether a step can only run from an elevated process.</summary>
        public static bool NeedsAdmin(MemoryStep step) =>
            step is not (MemoryStep.CollectOwnGarbage or MemoryStep.TrimProcessWorkingSets);

        /// <summary>True when this process can purge the standby list and the file cache itself.</summary>
        public static bool IsElevated
        {
            get
            {
                try
                {
                    if (!OperatingSystem.IsWindows())
                    {
                        // The other two ask for root, and geteuid is not worth a P/Invoke for a
                        // question the step itself answers when it is refused.
                        return false;
                    }

                    using WindowsIdentity identity = WindowsIdentity.GetCurrent();

                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <remarks>
        ///  The words come from the translated table, by the step's own name: a step added to the
        ///  enum without a row in gen.py shows its key, which the language tests then catch.
        /// </remarks>
        public static string Title(MemoryStep step) => Strings.Text(Key(step));

        public static string Explain(MemoryStep step) => Strings.Text(Key(step) + "Why");

        /// <summary>The table key for a step: the page-cache one differs between macOS and Linux.</summary>
        private static string Key(MemoryStep step) => step == MemoryStep.DropPageCache
            ? (OperatingSystem.IsMacOS() ? "MemStepDropPageCacheMac" : "MemStepDropPageCacheLinux")
            : "MemStep" + step;

        /// <summary>Why a step is not on offer here, for the one line beside a disabled row.</summary>
        public static string WhyUnsupported(MemoryStep step)
        {
            if (Supported(step))
            {
                return string.Empty;
            }

            if (step == MemoryStep.DropPageCache)
            {
                return Strings.Text("MemWhyNoPageCache");
            }

            return Strings.Text(step == MemoryStep.TrimProcessWorkingSets ? "MemWhyTrimWindowsOnly" : "MemWhyWindowsOnly");
        }

        public static string Describe(StepStatus status) => Strings.Text(status switch
        {
            StepStatus.Done => "MemStatusDone",
            StepStatus.NeedsAdmin => "MemStatusNeedsAdmin",
            StepStatus.Failed => "MemStatusFailed",
            StepStatus.Unsupported => "MemStatusUnsupported",
            _ => "MemStatusSkipped",
        });

        /// <summary>Reads a stored list of step names back, dropping any this build no longer has.</summary>
        public static List<MemoryStep> ParseSteps(IReadOnlyCollection<string>? names)
        {
            if (names is null)
            {
                return Defaults.Where(Supported).ToList();
            }

            List<MemoryStep> parsed = [];

            foreach (string name in names)
            {
                if (Enum.TryParse(name, ignoreCase: true, out MemoryStep step) && !parsed.Contains(step))
                {
                    parsed.Add(step);
                }
            }

            // Run order, not the order they happen to be stored in.
            return Order.Where(parsed.Contains).ToList();
        }

        /// <summary>
        ///  Runs the ticked steps in order, reporting each one as it starts and as it finishes.
        /// </summary>
        /// <remarks>
        ///  Both callbacks, not just the second: a step like trimming every working set takes long
        ///  enough to look like nothing is happening, and a list that only fills in after the fact
        ///  cannot say which one is taking the time.
        ///
        ///  Never throws for a step that fails: a clean is a list of independent attempts, and one
        ///  of them being refused is not a reason to abandon the rest. Cancellation is the one
        ///  exception, and it is the caller's own doing.
        /// </remarks>
        public static async Task<CleanReport> RunAsync(
            CleanOptions options,
            Action<MemoryStep>? starting,
            Action<StepOutcome>? progress,
            CancellationToken token)
        {
            MemoryStatus before = MemoryProbe.Read();

            List<StepOutcome> outcomes = [];

            // Steps this process cannot do itself, which is every privileged one unless the app
            // was started elevated. They are collected rather than run, so the whole set costs one
            // administrator prompt at the end instead of one prompt each.
            List<MemoryStep> deferred = [];

            bool canElevate = options.AllowElevation && Elevation.Available && !IsElevated;

            // Off the UI thread: trimming several hundred processes takes long enough to drop
            // frames, and the widget is spinning an animation while it happens.
            await Task.Run(
                async () =>
                {
                    if (OperatingSystem.IsWindows())
                    {
                        EnablePrivileges();
                    }

                    foreach (MemoryStep step in Order.Where(options.Steps.Contains))
                    {
                        token.ThrowIfCancellationRequested();

                        if (canElevate && Supported(step) && NeedsAdmin(step))
                        {
                            deferred.Add(step);
                            continue;
                        }

                        starting?.Invoke(step);

                        StepOutcome outcome = await ExecuteAsync(step, options, token);

                        outcomes.Add(outcome);
                        progress?.Invoke(outcome);
                    }
                },
                token);

            if (deferred.Count > 0)
            {
                foreach (MemoryStep step in deferred)
                {
                    starting?.Invoke(step);
                }

                foreach (StepOutcome outcome in await Elevation.RunAsync(deferred, token))
                {
                    outcomes.Add(outcome);
                    progress?.Invoke(outcome);
                }
            }

            return new CleanReport(before, MemoryProbe.Read(), outcomes);
        }

        private static async Task<StepOutcome> ExecuteAsync(
            MemoryStep step, CleanOptions options, CancellationToken token)
        {
            if (!Supported(step))
            {
                return new StepOutcome(step, StepStatus.Unsupported, WhyUnsupported(step));
            }

            try
            {
                // The OperatingSystem check is redundant - Supported(step) above already ruled
                // every Windows-only step out elsewhere - but the platform analyser cannot follow
                // that, and silencing it with a pragma would hide a real mistake later.
                (StepStatus status, string detail) = step switch
                {
                    MemoryStep.CollectOwnGarbage => CollectOwnGarbage(),
                    MemoryStep.DropPageCache => await DropPageCacheAsync(token),
                    _ when OperatingSystem.IsWindows() => RunWindowsStep(step, options, token),
                    _ => (StepStatus.Unsupported, WhyUnsupported(step)),
                };

                return new StepOutcome(step, status, detail);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new StepOutcome(step, StepStatus.Failed, exception.Message);
            }
        }

        /// <summary>
        ///  Runs one step in a process that is already elevated.
        /// </summary>
        /// <remarks>
        ///  The entry point for the helper started by <see cref="Elevation"/>. It enables the
        ///  privileges itself, because it is a fresh process that has not been through RunAsync.
        /// </remarks>
        public static StepOutcome RunOneElevated(MemoryStep step)
        {
            if (!OperatingSystem.IsWindows() || !Supported(step))
            {
                return new StepOutcome(step, StepStatus.Unsupported, WhyUnsupported(step));
            }

            EnablePrivileges();

            (StepStatus status, string detail) = step == MemoryStep.CollectOwnGarbage
                ? CollectOwnGarbage()
                : RunWindowsStep(step, new CleanOptions(), CancellationToken.None);

            return new StepOutcome(step, status, detail);
        }

        /// <summary>
        ///  Collects and hands this process's own pages back. The one step that works everywhere.
        /// </summary>
        private static (StepStatus, string) CollectOwnGarbage()
        {
            long before = GC.GetTotalMemory(false);

            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

            long after = GC.GetTotalMemory(false);

            // Collecting only returns memory to the runtime's own heap. On Windows this is what
            // hands the pages back to the OS, which is the part the user can actually see. The
            // other two kernels reclaim from an idle process on their own schedule.
            if (OperatingSystem.IsWindows())
            {
                EmptyWorkingSet(GetCurrentProcess());
            }

            return (StepStatus.Done,
                $"managed heap {MemoryProbe.Describe(before)} -> {MemoryProbe.Describe(after)}");
        }

        [SupportedOSPlatform("windows")]
        private static (StepStatus, string) RunWindowsStep(
            MemoryStep step, CleanOptions options, CancellationToken token) => step switch
            {
                MemoryStep.TrimProcessWorkingSets => TrimProcessWorkingSets(options, token),
                MemoryStep.EmptySystemWorkingSets => MemoryListCommand(MemoryEmptyWorkingSets),
                MemoryStep.FlushModifiedPages => MemoryListCommand(MemoryFlushModifiedList),
                MemoryStep.PurgeLowPriorityStandby => MemoryListCommand(MemoryPurgeLowPriorityStandbyList),
                MemoryStep.PurgeStandbyList => MemoryListCommand(MemoryPurgeStandbyList),
                MemoryStep.TrimFileCache => TrimFileCache(),
                _ => (StepStatus.Skipped, "unknown step"),
            };

        /// <summary>
        ///  Empties the working set of every process this account is allowed to open.
        /// </summary>
        /// <remarks>
        ///  Opened with PROCESS_SET_QUOTA | PROCESS_QUERY_LIMITED_INFORMATION rather than through
        ///  Process.Handle: the managed handle is opened for all access, which protected processes
        ///  and anything belonging to another account refuse outright.
        ///
        ///  Two things are deliberately left alone: anything on the exclusion list, and - when
        ///  <see cref="CleanOptions.SkipBusy"/> is set - anything that used processor time during a
        ///  short sample. Trimming a process that is working right now hands back a few megabytes
        ///  and charges a stall for them, because every page it is part way through using has to be
        ///  faulted straight back in.
        /// </remarks>
        [SupportedOSPlatform("windows")]
        private static (StepStatus, string) TrimProcessWorkingSets(
            CleanOptions options, CancellationToken token)
        {
            int trimmed = 0;
            int refused = 0;
            int spared = 0;

            int[] ids = EnumerateProcessIds();
            Dictionary<int, long> busy = options.SkipBusy ? SampleProcessorTime(ids) : [];

            if (options.SkipBusy)
            {
                // Long enough to catch anything genuinely working, short enough that nobody notices
                // the clean taking longer.
                Thread.Sleep(250);
            }

            foreach (int id in ids)
            {
                token.ThrowIfCancellationRequested();

                // 0 is the idle process and 4 is System; neither has a working set to empty.
                if (id is 0 or 4)
                {
                    continue;
                }

                IntPtr handle = OpenProcess(ProcessSetQuota | ProcessQueryLimitedInformation, false, id);

                if (handle == IntPtr.Zero)
                {
                    refused++;
                    continue;
                }

                try
                {
                    if (options.Excluded.Count > 0 && options.Excludes(NameOf(handle)))
                    {
                        spared++;
                        continue;
                    }

                    if (options.SkipBusy && WasBusy(handle, id, busy))
                    {
                        spared++;
                        continue;
                    }

                    if (EmptyWorkingSet(handle))
                    {
                        trimmed++;
                    }
                    else
                    {
                        refused++;
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }

            string left = spared > 0 ? $", {spared} left alone" : string.Empty;

            return (StepStatus.Done, $"{trimmed} process(es) trimmed, {refused} out of reach{left}");
        }

        /// <summary>Sends one SYSTEM_MEMORY_LIST_COMMAND. Administrator only.</summary>
        [SupportedOSPlatform("windows")]
        private static (StepStatus, string) MemoryListCommand(int command)
        {
            int value = command;
            int status = NtSetSystemInformation(SystemMemoryListInformation, ref value, sizeof(int));

            if (status == StatusSuccess)
            {
                return (StepStatus.Done, "done");
            }

            return status == StatusPrivilegeNotHeld
                ? (StepStatus.NeedsAdmin, "needs administrator")
                : (StepStatus.Failed, $"NTSTATUS 0x{status:X8}");
        }

        /// <summary>
        ///  Hands back the memory Windows is holding as cached file data.
        /// </summary>
        /// <remarks>
        ///  -1 for both sizes is the documented "flush it" value. Needs SeIncreaseQuotaPrivilege,
        ///  which in practice means an administrator token.
        /// </remarks>
        [SupportedOSPlatform("windows")]
        private static (StepStatus, string) TrimFileCache()
        {
            if (SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0))
            {
                return (StepStatus.Done, "done");
            }

            int error = Marshal.GetLastWin32Error();

            return error is ErrorPrivilegeNotHeld or ErrorAccessDenied
                ? (StepStatus.NeedsAdmin, "needs administrator")
                : (StepStatus.Failed, new System.ComponentModel.Win32Exception(error).Message);
        }

        /// <summary>Linux and macOS: the one privileged cache drop each of them has.</summary>
        private static async Task<(StepStatus, string)> DropPageCacheAsync(CancellationToken token)
        {
            bool mac = OperatingSystem.IsMacOS();

            if (!mac && !File.Exists("/proc/sys/vm/drop_caches"))
            {
                return (StepStatus.Failed, "this kernel has no /proc/sys/vm/drop_caches");
            }

            using Process process = new();
            process.StartInfo.FileName = mac ? "/usr/sbin/purge" : "/bin/sh";
            process.StartInfo.Arguments = mac
                ? string.Empty
                : "-c \"sync; echo 3 > /proc/sys/vm/drop_caches\"";
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            process.Start();

            string error = await process.StandardError.ReadToEndAsync(token);
            await process.WaitForExitAsync(token);

            if (process.ExitCode == 0)
            {
                return (StepStatus.Done, mac ? "purge finished" : "page cache dropped");
            }

            string detail = error.Trim().Length > 0 ? error.Trim() : $"exit code {process.ExitCode}";

            // Almost always the privilege, so say that rather than echoing a bare permission error.
            return (StepStatus.NeedsAdmin, $"needs {(mac ? "an administrator" : "root")} - {detail}");
        }

        /// <summary>Kernel plus user time for every process we can open, keyed by id.</summary>
        [SupportedOSPlatform("windows")]
        private static Dictionary<int, long> SampleProcessorTime(int[] ids)
        {
            Dictionary<int, long> sampled = [];

            foreach (int id in ids)
            {
                IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, id);

                if (handle == IntPtr.Zero)
                {
                    continue;
                }

                try
                {
                    if (GetProcessTimes(handle, out _, out _, out long kernel, out long user))
                    {
                        sampled[id] = kernel + user;
                    }
                }
                finally
                {
                    CloseHandle(handle);
                }
            }

            return sampled;
        }

        /// <summary>Whether this process has used any real processor time since the sample.</summary>
        [SupportedOSPlatform("windows")]
        private static bool WasBusy(IntPtr handle, int id, Dictionary<int, long> sampled)
        {
            if (!sampled.TryGetValue(id, out long before)
                || !GetProcessTimes(handle, out _, out _, out long kernel, out long user))
            {
                return false;
            }

            // Both clocks count 100ns ticks. A tenth of the sample window is a low bar on purpose:
            // the question is "is this thing doing anything at all", not "is it hot".
            const long threshold = 25 * 10_000;

            return kernel + user - before > threshold;
        }

        /// <summary>The executable name behind a handle, without path or extension.</summary>
        [SupportedOSPlatform("windows")]
        private static string NameOf(IntPtr handle)
        {
            int size = 260;
            System.Text.StringBuilder path = new(size);

            return QueryFullProcessImageName(handle, 0, path, ref size)
                ? Path.GetFileNameWithoutExtension(path.ToString())
                : string.Empty;
        }

        [SupportedOSPlatform("windows")]
        private static int[] EnumerateProcessIds()
        {
            int[] ids = new int[1024];

            while (true)
            {
                if (!EnumProcesses(ids, ids.Length * sizeof(int), out int needed))
                {
                    return [];
                }

                // A completely full buffer means the list was probably truncated: grow and ask again.
                if (needed < ids.Length * sizeof(int))
                {
                    return [.. ids.Take(needed / sizeof(int))];
                }

                ids = new int[ids.Length * 2];
            }
        }

        /// <summary>Turns on the two privileges the purge steps need, if this token has them at all.</summary>
        [SupportedOSPlatform("windows")]
        private static void EnablePrivileges()
        {
            EnablePrivilege(ProfileSingleProcessPrivilege);
            EnablePrivilege(IncreaseQuotaPrivilege);
        }

        [SupportedOSPlatform("windows")]
        private static bool EnablePrivilege(string name)
        {
            if (!OpenProcessToken(GetCurrentProcess(), TokenAdjustPrivileges | TokenQuery, out IntPtr token))
            {
                return false;
            }

            try
            {
                if (!LookupPrivilegeValue(null, name, out Luid value))
                {
                    return false;
                }

                TokenPrivileges state = new()
                {
                    Count = 1,
                    Privilege = new LuidAndAttributes { Value = value, Attributes = PrivilegeEnabled },
                };

                // AdjustTokenPrivileges reports success even when it assigned nothing, so the last
                // error is the only way to tell an enabled privilege from an absent one.
                bool adjusted = AdjustTokenPrivileges(
                    token, false, ref state, Marshal.SizeOf<TokenPrivileges>(), IntPtr.Zero, IntPtr.Zero);

                return adjusted && Marshal.GetLastWin32Error() != ErrorNotAllAssigned;
            }
            finally
            {
                CloseHandle(token);
            }
        }
    }
}
