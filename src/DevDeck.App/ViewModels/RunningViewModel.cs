using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  What is listening and what is containerised, on one page.
    /// </summary>
    /// <remarks>
    ///  Two lists rather than two sections, because they answer the same question from either end:
    ///  "what is running, and how do I stop it". Splitting them would mean knowing which of the two
    ///  a thing is before you could go looking for it, which is precisely what you do not know when
    ///  a port is taken.
    ///
    ///  Refreshed on demand and on a slow timer rather than continuously. Both listings shell out,
    ///  and a panel that spawns netstat twice a second to display a list nobody is reading is a
    ///  poor trade for numbers that change every few minutes.
    /// </remarks>
    internal sealed partial class RunningViewModel : ObservableObject, IDisposable
    {
        private readonly Timer timer;

        private readonly AppSettings settings;

        private readonly CommandsViewModel? deck;

        /// <summary>
        ///  The quick tick: the deck's running commands and how long each has been going.
        /// </summary>
        /// <remarks>
        ///  Separate from the slow refresh because it starts no process - it is a walk over a list
        ///  already in memory - so it can move at the speed an elapsed clock needs, while netstat
        ///  and docker stay at twelve seconds. CPU and memory are the Memory/CPU page's job.
        /// </remarks>
        private readonly Avalonia.Threading.DispatcherTimer pulse;

        /// <summary>The last full listings, before the filter box has its say.</summary>
        private IReadOnlyList<ListeningPort> allPorts = [];

        private IReadOnlyList<ContainerInfo> allContainers = [];

        /// <summary>Set by the window, so a container's log can become a command in the deck.</summary>
        public Action<string, string>? Adopt { get; set; }

        /// <summary>Set by the view: the clipboard is reached through the window.</summary>
        public Func<string, Task>? Copy { get; set; }

        public RunningViewModel(AppSettings settings, CommandsViewModel? deck = null)
        {
            this.settings = settings;
            this.deck = deck;

            autoRefresh = settings.RunningAutoRefresh;

            // Started stopped: the first refresh is kicked off by the view when the panel is first
            // shown, so an app that never opens this page never runs netstat at all.
            timer = new Timer(_ => Tick(), null, Timeout.Infinite, Timeout.Infinite);

            pulse = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            pulse.Tick += (_, _) => Pulse();

            foreach (string url in settings.HealthChecks)
            {
                HealthChecks.Add(new HealthCheck(url));
            }
        }

        public ObservableCollection<ListeningPort> Ports { get; } = [];

        public ObservableCollection<ContainerInfo> Containers { get; } = [];

        // --- The tiles along the top ------------------------------------------------------

        [ObservableProperty]
        private string portsText = "0";

        [ObservableProperty]
        private string containersText = "0";

        [ObservableProperty]
        private string deckText = "0";

        [ObservableProperty]
        private string healthText = "-";

        /// <summary>Whether every health check that has answered is healthy, for the tile's colour.</summary>
        [ObservableProperty]
        private bool healthBad;

        // --- Filter and refresh -----------------------------------------------------------

        /// <summary>Narrows both lists: a port number, a process, an image, a name.</summary>
        [ObservableProperty]
        private string filter = string.Empty;

        partial void OnFilterChanged(string value) => Apply();

        [ObservableProperty]
        private bool autoRefresh;

        partial void OnAutoRefreshChanged(bool value)
        {
            settings.RunningAutoRefresh = value;
            settings.Save();

            if (shown)
            {
                timer.Change(
                    value ? TimeSpan.FromSeconds(12) : Timeout.InfiniteTimeSpan,
                    value ? TimeSpan.FromSeconds(12) : Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>Whether the page is on screen, which is when the timers are allowed to run.</summary>
        private bool shown;

        // --- Health checks ----------------------------------------------------------------

        public ObservableCollection<HealthCheck> HealthChecks { get; } = [];

        public bool NoHealthChecks => HealthChecks.Count == 0;

        [ObservableProperty]
        private string newHealthUrl = string.Empty;

        [RelayCommand]
        private async Task AddHealthCheck()
        {
            string url = NewHealthUrl.Trim();

            if (url.Length == 0)
            {
                return;
            }

            // A bare "localhost:5000/health" is what people type; it means http.
            if (!url.Contains("://", StringComparison.Ordinal))
            {
                url = "http://" + url;
            }

            NewHealthUrl = string.Empty;

            if (HealthChecks.Any(check => check.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            HealthCheck added = new(url);
            HealthChecks.Add(added);
            SaveHealthChecks();

            await added.Check();
            Summarise();
        }

        [RelayCommand]
        private void RemoveHealthCheck(HealthCheck? check)
        {
            if (check is null)
            {
                return;
            }

            HealthChecks.Remove(check);
            SaveHealthChecks();
            Summarise();
        }

        [RelayCommand]
        private async Task CheckHealth()
        {
            await Task.WhenAll(HealthChecks.Select(check => check.Check()));
            Summarise();
        }

        [RelayCommand]
        private void OpenHealthCheck(HealthCheck? check)
        {
            if (check is not null)
            {
                Open?.Invoke(check.Url);
            }
        }

        private void SaveHealthChecks()
        {
            settings.HealthChecks = [.. HealthChecks.Select(check => check.Url)];
            settings.Save();
            OnPropertyChanged(nameof(NoHealthChecks));
        }

        // --- Port check -------------------------------------------------------------------

        [ObservableProperty]
        private string portToCheck = string.Empty;

        [ObservableProperty]
        private string portAnswer = string.Empty;

        [ObservableProperty]
        private bool portIsFree;

        [ObservableProperty]
        private bool portIsTaken;

        [RelayCommand]
        private async Task CheckPort()
        {
            PortIsFree = false;
            PortIsTaken = false;

            if (!int.TryParse(PortToCheck.Trim(), out int port) || port is < 1 or > 65535)
            {
                PortAnswer = Strings.Text("RunPortInvalid");

                return;
            }

            bool free = await Task.Run(() => Core.Ports.Free(port));

            if (free)
            {
                PortIsFree = true;
                PortAnswer = Strings.Format("RunPortFree", port);

                return;
            }

            PortIsTaken = true;

            // The full listing, not the filtered one: the owner is often exactly the kind of
            // process the "only likely ports" filter hides.
            IReadOnlyList<ListeningPort> listening = allPorts.Any(known => known.Port == port)
                ? allPorts
                : await Task.Run(() => Core.Ports.Listening());

            PortAnswer = listening.FirstOrDefault(known => known.Port == port) is { } owner
                ? Strings.Format("RunPortTaken", port, owner.Process, owner.ProcessId)
                : Strings.Format("RunPortTakenUnknown", port);
        }

        // --- The deck, and the heaviest processes -----------------------------------------

        public ObservableCollection<DeckActivity> DeckRunning { get; } = [];

        public bool DeckIdle => DeckRunning.Count == 0;

        [RelayCommand]
        private void StopDeckCommand(DeckActivity? activity)
        {
            if (activity?.Command is { IsRunning: true } command)
            {
                command.StopCommand.Execute(null);
            }
        }

        public ObservableCollection<ProcessUsage> TopProcesses { get; } = [];

        /// <summary>Ends one of the heaviest processes, then re-reads the page.</summary>
        [RelayCommand]
        private async Task KillProcess(ProcessUsage? process)
        {
            if (process is null)
            {
                return;
            }

            Status = await Task.Run(() => MemoryProbe.End(process.Id, process.Name));

            TopProcesses.Remove(process);

            await Refresh();
        }

        [RelayCommand]
        private async Task CopyPortUrl()
        {
            if (SelectedPort is { IsWeb: true } port && Copy is { } copy)
            {
                await copy(port.Url);
            }
        }

        /// <summary>The two-second tick. UI thread, and nothing in it waits on a process.</summary>
        private void Pulse()
        {
            Track();
        }

        /// <summary>Brings the deck list in line with what is actually running.</summary>
        private void Track()
        {
            if (deck is null)
            {
                return;
            }

            List<CommandItem> running = [.. deck.Commands.Where(command => command.IsRunning)];

            for (int at = DeckRunning.Count - 1; at >= 0; at--)
            {
                if (!running.Contains(DeckRunning[at].Command))
                {
                    DeckRunning.RemoveAt(at);
                }
            }

            foreach (CommandItem command in running)
            {
                if (DeckRunning.All(activity => activity.Command != command))
                {
                    DeckRunning.Add(new DeckActivity(command));
                }
            }

            foreach (DeckActivity activity in DeckRunning)
            {
                activity.Tick();
            }

            DeckText = Strings.Format("RunDeckRunning", DeckRunning.Count);
            OnPropertyChanged(nameof(DeckIdle));
        }

        /// <summary>Recomputes the tiles that come from the slow refresh.</summary>
        private void Summarise()
        {
            PortsText = allPorts.Count.ToString(System.Globalization.CultureInfo.CurrentCulture);

            ContainersText = Strings.Format(
                "RunContainersUp", allContainers.Count(container => container.IsRunning), allContainers.Count);

            List<HealthCheck> answered = [.. HealthChecks.Where(check => !check.IsWaiting)];

            HealthText = HealthChecks.Count == 0
                ? "-"
                : Strings.Format("RunHealthUp", answered.Count(check => check.IsGood), HealthChecks.Count);

            HealthBad = answered.Any(check => check.IsBad);
        }

        /// <summary>Re-applies the filter box to the last listings, keeping the selection.</summary>
        private void Apply()
        {
            int? wasPort = SelectedPort?.Port;
            string? wasContainer = SelectedContainer?.Id;

            string wanted = Filter.Trim();

            Ports.Clear();

            foreach (ListeningPort port in allPorts.Where(Interesting).Where(port => Matches(port, wanted)))
            {
                Ports.Add(port);
            }

            Containers.Clear();

            foreach (ContainerInfo container in allContainers.Where(container => Matches(container, wanted)))
            {
                Containers.Add(container);
            }

            SelectedPort = Ports.FirstOrDefault(port => port.Port == wasPort);
            SelectedContainer = Containers.FirstOrDefault(container => container.Id == wasContainer);
        }

        private static bool Matches(ListeningPort port, string wanted) =>
            wanted.Length == 0
            || port.Port.ToString(System.Globalization.CultureInfo.InvariantCulture).Contains(wanted, StringComparison.Ordinal)
            || port.Process.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || port.Note.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || port.Address.Contains(wanted, StringComparison.OrdinalIgnoreCase);

        private static bool Matches(ContainerInfo container, string wanted) =>
            wanted.Length == 0
            || container.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || container.ImageName.Contains(wanted, StringComparison.OrdinalIgnoreCase)
            || container.Status.Contains(wanted, StringComparison.OrdinalIgnoreCase);

        [ObservableProperty]
        private ListeningPort? selectedPort;

        [ObservableProperty]
        private ContainerInfo? selectedContainer;

        [ObservableProperty]
        private string status = string.Empty;

        [ObservableProperty]
        private string logs = string.Empty;

        [ObservableProperty]
        private bool busy;

        /// <summary>Hides the ports that are noise: system services and ephemeral ports.</summary>
        [ObservableProperty]
        private bool onlyInteresting = true;

        /// <summary>
        ///  Whether there is an engine that is actually answering.
        /// </summary>
        /// <remarks>
        ///  An installed CLI is not an available engine. Docker Desktop leaves "docker" on the PATH
        ///  when it is not running, so treating the CLI as the answer hid the buttons' uselessness
        ///  behind an empty list.
        /// </remarks>
        public bool HasEngine => Core.Containers.Available && Core.Containers.Trouble is null;

        public string EngineNote => Core.Containers.Trouble
            ?? (Core.Containers.Engine is { } engine
                ? Strings.Format("RunContainersVia", engine)
                : Strings.Text("RunNoEngine"));

        public bool HasLogs => Logs.Length > 0;

        /// <summary>
        ///  Whether the containers half of the page is worth showing at all.
        /// </summary>
        /// <remarks>
        ///  Only with an engine that answers and at least one container, running or not. On a
        ///  machine without Docker the section was an empty list and a note about installing
        ///  something, taking half the page from the ports list that is actually in use.
        /// </remarks>
        public bool ShowContainers => HasEngine && allContainers.Count > 0;

        /// <summary>Set by the view: opening a URL is a shell operation.</summary>
        public Action<string>? Open { get; set; }

        /// <summary>
        ///  Kicks off a refresh from somewhere that cannot await one.
        /// </summary>
        /// <remarks>
        ///  Through the generated command rather than the method, because the command carries the
        ///  exception rather than dropping it on an unobserved task - and because it is the same
        ///  path the Refresh button takes, so the re-entry guard covers both.
        /// </remarks>
        private void Begin() => RefreshCommand.Execute(null);

        partial void OnOnlyInterestingChanged(bool value) => Apply();

        partial void OnLogsChanged(string value) => OnPropertyChanged(nameof(HasLogs));

        /// <summary>Starts the slow refresh, and does one immediately.</summary>
        public void Start()
        {
            shown = true;

            Begin();

            if (AutoRefresh)
            {
                timer.Change(TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(12));
            }

            Pulse();
            pulse.Start();
        }

        public void Stop()
        {
            shown = false;

            timer.Change(Timeout.Infinite, Timeout.Infinite);
            pulse.Stop();
        }

        /// <summary>
        ///  The timer's tick, moved onto the UI thread before it does anything.
        /// </summary>
        /// <remarks>
        ///  A timer callback arrives on a pool thread with no synchronisation context, so the
        ///  continuation after the await inside <see cref="Refresh"/> would resume on a pool thread
        ///  too - and fill bound collections from there, which is a crash rather than a glitch.
        ///  Posting first means the whole refresh runs where the bindings expect it, with only the
        ///  process-starting part handed off.
        /// </remarks>
        private void Tick() => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!Busy)
            {
                RefreshCommand.Execute(null);
            }
        });

        /// <summary>
        ///  Re-reads both lists.
        /// </summary>
        /// <remarks>
        ///  Off the UI thread, because both sides of it start processes and wait for them. The
        ///  selection is restored by value afterwards: the rows are new objects each time, so a
        ///  straight rebuild would clear the selection under the user every twelve seconds.
        /// </remarks>
        [RelayCommand]
        private async Task Refresh()
        {
            if (Busy)
            {
                return;
            }

            Busy = true;

            // The health checks go out alongside the listings rather than after them, so a slow
            // docker does not hold up the answer to "is my API up".
            Task health = Task.WhenAll(HealthChecks.Select(check => check.Check()));

            (IReadOnlyList<ListeningPort> ports, IReadOnlyList<ContainerInfo> containers, IReadOnlyList<ProcessUsage> heaviest) =
                await Task.Run(() => (Core.Ports.Listening(), Core.Containers.List(), MemoryProbe.TopProcesses(6)));

            allPorts = ports;
            allContainers = containers;

            Apply();

            TopProcesses.Clear();

            foreach (ProcessUsage process in heaviest)
            {
                TopProcesses.Add(process);
            }

            await health;

            Summarise();

            // The engine's state is only known after a listing, so the note and the buttons are
            // told to re-read it here rather than caching an answer from startup.
            OnPropertyChanged(nameof(HasEngine));
            OnPropertyChanged(nameof(EngineNote));
            OnPropertyChanged(nameof(ShowContainers));

            Status = Strings.Format("RunSummary", Ports.Count, Containers.Count,
                DateTime.Now.ToString("HH:mm:ss"));

            Busy = false;
        }

        /// <summary>
        ///  Whether a port is one a developer put there.
        /// </summary>
        /// <remarks>
        ///  The unfiltered list on Windows is forty-odd rows of RPC and SMB, and the one row being
        ///  looked for is somewhere in it. The filter keeps anything with a known convention, plus
        ///  the range where development servers actually live - and the toggle is right there for
        ///  the times the answer is not in it.
        /// </remarks>
        private bool Interesting(ListeningPort port) =>
            !OnlyInteresting || port.HasNote || port.Port is >= 1024 and < 32_768;

        [RelayCommand]
        private async Task KillPort()
        {
            if (SelectedPort is not { } port)
            {
                return;
            }

            string said = await Task.Run(() => Core.Ports.Kill(port));

            Status = said;

            await Refresh();
        }

        [RelayCommand]
        private void OpenPort()
        {
            if (SelectedPort is { IsWeb: true } port)
            {
                Open?.Invoke(port.Url);
            }
        }

        [RelayCommand]
        private void OpenContainer()
        {
            if (SelectedContainer is { HasPublished: true } container)
            {
                Open?.Invoke(container.Url);
            }
        }

        [RelayCommand]
        private Task StartContainer() => Verb("start");

        [RelayCommand]
        private Task StopContainer() => Verb("stop");

        [RelayCommand]
        private Task RestartContainer() => Verb("restart");

        private async Task Verb(string verb)
        {
            if (SelectedContainer is not { } container)
            {
                return;
            }

            Busy = true;
            Status = $"{verb} {container.Name}…";

            string said = await Task.Run(() => Core.Containers.Do(verb, container));

            Busy = false;
            Status = said;

            await Refresh();
        }

        [RelayCommand]
        private async Task ShowLogs()
        {
            if (SelectedContainer is not { } container)
            {
                return;
            }

            Busy = true;
            Logs = "Reading…";

            Logs = await Task.Run(() => Core.Containers.Logs(container));

            Busy = false;
        }

        /// <summary>Turns "follow this container's log" into a saved command.</summary>
        [RelayCommand]
        private void FollowInDeck()
        {
            if (SelectedContainer is not { } container || Adopt is not { } adopt)
            {
                return;
            }

            adopt($"logs: {container.Name}", Core.Containers.FollowCommand(container));

            Status = Strings.Format("RunAddedLogs", container.Name);
        }

        [RelayCommand]
        private void CloseLogs() => Logs = string.Empty;

        public void Dispose()
        {
            timer.Dispose();
            pulse.Stop();
        }
    }
}
