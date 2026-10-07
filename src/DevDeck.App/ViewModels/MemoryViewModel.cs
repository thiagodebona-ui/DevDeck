using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  A process row in the readout.
    /// </summary>
    /// <remarks>
    ///  Mutable, and updated in place by <see cref="MemoryViewModel.Refresh"/>, rather than the
    ///  record it was: the list is rebuilt every two seconds, and clearing an ItemsControl to
    ///  refill it throws away every container - so the row under the pointer lost its highlight
    ///  twice a minute and the whole list flickered. Now a tick that does not change the order
    ///  changes nothing but the four numbers.
    /// </remarks>
    internal sealed partial class ProcessRow : ObservableObject
    {
        public ProcessRow(int id, string name, IReadOnlyList<int> ids)
        {
            Id = id;
            Name = name;
            this.ids = ids;
        }

        /// <summary>The process itself, or a group's heaviest member.</summary>
        public int Id { get; private set; }

        public string Name { get; }

        /// <summary>
        ///  The first letter, for the disc at the head of the row.
        /// </summary>
        /// <remarks>
        ///  Upper case even though the process name rarely is: a column of discs reading "c d e p"
        ///  looks like a mistake, and one reading "C D E P" looks like a design.
        /// </remarks>
        public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

        /// <summary>
        ///  What the row is keyed on between ticks: the name for a group, whose heaviest member -
        ///  and so its <see cref="Id"/> - can change from one tick to the next, or the id.
        /// </summary>
        public string Key => IsGroup ? "group:" + Name.ToUpperInvariant() : Id.ToString();

        /// <summary>Every process the row stands for; one, unless the list is grouped.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsGroup), nameof(CountText), nameof(Pid))]
        private IReadOnlyList<int> ids = [];

        public bool IsGroup => Ids.Count > 1;

        /// <summary>"×14", in the pill beside a grouped name.</summary>
        public string CountText => $"×{Ids.Count}";

        /// <summary>Beside the name, so nothing has to be stacked under it.</summary>
        public string Pid => IsGroup ? Strings.Format("MemProcessCount", Ids.Count) : Strings.Format("MemPid", Id);

        /// <summary>Working set, already in units.</summary>
        [ObservableProperty]
        private string size = "—";

        /// <summary>This process's share of physical memory, for the tooltip.</summary>
        [ObservableProperty]
        private double share;

        /// <summary>
        ///  How long this row's bar is, as a percentage of the longest one.
        /// </summary>
        /// <remarks>
        ///  Not the share of total memory, which is what it used to be. The biggest process on a
        ///  64 GB machine holds about one per cent of it, so every bar in the column came out a
        ///  pixel wide and the chart said nothing. Against the heaviest row, the column shows what
        ///  it is actually being asked - how these processes compare with each other.
        /// </remarks>
        [ObservableProperty]
        private double bar;

        [ObservableProperty]
        private string cpu = "—";

        [ObservableProperty]
        private double cpuPercent;

        /// <summary>Not using the processor right now, so its figure is drawn back.</summary>
        public bool IsIdle => CpuPercent < 0.05;

        /// <summary>Busy enough to be the answer to "what is making this machine slow".</summary>
        public bool IsHot => CpuPercent >= 20;

        public string Detail => IsGroup
            ? Strings.Format("MemDetailGroup", Name, Ids.Count, Size, Share.ToString("0.0"), string.Join(", ", Ids))
            : Strings.Format("MemDetail", Name, Id, Size, Share.ToString("0.0"));

        /// <summary>On the never-trim list, so the row says so and its button undoes it.</summary>
        [ObservableProperty]
        private bool isSpared;

        /// <summary>Takes this tick's figures without replacing the row.</summary>
        public void Update(ProcessUsage usage, string size, double share, double bar, double cpuPercent)
        {
            Id = usage.Id;

            // Only when the members changed: a fresh list every tick would redraw the pill and
            // the pid text twice a second for nothing.
            if (!Ids.SequenceEqual(usage.Ids))
            {
                Ids = usage.Ids;
            }

            Size = size;
            Share = share;
            Bar = bar;
            CpuPercent = cpuPercent;
            Cpu = $"{cpuPercent:0.0}%";

            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsHot));
            OnPropertyChanged(nameof(Detail));
        }
    }

    /// <summary>
    ///  One cleanup step, as a tick box.
    /// </summary>
    /// <remarks>
    ///  A step this platform cannot do is shown disabled with the reason, rather than hidden. The
    ///  list is then the same everywhere, and what Windows can do that Linux cannot is visible
    ///  instead of implied by an absence.
    /// </remarks>
    internal sealed partial class StepToggle : ObservableObject
    {
        private readonly Action changed;

        public StepToggle(MemoryStep step, bool on, Action changed)
        {
            Step = step;
            this.changed = changed;
            isOn = on && MemoryClean.Supported(step);

            // Read from the translated table, so a change of language has to redraw them.
            Strings.Changed += () =>
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Explain));
            };
        }

        public MemoryStep Step { get; }

        public string Title => MemoryClean.Title(Step);

        public string Explain => MemoryClean.Supported(Step)
            ? MemoryClean.Explain(Step)
            : MemoryClean.WhyUnsupported(Step);

        public bool IsSupported => MemoryClean.Supported(Step);

        public bool NeedsAdmin => IsSupported && MemoryClean.NeedsAdmin(Step);

        [ObservableProperty]
        private bool isOn;

        [ObservableProperty]
        private string outcome = string.Empty;

        [ObservableProperty]
        private bool outcomeIsBad;

        /// <summary>
        ///  Where this step is in the run: waiting its turn, working, or finished.
        /// </summary>
        /// <remarks>
        ///  Three flags rather than one enum plus converters, because the row switches style classes
        ///  on them directly. Trimming every working set on a busy machine takes seconds, and
        ///  without this the whole list sits blank until it is over with no clue which step is
        ///  responsible for the wait.
        /// </remarks>
        [ObservableProperty]
        private bool isWaiting;

        [ObservableProperty]
        private bool isRunning;

        [ObservableProperty]
        private bool isFinished;

        /// <summary>Resets the row to "ticked, not started" at the top of a run.</summary>
        public void Queue()
        {
            Outcome = Strings.Text("MemWaiting");
            OutcomeIsBad = false;
            IsWaiting = true;
            IsRunning = false;
            IsFinished = false;
        }

        public void Begin()
        {
            Outcome = "running…";
            IsWaiting = false;
            IsRunning = true;
            IsFinished = false;
        }

        public void Finish(string detail, bool bad)
        {
            Outcome = detail;
            OutcomeIsBad = bad;
            IsWaiting = false;
            IsRunning = false;
            IsFinished = true;
        }

        /// <summary>Clears the run state without clearing what the last run reported.</summary>
        public void Idle()
        {
            IsWaiting = false;
            IsRunning = false;
        }

        partial void OnIsOnChanged(bool value) => changed();
    }

    /// <summary>
    ///  One place disk space can come back from, as a tick box with its size beside it.
    /// </summary>
    /// <remarks>
    ///  The size is the point of the row. A cleaner that only says what it freed afterwards asks
    ///  for trust up front; one that says "npm cache, 3.1 GB" before anything is deleted lets the
    ///  user decide with the number in front of them.
    /// </remarks>
    internal sealed partial class SpaceItem : ObservableObject
    {
        private readonly Action changed;

        public SpaceItem(SpaceTarget target, bool on, Action changed)
        {
            Target = target;
            this.changed = changed;
            isOn = on && SpaceClean.Supported(target);

            Strings.Changed += () =>
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Explain));
                OnPropertyChanged(nameof(SizeText));
            };
        }

        public SpaceTarget Target { get; }

        public string Title => SpaceClean.Title(Target);

        public string Explain => SpaceClean.Explain(Target);

        public bool IsSupported => SpaceClean.Supported(Target);

        [ObservableProperty]
        private bool isOn;

        /// <summary>Null until measured, so "not measured" and "empty" read differently.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SizeText), nameof(IsEmpty))]
        private SpaceMeasure? measure;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SizeText))]
        private bool isMeasuring;

        [ObservableProperty]
        private bool isWorking;

        /// <summary>Where it looks, for the tooltip: a cleaner should never be vague about that.</summary>
        [ObservableProperty]
        private string where = string.Empty;

        [ObservableProperty]
        private string outcome = string.Empty;

        [ObservableProperty]
        private bool outcomeIsBad;

        public long Bytes => Measure?.Bytes ?? 0;

        /// <summary>Measured and found nothing, so the size is drawn back.</summary>
        public bool IsEmpty => Measure is { Bytes: 0 };

        public string SizeText => !IsSupported ? string.Empty
            : IsMeasuring ? "…"
            : Measure is not { } measured ? "—"
            : measured.Bytes == 0 ? Strings.Text("SpaceEmpty")
            : MemoryProbe.Describe(measured.Bytes);

        partial void OnIsOnChanged(bool value) => changed();
    }

    /// <summary>One clean in the history strip: a bar, and what it says on hover.</summary>
    internal sealed record HistoryBar(double Height, string Tip, bool Good);

    /// <summary>
    ///  Live memory and processor load, and everything V2's cleaner could do about the first.
    /// </summary>
    /// <remarks>
    ///  One view model for both the panel and the floating widget. They share it rather than owning
    ///  one each, so they are on the same two-second tick and cannot disagree about how much memory
    ///  is in use - and so pressing Clean on the widget updates the panel's step outcomes too.
    /// </remarks>
    internal sealed partial class MemoryViewModel : ObservableObject, IDisposable
    {
        /// <summary>
        ///  How long the widget keeps animating even when the clean was quicker than that.
        /// </summary>
        /// <remarks>
        ///  Straight from V2, and for V2's reason: a clean with only the local steps can be over in
        ///  under a tenth of a second. Without a floor the animation appears and vanishes in the
        ///  same frame, which reads as a click that did nothing rather than as work that finished.
        /// </remarks>
        private static readonly TimeSpan MinimumSpin = TimeSpan.FromMilliseconds(900);

        /// <summary>How long the result stays on the widget before it goes back to the reading.</summary>
        private static readonly TimeSpan FlashFor = TimeSpan.FromSeconds(4);

        private readonly AppSettings settings;
        private readonly DispatcherTimer timer;
        private readonly CpuProbe cpu = new();
        private readonly LoadProbe load = new();
        private readonly ProcessSampler processes = new();

        private CancellationTokenSource? flashCancellation;

        public MemoryViewModel(AppSettings settings)
        {
            this.settings = settings;

            IsElevated = MemoryClean.IsElevated;

            List<MemoryStep> chosen = MemoryClean.ParseSteps(settings.MemorySteps);

            Steps = new ObservableCollection<StepToggle>(
                MemoryClean.Order.Select(step => new StepToggle(step, chosen.Contains(step), SaveSteps)));

            List<SpaceTarget> targets = SpaceClean.ParseTargets(settings.SpaceTargets);

            SpaceItems = new ObservableCollection<SpaceItem>(
                SpaceClean.Order.Select(target => new SpaceItem(target, targets.Contains(target), SaveTargets)));

            Excluded = new ObservableCollection<string>(settings.MemoryExcluded);

            skipBusy = settings.MemorySkipBusy;
            groupProcesses = settings.MemoryGroupProcesses;
            allowElevation = settings.MemoryAllowElevation;
            autoClean = settings.MemoryAutoClean;
            autoThreshold = settings.MemoryAutoThreshold;
            autoCooldownMinutes = settings.MemoryAutoCooldownMinutes;
            autoWhenIdle = settings.MemoryAutoWhenIdle;
            autoIdleSeconds = settings.MemoryAutoIdleSeconds;

            ReadHistory();

            // The summaries below are built in code from the table, so a change of language has
            // to rebuild them; the bound {loc:T} labels look after themselves.
            Strings.Changed += () =>
            {
                ReadHistory();
                OnPropertyChanged(nameof(TickedSummary));
                OnPropertyChanged(nameof(PresetName));
                OnPropertyChanged(nameof(SpaceFreeLabel));
                OnPropertyChanged(nameof(CleanLabel));
            };

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => Refresh();
            timer.Start();

            Refresh();
        }

        public ObservableCollection<ProcessRow> Processes { get; } = new();

        public ObservableCollection<StepToggle> Steps { get; }

        /// <summary>Whether the privileged steps can run without a second process.</summary>
        public bool IsElevated { get; }

        public bool NeedsElevation => !IsElevated && AllowElevation && Steps.Any(s => s.IsOn && s.NeedsAdmin);

        /// <summary>Ticked admin steps that will be skipped, because the prompt has been switched off.</summary>
        public bool AdminStepsSkipped => !IsElevated && !AllowElevation && Steps.Any(s => s.IsOn && s.NeedsAdmin);

        #region Memory
        [ObservableProperty]
        private string total = "—";

        [ObservableProperty]
        private string used = "—";

        [ObservableProperty]
        private string available = "—";

        /// <summary>"of 63.7 GB physical", in the language the app is read in.</summary>
        public string TotalLine => Strings.Format("MemOfPhysical", Total);

        /// <summary>"38.4 GB available".</summary>
        public string AvailableLine => Strings.Format("MemAvailable", Available);

        partial void OnTotalChanged(string value) => OnPropertyChanged(nameof(TotalLine));

        partial void OnAvailableChanged(string value) => OnPropertyChanged(nameof(AvailableLine));

        [ObservableProperty]
        private double usedPercent;

        [ObservableProperty]
        private string usedPercentText = "—";

        [ObservableProperty]
        private bool isKnown;

        /// <summary>Above this the readout turns amber, then red - the usual pressure bands.</summary>
        public bool IsHigh => UsedPercent >= 75 && UsedPercent < 90;

        public bool IsCritical => UsedPercent >= 90;

        partial void OnUsedPercentChanged(double value)
        {
            OnPropertyChanged(nameof(IsHigh));
            OnPropertyChanged(nameof(IsCritical));
        }
        #endregion

        #region Processor
        [ObservableProperty]
        private double cpuPercent;

        [ObservableProperty]
        private string cpuPercentText = "—";

        [ObservableProperty]
        private bool isCpuKnown;

        public string Cores => Strings.Format("MemCores", Environment.ProcessorCount);

        /// <summary>
        ///  The same bands as memory, one notch higher.
        /// </summary>
        /// <remarks>
        ///  A machine at 80% memory is under pressure; a machine at 80% processor is working, which
        ///  is what a processor is for. Amber starts at 85 rather than 75 so a build does not paint
        ///  the readout as a problem.
        /// </remarks>
        public bool IsCpuHigh => CpuPercent >= 85 && CpuPercent < 95;

        public bool IsCpuCritical => CpuPercent >= 95;

        partial void OnCpuPercentChanged(double value)
        {
            OnPropertyChanged(nameof(IsCpuHigh));
            OnPropertyChanged(nameof(IsCpuCritical));
        }
        #endregion

        #region Disk and GPU
        [ObservableProperty]
        private double diskPercent;

        [ObservableProperty]
        private string diskPercentText = "—";

        /// <summary>The system drive and its size, under the activity figure.</summary>
        [ObservableProperty]
        private string diskDetail = string.Empty;

        /// <summary>How full the system drive is, for the space bar under the gauge.</summary>
        [ObservableProperty]
        private double diskSpacePercent;

        [ObservableProperty]
        private string diskSpaceText = string.Empty;

        [ObservableProperty]
        private bool hasDiskSpace;

        /// <summary>A drive nearly full is the one disk figure that needs acting on.</summary>
        public bool IsDiskSpaceHigh => DiskSpacePercent >= 85 && DiskSpacePercent < 95;

        public bool IsDiskSpaceCritical => DiskSpacePercent >= 95;

        partial void OnDiskSpacePercentChanged(double value)
        {
            OnPropertyChanged(nameof(IsDiskSpaceHigh));
            OnPropertyChanged(nameof(IsDiskSpaceCritical));
        }

        /// <summary>
        ///  When the drive was last asked. Space is read every five seconds rather than on every
        ///  two-second tick: it moves slowly, and asking the file system is not free.
        /// </summary>
        private DateTime spaceReadAt = DateTime.MinValue;

        private static readonly TimeSpan SpaceEvery = TimeSpan.FromSeconds(5);

        [ObservableProperty]
        private double gpuPercent;

        [ObservableProperty]
        private string gpuPercentText = "—";

        [ObservableProperty]
        private string gpuDetail = Strings.Text("MemoryGpu3d");

        /// <summary>Disk is busy long before it is a problem; amber at 80, red at 95.</summary>
        public bool IsDiskHigh => DiskPercent >= 80 && DiskPercent < 95;

        public bool IsDiskCritical => DiskPercent >= 95;

        /// <summary>The processor's bands: a GPU at 80% is a GPU doing its job.</summary>
        public bool IsGpuHigh => GpuPercent >= 85 && GpuPercent < 95;

        public bool IsGpuCritical => GpuPercent >= 95;

        /// <summary>The widget's third line: both of the quieter readings at once.</summary>
        public string GpuDiskText => Strings.Format("MemWidgetGpuDisk", GpuPercentText, DiskPercentText);

        partial void OnDiskPercentChanged(double value)
        {
            OnPropertyChanged(nameof(IsDiskHigh));
            OnPropertyChanged(nameof(IsDiskCritical));
        }

        partial void OnGpuPercentChanged(double value)
        {
            OnPropertyChanged(nameof(IsGpuHigh));
            OnPropertyChanged(nameof(IsGpuCritical));
        }

        partial void OnDiskPercentTextChanged(string value) => OnPropertyChanged(nameof(GpuDiskText));

        partial void OnGpuPercentTextChanged(string value) => OnPropertyChanged(nameof(GpuDiskText));

        /// <summary>Reads disk activity, GPU load and free space. Unknown readings keep their dash.</summary>
        private void ReadLoad()
        {
            (double? disk, double? gpu) = load.Read();

            if (disk is { } busy)
            {
                DiskPercent = busy;
                DiskPercentText = $"{busy:0}%";
            }

            if (gpu is { } graphics)
            {
                GpuPercent = graphics;
                GpuPercentText = $"{graphics:0}%";
            }
            else if (!OperatingSystem.IsWindows())
            {
                GpuDetail = Strings.Text("MemNotAvailable");
            }

            if (DateTime.UtcNow - spaceReadAt < SpaceEvery)
            {
                return;
            }

            spaceReadAt = DateTime.UtcNow;

            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) is { Length: > 0 } system
                    ? system
                    : "/";

                DriveInfo drive = new(root);

                if (drive.IsReady && drive.TotalSize > 0)
                {
                    long used = drive.TotalSize - drive.AvailableFreeSpace;

                    DiskDetail = Strings.Format(
                        "MemDiskTotal",
                        drive.Name.TrimEnd('\\', '/') is { Length: > 0 } name ? name : drive.Name,
                        MemoryProbe.Describe(drive.TotalSize));

                    DiskSpaceText = Strings.Format(
                        "MemDiskSpace",
                        MemoryProbe.Describe(used),
                        MemoryProbe.Describe(drive.AvailableFreeSpace));

                    DiskSpacePercent = used * 100.0 / drive.TotalSize;
                    HasDiskSpace = true;
                }
            }
            catch (Exception)
            {
                // A drive that cannot be asked keeps the last answer it gave.
            }
        }
        #endregion

        /// <summary>Ends a process from the list - and whatever it started - then re-reads it.</summary>
        [RelayCommand]
        private async Task EndProcess(ProcessRow? row)
        {
            if (row is null)
            {
                return;
            }

            if (!row.IsGroup)
            {
                Status = await Task.Run(() => MemoryProbe.End(row.Id, row.Name));
                Refresh();

                return;
            }

            // A group is every copy of a program - ending all of them is closing the program, and
            // that is worth one question first.
            List<int> ids = [.. row.Ids];

            bool sure = Confirm is { } ask && await ask(
                Strings.Format("MemEndGroup", row.Name, ids.Count),
                Strings.Format("MemEndGroupDetail", ids.Count, row.Name),
                Strings.Text("RunningKill"));

            if (!sure)
            {
                return;
            }

            int ended = await Task.Run(() => ids.Count(MemoryProbe.TryEnd));

            Status = Strings.Format("MemEndedGroup", ended, ids.Count, row.Name);
            Refresh();
        }

        /// <summary>One row per program, its processes summed, rather than one row per process.</summary>
        [ObservableProperty]
        private bool groupProcesses;

        partial void OnGroupProcessesChanged(bool value)
        {
            settings.MemoryGroupProcesses = value;
            settings.Save();

            // Rows keyed one way are the wrong rows the other way; start the list again.
            Processes.Clear();
            Refresh();
        }

        #region Spared processes
        /// <summary>
        ///  Processes no trim touches, by name. Shown as chips under the steps, each removable.
        /// </summary>
        public ObservableCollection<string> Excluded { get; }

        public bool HasExcluded => Excluded.Count > 0;

        /// <summary>
        ///  Puts a process on the never-trim list, or takes it off.
        /// </summary>
        /// <remarks>
        ///  From the process row, because that is where the user is looking when they notice the
        ///  thing they do not want touched - the database, the IDE - with its name already spelled
        ///  right. Typing a process name into a box is where exclusions go wrong.
        /// </remarks>
        [RelayCommand]
        private void SpareProcess(ProcessRow? row)
        {
            if (row is null)
            {
                return;
            }

            if (IsSpared(row.Name))
            {
                Unspare(row.Name);
            }
            else
            {
                settings.MemoryExcluded.Add(row.Name);
                Excluded.Add(row.Name);
                SaveExcluded();
            }
        }

        [RelayCommand]
        private void Unspare(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            settings.MemoryExcluded.RemoveAll(entry => string.Equals(entry, name, StringComparison.OrdinalIgnoreCase));

            foreach (string entry in Excluded.Where(entry => string.Equals(entry, name, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                Excluded.Remove(entry);
            }

            SaveExcluded();
        }

        private bool IsSpared(string name) =>
            settings.MemoryExcluded.Any(entry => string.Equals(entry, name, StringComparison.OrdinalIgnoreCase));

        private void SaveExcluded()
        {
            settings.Save();
            OnPropertyChanged(nameof(HasExcluded));

            foreach (ProcessRow row in Processes)
            {
                row.IsSpared = IsSpared(row.Name);
            }
        }
        #endregion

        #region Options
        /// <summary>Leave processes that are working right now out of the trim.</summary>
        [ObservableProperty]
        private bool skipBusy;

        /// <summary>Whether a clean may raise the administrator prompt at all.</summary>
        [ObservableProperty]
        private bool allowElevation;

        partial void OnSkipBusyChanged(bool value)
        {
            settings.MemorySkipBusy = value;
            settings.Save();
        }

        partial void OnAllowElevationChanged(bool value)
        {
            settings.MemoryAllowElevation = value;
            settings.Save();
            OnPropertyChanged(nameof(NeedsElevation));
            OnPropertyChanged(nameof(AdminStepsSkipped));
        }
        #endregion

        #region Presets
        /// <summary>"6 of 10 steps ticked", under the preset buttons.</summary>
        public string TickedSummary => Strings.Format(
            "MemTicked", Steps.Count(s => s.IsOn && s.IsSupported), Steps.Count(s => s.IsSupported));

        private CleanPreset? Preset => MemoryClean.PresetOf(Steps.Where(s => s.IsOn).Select(s => s.Step));

        public bool IsQuick => Preset == CleanPreset.Quick;

        public bool IsRecommended => Preset == CleanPreset.Recommended;

        public bool IsDeep => Preset == CleanPreset.Deep;

        /// <summary>Which preset the ticks amount to, or "custom" - so hand-ticking is not a mystery.</summary>
        public string PresetName => Strings.Text(Preset switch
        {
            CleanPreset.Quick => "MemPresetQuick",
            CleanPreset.Recommended => "MemPresetRecommended",
            CleanPreset.Deep => "MemPresetDeep",
            _ => "MemPresetCustom",
        });

        /// <summary>Set while a preset ticks its steps, so the row callbacks do not save ten times.</summary>
        private bool applyingPreset;

        [RelayCommand]
        private void ApplyPreset(string? name)
        {
            if (IsBusy || !Enum.TryParse(name, out CleanPreset preset))
            {
                return;
            }

            IReadOnlyList<MemoryStep> wanted = MemoryClean.StepsFor(preset);

            applyingPreset = true;

            try
            {
                foreach (StepToggle toggle in Steps.Where(s => s.IsSupported))
                {
                    toggle.IsOn = wanted.Contains(toggle.Step);
                }
            }
            finally
            {
                applyingPreset = false;
            }

            SaveSteps();
        }
        #endregion

        #region History
        /// <summary>The last cleans as bars, oldest on the left, scaled to the biggest.</summary>
        public ObservableCollection<HistoryBar> History { get; } = new();

        [ObservableProperty]
        private bool hasHistory;

        /// <summary>"Last clean freed 412 MB · 5 min ago".</summary>
        [ObservableProperty]
        private string lastCleanText = string.Empty;

        /// <summary>"9 cleans · 3.1 GB freed in all".</summary>
        [ObservableProperty]
        private string historyTotalText = string.Empty;

        /// <summary>Tallest bar in the strip, in pixels.</summary>
        private const double BarRoom = 34;

        /// <summary>
        ///  Rebuilds the strip from the stored lines.
        /// </summary>
        /// <remarks>
        ///  The "is this doing anything for me" answer, which is the one question a memory cleaner
        ///  owes its user. A clean that left less available draws as a stub, not as nothing, so a
        ///  row of failures still reads as a row of attempts.
        /// </remarks>
        private void ReadHistory()
        {
            List<CleanRecord> records = CleanHistory.Parse(settings.MemoryHistory);
            DateTime now = DateTime.UtcNow;

            History.Clear();

            long biggest = records.Count > 0 ? Math.Max(records.Max(r => r.Freed), 1) : 1;

            foreach (CleanRecord record in records)
            {
                double height = record.Freed > 0 ? Math.Max(3, record.Freed * BarRoom / biggest) : 2;

                History.Add(new HistoryBar(
                    height,
                    Strings.Format("MemStepOutcome", Describe(record.Freed), CleanHistory.Ago(record.When, now)),
                    record.Freed > 0));
            }

            HasHistory = records.Count > 0;

            if (records.Count == 0)
            {
                LastCleanText = Strings.Format("MemHistoryNone", CleanHistory.Keep);
                HistoryTotalText = string.Empty;

                return;
            }

            CleanRecord last = records[^1];

            LastCleanText = Strings.Format("MemLastClean", Describe(last.Freed), CleanHistory.Ago(last.When, now));
            HistoryTotalText = Strings.Format(
                "MemHistoryTotal",
                records.Count,
                MemoryProbe.Describe(records.Where(r => r.Freed > 0).Sum(r => r.Freed)));
        }

        private void Remember(long freed)
        {
            settings.MemoryHistory = CleanHistory.Append(settings.MemoryHistory, new CleanRecord(DateTime.UtcNow, freed));
            settings.Save();

            ReadHistory();
        }
        #endregion

        #region Automatic clean
        [ObservableProperty]
        private bool autoClean;

        [ObservableProperty]
        private int autoThreshold;

        [ObservableProperty]
        private int autoCooldownMinutes;

        [ObservableProperty]
        private bool autoWhenIdle;

        [ObservableProperty]
        private int autoIdleSeconds;

        /// <summary>What the automatic clean last did, under its settings.</summary>
        [ObservableProperty]
        private string autoStatus = string.Empty;

        private DateTime lastAutoClean = DateTime.MinValue;

        partial void OnAutoCleanChanged(bool value)
        {
            settings.MemoryAutoClean = value;
            settings.Save();
        }

        partial void OnAutoThresholdChanged(int value)
        {
            settings.MemoryAutoThreshold = Math.Clamp(value, 50, 99);
            settings.Save();
        }

        partial void OnAutoCooldownMinutesChanged(int value)
        {
            settings.MemoryAutoCooldownMinutes = Math.Clamp(value, 1, 1440);
            settings.Save();
        }

        partial void OnAutoWhenIdleChanged(bool value)
        {
            settings.MemoryAutoWhenIdle = value;
            settings.Save();
        }

        partial void OnAutoIdleSecondsChanged(int value)
        {
            settings.MemoryAutoIdleSeconds = Math.Clamp(value, 10, 3600);
            settings.Save();
        }

        /// <summary>
        ///  Starts a clean by itself when memory is past the line, the gap has passed and - if
        ///  asked - nobody is at the keyboard.
        /// </summary>
        /// <remarks>
        ///  The cooldown counts from the last clean of any kind, not only the last automatic one:
        ///  a clean the user pressed a minute ago has just done this job.
        ///
        ///  Never raises the administrator prompt. A UAC dialog appearing out of nowhere, over
        ///  whatever the user is doing, is indistinguishable from something malicious asking.
        /// </remarks>
        private void MaybeAutoClean(MemoryStatus memory)
        {
            if (!settings.MemoryAutoClean || IsBusy || memory.UsedPercent < settings.MemoryAutoThreshold)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            DateTime lastAny = CleanHistory.Parse(settings.MemoryHistory) is { Count: > 0 } records
                ? records[^1].When
                : DateTime.MinValue;

            TimeSpan gap = TimeSpan.FromMinutes(Math.Max(1, settings.MemoryAutoCooldownMinutes));

            if (now - lastAutoClean < gap || now - lastAny < gap)
            {
                return;
            }

            if (settings.MemoryAutoWhenIdle && KeepAwake.Idle < TimeSpan.FromSeconds(settings.MemoryAutoIdleSeconds))
            {
                return;
            }

            lastAutoClean = now;

            _ = RunClean(automatic: true);
        }
        #endregion

        #region Disk space
        public ObservableCollection<SpaceItem> SpaceItems { get; }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ScanSpaceCommand), nameof(FreeSpaceCommand))]
        private bool isSpaceBusy;

        [ObservableProperty]
        private string spaceStatus = string.Empty;

        /// <summary>"Free up 3.4 GB", or plain "Free up space" before anything is measured.</summary>
        public string SpaceFreeLabel
        {
            get
            {
                if (IsSpaceBusy)
                {
                    return Strings.Text("SpaceFreeing");
                }

                long ticked = SpaceItems.Where(item => item.IsOn && item.IsSupported).Sum(item => item.Bytes);

                return ticked > 0
                    ? Strings.Format("SpaceFree", MemoryProbe.Describe(ticked))
                    : Strings.Text("SpaceFreeNothing");
            }
        }

        partial void OnIsSpaceBusyChanged(bool value) => OnPropertyChanged(nameof(SpaceFreeLabel));

        private DateTime spaceScannedAt = DateTime.MinValue;

        /// <summary>How long a scan is trusted before showing the page measures again.</summary>
        private static readonly TimeSpan SpaceStale = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Asks for confirmation; set by the view, which owns windows. Null answers "no".
        /// </summary>
        public Func<string, string, string, Task<bool>>? Confirm { get; set; }

        /// <summary>
        ///  Measures when the page is shown, unless it did so in the last few minutes.
        /// </summary>
        /// <remarks>
        ///  Not at start-up: walking a temp folder with a hundred thousand files in it is not
        ///  something to charge every launch for, when most launches never open this page.
        /// </remarks>
        public void ScanSpaceIfStale()
        {
            if (!IsSpaceBusy && DateTime.UtcNow - spaceScannedAt > SpaceStale)
            {
                _ = ScanSpace();
            }
        }

        private bool CanSpace() => !IsSpaceBusy;

        [RelayCommand(CanExecute = nameof(CanSpace))]
        private async Task ScanSpace()
        {
            IsSpaceBusy = true;
            SpaceStatus = Strings.Text("SpaceScanning");

            try
            {
                await MeasureAll(SpaceItems.Where(item => item.IsSupported).ToList());
                spaceScannedAt = DateTime.UtcNow;
                SpaceStatus = string.Empty;
            }
            finally
            {
                IsSpaceBusy = false;
            }
        }

        /// <summary>Measures each item in turn, so the sizes fill in down the list as they arrive.</summary>
        private async Task MeasureAll(IReadOnlyList<SpaceItem> items)
        {
            foreach (SpaceItem item in items)
            {
                item.IsMeasuring = true;
            }

            foreach (SpaceItem item in items)
            {
                (SpaceMeasure measured, string where) = await Task.Run(() =>
                {
                    IReadOnlyList<string> folders = SpaceClean.Folders(item.Target);

                    return (SpaceClean.Measure(item.Target, CancellationToken.None), string.Join("\n", folders));
                });

                item.Measure = measured;
                item.Where = where.Length > 0 ? where : Strings.Text("SpaceNothingHere");
                item.IsMeasuring = false;

                OnPropertyChanged(nameof(SpaceFreeLabel));
            }
        }

        /// <summary>
        ///  Deletes the ticked targets, one at a time, then measures them again.
        /// </summary>
        /// <remarks>
        ///  The recycle bin is the one target that holds something the user put there on purpose,
        ///  so emptying it asks first even though it was ticked - a tick made a month ago is not a
        ///  decision about what is in the bin today.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(CanSpace))]
        private async Task FreeSpace()
        {
            List<SpaceItem> ticked = SpaceItems.Where(item => item.IsOn && item.IsSupported).ToList();

            if (ticked.Count == 0)
            {
                SpaceStatus = Strings.Text("SpaceNothingTicked");
                return;
            }

            if (ticked.FirstOrDefault(item => item.Target == SpaceTarget.RecycleBin) is { } bin
                && bin.Measure is not { Bytes: 0 })
            {
                bool sure = Confirm is { } ask && await ask(
                    Strings.Text("SpaceRecycleConfirm"),
                    Strings.Format("SpaceRecycleConfirmDetail", bin.SizeText),
                    Strings.Text("SpaceRecycleConfirmGo"));

                if (!sure)
                {
                    ticked.Remove(bin);
                }
            }

            IsSpaceBusy = true;

            long total = 0;

            try
            {
                foreach (SpaceItem item in ticked)
                {
                    item.IsWorking = true;
                    item.Outcome = Strings.Text("MemWaiting");
                    item.OutcomeIsBad = false;
                }

                foreach (SpaceItem item in ticked)
                {
                    SpaceStatus = Strings.Format("MemRunningStep", item.Title);

                    SpaceOutcome outcome = await Task.Run(() => SpaceClean.Free(item.Target, CancellationToken.None));

                    total += outcome.Freed;
                    item.IsWorking = false;
                    item.OutcomeIsBad = outcome.Failed;
                    item.Outcome = outcome.Failed
                        ? Strings.Format("SpaceFailed", outcome.Error)
                        : outcome.Skipped > 0
                            ? Strings.Format("SpaceOutcomeSkipped", MemoryProbe.Describe(outcome.Freed), outcome.Removed, outcome.Skipped)
                            : Strings.Format("SpaceOutcome", MemoryProbe.Describe(outcome.Freed), outcome.Removed);
                }

                await MeasureAll(ticked);
                spaceScannedAt = DateTime.UtcNow;

                SpaceStatus = Strings.Format("SpaceDone", MemoryProbe.Describe(total));
                Status = SpaceStatus;
                holdStatusUntil = DateTime.UtcNow + TimeSpan.FromSeconds(12);
            }
            finally
            {
                foreach (SpaceItem item in ticked)
                {
                    item.IsWorking = false;
                }

                IsSpaceBusy = false;

                // The drive's free-space bar is otherwise up to five seconds behind what just happened.
                spaceReadAt = DateTime.MinValue;
                ReadLoad();
            }
        }

        private void SaveTargets()
        {
            settings.SpaceTargets = SpaceItems.Where(item => item.IsOn).Select(item => item.Target.ToString()).ToList();
            settings.Save();

            OnPropertyChanged(nameof(SpaceFreeLabel));
        }
        #endregion

        #region Cleaning
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CleanCommand))]
        private bool isBusy;

        [ObservableProperty]
        private string status = "Reading…";

        /// <summary>The result the widget shows for a few seconds after a clean.</summary>
        [ObservableProperty]
        private string flashText = string.Empty;

        [ObservableProperty]
        private bool flashGood;

        [ObservableProperty]
        private bool isFlashing;

        public string CleanLabel => IsBusy ? Strings.Text("MemCleaning") : Strings.Text("MemCleanLabel");

        partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(CleanLabel));

        /// <summary>
        ///  Runs the ticked steps, then says what it bought.
        /// </summary>
        /// <remarks>
        ///  The minimum spin is honoured here rather than in the widget so both the panel's
        ///  progress and the widget's animation agree on when the work is "over", and so a clean
        ///  started from the panel animates the widget for the same length of time.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(CanClean))]
        private Task Clean() => RunClean(automatic: false);

        /// <summary>How far through the ticked steps a run is, 0 to 100, for the bar on the card.</summary>
        [ObservableProperty]
        private double cleanProgress;

        private int stepsToRun;

        /// <summary>Until when the status line keeps a result rather than the live reading.</summary>
        private DateTime holdStatusUntil = DateTime.MinValue;

        private int stepsRun;

        /// <summary>
        ///  The clean itself, from the button, the widget or the automatic trigger.
        /// </summary>
        /// <remarks>
        ///  An automatic run is the same run with the administrator prompt forced off: see
        ///  <see cref="MaybeAutoClean"/>.
        /// </remarks>
        private async Task RunClean(bool automatic)
        {
            if (IsBusy)
            {
                return;
            }

            List<MemoryStep> chosen = Steps.Where(s => s.IsOn && s.IsSupported).Select(s => s.Step).ToList();

            if (chosen.Count == 0)
            {
                if (!automatic)
                {
                    Flash("none", good: false);
                    Status = Strings.Text("MemNothingTicked");
                }

                return;
            }

            IsBusy = true;
            Status = "Working…";
            stepsToRun = chosen.Count;
            stepsRun = 0;
            CleanProgress = 0;

            // Every ticked step shows as waiting up front, so the list reads as a plan before it
            // reads as a result. Unticked ones are cleared rather than left showing last time's.
            foreach (StepToggle toggle in Steps)
            {
                if (chosen.Contains(toggle.Step))
                {
                    toggle.Queue();
                }
                else
                {
                    toggle.Outcome = string.Empty;
                    toggle.OutcomeIsBad = false;
                    toggle.IsFinished = false;
                    toggle.Idle();
                }
            }

            Stopwatch spinning = Stopwatch.StartNew();

            try
            {
                CleanOptions options = new()
                {
                    Steps = chosen,
                    SkipBusy = settings.MemorySkipBusy,
                    Excluded = [.. settings.MemoryExcluded],

                    // The setting existed but was never passed on, so switching the prompt off
                    // did nothing.
                    AllowElevation = settings.MemoryAllowElevation && !automatic,
                };

                CleanReport report = await MemoryClean.RunAsync(
                    options,
                    step => Dispatcher.UIThread.Post(() => Begin(step)),
                    outcome => Dispatcher.UIThread.Post(() => Record(outcome)),
                    CancellationToken.None);

                if (spinning.Elapsed < MinimumSpin)
                {
                    await Task.Delay(MinimumSpin - spinning.Elapsed);
                }

                Status = Strings.Format(
                    "MemFreedSummaryAvail",
                    Describe(report.Freed),
                    Ran(report),
                    MemoryProbe.Describe(report.Before.Available),
                    MemoryProbe.Describe(report.After.Available));

                if (automatic)
                {
                    AutoStatus = Strings.Format("MemAutoLast", Describe(report.Freed), DateTime.Now.ToString("HH:mm"));
                    Status = Strings.Format("MemAutoRan", Status);
                }

                Remember(report.Freed);

                // The summary is the answer to the button; the live reading can wait a moment.
                holdStatusUntil = DateTime.UtcNow + TimeSpan.FromSeconds(12);

                Flash(
                    report.Worthwhile ? $"−{MemoryProbe.Describe(report.Freed)}" : "0 MB",
                    report.Worthwhile);
            }
            catch (Exception exception)
            {
                Status = Strings.Format("MemCleanFailed", exception.Message);
                Flash("failed", good: false);
            }
            finally
            {
                // A run that threw part way through can leave a row saying "running…" for ever.
                foreach (StepToggle toggle in Steps)
                {
                    toggle.Idle();
                }

                IsBusy = false;
                CleanProgress = 0;
                OnPropertyChanged(nameof(NeedsElevation));
                Refresh();
            }
        }

        private bool CanClean() => !IsBusy;

        /// <summary>Marks the step that has just started, so the row shows it working.</summary>
        private void Begin(MemoryStep step)
        {
            if (Steps.FirstOrDefault(s => s.Step == step) is { } toggle)
            {
                toggle.Begin();
                Status = Strings.Format("MemRunningStep", MemoryClean.Title(step));
            }
        }

        /// <summary>Puts one step's result on its own row, where the tick box is.</summary>
        private void Record(StepOutcome outcome)
        {
            if (Steps.FirstOrDefault(s => s.Step == outcome.Step) is not { } toggle)
            {
                return;
            }

            toggle.Finish(
                Strings.Format("MemStepOutcome", MemoryClean.Describe(outcome.Status), outcome.Detail),
                outcome.IsBad);

            stepsRun++;
            CleanProgress = stepsToRun > 0 ? stepsRun * 100.0 / stepsToRun : 0;
        }

        /// <summary>
        ///  Shows a result on the widget for a few seconds.
        /// </summary>
        /// <remarks>
        ///  The previous flash is cancelled rather than left to expire, or a second clean inside
        ///  four seconds has its result wiped early by the first one's timer.
        /// </remarks>
        private void Flash(string text, bool good)
        {
            flashCancellation?.Cancel();
            flashCancellation?.Dispose();
            flashCancellation = new CancellationTokenSource();

            CancellationToken token = flashCancellation.Token;

            FlashText = text;
            FlashGood = good;
            IsFlashing = true;

            _ = Task.Delay(FlashFor, token).ContinueWith(
                _ => Dispatcher.UIThread.Post(() => IsFlashing = false),
                token,
                TaskContinuationOptions.OnlyOnRanToCompletion,
                TaskScheduler.Default);
        }

        /// <summary>"Freed 412 MB", "38 MB less" - reclaim can leave less than before.</summary>
        private static string Describe(long freed) => freed switch
        {
            > 0 => Strings.Format("MemFreed", MemoryProbe.Describe(freed)),
            < 0 => Strings.Format("MemLessAvailable", MemoryProbe.Describe(-freed)),
            _ => Strings.Text("MemNoChange"),
        };

        private static string Ran(CleanReport report)
        {
            int done = report.Steps.Count(step => step.IsDone);

            return Strings.Format(
                report.Steps.Count == 1 ? "MemStepsRanOne" : "MemStepsRan",
                done,
                report.Steps.Count);
        }

        private void SaveSteps()
        {
            if (applyingPreset)
            {
                return;
            }

            settings.MemorySteps = Steps.Where(s => s.IsOn).Select(s => s.Step.ToString()).ToList();
            settings.Save();

            OnPropertyChanged(nameof(NeedsElevation));
            OnPropertyChanged(nameof(AdminStepsSkipped));
            OnPropertyChanged(nameof(TickedSummary));
            OnPropertyChanged(nameof(PresetName));
            OnPropertyChanged(nameof(IsQuick));
            OnPropertyChanged(nameof(IsRecommended));
            OnPropertyChanged(nameof(IsDeep));
        }
        #endregion

        [RelayCommand]
        private void Refresh()
        {
            MemoryStatus memory = MemoryProbe.Read();

            IsKnown = memory.IsKnown;

            CpuStatus load = cpu.Read();

            // The probe reports unknown on its first call and after a counter wrap. Holding the
            // previous figure is better than blinking to a dash twice a minute.
            if (load.IsKnown)
            {
                IsCpuKnown = true;
                CpuPercent = load.UsedPercent;
                CpuPercentText = CpuProbe.Describe(load.UsedPercent);
            }

            if (!memory.IsKnown)
            {
                Status = Strings.Text("MemUnreadable");
                return;
            }

            Total = MemoryProbe.Describe(memory.Total);
            Used = MemoryProbe.Describe(memory.Used);
            Available = MemoryProbe.Describe(memory.Available);
            UsedPercent = memory.UsedPercent;
            UsedPercentText = $"{memory.UsedPercent:0}%";

            ReadLoad();

            Fill(processes.Top(12, GroupProcesses), memory.Total);

            if (!IsBusy && !IsSpaceBusy && DateTime.UtcNow >= holdStatusUntil)
            {
                Status = Strings.Format("MemInUse", Used, Total, CpuPercentText);
            }

            MaybeAutoClean(memory);
        }

        /// <summary>
        ///  Puts this tick's sample into <see cref="Processes"/>, reusing the rows already there.
        /// </summary>
        /// <remarks>
        ///  Row <c>n</c> is overwritten in place when it is still the same process, which is the
        ///  usual case: the heaviest dozen processes rarely reorder between two ticks. Only a row
        ///  whose process has changed is replaced, and the tail is trimmed rather than the whole
        ///  list cleared, so the control keeps its containers and the list does not blink.
        /// </remarks>
        private void Fill(IReadOnlyList<ProcessUsage> top, long totalMemory)
        {
            // The sample is ordered by working set, so the first row is the longest bar and every
            // other bar is measured against it.
            long biggest = top.Count > 0 ? top[0].WorkingSet : 0;

            for (int index = 0; index < top.Count; index++)
            {
                ProcessUsage usage = top[index];

                string key = usage.IsGroup ? "group:" + usage.Name.ToUpperInvariant() : usage.Id.ToString();

                if (index >= Processes.Count || Processes[index].Key != key)
                {
                    ProcessRow fresh = new(usage.Id, usage.Name, usage.Ids);

                    if (index < Processes.Count)
                    {
                        Processes[index] = fresh;
                    }
                    else
                    {
                        Processes.Add(fresh);
                    }
                }

                Processes[index].IsSpared = IsSpared(usage.Name);
                Processes[index].Update(
                    usage,
                    MemoryProbe.Describe(usage.WorkingSet),
                    totalMemory > 0 ? usage.WorkingSet * 100.0 / totalMemory : 0,
                    biggest > 0 ? usage.WorkingSet * 100.0 / biggest : 0,
                    usage.CpuPercent);
            }

            while (Processes.Count > top.Count)
            {
                Processes.RemoveAt(Processes.Count - 1);
            }
        }

        #region Widget
        /// <summary>
        ///  Shows the floating readout, or brings the open one forward.
        /// </summary>
        /// <remarks>
        ///  Set from the view, which is the only part that may know about windows.
        /// </remarks>
        public Func<bool>? ToggleWidget { get; set; }

        [ObservableProperty]
        private bool isWidgetOpen;

        [RelayCommand]
        private void Widget()
        {
            if (ToggleWidget is { } toggle)
            {
                IsWidgetOpen = toggle();
            }
        }

        public string WidgetLabel => IsWidgetOpen ? Strings.Text("MemHideWidget") : Strings.Text("MemFloatWidget");

        /// <summary>
        ///  Brings the main window back, and ends the app. Both set by the view.
        /// </summary>
        /// <remarks>
        ///  The widget is deliberately not in the taskbar - it is an instrument on the desktop, not
        ///  a window you alt-tab to. That left one way to lose the app entirely: close the main
        ///  window with the widget floating, and there was nothing left that could bring it back.
        ///  The widget's own menu is now the way back, and the way out.
        /// </remarks>
        public Action? ShowApp { get; set; }

        public Action? QuitApp { get; set; }

        [RelayCommand]
        private void Show() => ShowApp?.Invoke();

        [RelayCommand]
        private void Quit() => QuitApp?.Invoke();

        [RelayCommand]
        private void HideWidget()
        {
            if (IsWidgetOpen && ToggleWidget is { } toggle)
            {
                IsWidgetOpen = toggle();
            }
        }

        partial void OnIsWidgetOpenChanged(bool value) => OnPropertyChanged(nameof(WidgetLabel));
        #endregion

        public void Dispose()
        {
            timer.Stop();
            load.Dispose();
            flashCancellation?.Cancel();
            flashCancellation?.Dispose();
        }
    }
}
