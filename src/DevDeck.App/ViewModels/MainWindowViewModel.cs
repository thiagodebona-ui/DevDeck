using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One entry in the left rail.</summary>
    /// <remarks>
    ///  Sections with nothing behind them yet carry their own note rather than being hidden, so the
    ///  gaps are visible instead of implied.
    /// </remarks>
    internal sealed partial class Section : ObservableObject
    {
        /// <summary>The key the title is looked up under, held so it can be looked up again.</summary>
        private readonly string titleKey;

        private readonly string blurbKey;

        public Section(
            string name,
            string titleKey,
            string blurbKey,
            Avalonia.Media.Geometry icon,
            object? content = null)
        {
            Name = name;
            this.titleKey = titleKey;
            this.blurbKey = blurbKey;
            Icon = icon;
            Content = content;

            Strings.Changed += () =>
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(Blurb));
            };
        }

        /// <summary>
        ///  What this section is called internally, which is not what is drawn.
        /// </summary>
        /// <remarks>
        ///  Deliberately never translated. It is the identity: the settings file records which
        ///  section was open by this name, a devdeck:// link names one by it, and Show() finds one
        ///  by it. Translating it would mean a deck saved in Portuguese opens on the wrong page in
        ///  English, and every link in every README stops working the moment someone switches.
        /// </remarks>
        public string Name { get; }

        /// <summary>The name as drawn in the rail, in the language the app is being read in.</summary>
        public string Title => Strings.Text(titleKey);

        /// <summary>What this section is for, in the user's terms.</summary>
        public string Blurb => Strings.Text(blurbKey);

        /// <summary>
        ///  The rail glyph, from <see cref="Icons"/>.
        /// </summary>
        /// <remarks>
        ///  Carried on the section rather than chosen in the XAML by a converter on Name, because
        ///  the rail is a single DataTemplate over a collection: picking per row in markup would
        ///  mean ten templates or a switch written in a converter, and both put the icon somewhere
        ///  other than where the section is declared. Here the name, the title, the blurb and the
        ///  glyph are one line, and adding a section cannot forget one of them.
        /// </remarks>
        public Avalonia.Media.Geometry Icon { get; }

        /// <summary>The view model for the panel, or null while it is still a placeholder.</summary>
        public object? Content { get; }

        public bool IsBuilt => Content is not null;

        /// <summary>
        ///  Whether there is work going on behind this section.
        /// </summary>
        /// <remarks>
        ///  A section is told rather than asking: the rail is built out of view models it treats
        ///  as opaque, and teaching it which of them has a notion of "busy" would put a switch on
        ///  the type of every panel into the one class that deliberately knows nothing about
        ///  them. Whoever knows - the deck, which already tracks whether anything is running -
        ///  sets it.
        /// </remarks>
        [ObservableProperty]
        private bool isBusy;
    }

    internal sealed partial class MainWindowViewModel : ObservableObject
    {
        private readonly AppSettings settings;

        public MainWindowViewModel(AppSettings settings)
        {
            this.settings = settings;

            Title = $"DevDeck {AppVersion.Number}";
            Platform = $"{Describe()} · {AppVersion.Display}";

            CommandsViewModel commands = new(settings);

            // The assistant hands code blocks to the deck and then shows them, so it needs a way in
            // rather than a reference to the whole window. The lookup is deferred because Sections
            // does not exist yet at this point - it is built out of the assistant below.
            AssistantViewModel assistant = new(
                settings,
                commands.Add,
                () =>
                {
                    if (Sections is { } sections)
                    {
                        Selected = sections.FirstOrDefault(s => s.Name == "Commands") ?? Selected;
                    }
                },
                // Code run from the transcript runs where every other command runs, rather than in
                // whatever directory the app happened to start in.
                () => commands.Workspace);

            Memory = new MemoryViewModel(settings);
            Commands = commands;

            // The other direction of the bridge the assistant already has into the deck: a failed
            // run can be handed to the model with its output attached, without either panel
            // holding a reference to the other.
            commands.Explain = failed =>
            {
                assistant.Explain(failed.Name, failed.Body, failed.Tail(), failed.LastExitCode);

                // Deferred like the assistant's own lookup above: Sections is built below this
                // point, so the lambda cannot close over it until it is being called rather than
                // being created.
                Show("Assistant");
            };

            // The same bridge again, from the other new panel: following a container's log is a
            // long-running command, and the deck is already the place long-running commands live.
            // No navigation with it - the panel says what it did, and jumping away from the
            // container list would interrupt whatever the user was doing there.
            Running = new RunningViewModel(settings, commands)
            {
                Adopt = (name, body) => commands.Add(new CustomCommand
                {
                    Name = name,
                    Command = body,
                    Kind = CommandKind.Shell,
                }),
            };

            // Chains, watches and the project deck: everything that starts a command without the
            // user pressing Run. Built after the deck, because all of it goes through the deck.
            Automation = new AutomationViewModel(settings, commands);

            commands.Renamed = Automation.Renamed;

            // A chain the assistant wrote lands where chains live, and is run from there even when
            // it is started from the transcript. Create chain opens the page on it - the same as a
            // command it wrote landing in the deck; Run chain stays in the conversation.
            assistant.Automation = Automation;
            assistant.ShowChain = chain =>
            {
                Automation.SelectedChain = chain;
                Show("Automation");
            };
            assistant.FindCommand = name => commands.Commands.FirstOrDefault(
                command => command.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Source;
            assistant.UniqueCommandName = commands.UniqueName;
            commands.WorkspaceChanged = Automation.Reload;

            // A long command is one the user walked away from, so finishing it is news. Anything
            // short finished while they were still looking at it, and saying so would be noise.
            commands.Finished = (command, elapsed) =>
            {
                if (!settings.NotifyOnFinish || elapsed.TotalSeconds < settings.NotifyAfterSeconds)
                {
                    return;
                }

                string outcome = Strings.Text(command.State switch
                {
                    RunState.Succeeded => "NavFinished",
                    RunState.Stopped => "NavWasStopped",
                    _ => "NavFailed",
                });

                Announce?.Invoke(
                    $"{command.Name} {outcome}",
                    Strings.Format(
                        "NavRanSummary", Math.Round(elapsed.TotalSeconds), command.LastExitCode));
            };

            // The AI card on the settings page edits the assistant's own endpoint rather than a
            // copy of it, so what is chosen there is what the next question goes to.
            Preferences = new SettingsViewModel(settings) { Ai = assistant };

            assistant.ShowSettings = () => Show("Settings");

            Sections = new ObservableCollection<Section>
            {
                new("Commands", "NavCommands", "NavCommandsBlurb", Icons.Commands, commands),
                new("Assistant", "NavAssistant", "NavAssistantBlurb", Icons.Assistant, assistant),
                new("HTTP", "NavHttp", "NavHttpBlurb", Icons.Http, new HttpViewModel(settings)),
                new("Toolbox", "NavToolbox", "NavToolboxBlurb", Icons.Toolbox, new ToolboxViewModel()),
                new("Running", "NavRunning", "NavRunningBlurb", Icons.Running, Running),
                new("Automation", "NavAutomation", "NavAutomationBlurb", Icons.Automation, Automation),
                new("Clipboard", "NavClipboard", "NavClipboardBlurb", Icons.Clipboard, new ClipsViewModel(settings)),
                new("Log", "NavLog", "NavLogBlurb", Icons.Log, new LogViewModel()),
                new("Memory/CPU", "NavMemory", MemoryBlurb(), Icons.Memory, Memory),
                new("Settings", "NavSettings", "NavSettingsBlurb", Icons.Settings, Preferences),
                new("Changelog", "NavChangelog", "NavChangelogBlurb", Icons.Changelog, new ChangelogViewModel()),
            };

            Arrange(Sections, settings.SectionOrder);

            selected = Sections[0];

            // The first start of a new version opens on what it brought, once. An empty value is
            // treated as different too: every build before this one wrote none, and those are
            // exactly the users an update should tell.
            if (settings.LastSeenVersion != AppVersion.Number)
            {
                selected = Sections.First(section => section.Name == "Changelog");

                settings.LastSeenVersion = AppVersion.Number;
                settings.Save();
            }

            // The rail's busy mark. Hooked up after Sections exists, because the handler needs to
            // find the section it is about - and the deck is constructed well before the rail is.
            Section deckSection = Sections.First(section => section.Name == "Commands");

            commands.PropertyChanged += (_, changed) =>
            {
                if (changed.PropertyName == nameof(CommandsViewModel.AnyRunning))
                {
                    deckSection.IsBusy = commands.AnyRunning;
                }
            };
        }

        public string Title { get; }

        public string Platform { get; }

        public ObservableCollection<Section> Sections { get; }

        /// <summary>Puts the sections in the user's saved order. Ones it does not name keep their place at the end.</summary>
        private static void Arrange(ObservableCollection<Section> sections, List<string> order)
        {
            if (order.Count == 0)
            {
                return;
            }

            // Stable, so the sections the saved order does not mention stay in their shipped order.
            List<Section> arranged = [.. sections.OrderBy(section =>
            {
                int at = order.IndexOf(section.Name);

                return at < 0 ? int.MaxValue : at;
            })];

            sections.Clear();

            foreach (Section section in arranged)
            {
                sections.Add(section);
            }
        }

        /// <summary>
        ///  Moves a section to a new place in the rail, and remembers the order.
        /// </summary>
        /// <remarks>
        ///  The selection is put back afterwards: to the list a moved row is a row removed and
        ///  another added, and losing the selection here would blank the page being looked at.
        /// </remarks>
        public void MoveSection(Section section, int to)
        {
            int from = Sections.IndexOf(section);

            if (from < 0 || to < 0 || to >= Sections.Count || from == to)
            {
                return;
            }

            Section? keep = Selected;

            Sections.Move(from, to);
            Selected = keep;

            settings.SectionOrder = [.. Sections.Select(each => each.Name)];
            settings.Save();
        }

        /// <summary>
        ///  Held by name as well as by section, because the window's own lifecycle depends on it.
        /// </summary>
        /// <remarks>
        ///  The floating widget keeps the app alive after the main window is closed, so the window
        ///  has to be able to ask whether one is open before it agrees to close. Digging that out
        ///  of the Sections collection by string would work and would break the first time a
        ///  section was renamed.
        /// </remarks>
        public MemoryViewModel Memory { get; }

        /// <summary>
        ///  Held so the window can stop its polling when the app is not looking at it.
        /// </summary>
        /// <remarks>
        ///  Same reasoning as Memory above, and the same alternative rejected for the same reason:
        ///  the view starts and stops it on attach, but the window needs it on the way out too, and
        ///  finding it by section name would break the first time the section is renamed.
        /// </remarks>
        public RunningViewModel Running { get; }

        /// <summary>
        ///  Held so the window can start its watches and stop them on the way out.
        /// </summary>
        /// <remarks>
        ///  A file watch outlives the panel being looked at - that is the whole point of it - so
        ///  unlike the clipboard it is started by the window rather than by the view appearing.
        /// </remarks>
        public AutomationViewModel Automation { get; }

        /// <summary>
        ///  Held so the app can hand it the things a settings page cannot reach by itself.
        /// </summary>
        /// <remarks>
        ///  Claiming a global key and opening a release page both need something above a view
        ///  model - a window to raise, and a top level to launch from - so the page exposes the
        ///  intent and something with those things supplies the doing.
        /// </remarks>
        public SettingsViewModel Preferences { get; }

        /// <summary>
        ///  How a finished run reaches the desktop, supplied by the window.
        /// </summary>
        /// <remarks>
        ///  Set by the window because a notification needs a window: where the system has no
        ///  channel of its own, the fallback is to make this one ask for attention.
        /// </remarks>
        public Action<string, string>? Announce { get; set; }

        /// <summary>
        ///  The last thing that finished while the user was elsewhere, shown in the title bar.
        /// </summary>
        /// <remarks>
        ///  The last fallback, for a platform with no notification channel and no way to flash a
        ///  taskbar button. It is not a notification - nothing about it reaches the user where they
        ///  are - but it does mean the answer is on screen the moment they look back, rather than
        ///  the run having finished silently with nothing to show for it.
        /// </remarks>
        [ObservableProperty]
        private string announcement = string.Empty;

        public bool HasAnnouncement => !string.IsNullOrEmpty(Announcement);

        partial void OnAnnouncementChanged(string value) => OnPropertyChanged(nameof(HasAnnouncement));

        [RelayCommand]
        private void Dismiss() => Announcement = string.Empty;

        /// <summary>
        ///  Held for the palette, which offers every saved command as something to run.
        /// </summary>
        public CommandsViewModel Commands { get; }

        /// <summary>
        ///  Everything the palette can reach, rebuilt each time it opens.
        /// </summary>
        /// <remarks>
        ///  Rebuilt rather than kept, because the commands are the bulk of it and they change -
        ///  added, renamed, deleted - between one press of the key and the next. A cached list
        ///  would offer a command that no longer exists, and running it would do nothing with no
        ///  explanation.
        /// </remarks>
        public IReadOnlyList<PaletteAction> Actions()
        {
            List<PaletteAction> actions = [];

            // The commands first and unqualified: "build" should find the user's own build command,
            // not a panel that happens to contain the word.
            foreach (CommandItem command in Commands.Commands)
            {
                CommandItem captured = command;

                actions.Add(new PaletteAction(
                    captured.Name,
                    Strings.Text("PaletteCategoryRun"),
                    () =>
                    {
                        Show("Commands");
                        Commands.Selected = captured;
                        captured.RunCommand.Execute(null);
                    },
                    Summarise(captured)));
            }

            foreach (Section section in Sections.Where(section => section.IsBuilt))
            {
                Section captured = section;

                actions.Add(new PaletteAction(
                    captured.Title,
                    Strings.Text("PaletteCategoryGoTo"),
                    () => Selected = captured,
                    captured.Blurb));
            }

            actions.Add(new PaletteAction(Strings.Text("PaletteNewCommand"), Strings.Text("PaletteCategoryDeck"), () =>
            {
                Show("Commands");
                Commands.NewCommand.Execute(null);
            }));

            actions.Add(new PaletteAction(Strings.Text("PaletteStopEverything"), Strings.Text("PaletteCategoryDeck"), () =>
                Commands.StopAllCommand.Execute(null)));

            actions.Add(new PaletteAction(Strings.Text("PaletteImportRunnables"), Strings.Text("PaletteCategoryDeck"), () =>
            {
                Show("Commands");
                Commands.ImportCommand.Execute(null);
            }, Strings.Text("PaletteImportRunnablesHint")));

            // The chains go in unqualified alongside the commands, for the same reason: a chain
            // called "ship" is something the user runs, not a setting they configure.
            foreach (ChainItem chain in Automation.Chains)
            {
                ChainItem captured = chain;

                actions.Add(new PaletteAction(
                    captured.Name,
                    Strings.Text("PaletteCategoryChain"),
                    () =>
                    {
                        Show("Commands");
                        Automation.RunChainCommand.Execute(captured);
                    },
                    captured.Summary));
            }

            actions.Add(new PaletteAction(Strings.Text("PaletteNewChain"), Strings.Text("PaletteCategoryAutomation"), () =>
            {
                Show("Automation");
                Automation.NewChainCommand.Execute(null);
            }, Strings.Text("PaletteNewChainHint")));

            actions.Add(new PaletteAction(Strings.Text("PaletteImportDeck"), Strings.Text("PaletteCategoryAutomation"), () =>
            {
                Show("Automation");
                Automation.ImportDeckCommand.Execute(null);
            }, ProjectDeck.FileName));

            actions.Add(new PaletteAction(Strings.Text("PaletteExportDeck"), Strings.Text("PaletteCategoryAutomation"), () =>
            {
                Show("Automation");
                Automation.ExportDeckCommand.Execute(null);
            }, Strings.Format("PaletteExportDeckHint", ProjectDeck.FileName)));

            return actions;
        }

        /// <summary>
        ///  Asked, by the window, to bring itself forward - set by the window.
        /// </summary>
        /// <remarks>
        ///  A link arriving from the desktop is useless if the window it acts on stays behind a
        ///  browser. Only the window can raise itself, so it lends the view model the ability.
        /// </remarks>
        public Action? Surface { get; set; }

        /// <summary>
        ///  Carries out a request that arrived as a link or on a command line.
        /// </summary>
        /// <remarks>
        ///  Everything a link can ask for is something the user could do by hand in the window, and
        ///  it is done here through the same view models rather than by a second path into the
        ///  runner - so a linked run has the same output, history, parameters and Stop button as
        ///  any other.
        ///
        ///  Names are matched exactly first, then case-insensitively, and then not at all: a link
        ///  naming something that does not exist says so in the log and brings the window up, which
        ///  is a better answer than starting whichever command happened to be nearest.
        /// </remarks>
        public void Handle(LinkRequest request)
        {
            if (!request.IsSomething)
            {
                return;
            }

            AppLog.Instance.Info("link", $"Asked to {request.Describe()}.");

            Surface?.Invoke();

            switch (request.Intent)
            {
                case Intent.Run:
                    Linked(request);
                    break;

                case Intent.Chain:
                    LinkedChain(request);
                    break;

                case Intent.Workspace:
                    if (Directory.Exists(request.Target))
                    {
                        Show("Commands");
                        Commands.UseWorkspace(request.Target);
                    }
                    else
                    {
                        AppLog.Instance.Warn("link", $"No folder at {request.Target}.");
                    }

                    break;

                default:
                    if (request.Target.Length > 0)
                    {
                        Show(request.Target);
                    }

                    break;
            }
        }

        private void Linked(LinkRequest request)
        {
            if (Find(request.Target) is not { } command)
            {
                AppLog.Instance.Warn("link", $"No command called \"{request.Target}\".");
                Show("Commands");

                return;
            }

            Show("Commands");
            Commands.Selected = command;

            // Values from the link are supplied rather than asked for, which is the point of
            // linking to a parameterised command at all. Anything the link did not mention is
            // still asked for in the usual way.
            command.Supplied = request.Values;
            command.RunCommand.Execute(null);
        }

        private void LinkedChain(LinkRequest request)
        {
            ChainItem? chain = Automation.Chains.FirstOrDefault(
                item => string.Equals(item.Name, request.Target, StringComparison.OrdinalIgnoreCase));

            if (chain is null)
            {
                AppLog.Instance.Warn("link", $"No chain called \"{request.Target}\".");
                Show("Automation");

                return;
            }

            Show("Commands");
            Automation.RunChainCommand.Execute(chain);
        }

        private CommandItem? Find(string name) =>
            Commands.Commands.FirstOrDefault(command => command.Name == name)
            ?? Commands.Commands.FirstOrDefault(
                command => string.Equals(command.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>The first line of a command's body, which is what identifies it at a glance.</summary>
        private static string Summarise(CommandItem command)
        {
            string body = command.Body
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.Trim().Length > 0)?.Trim() ?? string.Empty;

            return body.Length > 70 ? body[..70] + "…" : body;
        }

        /// <summary>Switches the rail to a section by name, for actions that need one shown first.</summary>
        private void Show(string name)
        {
            if (Sections is { } sections
                && sections.FirstOrDefault(section => section.Name == name) is { } found)
            {
                Selected = found;
            }
        }

        [ObservableProperty]
        private Section? selected;

        /// <summary>
        ///  What this section offers, which genuinely differs by platform.
        /// </summary>
        /// <remarks>
        ///  The monitoring half is the same everywhere. The cleaning half is not, and the rail says
        ///  so rather than letting the user find out by pressing the button.
        /// </remarks>
        /// <summary>Which blurb the memory page gets, since it can do less off Windows.</summary>
        private static string MemoryBlurb() => OperatingSystem.IsWindows()
            ? "NavMemoryBlurbWindows"
            : "NavMemoryBlurbOther";

        private static string Describe()
        {
            string os = RuntimeInformation.OSDescription.Trim();

            return $"{os} ({RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()})";
        }
    }
}
