using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  The command deck: pick a command, run it, watch it, stop it.
    /// </summary>
    /// <remarks>
    ///  The panel owns no run state. Each <see cref="CommandItem"/> owns its own, so several
    ///  commands run at once and selecting a different one shows that command's output without
    ///  touching what is still running - which is what V2's run tabs did.
    /// </remarks>
    internal sealed partial class CommandsViewModel : ObservableObject
    {
        private readonly AppSettings settings;

        public CommandsViewModel(AppSettings settings)
        {
            this.settings = settings;

            Workspaces = new ObservableCollection<string>(settings.Workspaces);
            workspace = ResolveWorkspace();
            follow = settings.AutoScrollLog;

            CommandItem.SharedVariables = () => settings.CommandVariables;

            Commands = new ObservableCollection<CommandItem>(
                settings.CustomCommands.Select(c => new CommandItem(c, () => Workspace)));

            // Stop all is only enabled while something is running, and what is running lives on the
            // items rather than here - so the panel listens to each one.
            foreach (CommandItem item in Commands)
            {
                Watch(item);
                Attach(item);
            }

            Commands.CollectionChanged += CommandsChanged;

            // The reason Delete is off is a sentence built here, not a view string, so it has to
            // be rebuilt by hand when the language changes.
            Strings.Changed += UsageChanged;

            selected =Commands.FirstOrDefault(c => c.Name == settings.SelectedCommand)
                ?? Commands.FirstOrDefault();
        }

        public ObservableCollection<CommandItem> Commands { get; }

        /// <summary>
        ///  How a parameterised command asks for its values, supplied by the view.
        /// </summary>
        /// <remarks>
        ///  Set on every item rather than consulted by the panel, because it is the item that runs
        ///  and the item that must not start until the answer is in. Assigning here propagates it
        ///  to commands that already exist and to any added later.
        /// </remarks>
        public Func<CommandItem, IReadOnlyList<CommandParameter>, Task<IReadOnlyDictionary<string, string>?>>? Prompt
        {
            get => prompt;
            set
            {
                prompt = value;

                foreach (CommandItem item in Commands)
                {
                    item.Prompt = value;
                }
            }
        }

        private Func<CommandItem, IReadOnlyList<CommandParameter>, Task<IReadOnlyDictionary<string, string>?>>? prompt;

        /// <summary>
        ///  Sends the selected command's failure to the assistant, and shows it.
        /// </summary>
        /// <remarks>
        ///  Set by the window, which is the only thing that can see both panels. The deck must not
        ///  hold a reference to the assistant: the assistant already holds one to the deck, and a
        ///  pair that each own the other is how a panel ends up unable to exist without the other
        ///  being constructed first.
        /// </remarks>
        public Action<CommandItem>? Explain { get; set; }

        /// <summary>
        ///  Offered on a command that has just failed.
        /// </summary>
        /// <remarks>
        ///  Only after a failure, deliberately. A button that is always there invites asking about
        ///  runs that went fine, which costs a round trip to be told nothing is wrong.
        /// </remarks>
        public bool CanExplain => Explain is not null && Selected is { State: RunState.Failed, HasOutput: true };

        [RelayCommand]
        private void ExplainFailure()
        {
            if (Selected is { } failed)
            {
                Explain?.Invoke(failed);
            }
        }

        public ObservableCollection<string> Workspaces { get; }

        /// <summary>Only the kinds that have an interpreter on this machine.</summary>
        public IReadOnlyList<CommandKind> Kinds { get; } = Enum.GetValues<CommandKind>()
            .Where(Supported)
            .ToList();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        private CommandItem? selected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasWorkspace))]
        [NotifyPropertyChangedFor(nameof(NeedsWorkspace))]
        private string workspace;

        /// <summary>
        ///  Whether a real folder has been chosen, which is what opens the rest of the page.
        /// </summary>
        /// <remarks>
        ///  The workspace used to default to the user's home directory, which meant Run always had
        ///  somewhere to go and that somewhere was almost never what was wanted - a build command
        ///  aimed at a repository quietly ran against the home folder and either failed oddly or,
        ///  worse, did something. A command is only meaningful against a project, so the page now
        ///  asks for one before it will offer anything to run.
        /// </remarks>
        public bool HasWorkspace =>
            !string.IsNullOrWhiteSpace(Workspace) && Directory.Exists(Workspace);

        public bool NeedsWorkspace => !HasWorkspace;

        /// <summary>Follows the tail of the output as it arrives. V2's auto-scroll tick box.</summary>
        [ObservableProperty]
        private bool follow;

        public bool HasSelection => Selected is not null;

        public bool HasCommands => Commands.Count > 0;

        partial void OnSelectedChanged(CommandItem? value)
        {
            settings.SelectedCommand = value?.Name ?? string.Empty;
            OnPropertyChanged(nameof(CanExplain));
            UsageChanged();
        }

        partial void OnFollowChanged(bool value)
        {
            settings.AutoScrollLog = value;
            Save();
        }

        /// <summary>Told when the workspace changes, so what depends on the folder can follow.</summary>
        public Action? WorkspaceChanged { get; set; }

        partial void OnWorkspaceChanged(string value)
        {
            settings.SelectedWorkspace = value;

            if (!string.IsNullOrWhiteSpace(value) && !Workspaces.Contains(value))
            {
                Workspaces.Add(value);
                settings.Workspaces.Add(value);
            }

            Save();

            WorkspaceChanged?.Invoke();
        }

        /// <summary>Adds a folder chosen through the picker and makes it current.</summary>
        public void UseWorkspace(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            Workspace = path;
        }

        /// <summary>Adds a command, from the panel or from a code block in the assistant.</summary>
        public CommandItem Add(CustomCommand command)
        {
            CommandItem item = new(command, () => Workspace) { Prompt = prompt };

            Attach(item);

            Commands.Add(item);
            settings.CustomCommands.Add(command);
            Selected = item;

            OnPropertyChanged(nameof(HasCommands));
            Save();

            return item;
        }

        /// <summary>
        ///  Adds any starter command this deck does not already have.
        /// </summary>
        /// <remarks>
        ///  The starters are seeded only on a genuinely fresh install, so that deleting one does
        ///  not bring it back on the next launch. The cost of that rule is that anyone who already
        ///  had a settings file never sees them at all - which is every existing user, and was not
        ///  a fair trade. This is the way in for them.
        ///
        ///  Matched by name, so pressing it twice adds nothing the second time, and a starter the
        ///  user has edited is left exactly as they edited it.
        /// </remarks>
        [RelayCommand]
        private void AddStarters()
        {
            int added = 0;

            foreach (CustomCommand starter in StarterCommands.For().Concat(StarterExamples.Commands()))
            {
                if (Commands.Any(existing => existing.Name == starter.Name))
                {
                    continue;
                }

                Add(starter);
                added++;
            }

            StarterNote = added switch
            {
                0 => Strings.Text("CmdHaveAllStarters"),
                1 => Strings.Text("CmdAddedOneStarter"),
                _ => Strings.Format("CmdAddedStarters", added),
            };
        }

        /// <summary>What the last press of Add starters did, shown beside the button.</summary>
        [ObservableProperty]
        private string starterNote = string.Empty;

        /// <summary>
        ///  Reads the project's own build files and offers what it finds as commands.
        /// </summary>
        /// <remarks>
        ///  Every repository already declares how to build, test and run it - in package.json, a
        ///  Makefile, Cargo.toml, a compose file. Retyping those into the deck by hand was the
        ///  tedious half of setting it up, and the half most likely to be skipped, which left the
        ///  deck empty on exactly the projects it would have helped most.
        ///
        ///  Matched by name against what is already here, so importing twice adds nothing the
        ///  second time and never overwrites a command the user has since edited.
        /// </remarks>
        [RelayCommand]
        private void Import()
        {
            if (!HasWorkspace)
            {
                StarterNote = Strings.Text("CmdChooseWorkspaceFirst");

                return;
            }

            int added = 0;
            int skipped = 0;

            foreach (Runnable runnable in WorkspaceScan.Scan(Workspace))
            {
                if (Commands.Any(existing => existing.Name == runnable.Name))
                {
                    skipped++;

                    continue;
                }

                Add(runnable.ToCommand());
                added++;
            }

            StarterNote = (added, skipped) switch
            {
                (0, 0) => Strings.Text("CmdNothingFound"),
                (0, _) => Strings.Text("CmdAllPresent"),
                (1, 0) => Strings.Text("CmdImportedOne"),
                (_, 0) => Strings.Format("CmdImportedMany", added),
                _ => Strings.Format("CmdImportedSome", added, skipped),
            };
        }

        /// <summary>Set by the view, which is the only thing that can reach the clipboard.</summary>
        public Func<string, Task>? Copy { get; set; }

        /// <summary>
        ///  Copies a link that runs the selected command.
        /// </summary>
        /// <remarks>
        ///  Here rather than only in Settings because this is where the thought occurs: the command
        ///  worth linking to from a README or a ticket is the one on screen, and a feature that can
        ///  only be found by reading the Settings page is a feature nobody finds.
        /// </remarks>
        [RelayCommand]
        private async Task CopyLink()
        {
            if (Selected is not { } command || Copy is not { } copy)
            {
                return;
            }

            await copy(DeepLink.For(Intent.Run, command.Name));

            StarterNote = Strings.Format("CmdCopiedLink", command.Name);
        }

        [RelayCommand]
        private void New() => Add(new CustomCommand
        {
            Name = UniqueName(Strings.Text("CmdNewCommand")),
            Command = string.Empty,
            Kind = CommandKind.Shell,
        });

        /// <summary>
        ///  The chains and watches that would break if <paramref name="name"/> went away.
        /// </summary>
        /// <remarks>
        ///  Read from the saved lists rather than from the automation panel, which this panel must
        ///  not hold - see <see cref="Renamed"/>. They are the same objects the automation panel
        ///  edits, so this is never stale; it only needs asking again, which is what
        ///  <see cref="UsageChanged"/> is for.
        /// </remarks>
        private List<string> UsedBy(string name)
        {
            List<string> users = [];

            users.AddRange(settings.Chains
                .Where(chain => chain.Steps.Any(step => step.Equals(name, StringComparison.OrdinalIgnoreCase)))
                .Select(chain => Strings.Format("CmdInUseChain", chain.Name)));

            users.AddRange(settings.Watches
                .Where(watch => watch.Command.Equals(name, StringComparison.OrdinalIgnoreCase))
                .Select(watch => Strings.Format("CmdInUseWatch", watch.Name)));

            return users;
        }

        /// <summary>
        ///  Whether the selected command can be deleted: it exists, and nothing runs it.
        /// </summary>
        /// <remarks>
        ///  Refused rather than allowed with a warning. A chain step or a watch that names a
        ///  command that has gone is only found out when it runs - usually a watch firing while the
        ///  user is in another application - so the moment to say so is the delete, while the user
        ///  is looking at the thing they are about to break.
        /// </remarks>
        public bool CanDelete => Selected is { } command && UsedBy(command.Name).Count == 0;

        /// <summary>What the delete button says when hovered: what it does, or why it will not.</summary>
        public string DeleteTip => Selected is { } command && UsedBy(command.Name) is { Count: > 0 } users
            ? Strings.Format("CmdInUse", string.Join(", ", users))
            : Strings.Text("Delete");

        /// <summary>
        ///  Asks again whether the selected command is in use.
        /// </summary>
        /// <remarks>
        ///  Called by the automation panel whenever a chain or a watch changes, and here whenever
        ///  the selection does.
        /// </remarks>
        public void UsageChanged()
        {
            OnPropertyChanged(nameof(CanDelete));
            OnPropertyChanged(nameof(DeleteTip));
            DeleteCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDelete))]
        private void Delete()
        {
            if (Selected is not { } doomed || !CanDelete)
            {
                return;
            }

            // Stopping first: a deleted command that is still running has nothing left to show its
            // output in and no way to be stopped afterwards.
            if (doomed.IsRunning)
            {
                doomed.StopCommand.Execute(null);
            }

            Commands.Remove(doomed);
            settings.CustomCommands.Remove(doomed.Source);

            // The command is gone, so its timings are no longer comparable to anything. Left
            // behind, they would attach themselves to the next command that happened to reuse
            // the name and report a trend from a different command's runs.
            RunHistory.Instance.Forget(doomed.Name);
            Selected = Commands.FirstOrDefault();

            OnPropertyChanged(nameof(HasCommands));
            Save();
        }

        [RelayCommand]
        private void Save() => settings.Save();

        /// <summary>
        ///  Whether anything is running, which is the only time Stop all has work to do.
        /// </summary>
        /// <remarks>
        ///  A button that is always clickable and usually does nothing teaches the user to ignore
        ///  it. This is also the honest readout of whether the deck is busy: an item that finished
        ///  on its own clears it without anyone pressing anything.
        /// </remarks>
        public bool AnyRunning => Commands.Any(command => command.IsRunning);

        /// <summary>How many are running, for the button's own label.</summary>
        public int RunningCount => Commands.Count(command => command.IsRunning);

        public string StopAllLabel => RunningCount > 1
            ? Strings.Format("CmdStopAllCount", RunningCount)
            : Strings.Text("CmdStopAll");

        private void CommandsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (CommandItem item in e.OldItems?.OfType<CommandItem>() ?? [])
            {
                item.PropertyChanged -= ItemChanged;
            }

            foreach (CommandItem item in e.NewItems?.OfType<CommandItem>() ?? [])
            {
                Watch(item);
            }

            RunningChanged();
        }

        private void Watch(CommandItem item) => item.PropertyChanged += ItemChanged;

        /// <summary>
        ///  Told when any command is renamed, so chains and watches can follow it.
        /// </summary>
        /// <remarks>
        ///  Set by the window rather than by the automation panel itself, for the same reason the
        ///  assistant's bridge is: two panels that each hold a reference to the other cannot be
        ///  constructed independently.
        /// </remarks>
        public Action<string, string>? Renamed { get; set; }

        /// <summary>Told when any command finishes, with how long it took.</summary>
        public Action<CommandItem, TimeSpan>? Finished { get; set; }

        /// <summary>
        ///  Puts the panel's callbacks on an item.
        /// </summary>
        /// <remarks>
        ///  Through a lambda that reads the property rather than by copying it, so a callback set
        ///  after the deck was built still reaches commands that already existed - which is always,
        ///  since the window wires these up once every panel has been constructed.
        /// </remarks>
        private void Attach(CommandItem item)
        {
            item.Renamed = (from, to) => Renamed?.Invoke(from, to);
            item.Finished = (finished, elapsed) => Finished?.Invoke(finished, elapsed);
        }

        private void ItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CommandItem.IsRunning))
            {
                RunningChanged();
            }

            if (e.PropertyName == nameof(CommandItem.State) && ReferenceEquals(sender, Selected))
            {
                OnPropertyChanged(nameof(CanExplain));
            }
        }

        private void RunningChanged()
        {
            OnPropertyChanged(nameof(AnyRunning));
            OnPropertyChanged(nameof(RunningCount));
            OnPropertyChanged(nameof(StopAllLabel));
            StopAllCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(AnyRunning))]
        private void StopAll()
        {
            foreach (CommandItem item in Commands.Where(c => c.IsRunning).ToList())
            {
                item.StopCommand.Execute(null);
            }
        }

        /// <summary>A name no other command already has, so Run is never ambiguous.</summary>
        public string UniqueName(string stem)
        {
            if (Commands.All(c => c.Name != stem))
            {
                return stem;
            }

            int suffix = 2;

            while (Commands.Any(c => c.Name == $"{stem} {suffix}"))
            {
                suffix++;
            }

            return $"{stem} {suffix}";
        }

        /// <summary>
        ///  The saved workspace if it is still there, else nothing.
        /// </summary>
        /// <remarks>
        ///  Empty rather than the home folder, deliberately - see <see cref="HasWorkspace"/>. A
        ///  saved workspace on a drive that is no longer mounted also lands here, which is right:
        ///  it is no more runnable than never having chosen one.
        /// </remarks>
        private string ResolveWorkspace()
        {
            return !string.IsNullOrWhiteSpace(settings.SelectedWorkspace)
                && Directory.Exists(settings.SelectedWorkspace)
                    ? settings.SelectedWorkspace
                    : string.Empty;
        }

        /// <summary>
        ///  Hides the kinds that cannot run here, rather than offering them and failing.
        /// </summary>
        /// <remarks>
        ///  Bash is offered on Windows too when a bash can be found - Git for Windows or WSL - so
        ///  the list reflects the machine rather than the operating system.
        /// </remarks>
        private static bool Supported(CommandKind kind) => kind switch
        {
            CommandKind.Batch => OperatingSystem.IsWindows(),
            CommandKind.Bash => ScriptFile.FindBash() is not null,
            _ => true,
        };
    }
}
