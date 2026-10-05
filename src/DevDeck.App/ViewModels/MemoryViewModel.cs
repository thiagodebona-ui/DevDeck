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
        public ProcessRow(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }

        /// <summary>
        ///  The first letter, for the disc at the head of the row.
        /// </summary>
        /// <remarks>
        ///  Upper case even though the process name rarely is: a column of discs reading "c d e p"
        ///  looks like a mistake, and one reading "C D E P" looks like a design.
        /// </remarks>
        public string Initial => Name.Length > 0 ? Name[..1].ToUpperInvariant() : "?";

        /// <summary>Beside the name, so nothing has to be stacked under it.</summary>
        public string Pid => Strings.Format("MemPid", Id);

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

        public string Detail => Strings.Format("MemDetail", Name, Id, Size, Share.ToString("0.0"));

        /// <summary>Takes this tick's figures without replacing the row.</summary>
        public void Update(string size, double share, double bar, double cpuPercent)
        {
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

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => Refresh();
            timer.Start();

            Refresh();
        }

        public ObservableCollection<ProcessRow> Processes { get; } = new();

        public ObservableCollection<StepToggle> Steps { get; }

        /// <summary>Whether the privileged steps can run without a second process.</summary>
        public bool IsElevated { get; }

        public bool NeedsElevation => !IsElevated && Steps.Any(s => s.IsOn && s.NeedsAdmin);

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

            Status = await Task.Run(() => MemoryProbe.End(row.Id, row.Name));

            Refresh();
        }

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
        private async Task Clean()
        {
            List<MemoryStep> chosen = Steps.Where(s => s.IsOn && s.IsSupported).Select(s => s.Step).ToList();

            if (chosen.Count == 0)
            {
                Flash("none", good: false);
                Status = Strings.Text("MemNothingTicked");

                return;
            }

            IsBusy = true;
            Status = "Working…";

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
                    Excluded = settings.MemoryExcluded,
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

                Status = Strings.Format("MemFreedSummary", Describe(report.Freed), Ran(report));

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
            settings.MemorySteps = Steps.Where(s => s.IsOn).Select(s => s.Step.ToString()).ToList();
            settings.Save();

            OnPropertyChanged(nameof(NeedsElevation));
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

            Fill(processes.Top(12), memory.Total);

            if (!IsBusy)
            {
                Status = Strings.Format("MemInUse", Used, Total, CpuPercentText);
            }
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

                if (index >= Processes.Count || Processes[index].Id != usage.Id)
                {
                    ProcessRow fresh = new(usage.Id, usage.Name);

                    if (index < Processes.Count)
                    {
                        Processes[index] = fresh;
                    }
                    else
                    {
                        Processes.Add(fresh);
                    }
                }

                Processes[index].Update(
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
