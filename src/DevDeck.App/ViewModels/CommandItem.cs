using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>How a command's last run ended, which is the one thing the panel colours.</summary>
    internal enum RunState
    {
        Idle,
        Running,
        Succeeded,
        Failed,
        Stopped,
    }

    /// <summary>One line of process output, kept with the level the runner gave it.</summary>
    /// <remarks>
    ///  The three flags are here rather than in a converter so the item template can switch style
    ///  classes directly, which is the cheaper path when thousands of these go past.
    ///
    ///  The stamp is taken when the line arrives rather than when it is drawn: output is batched
    ///  before it reaches the UI, so drawing time would cluster a whole burst onto one instant.
    /// </remarks>
    internal sealed record OutputLine(
        DateTime At,
        string Text,
        LogLevel Level,
        IReadOnlyList<AnsiSpan>? Spans = null,
        IReadOnlyList<SourceLink>? Links = null,
        bool IsHeading = false,
        bool IsLaunch = false)
    {
        // IsLaunch marks the line a run starts with - the interpreter and the script it was handed.
        // It belongs on the command's own page, as a record of what was started, and nowhere that
        // reads the command's output as its answer: a chain's log, the next step, the assistant.

        public bool IsError => Level == LogLevel.Error;

        public bool IsWarning => Level == LogLevel.Warning;

        public bool IsNotice => Level == LogLevel.Success;

        /// <summary>The same "HH:mm:ss" V2's log used, so output reads the same in both.</summary>
        public string Stamp => At.ToString("HH:mm:ss");

        /// <summary>
        ///  True when the line carries colour the panel should honour rather than painting the
        ///  whole row by level.
        /// </summary>
        /// <remarks>
        ///  Most lines are one plain span, and for those the cheap single-TextBlock path is right.
        ///  The template only pays for an inline run per span on the lines that actually asked for
        ///  colour, which is a small fraction of any real build log.
        /// </remarks>
        public bool IsColoured => Spans is { Count: > 0 }
            && (Spans.Count > 1 || Spans[0].Foreground != AnsiColor.None || Spans[0].Bold);

        /// <summary>The file references in this line, for the "open in editor" affordance.</summary>
        public bool HasLinks => Links is { Count: > 0 };

        /// <summary>The first reference, which is the one a click on the row should follow.</summary>
        public SourceLink? Link => Links is { Count: > 0 } ? Links[0] : null;
    }

    /// <summary>One row of a command's parameter list, wrapping the saved argument.</summary>
    internal sealed class ArgumentRow : ObservableObject
    {
        public ArgumentRow(CommandArgument source) => Source = source;

        public CommandArgument Source { get; }

        public string Name
        {
            get => Source.Name;
            set
            {
                Source.Name = value;
                OnPropertyChanged();
            }
        }

        public string Value
        {
            get => Source.Value;
            set
            {
                Source.Value = value;
                OnPropertyChanged();
            }
        }

        public bool Enabled
        {
            get => Source.Enabled;
            set
            {
                Source.Enabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    ///  A saved command, plus everything about the run it is currently having.
    /// </summary>
    /// <remarks>
    ///  Two things put the run state here rather than on the panel.
    ///
    ///  One, a command has to keep running when you select a different one, and come back with its
    ///  output intact - the panel shows whichever command is selected, it does not own the run.
    ///  Several commands run at once, as V2's run tabs did.
    ///
    ///  Two, CustomCommand is a plain settings record with no change notification. Editing the name
    ///  through this wrapper raises PropertyChanged, so the list beside the editor redraws itself.
    ///  The panel used to replace the item in the collection to force that redraw, which rebuilt
    ///  the row and stole focus out of the name box on every keystroke.
    /// </remarks>
    internal sealed partial class CommandItem : ObservableObject
    {
        private readonly CustomCommand command;

        private CancellationTokenSource? cancellation;

        /// <summary>Anything past this and the panel is scrolling faster than it can be read.</summary>
        private const int MaxOutputLines = 5000;

        /// <summary>
        ///  How many lines may carry an open-in-editor link before the scan gives up.
        /// </summary>
        /// <remarks>
        ///  The second half of why a chatty command froze the window, and the half a cache cannot
        ///  fix. Finding links means running three regular expressions over every line and then
        ///  confirming each candidate against the disk, and a `git grep` for TODOs across a large
        ///  checkout is tens of thousands of lines <i>all of which match</i> - every one of them
        ///  names a real file at a real line, so nothing is filtered out and the full cost is paid
        ///  throughout.
        ///
        ///  It is also work with no reader. The panel keeps the last few thousand lines, so a link
        ///  on line forty thousand has already been discarded by the time the run ends, and nobody
        ///  clicks the four-hundredth link in a list in any case. A thousand is past the point of
        ///  usefulness and well inside the point of pain.
        ///
        ///  Counted in lines that produced a link rather than lines scanned, so ordinary build
        ///  output - thousands of lines, a handful of errors - never reaches it at all. It is the
        ///  grep-shaped case this is aimed at.
        /// </remarks>
        private const int MostLinkedLines = 1000;

        /// <summary>Lines of this run that produced a link. Touched from the runner's thread.</summary>
        private int linked;

        private readonly Func<string> workingDirectory;

        /// <summary>Lines waiting to be handed to the UI thread, and the lock over them.</summary>
        private readonly object gate = new();

        private readonly List<OutputLine> pending = [];

        private bool flushQueued;

        /// <summary>
        ///  Colour carried between lines, because a tool may open a colour on one line and close
        ///  it several lines later.
        /// </summary>
        /// <remarks>
        ///  Only ever touched from the runner's reader threads, which the process runner serialises
        ///  onto one at a time, and reset at the start of each run - so it needs no lock of its own.
        /// </remarks>
        private Ansi.AnsiState ansi = Ansi.AnsiState.Clear;

        /// <summary>
        ///  Asks the user for the command's parameters, or returns nothing if they cancel.
        /// </summary>
        /// <remarks>
        ///  A delegate rather than a dialog call, because this class knows nothing about windows
        ///  and must stay testable. The panel supplies it; when nothing does - a unit test, or a
        ///  run triggered before the view exists - a parameterised command falls back to its
        ///  defaults rather than refusing to run.
        /// </remarks>
        public Func<CommandItem, IReadOnlyList<CommandParameter>, Task<IReadOnlyDictionary<string, string>?>>? Prompt { get; set; }

        /// <summary>
        ///  Parameter values handed over by a link or a command line, used once.
        /// </summary>
        /// <remarks>
        ///  Cleared by the run that consumes them, because these belong to the request that arrived
        ///  rather than to the command: a link supplying a branch must not leave that branch silently
        ///  filled in for every later run started by hand.
        ///
        ///  Only the parameters a request actually named are taken; anything else is still asked for
        ///  in the usual way, so a half-specified link prompts for the rest rather than quietly
        ///  running with a default nobody chose.
        /// </remarks>
        public IReadOnlyDictionary<string, string>? Supplied { get; set; }

        /// <summary>
        ///  Told when this command is renamed, so that whatever refers to it by name can follow.
        /// </summary>
        /// <remarks>
        ///  A callback rather than a reference to the settings: the item knows how to run one
        ///  command and nothing about chains, watches or storage, and it should stay that way.
        /// </remarks>
        public Action<string, string>? Renamed { get; set; }

        /// <summary>
        ///  Told when a run finishes, with how long it took.
        /// </summary>
        /// <remarks>
        ///  How a chain knows to start its next step and how a long build announces itself. Not
        ///  raised for a detached run, which has only been started.
        /// </remarks>
        public Action<CommandItem, TimeSpan>? Finished { get; set; }

        public CommandItem(CustomCommand command, Func<string> workingDirectory)
        {
            this.command = command;
            this.workingDirectory = workingDirectory;
        }

        /// <summary>The settings object this wraps, for saving.</summary>
        public CustomCommand Source => command;

        /// <summary>
        ///  Read per run, not captured, so changing the workspace applies to the next run of every
        ///  command rather than only to ones created afterwards.
        /// </summary>
        public string WorkingDirectory => workingDirectory();

        public ObservableCollection<OutputLine> Output { get; } = new();

        public string Name
        {
            get => command.Name;
            set
            {
                if (command.Name == value)
                {
                    return;
                }

                // Before the field moves, so the trend recorded under the old name follows the
                // command through a rename rather than being orphaned by it.
                RunHistory.Instance.Rename(command.Name, value);

                // Same argument, for the things that refer to this command by name: a chain step
                // or a watch rule left pointing at the old name is broken by a rename nobody would
                // think of as a destructive edit.
                Renamed?.Invoke(command.Name, value);

                command.Name = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Dates));
                OnPropertyChanged(nameof(Typical));
                OnPropertyChanged(nameof(HasHistory));
            }
        }

        /// <summary>
        ///  The environment variables from Settings, set on every command. Set once by the deck,
        ///  and read per run so an edit on the Settings page applies to the next run.
        /// </summary>
        public static Func<IEnumerable<CommandArgument>>? SharedVariables { get; set; }

        /// <summary>The parameters passed on every run, as editable rows.</summary>
        /// <remarks>
        ///  Rows wrap the saved objects rather than copying them, so editing a row edits what is
        ///  saved; only adding and removing has to touch the saved list too.
        /// </remarks>
        public ObservableCollection<ArgumentRow> Arguments => arguments ??= new(command.Arguments.Select(argument => new ArgumentRow(argument)));

        private ObservableCollection<ArgumentRow>? arguments;

        public bool HasArguments => command.Arguments.Count > 0;

        [RelayCommand]
        private void AddArgument()
        {
            CommandArgument added = new();

            command.Arguments.Add(added);
            Arguments.Add(new ArgumentRow(added));

            OnPropertyChanged(nameof(HasArguments));
        }

        [RelayCommand]
        private void RemoveArgument(ArgumentRow? row)
        {
            if (row is null)
            {
                return;
            }

            command.Arguments.Remove(row.Source);
            Arguments.Remove(row);

            OnPropertyChanged(nameof(HasArguments));
        }

        public string Body
        {
            get => command.Command;
            set
            {
                if (command.Command == value)
                {
                    return;
                }

                command.Command = value;
                OnPropertyChanged();
            }
        }

        public CommandKind Kind
        {
            get => command.Kind;
            set
            {
                if (command.Kind == value)
                {
                    return;
                }

                command.Kind = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Dates));
            }
        }

        /// <summary>
        ///  Start it and forget it: no output captured, no exit code, no waiting.
        /// </summary>
        /// <remarks>
        ///  For commands that open something rather than finish something - an editor, a dev
        ///  server, an interactive session. Watching one of those makes the app look hung: it holds
        ///  the output pipes and has nothing to report until they close, and a session the user is
        ///  meant to type into never closes them.
        /// </remarks>
        public bool Detached
        {
            get => command.Detached;
            set
            {
                if (command.Detached == value)
                {
                    return;
                }

                command.Detached = value;
                OnPropertyChanged();
            }
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        [NotifyCanExecuteChangedFor(nameof(StopCommand))]
        private bool isRunning;

        [ObservableProperty]
        private RunState state = RunState.Idle;

        [ObservableProperty]
        private string status = Strings.Text("CmdNotRunYet");

        public bool IsBusy => State == RunState.Running;

        public bool IsOk => State == RunState.Succeeded;

        public bool IsBad => State is RunState.Failed or RunState.Stopped;

        public bool HasOutput => Output.Count > 0;

        /// <summary>True when this command takes values before it runs.</summary>
        public bool HasParameters => CommandParameters.Any(Body);

        /// <summary>Past runs of this command, newest first.</summary>
        public IReadOnlyList<RunRecord> History => RunHistory.Instance.For(Name);

        /// <summary>The small line under the name in the deck: its kind, when it was made and last ran.</summary>
        public string Dates => $"{Kind} · " + CommandDates.Describe(
            command.Created,
            History.FirstOrDefault()?.At,
            DateTime.Now,
            System.Globalization.CultureInfo.GetCultureInfo(Strings.Language.Id));

        /// <summary>Asks the row to read its date line again, after a run or a change of language.</summary>
        public void Relabel() => OnPropertyChanged(nameof(Dates));

        /// <summary>
        ///  Whether a chain or a watch runs this command, which takes its row out of reach in the deck.
        /// </summary>
        /// <remarks>
        ///  Set by the deck, which is the one that can see the chains and watches. Only the row is
        ///  affected: the command still runs, from its chain, its watch, a link or the palette.
        /// </remarks>
        [ObservableProperty]
        private bool isInUse;

        /// <summary>Why the row is out of reach, for its tooltip. Null when it is not.</summary>
        [ObservableProperty]
        private string? inUseTip;

        public bool HasHistory => History.Count > 0;

        /// <summary>
        ///  "usually 4.2 s", or nothing until there is enough history to mean anything.
        /// </summary>
        public string? Typical => RunHistory.Instance.Typical(Name) is { } median
            ? Strings.Format("CmdUsually", Describe(median))
            : null;

        /// <summary>
        ///  Set when the run just finished was notably slower or faster than this command's norm.
        /// </summary>
        /// <remarks>
        ///  The interesting question after a build is not how long it took but whether that is
        ///  normal, and a number on its own cannot answer it. This is the only place the app
        ///  volunteers that comparison, and it stays quiet inside the band where the answer is
        ///  "about the same as always".
        /// </remarks>
        [ObservableProperty]
        private string? trend;

        /// <summary>
        ///  What the last run exited with, kept so a failure can be described after the fact.
        /// </summary>
        /// <remarks>
        ///  Status holds this as prose, which reads well and cannot be parsed back. A process that
        ///  never started has no exit code of its own, so it reports 1 - the shell's own convention
        ///  for "this did not work".
        /// </remarks>
        public int LastExitCode { get; private set; }

        partial void OnStateChanged(RunState value)
        {
            OnPropertyChanged(nameof(IsBusy));
            OnPropertyChanged(nameof(IsOk));
            OnPropertyChanged(nameof(IsBad));
        }

        /// <summary>
        ///  The variables this run was started with, cleared when it ends.
        /// </summary>
        /// <remarks>
        ///  A field rather than a parameter because the command that the button invokes takes none
        ///  and cannot be given one: IRelayCommand is what the view binds to.
        /// </remarks>
        private IReadOnlyDictionary<string, string>? context;

        /// <summary>
        ///  Runs this command, telling it why it is running.
        /// </summary>
        /// <remarks>
        ///  The way a watch or a chain starts a command. Everything else about the run is identical
        ///  to pressing the button - same output, same history, same Stop - which is the property
        ///  worth protecting: a run that behaves differently because nobody clicked it is a run
        ///  nobody can debug.
        /// </remarks>
        public void RunFor(IReadOnlyDictionary<string, string> environment) =>
            _ = RunForAsync(environment);

        /// <summary>
        ///  The same run, awaitable.
        /// </summary>
        /// <remarks>
        ///  For a chain, which cannot start its next step until this one has genuinely exited -
        ///  the whole reason chains exist is that these commands must not overlap. A watch wants
        ///  the other shape: it fires and forgets, because the file that changed is not waiting
        ///  for an answer.
        /// </remarks>
        public Task RunForAsync(IReadOnlyDictionary<string, string> environment)
        {
            context = environment;

            return RunCommand.ExecuteAsync(null);
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private async Task Run()
        {
            // Asked before anything is cleared or marked running, so cancelling the prompt leaves
            // the previous run's output and status exactly where they were.
            string body = Body;

            if (CommandParameters.Find(body) is { Count: > 0 } parameters)
            {
                if (await Values(parameters) is not { } values)
                {
                    return;
                }

                body = CommandParameters.Fill(body, values, SecretVault.Instance.Value);
            }

            // Taken and cleared here, so a run started by the button after one started by a watch
            // does not inherit the watch's variables. Read once rather than per use, because Run is
            // async and the field could be rewritten underneath it.
            IReadOnlyDictionary<string, string> environment = context ?? RunContext.Manual();
            context = null;

            Output.Clear();
            OnPropertyChanged(nameof(HasOutput));

            // A build has just written the files its errors are about to name, so what was on disk
            // before this run started is the one thing the link cache can be confidently wrong
            // about.
            SourceLinks.Forget();

            linked = 0;

            IsRunning = true;
            State = RunState.Running;
            Status = "Running";
            Trend = null;
            ansi = Ansi.AnsiState.Clear;

            cancellation = new CancellationTokenSource();
            Stopwatch stopwatch = Stopwatch.StartNew();

            // Not a using: a detached run outlives this method, and disposing the ScriptFile
            // deletes the .bat or .ps1 the child process has not finished reading yet. The watched
            // path disposes it below, once the process it backs has exited.
            ScriptFile? script = null;

            try
            {
                // A copy when values were substituted, so the filled-in text - which may contain a
                // secret - is never written back into the saved command.
                script = ScriptFile.Create(ReferenceEquals(body, Body)
                    ? command
                    : new CustomCommand
                    {
                        Name = command.Name,
                        Command = body,
                        Kind = command.Kind,
                        Detached = command.Detached,
                        Arguments = command.Arguments,
                    });

                // Settings' environment variables first, then this command's parameters, then what
                // the run itself carries (DEVDECK_TRIGGER and the rest), each able to override the
                // one before. The parameters go in as DEVDECK_ARG_NAME as well as on the command
                // line, for the scripts that would rather read those than parse their arguments.
                Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);

                foreach (CommandArgument shared in SharedVariables?.Invoke() ?? [])
                {
                    if (shared.Enabled && shared.Name.Trim().Length > 0)
                    {
                        merged[shared.Name.Trim()] = shared.Value;
                    }
                }

                foreach (KeyValuePair<string, string> variable in ScriptFile.EnvironmentFor(command))
                {
                    merged[variable.Key] = variable.Value;
                }

                if (merged.Count > 0)
                {
                    foreach (KeyValuePair<string, string> variable in environment)
                    {
                        merged[variable.Key] = variable.Value;
                    }

                    environment = merged;
                }

                Append($"{script.FileName} {script.Arguments}", LogLevel.Info, launch: true);


                if (Detached)
                {
                    Launch(script, environment);
                    script = null;
                    State = RunState.Succeeded;
                    Status = Strings.Text("CmdStartedDetached");
                    Append(Strings.Text("CmdStartedDetachedTail"), LogLevel.Success);
                    return;
                }

                int exitCode = await ProcessRunner.RunAsync(
                    script.FileName,
                    script.Arguments,
                    WorkingDirectory,
                    Append,
                    cancellationToken: cancellation.Token,
                    environment: environment);

                LastExitCode = exitCode;
                State = exitCode == 0 ? RunState.Succeeded : RunState.Failed;
                Status = Strings.Format("CmdExitAfter", exitCode, Describe(stopwatch.Elapsed));
            }
            catch (OperationCanceledException)
            {
                State = RunState.Stopped;
                Status = Strings.Format("CmdStoppedAfter", Describe(stopwatch.Elapsed));
            }
            catch (PlatformNotSupportedException exception)
            {
                // The one failure the user can act on, so it reads as guidance rather than a fault.
                LastExitCode = 1;
                State = RunState.Failed;
                Status = exception.Message;
                Append(exception.Message, LogLevel.Error);
            }
            catch (Exception exception)
            {
                LastExitCode = 1;
                State = RunState.Failed;
                Status = Strings.Format("CmdCouldNotStart", exception.Message);
                Append(exception.Message, LogLevel.Error);
            }
            finally
            {
                // Null for a detached run, which deliberately leaves its script in the temp folder.
                script?.Dispose();

                stopwatch.Stop();

                // A detached run reports no exit code and no duration worth keeping - it has only
                // been started - so there is nothing to record and nothing to compare against.
                if (!Detached)
                {
                    Remember(stopwatch.Elapsed);
                }

                IsRunning = false;
                cancellation?.Dispose();
                cancellation = null;

                // Last, so that anything waiting on this run - the next step of a chain, the
                // notification that it is done - sees a command that has actually finished rather
                // than one still marked as running.
                if (!Detached)
                {
                    Finished?.Invoke(this, stopwatch.Elapsed);
                }
            }
        }

        [RelayCommand(CanExecute = nameof(IsRunning))]
        private void Stop() => cancellation?.Cancel();

        private bool CanRun() => !IsRunning;

        /// <summary>
        ///  Collects the values for a parameterised command, from the panel if it can and from the
        ///  defaults if it cannot.
        /// </summary>
        /// <remarks>
        ///  Returning null means the user cancelled, which must not start anything. An absent
        ///  prompt is the different case of nobody having been asked, and there the defaults are
        ///  the right answer - refusing to run would make a headless or scripted run impossible.
        /// </remarks>
        private async Task<IReadOnlyDictionary<string, string>?> Values(IReadOnlyList<CommandParameter> parameters)
        {
            IReadOnlyDictionary<string, string>? supplied = Supplied;

            Supplied = null;

            // The parameter rows first, then what a link named: see CommandParameters.Given.
            Dictionary<string, string> given = CommandParameters.Given(command.Arguments, supplied);

            if (CommandParameters.Answered(parameters, given, SecretVault.Instance.Value))
            {
                // Every one of them has a value already, so there is nothing left to ask about, and
                // asking anyway would turn a filled-in command or a one-click link into a dialog.
                return parameters
                    .Where(parameter => !parameter.IsSecret)
                    .ToDictionary(parameter => parameter.Name, parameter => given[parameter.Name], StringComparer.OrdinalIgnoreCase);
            }

            if (Prompt is null)
            {
                return parameters.ToDictionary(
                    p => p.Name,
                    p => given.TryGetValue(p.Name, out string? value) ? value : p.Default,
                    StringComparer.OrdinalIgnoreCase);
            }

            // A partly-answered run still helps: what is already known becomes the starting value
            // in the box, so the user confirms rather than retypes.
            return await Prompt(this, CommandParameters.Prefill(parameters, given));
        }

        /// <summary>
        ///  Files the finished run, and says whether it was unusual.
        /// </summary>
        /// <remarks>
        ///  Asked for the comparison before the record is added, so the run being judged is not
        ///  part of the average it is being judged against - otherwise every run drags the median
        ///  toward itself and a genuine regression looks smaller than it is.
        /// </remarks>
        private void Remember(TimeSpan elapsed)
        {
            Trend = State == RunState.Succeeded ? RunHistory.Instance.Compare(Name, elapsed) : null;

            RunHistory.Instance.Add(new RunRecord
            {
                Command = Name,
                At = DateTime.Now,
                ExitCode = LastExitCode,
                Milliseconds = (long)elapsed.TotalMilliseconds,
                Stopped = State == RunState.Stopped,
                Workspace = WorkingDirectory,
            });

            OnPropertyChanged(nameof(History));
            OnPropertyChanged(nameof(Dates));
            OnPropertyChanged(nameof(HasHistory));
            OnPropertyChanged(nameof(Typical));
        }

        /// <summary>
        ///  The last run's output as plain text, for handing to the assistant.
        /// </summary>
        /// <remarks>
        ///  Tail rather than all of it: a failing build's useful part is at the end, and the front
        ///  of a five-thousand-line log is restore chatter that would crowd out the error in any
        ///  model's context.
        /// </remarks>
        public string Tail(int lines = 200) =>
            string.Join(Environment.NewLine, Output.Where(line => !line.IsLaunch).TakeLast(lines).Select(line => line.Text));

        /// <summary>
        ///  Starts a detached command in its own window and lets go of it.
        /// </summary>
        /// <remarks>
        ///  UseShellExecute, so the child gets a console of its own rather than inheriting ours and
        ///  writing into pipes nobody is reading. Nothing is disposed or awaited: the point is that
        ///  it outlives the call.
        /// </remarks>
        private void Launch(ScriptFile script, IReadOnlyDictionary<string, string> environment)
        {
            ProcessStartInfo start = new()
            {
                FileName = script.FileName,
                Arguments = script.Arguments,
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = true,
            };

            foreach (KeyValuePair<string, string> variable in environment)
            {
                start.Environment[variable.Key] = variable.Value;
            }

            // UseShellExecute and a populated Environment are mutually exclusive - the shell starts
            // the process with its own block, and .NET throws rather than silently dropping the
            // variables - so it is turned off here. A detached run still gets a console window,
            // because CreateNoWindow stays false and Windows gives a console application one
            // either way; what it loses is the shell's file association handling, which a script
            // started by its interpreter was not relying on.
            if (start.Environment.Count > 0)
            {
                start.UseShellExecute = false;
                start.CreateNoWindow = false;
            }

            Process.Start(start);
        }

        /// <summary>
        ///  Takes a line from the runner's threads onto the UI thread, in batches.
        /// </summary>
        /// <remarks>
        ///  This used to post one dispatcher job per line, and that is what made the window crawl
        ///  whenever a command was chatty. A build writing a few thousand lines queued a few
        ///  thousand jobs, each one mutating a bound collection and each one costing a full layout
        ///  and render pass - so the UI spent all its time re-laying-out text nobody could read at
        ///  that speed anyway.
        ///
        ///  Now a line joins a list and only the first line of a burst queues the flush. Everything
        ///  that arrives before the dispatcher gets round to it rides along in the same batch, so a
        ///  thousand lines cost one layout pass instead of a thousand. At Background priority, so
        ///  input and rendering are served first - output that is a frame late is not a problem;
        ///  a window that will not respond to a click is.
        /// </remarks>
        private void Append(string line, LogLevel level) => Append(line, level, launch: false);

        private void Append(string line, LogLevel level, bool launch)
        {
            // Parsed off the UI thread, where there is time for it: by the time the batch flushes,
            // the panel only has to draw what is already decided.
            IReadOnlyList<AnsiSpan>? spans = null;
            string text = line;

            if (Ansi.Has(line))
            {
                spans = Ansi.Parse(line, ref ansi);
                text = string.Concat(spans.Select(span => span.Text));
            }

            IReadOnlyList<SourceLink> links = [];

            // Past the ceiling the scan is skipped outright. See MostLinkedLines.
            if (linked < MostLinkedLines)
            {
                links = SourceLinks.Find(text, WorkingDirectory);

                if (links.Count > 0)
                {
                    Interlocked.Increment(ref linked);
                }
            }

            lock (gate)
            {
                pending.Add(new OutputLine(
                    DateTime.Now,
                    text,
                    level,
                    spans,
                    links.Count > 0 ? links : null,
                    IsLaunch: launch));

                if (flushQueued)
                {
                    return;
                }

                flushQueued = true;
            }

            Dispatcher.UIThread.Post(Flush, DispatcherPriority.Background);
        }

        /// <summary>Moves one batch of lines into the bound collection. UI thread only.</summary>
        private void Flush()
        {
            List<OutputLine> batch;

            lock (gate)
            {
                batch = [.. pending];
                pending.Clear();
                flushQueued = false;
            }

            if (batch.Count == 0)
            {
                return;
            }

            bool wasEmpty = Output.Count == 0;

            // Trimmed before the batch goes in rather than per line, so a burst larger than the cap
            // does not remove from the front of the collection thousands of times over.
            int over = Output.Count + batch.Count - MaxOutputLines;

            // A single batch can be bigger than the whole cap - a grep across a large tree arrives
            // as one burst of tens of thousands of lines - and in that case every surviving line
            // comes from the batch. Removing them one at a time would shift the list and raise an
            // event per removal, which is the same freeze the batching exists to avoid, so the
            // collection is emptied in one go instead.
            if (over >= Output.Count)
            {
                Output.Clear();
            }
            else
            {
                for (int removed = 0; removed < over && Output.Count > 0; removed++)
                {
                    Output.RemoveAt(0);
                }
            }

            foreach (OutputLine line in batch.TakeLast(MaxOutputLines))
            {
                Output.Add(line);
            }

            if (wasEmpty)
            {
                OnPropertyChanged(nameof(HasOutput));
            }
        }

        private static string Describe(TimeSpan elapsed) => elapsed.TotalSeconds < 1
            ? $"{elapsed.TotalMilliseconds:F0} ms"
            : elapsed.TotalSeconds < 60
                ? $"{elapsed.TotalSeconds:F1} s"
                : $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
    }
}
