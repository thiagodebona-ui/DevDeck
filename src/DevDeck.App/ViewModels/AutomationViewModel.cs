using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One step of a chain, as a row that can be moved and removed.</summary>
    /// <remarks>
    ///  An object rather than the bare string it wraps, because a chain is allowed to run the same
    ///  command twice - "build, test, build" is a perfectly sensible chain - and a list of strings
    ///  gives the row buttons nothing to identify a row by. Asked to remove "build", a list of
    ///  strings removes the first one, which is not the one whose button was pressed.
    /// </remarks>
    internal sealed partial class StepRow : ObservableObject
    {
        public StepRow(string name, bool on = true)
        {
            this.name = name;
            isOn = on;
        }

        [ObservableProperty]
        private string name;

        /// <summary>Whether the step runs. Off keeps it in the chain, in its place, for later.</summary>
        [ObservableProperty]
        private bool isOn;

        /// <summary>Set by the chain, so a row knows where it sits without holding the list.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Position))]
        private int index;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanMoveDown))]
        private int total;

        /// <summary>The number shown beside the step, counting from one as people do.</summary>
        public string Position => (Index + 1).ToString(CultureInfo.InvariantCulture);

        public bool CanMoveUp => Index > 0;

        public bool CanMoveDown => Index < Total - 1;

        /// <summary>Whether the deck still has a command of this name.</summary>
        [ObservableProperty]
        private bool isMissing;

        partial void OnIndexChanged(int value)
        {
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
        }
    }

    /// <summary>One chain, as the panel edits it.</summary>
    /// <remarks>
    ///  A wrapper rather than the stored object, for the same reason <see cref="CommandItem"/> is
    ///  one: the panel needs change notification and a running/not-running state, and the thing
    ///  that gets written to settings should hold neither.
    /// </remarks>
    internal sealed partial class ChainItem : ObservableObject
    {
        private readonly CommandChain chain;

        public ChainItem(CommandChain chain)
        {
            this.chain = chain;

            Steps = new ObservableCollection<StepRow>(chain.Steps.Select((step, at) => Row(step, chain.IsOn(at))));

            Number();
        }

        /// <summary>Set by the panel: the commands a step may name, for the picker.</summary>
        /// <remarks>
        ///  A delegate rather than a copied list, for the same reason the watch rules use one: the
        ///  deck changes underneath this panel when a command is added, renamed or deleted, and a
        ///  list taken at construction would go on offering names that are no longer there.
        /// </remarks>
        public Func<IEnumerable<string>>? Choices { get; set; }

        /// <summary>The names the step picker offers.</summary>
        public IEnumerable<string> CommandChoices => Choices?.Invoke() ?? [];

        /// <summary>
        ///  The steps, in the order they run.
        /// </summary>
        /// <remarks>
        ///  This replaced a text box holding one command name per line. The text box was quick to
        ///  write and wrong to use: every step had to be typed exactly, a typo was not discovered
        ///  until the chain was run and failed at that step, and the list of names it had to match
        ///  was on a different page. A picker can only produce names that exist, and reordering is
        ///  two buttons rather than a cut and paste.
        ///
        ///  The rows are the editable copy and <see cref="CommandChain.Steps"/> is what is saved;
        ///  <see cref="Commit"/> is what puts one into the other, and every mutation here ends with
        ///  a call to it.
        /// </remarks>
        public ObservableCollection<StepRow> Steps { get; }

        public bool HasSteps => Steps.Count > 0;

        /// <summary>The step the picker is currently showing, before it is added.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddStepCommand))]
        private string? picked;

        /// <summary>Appends the picked command as the next step.</summary>
        /// <remarks>
        ///  The picker is cleared afterwards so that it reads as an action rather than as a field
        ///  holding a value - and so that adding the same command twice in a row does not need the
        ///  user to select something else and come back to it.
        /// </remarks>
        [RelayCommand(CanExecute = nameof(CanAddStep))]
        private void AddStep()
        {
            if (Picked is not { Length: > 0 } name)
            {
                return;
            }

            Steps.Add(Row(name, on: true));
            Picked = null;

            Commit();
        }

        private bool CanAddStep() => Picked is { Length: > 0 };

        [RelayCommand]
        private void RemoveStep(StepRow? row)
        {
            if (row is null)
            {
                return;
            }

            Steps.Remove(row);

            Commit();
        }

        [RelayCommand]
        private void MoveStepUp(StepRow? row) => Shift(row, -1);

        [RelayCommand]
        private void MoveStepDown(StepRow? row) => Shift(row, 1);

        private void Shift(StepRow? row, int by)
        {
            if (row is null)
            {
                return;
            }

            int from = Steps.IndexOf(row);
            int to = from + by;

            // Silently, rather than disabling only in the view. The buttons are bound to CanMoveUp
            // and CanMoveDown already; this is the guard for the moment between a list changing and
            // those being re-read.
            if (from < 0 || to < 0 || to >= Steps.Count)
            {
                return;
            }

            Steps.Move(from, to);

            Commit();
        }

        /// <summary>Writes the rows back onto the chain that is saved, and renumbers them.</summary>
        /// <summary>A row for a step, saved again whenever it is switched on or off.</summary>
        private StepRow Row(string name, bool on)
        {
            StepRow row = new(name, on);

            row.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName == nameof(StepRow.IsOn))
                {
                    Commit();
                }
            };

            return row;
        }

        private void Commit()
        {
            chain.Steps = [.. Steps.Select(step => step.Name)];

            // Read off the rows' order now, so a step moved up or down keeps its own switch.
            chain.SkippedSteps = [.. Steps.Select((step, at) => (step, at)).Where(row => !row.step.IsOn).Select(row => row.at)];

            Number();

            OnPropertyChanged(nameof(HasSteps));
            OnPropertyChanged(nameof(Summary));
        }

        /// <summary>Tells each row where it sits, so it can draw its number and arrows.</summary>
        private void Number()
        {
            for (int at = 0; at < Steps.Count; at++)
            {
                Steps[at].Index = at;
                Steps[at].Total = Steps.Count;
            }
        }

        public CommandChain Source => chain;

        public string Name
        {
            get => chain.Name;
            set
            {
                if (chain.Name == value)
                {
                    return;
                }

                chain.Name = value;
                OnPropertyChanged();
            }
        }

        public bool StopOnFailure
        {
            get => chain.StopOnFailure;
            set
            {
                chain.StopOnFailure = value;
                OnPropertyChanged();
            }
        }

        /// <summary>The steps on one line, for the row in the list.</summary>
        public string Summary => chain.Steps.Count == 0
            ? Strings.Text("AutoNoSteps")
            : string.Join(" → ", chain.Steps);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsIdle))]
        private bool isRunning;

        public bool IsIdle => !IsRunning;

        [ObservableProperty]
        private string status = Strings.Text("AutoNotRunYet");

        /// <summary>
        ///  Every step's output from the last run, one after another, under a heading per step.
        /// </summary>
        /// <remarks>
        ///  Kept on the chain rather than read off the deck, so the chain can be watched from the
        ///  Automation page without the user being sent anywhere, and so two chains running at once
        ///  each keep a log of their own. The deck still has each command's own output as well.
        /// </remarks>
        public ObservableCollection<OutputLine> Output { get; } = [];

        public bool HasOutput => Output.Count > 0;

        /// <summary>The longest the combined log may grow before its oldest lines go.</summary>
        private const int MaxOutputLines = 10000;

        /// <summary>This chain's own run, so stopping one chain leaves the others going.</summary>
        internal CancellationTokenSource? Cancellation { get; set; }

        /// <summary>The deck command this chain is running right now, for Stop.</summary>
        internal CommandItem? Current { get; set; }

        /// <summary>Adds lines to the combined log, trimming the oldest past the ceiling.</summary>
        internal void Write(IEnumerable<OutputLine> lines)
        {
            bool wasEmpty = Output.Count == 0;

            foreach (OutputLine line in lines)
            {
                Output.Add(line);
            }

            if (Output.Count > MaxOutputLines)
            {
                // A tenth at a time rather than a line at a time, so a chatty build does not pay
                // for a removal on every line once it reaches the ceiling.
                int over = Output.Count - MaxOutputLines + (MaxOutputLines / 10);

                for (int removed = 0; removed < over; removed++)
                {
                    Output.RemoveAt(0);
                }
            }

            if (wasEmpty != (Output.Count == 0))
            {
                OnPropertyChanged(nameof(HasOutput));
            }
        }

        internal void Write(string text, LogLevel level, bool heading = false) =>
            Write([new OutputLine(DateTime.Now, text, level, IsHeading: heading)]);

        /// <summary>
        ///  Swaps a step's heading for its finished form, in place.
        /// </summary>
        /// <remarks>
        ///  The heading is how a step's result is shown: written when the step starts, then
        ///  replaced with a tick or a cross when it ends. One line per step that says what it was
        ///  and how it went, rather than a heading above and an "exited with code 0" below - the
        ///  outcome is where the eye already is, and a run of passing steps reads as a column of
        ///  ticks.
        ///
        ///  Found by reference from the end, because it is nearly always recent and a log of a
        ///  thousand lines should not be walked from the top. A heading already trimmed off the
        ///  front of a very long log is written again at the end instead, so the outcome is not
        ///  lost with it.
        /// </remarks>
        internal void Settle(OutputLine heading, OutputLine settled)
        {
            for (int at = Output.Count - 1; at >= 0; at--)
            {
                if (ReferenceEquals(Output[at], heading))
                {
                    Output[at] = settled;

                    return;
                }
            }

            Write([settled]);
        }

        [RelayCommand]
        private void ClearOutput()
        {
            Output.Clear();
            OnPropertyChanged(nameof(HasOutput));
        }

        /// <summary>The steps naming a command that no longer exists, if any.</summary>
        [ObservableProperty]
        private string broken = string.Empty;

        public bool IsBroken => !string.IsNullOrEmpty(Broken);

        partial void OnBrokenChanged(string value) => OnPropertyChanged(nameof(IsBroken));

        /// <summary>Re-reads the steps after something else edited them - a rename, say.</summary>
        /// <remarks>
        ///  The rows are rebuilt rather than poked, because what changed may be their count as well
        ///  as their text: a chain edited on disk and reloaded is the same object with a different
        ///  list in it. Rebuilding is a handful of strings and happens on a panel switch.
        /// </remarks>
        public void Refresh()
        {
            Steps.Clear();

            for (int at = 0; at < chain.Steps.Count; at++)
            {
                Steps.Add(Row(chain.Steps[at], chain.IsOn(at)));
            }

            Number();

            OnPropertyChanged(nameof(HasSteps));
            OnPropertyChanged(nameof(CommandChoices));
            OnPropertyChanged(nameof(Summary));
        }

        /// <summary>Marks the steps naming a command the deck no longer has.</summary>
        /// <remarks>
        ///  Per row as well as in the sentence below the list, because "deploy is not a command in
        ///  this deck" does not say which of the three deploy steps it meant.
        /// </remarks>
        public void Mark(Func<string, bool> exists)
        {
            foreach (StepRow step in Steps)
            {
                step.IsMissing = !exists(step.Name);
            }
        }

        public override string ToString() => Name;
    }

    /// <summary>One watch rule, as the panel edits it.</summary>
    internal sealed partial class WatchItem : ObservableObject, IDisposable
    {
        private readonly WatchRule rule;

        private FileWatch? watch;

        public WatchItem(WatchRule rule) => this.rule = rule;

        public WatchRule Source => rule;

        /// <summary>Set by the panel: starts the command this rule names, for what just changed.</summary>
        public Action<WatchItem, WatchedChange>? Fire { get; set; }

        /// <summary>Set by the panel: whether the command this rule names is already running.</summary>
        public Func<WatchItem, bool>? Busy { get; set; }

        /// <summary>Set by the panel: the workspace, used when the rule names no folder.</summary>
        public Func<string>? Fallback { get; set; }

        /// <summary>Set by the panel: the commands this rule may name, for the picker.</summary>
        public Func<IEnumerable<string>>? Choices { get; set; }

        /// <summary>
        ///  The names the command picker offers.
        /// </summary>
        /// <remarks>
        ///  Read through a delegate rather than held, because the deck changes under this panel -
        ///  a command added, renamed or deleted while the automation page is open - and a list
        ///  copied at construction would offer names that no longer exist.
        /// </remarks>
        public IEnumerable<string> CommandChoices => Choices?.Invoke() ?? [];

        public string Name
        {
            get => rule.Name;
            set { rule.Name = value; OnPropertyChanged(); }
        }

        public string Command
        {
            get => rule.Command;
            set { rule.Command = value; OnPropertyChanged(); Restart(); }
        }

        public string Folder
        {
            get => rule.Folder;
            set { rule.Folder = value; OnPropertyChanged(); OnPropertyChanged(nameof(Watching)); Restart(); }
        }

        public string Pattern
        {
            get => rule.Pattern;
            set { rule.Pattern = value; OnPropertyChanged(); Restart(); }
        }

        public bool Notify
        {
            get => rule.Notify;
            set { rule.Notify = value; OnPropertyChanged(); }
        }

        public bool Enabled
        {
            get => rule.Enabled;
            set
            {
                if (rule.Enabled == value)
                {
                    return;
                }

                rule.Enabled = value;
                OnPropertyChanged();
                Restart();
            }
        }

        /// <summary>The folder this actually watches, once the fallback is applied.</summary>
        public string Watching => string.IsNullOrWhiteSpace(rule.Folder)
            ? Fallback?.Invoke() ?? string.Empty
            : rule.Folder;

        [ObservableProperty]
        private string status = "Off.";

        [ObservableProperty]
        private int runs;

        /// <summary>
        ///  Starts or stops the underlying watcher to match <see cref="Enabled"/>.
        /// </summary>
        /// <remarks>
        ///  Called on every edit, because a rule whose folder or pattern changed while it was
        ///  running would otherwise keep watching the old one - a watch that reports the wrong
        ///  thing is worse than one that is off, since the user believes it.
        /// </remarks>
        public void Restart()
        {
            watch?.Dispose();
            watch = null;

            if (!Enabled)
            {
                Status = "Off.";

                return;
            }

            string folder = Watching;

            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                Status = Strings.Text("AutoNoFolder");

                return;
            }

            if (string.IsNullOrWhiteSpace(Command))
            {
                Status = Strings.Text("AutoNoCommand");

                return;
            }

            try
            {
                watch = new FileWatch(
                    rule,
                    folder,
                    change => Fire?.Invoke(this, change),
                    () => Busy?.Invoke(this) ?? false);

                Status = Strings.Format("AutoWatching", Pattern, folder);

                AppLog.Instance.Info("watch", $"{Name}: watching {Pattern} in {folder}");
            }
            catch (Exception exception)
            {
                // A folder on a filesystem that cannot be watched - some network shares, some
                // container mounts - fails here rather than never firing for no stated reason.
                Status = Strings.Format("AutoCannotWatch", exception.Message);

                AppLog.Instance.Failure("watch", $"{Name} could not watch {folder}", exception);
            }
        }

        /// <summary>Re-reads the folder after the workspace moved under it.</summary>
        public void Refresh() => OnPropertyChanged(nameof(Watching));

        /// <summary>Notes that the rule fired, for the row to show.</summary>
        public void Fired(WatchedChange change)
        {
            Runs++;
            Status = Strings.Format(
                "AutoRanAtFor",
                DateTime.Now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                Describe(change),
                Runs);

            // The whole reason the log exists: something ran that the user did not press.
            AppLog.Instance.Good("watch", $"{Name} ran {Command} - {Describe(change)} changed");
        }

        /// <summary>
        ///  What changed, in a few words: one file by name, or a count.
        /// </summary>
        /// <remarks>
        ///  Naming the file is the whole value of the message when there is one file, and listing
        ///  forty paths is the whole problem when there are forty - so the two cases read
        ///  differently rather than sharing one shape that suits neither.
        /// </remarks>
        public static string Describe(WatchedChange change) => change.Count switch
        {
            0 => Strings.Text("AutoSomethingChanged"),
            1 => System.IO.Path.GetFileName(change.First),
            _ => Strings.Format("AutoAndOthers", System.IO.Path.GetFileName(change.First), change.Count - 1),
        };

        public void Dispose()
        {
            watch?.Dispose();
            watch = null;
        }

        public override string ToString() => Name;
    }

    /// <summary>
    ///  Chains, watches and the deck a project commits.
    /// </summary>
    /// <remarks>
    ///  All three are the same idea from different directions: something other than a click starts
    ///  a command. Keeping them in one panel means the deck stays a list of commands rather than
    ///  growing three kinds of row, and the things that run commands are in one place to look at
    ///  when something ran that the user did not press.
    ///
    ///  Nothing here runs anything itself. Every run goes through the deck's own
    ///  <see cref="CommandItem"/>, so a chained or watched run has the same output, the same
    ///  history and the same Stop button as one started by hand.
    /// </remarks>
    internal sealed partial class AutomationViewModel : ObservableObject, IDisposable
    {
        private readonly AppSettings settings;
        private readonly CommandsViewModel deck;

        public AutomationViewModel(AppSettings settings, CommandsViewModel deck)
        {
            this.settings = settings;
            this.deck = deck;

            Chains = new ObservableCollection<ChainItem>(settings.Chains.Select(Wrap));
            Watches = new ObservableCollection<WatchItem>(settings.Watches.Select(Wrap));

            selectedChain = Chains.FirstOrDefault();
            selectedWatch = Watches.FirstOrDefault();

            // The deck will not delete a command a chain or a watch runs, so it has to hear about
            // every change to which commands those name - a chain added or removed here, and a
            // step or a watch's command edited inside one (see Wrap).
            Chains.CollectionChanged += (_, _) => deck.UsageChanged();
            Watches.CollectionChanged += (_, _) => deck.UsageChanged();

            Check();
        }

        public ObservableCollection<ChainItem> Chains { get; }

        /// <summary>Whether each section of the page is folded open. Remembered in settings.</summary>
        public bool ChainsOpen
        {
            get => IsOpen("chains");
            set => SetOpen("chains", value);
        }

        public bool WatchesOpen
        {
            get => IsOpen("watches");
            set => SetOpen("watches", value);
        }

        public bool DeckOpen
        {
            get => IsOpen("deck");
            set => SetOpen("deck", value);
        }

        /// <summary>The smallest and largest the chains section can be dragged to, and where it starts.</summary>
        /// <remarks>
        ///  The floor keeps the chain list and the top of the editor on screen together; below it
        ///  the section is better folded than squeezed. The ceiling stops a stray drag from making a
        ///  section taller than any monitor, which would bring back the problem this solves.
        /// </remarks>
        public const double ChainsMinHeight = 220, ChainsMaxHeight = 1600, ChainsDefaultHeight = 460;

        /// <summary>How tall the chains section is. Remembered in settings.</summary>
        /// <remarks>
        ///  Saved by the view when a drag ends rather than on every step of it: a drag is dozens of
        ///  changes a second, and each one would otherwise be a write of the whole settings file.
        /// </remarks>
        public double ChainsHeight
        {
            get => settings.AutomationChainsHeight > 0
                ? Math.Clamp(settings.AutomationChainsHeight, ChainsMinHeight, ChainsMaxHeight)
                : ChainsDefaultHeight;
            set
            {
                double height = Math.Clamp(value, ChainsMinHeight, ChainsMaxHeight);

                if (height == settings.AutomationChainsHeight)
                {
                    return;
                }

                settings.AutomationChainsHeight = height;
                settings.Save();

                OnPropertyChanged();
            }
        }

        private bool IsOpen(string section) =>
            !settings.AutomationClosed.Contains(section, StringComparer.Ordinal);

        private void SetOpen(
            string section,
            bool open,
            [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
        {
            if (IsOpen(section) == open)
            {
                return;
            }

            List<string> closed = [.. settings.AutomationClosed.Where(name => name != section)];

            if (!open)
            {
                closed.Add(section);
            }

            settings.AutomationClosed = closed;
            settings.Save();

            OnPropertyChanged(property);
        }

        [RelayCommand]
        private void ToggleChains() => ChainsOpen = !ChainsOpen;

        [RelayCommand]
        private void ToggleWatches() => WatchesOpen = !WatchesOpen;

        [RelayCommand]
        private void ToggleDeck() => DeckOpen = !DeckOpen;

        public ObservableCollection<WatchItem> Watches { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasChain))]
        private ChainItem? selectedChain;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasWatch))]
        private WatchItem? selectedWatch;

        public bool HasChain => SelectedChain is not null;

        public bool HasWatch => SelectedWatch is not null;

        public bool NoChains => Chains.Count == 0;

        public bool NoWatches => Watches.Count == 0;

        [ObservableProperty]
        private string deckNote = string.Empty;

        /// <summary>Whether this project has a deck file to import from.</summary>
        [ObservableProperty]
        private bool hasProjectDeck;

        public string DeckPath => string.IsNullOrWhiteSpace(deck.Workspace)
            ? Strings.Text("AutoChooseWorkspace")
            : ProjectDeck.PathIn(deck.Workspace);

        /// <summary>Called when the workspace changes, so the deck file and watches follow it.</summary>
        public void Reload()
        {
            OnPropertyChanged(nameof(DeckPath));
            HasProjectDeck = ProjectDeck.Exists(deck.Workspace);

            DeckNote = HasProjectDeck
                ? Strings.Text("AutoDeckPresent")
                : string.Empty;

            foreach (WatchItem watch in Watches)
            {
                // A rule with no folder of its own follows the workspace, so changing projects has
                // to re-point it rather than leave it watching the last one.
                watch.Restart();
                watch.Refresh();
            }
        }

        /// <summary>Starts the watches that are meant to be on, when the panel first appears.</summary>
        public void Start()
        {
            foreach (WatchItem watch in Watches)
            {
                watch.Restart();
            }

            Reload();
        }

        /// <summary>A chain, with the step picker pointed at the deck.</summary>
        private ChainItem Wrap(CommandChain chain)
        {
            ChainItem item = new(chain)
            {
                Choices = () => deck.Commands.Select(command => command.Name),
            };

            // Summary is raised on every change to the steps, which is the change the deck needs.
            item.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName == nameof(ChainItem.Summary))
                {
                    deck.UsageChanged();
                }
            };

            return item;
        }

        private WatchItem Wrap(WatchRule rule)
        {
            WatchItem item = new(rule)
            {
                Fallback = () => deck.Workspace,
                Busy = watch => Find(watch.Command) is { IsRunning: true },
                Choices = () => deck.Commands.Select(command => command.Name),
            };

            item.PropertyChanged += (_, change) =>
            {
                if (change.PropertyName == nameof(WatchItem.Command))
                {
                    deck.UsageChanged();
                }
            };

            item.Fire = (watch, change) => Dispatcher.UIThread.Post(() =>
            {
                // Onto the UI thread first: the watcher raises its callback on a thread pool
                // thread, and everything from here down touches observable collections.
                if (Find(watch.Command) is not { } command)
                {
                    watch.Status = Strings.Format("AutoNoSuchCommand", watch.Command);

                    AppLog.Instance.Warn("watch", $"{watch.Name} fired, but there is no command called \"{watch.Command}\".");

                    return;
                }

                watch.Fired(change);

                // Said before the run starts rather than after it finishes. The question this
                // answers is "why did something just start running", and an answer that arrives
                // when the run ends is too late to be that.
                if (watch.Notify)
                {
                    Changed?.Invoke(
                        Strings.Format("AutoWatchFired", watch.Name),
                        Strings.Format("AutoRunningBecause", WatchItem.Describe(change), watch.Command));
                }

                command.RunFor(RunContext.Watch(watch.Name, watch.Watching, change));
            });

            return item;
        }

        /// <summary>
        ///  How a watch tells the user it fired, supplied by the window.
        /// </summary>
        /// <remarks>
        ///  The same channel a finished run uses, for the same reason: a watch fires because of
        ///  something the user did in another application, so by definition they are not looking
        ///  at this window when it happens.
        /// </remarks>
        public Action<string, string>? Changed { get; set; }

        private CommandItem? Find(string name) => deck.Commands.FirstOrDefault(
            command => command.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Marks the chains whose steps no longer all exist.</summary>
        public void Check()
        {
            List<CustomCommand> commands = [.. deck.Commands.Select(command => command.Source)];

            foreach (ChainItem chain in Chains)
            {
                IReadOnlyList<string> missing = chain.Source.Missing(commands);

                chain.Broken = missing.Count == 0
                    ? string.Empty
                    : Strings.Format("AutoNoSuchCommands", string.Join(", ", missing.Select(step => $"\"{step}\"")));

                // And on the rows themselves, so the warning points at a step rather than only
                // naming one - which is not the same thing when a chain runs it twice.
                chain.Mark(name => commands.Exists(
                    command => command.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
            }
        }

        /// <summary>Follows a command that was renamed, so the things naming it are not broken by it.</summary>
        public void Renamed(string from, string to)
        {
            bool changed = false;

            foreach (ChainItem chain in Chains)
            {
                if (chain.Source.Rename(from, to))
                {
                    chain.Refresh();
                    changed = true;
                }
            }

            foreach (WatchItem watch in Watches.Where(
                watch => watch.Command.Equals(from, StringComparison.OrdinalIgnoreCase)))
            {
                watch.Command = to;
                changed = true;
            }

            if (changed)
            {
                settings.Save();
            }

            Check();
        }

        [RelayCommand]
        private void NewChain()
        {
            CommandChain chain = new()
            {
                Name = Strings.Text("AutoNewChain"),

                // Seeded with what is selected in the deck, since a chain is nearly always started
                // from a command the user was already looking at.
                Steps = deck.Selected is { } current ? [current.Name] : [],
            };

            Add(chain);
        }

        /// <summary>
        ///  Puts a chain on the page and selects it - from New chain, or from the assistant.
        /// </summary>
        /// <remarks>
        ///  The name is made unique here rather than by the caller, so a chain the assistant asks
        ///  for twice arrives as "Report 2" instead of a second "Report" that Run cannot tell apart
        ///  from the first. The section is opened too: a chain added to a folded section looks, from
        ///  the assistant's side, like nothing happened.
        /// </remarks>
        public ChainItem Add(CommandChain chain)
        {
            chain.Name = Unique(chain.Name);

            settings.Chains.Add(chain);

            ChainItem item = Wrap(chain);

            Chains.Add(item);
            SelectedChain = item;
            ChainsOpen = true;

            OnPropertyChanged(nameof(NoChains));
            settings.Save();
            Check();

            return item;
        }

        [RelayCommand]
        private void DeleteChain()
        {
            if (SelectedChain is not { } doomed)
            {
                return;
            }

            settings.Chains.Remove(doomed.Source);
            Chains.Remove(doomed);
            SelectedChain = Chains.FirstOrDefault();

            OnPropertyChanged(nameof(NoChains));
            settings.Save();
        }

        /// <summary>
        ///  How many lines of a step's output are offered to the next step.
        /// </summary>
        /// <remarks>
        ///  Generous, because the clipping that matters happens by character count further down -
        ///  this only stops a fifty-thousand-line build log being joined into one string on its
        ///  way to being thrown away.
        /// </remarks>
        private const int StepOutputLines = 2000;

        /// <summary>
        ///  Runs a chain, one step at a time - and any number of chains at once.
        /// </summary>
        /// <remarks>
        ///  Sequential within a chain by construction rather than by convention: each step is
        ///  awaited through the deck's own run, so the next one starts when the last has genuinely
        ///  exited. Testing what has not finished building is the bug this exists to prevent.
        ///
        ///  Chains run alongside each other because each carries its own cancellation and its own
        ///  log. What two chains can share is a command, and a deck command runs once at a time, so
        ///  a chain that reaches a step already busy elsewhere waits for it rather than restarting
        ///  it underneath the other run.
        ///
        ///  Everything a step prints is copied into <see cref="ChainItem.Output"/> as it arrives,
        ///  so the chain is watched where it was started, on this page. Nothing switches pages.
        ///
        ///  Each step is also handed the one before it, through the environment - see
        ///  <see cref="RunContext.Chain"/> for what a step can read and why it is passed that way.
        /// </remarks>
        /// <summary>
        ///  The marks on a step's heading in the chain log.
        /// </summary>
        /// <remarks>
        ///  Characters rather than icons, so they go wherever the log goes: the page, Select text,
        ///  the copy in the assistant's transcript, a paste into a ticket. The heading's colour comes
        ///  from its level as well, so the mark is never the only thing saying how a step went.
        /// </remarks>
        private const string Running = "▶", Passed = "✓", Failed = "✗", Halted = "■", Skipped = "⚠", Off = "○";

        /// <summary>A step's heading once it has been stopped, by the user or by its chain.</summary>
        private static OutputLine Halt(OutputLine heading, string title) => heading with
        {
            Text = $"{Halted}  {title} · {Strings.Text("AutoStepStopped")}",
            Level = LogLevel.Warning,
        };

        [RelayCommand(AllowConcurrentExecutions = true)]
        private async Task RunChain(ChainItem? which)
        {
            if ((which ?? SelectedChain) is not { } chain || chain.IsRunning)
            {
                return;
            }

            List<CustomCommand> all = [.. deck.Commands.Select(command => command.Source)];
            IReadOnlyList<string> missing = chain.Source.Missing(all, onlyOn: true);

            if (missing.Count > 0 && chain.StopOnFailure)
            {
                // Refused rather than partly run: a chain missing its build step would otherwise
                // deploy whatever was last built, which is the worst possible outcome.
                chain.Status = Strings.Format("AutoNotRunNoCommand", missing[0]);

                return;
            }

            using CancellationTokenSource cancellation = new();
            chain.Cancellation = cancellation;

            CancellationToken token = cancellation.Token;

            chain.IsRunning = true;
            chain.ClearOutputCommand.Execute(null);

            AppLog.Instance.Info("chain", $"{chain.Name} started - {chain.Source.Steps.Count} steps.");

            int at = 0;
            int total = chain.Source.Steps.Count;

            // What the last step that actually ran left behind. A skipped step - one naming a
            // command the deck no longer has, in a chain that carries on regardless - deliberately
            // does not clear these: the next step should be fed by the last step that produced
            // something, not by a gap in the list.
            string? previousName = null;
            string previousOutput = string.Empty;
            int previousExit = 0;

            // Every step's output, for a step that needs more than the one before it. Gone when
            // the run ends, however it ends.
            using ChainOutputs? outputs = ChainOutputs.Create();

            try
            {
                foreach (string step in chain.Source.Steps)
                {
                    at++;

                    if (token.IsCancellationRequested)
                    {
                        chain.Status = Strings.Format("AutoStoppedAfter", at - 1, total);

                        return;
                    }

                    // Written now as running, and settled to a tick or a cross when the step ends.
                    string title = Strings.Format("AutoOutputStep", at, total, step);
                    OutputLine heading = new(DateTime.Now, $"{Running}  {title}", LogLevel.Info, IsHeading: true);

                    chain.Write([heading]);

                    // Switched off: said in the log so the numbering still adds up, and nothing else
                    // touched - the next step is fed by the last one that ran, as with a gap.
                    if (!chain.Source.IsOn(at - 1))
                    {
                        chain.Settle(heading, heading with
                        {
                            Text = $"{Off}  {title} · {Strings.Text("AutoStepOff")}",
                            Level = LogLevel.Info,
                            IsHeading = false,
                        });

                        continue;
                    }

                    if (Find(step) is not { } command)
                    {
                        chain.Settle(heading, heading with { Text = $"{Skipped}  {title}", Level = LogLevel.Warning });
                        chain.Write(Strings.Format("AutoOutputSkipped", step), LogLevel.Warning);

                        if (chain.StopOnFailure)
                        {
                            chain.Status = Strings.Format("AutoStoppedAtStepMissing", at, step);

                            return;
                        }

                        continue;
                    }

                    // Another chain, a watch or a click may have this command running already.
                    // Starting it again would clear that run's output out from under it.
                    if (command.IsRunning)
                    {
                        chain.Status = Strings.Format("AutoWaitingFor", at, total, step);

                        try
                        {
                            while (command.IsRunning)
                            {
                                await Task.Delay(250, token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            chain.Settle(heading, Halt(heading, title));
                            chain.Status = Strings.Format("AutoStoppedAfter", at - 1, total);

                            return;
                        }
                    }

                    chain.Status = Strings.Format("AutoStepOf", at, total, step);
                    chain.Current = command;

                    NotifyCollectionChangedEventHandler copy = (_, change) =>
                    {
                        // Additions only: a Reset is the command clearing its last run as it
                        // starts, and a removal is it trimming its own log - neither is output.
                        if (change.Action == NotifyCollectionChangedAction.Add && change.NewItems is { } lines)
                        {
                            chain.Write(lines.OfType<OutputLine>());
                        }
                    };

                    command.Output.CollectionChanged += copy;

                    try
                    {
                        await command.RunForAsync(RunContext.Chain(
                            chain.Name,
                            at,
                            total,
                            previousName,
                            previousOutput,
                            previousExit,
                            outputs?.Folder));

                        // The command hands its output over in batches at background priority,
                        // so the last one can still be queued when the run returns. Waiting
                        // behind it at the same priority lets it land first.
                        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                    }
                    finally
                    {
                        command.Output.CollectionChanged -= copy;
                        chain.Current = null;
                    }

                    chain.Settle(heading, command.State switch
                    {
                        RunState.Succeeded => heading with { Text = $"{Passed}  {title}", Level = LogLevel.Success },
                        RunState.Stopped => Halt(heading, title),
                        _ => heading with
                        {
                            Text = $"{Failed}  {title} · {Strings.Format("AutoStepExit", command.LastExitCode)}",
                            Level = LogLevel.Error,
                        },
                    });

                    // Read before the failure checks below, so that a step written to explain a
                    // failure still receives what failed.
                    previousName = command.Name;
                    previousOutput = command.Tail(StepOutputLines);
                    previousExit = command.LastExitCode;

                    outputs?.Write(at, previousOutput);

                    if (command.State == RunState.Failed && chain.StopOnFailure)
                    {
                        chain.Status = Strings.Format("AutoStoppedAtStepFailed", at, total, step);

                        AppLog.Instance.Bad("chain", $"{chain.Name} stopped at step {at} of {total}: {step} failed.");

                        return;
                    }

                    if (command.State == RunState.Stopped)
                    {
                        // Stopping a step is the user stopping the chain. Carrying on would
                        // ignore the only instruction they gave it.
                        chain.Status = Strings.Format("AutoStoppedAtStep", at, total);

                        return;
                    }
                }

                chain.Status = total == 0
                    ? Strings.Text("AutoNothingToRun")
                    : Strings.Format("AutoFinishedAll", total);

                if (total > 0)
                {
                    AppLog.Instance.Good("chain", $"{chain.Name} finished all {total} steps.");
                }
            }
            finally
            {
                chain.Cancellation = null;
                chain.IsRunning = false;
            }
        }

        /// <summary>Stops one chain - the one given, or the selected one - and only its step.</summary>
        [RelayCommand]
        private void StopChain(ChainItem? which)
        {
            if ((which ?? SelectedChain) is not { } chain)
            {
                return;
            }

            chain.Cancellation?.Cancel();

            // The step running now is the deck's, and stopping the chain without it would leave a
            // build going that nothing is waiting for. Only this chain's step, though: whatever
            // else is running may belong to another chain that is still meant to be going.
            if (chain.Current is { IsRunning: true } step)
            {
                step.StopCommand.Execute(null);
            }
        }

        [RelayCommand]
        private void NewWatch()
        {
            WatchRule rule = new()
            {
                Name = Strings.Text("AutoWhenFilesChange"),
                Command = deck.Selected?.Name ?? string.Empty,

                // Off, deliberately. A rule created with a half-filled command that immediately
                // started running things on every save would be a nasty surprise.
                Enabled = false,
            };

            settings.Watches.Add(rule);

            WatchItem item = Wrap(rule);

            Watches.Add(item);
            SelectedWatch = item;

            OnPropertyChanged(nameof(NoWatches));
            settings.Save();
        }

        [RelayCommand]
        private void DeleteWatch()
        {
            if (SelectedWatch is not { } doomed)
            {
                return;
            }

            doomed.Dispose();

            settings.Watches.Remove(doomed.Source);
            Watches.Remove(doomed);
            SelectedWatch = Watches.FirstOrDefault();

            OnPropertyChanged(nameof(NoWatches));
            settings.Save();
        }

        [RelayCommand]
        private void Save()
        {
            settings.Save();
            Check();
        }

        /// <summary>Brings in the commands this project commits.</summary>
        [RelayCommand]
        private void ImportDeck()
        {
            if (ProjectDeck.Read(deck.Workspace) is not { } file)
            {
                DeckNote = Strings.Text("AutoNothingToImport");

                return;
            }

            int added = 0;
            int skipped = 0;

            foreach (DeckEntry entry in file.Commands)
            {
                if (deck.Commands.Any(existing =>
                    existing.Name.Equals(entry.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;

                    continue;
                }

                deck.Add(ProjectDeck.ToCommand(entry));
                added++;
            }

            DeckNote = (added, skipped) switch
            {
                (0, 0) => Strings.Text("AutoDeckEmpty"),
                (0, _) => Strings.Text("AutoDeckAllPresent"),
                (1, 0) => Strings.Text("AutoImportedOne"),
                (_, 0) => Strings.Format("AutoImportedMany", added),
                _ => Strings.Format("AutoImportedSome", added, skipped),
            };

            Check();
        }

        /// <summary>Writes the deck out for the project to commit.</summary>
        [RelayCommand]
        private void ExportDeck()
        {
            if (string.IsNullOrWhiteSpace(deck.Workspace) || !Directory.Exists(deck.Workspace))
            {
                DeckNote = Strings.Text("AutoChooseWorkspace");

                return;
            }

            try
            {
                string path = ProjectDeck.Write(
                    deck.Workspace,
                    new DirectoryInfo(deck.Workspace).Name,
                    deck.Commands.Select(command => command.Source));

                HasProjectDeck = true;
                DeckNote = Strings.Format("AutoWroteDeck", deck.Commands.Count, Path.GetFileName(path));
            }
            catch (Exception exception)
            {
                DeckNote = Strings.Format("AutoCouldNotWrite", exception.Message);
            }
        }

        private string Unique(string stem)
        {
            if (Chains.All(chain => chain.Name != stem))
            {
                return stem;
            }

            int at = 2;

            while (Chains.Any(chain => chain.Name == $"{stem} {at}"))
            {
                at++;
            }

            return $"{stem} {at}";
        }

        public void Dispose()
        {
            foreach (ChainItem chain in Chains)
            {
                chain.Cancellation?.Cancel();
            }

            foreach (WatchItem watch in Watches)
            {
                watch.Dispose();
            }
        }
    }
}
