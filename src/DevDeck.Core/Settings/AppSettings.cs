using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevDeck.Core
{
    /// <summary>
    ///  Everything the app remembers between runs, in one serialisable object.
    /// </summary>
    internal sealed class AppSettings
    {
        /// <summary>Krypton palette name. Empty means "use the default".</summary>
        public string Theme { get; set; } = string.Empty;

        /// <summary>
        ///  Language id, or "System". Empty means the same as System, for a file written before
        ///  there was a choice.
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>Workspace folders offered in the drop down.</summary>
        public List<string> Workspaces { get; set; } = [];

        /// <summary>Workspace selected when the app last closed.</summary>
        public string SelectedWorkspace { get; set; } = string.Empty;

        public List<CustomCommand> CustomCommands { get; set; } = [];

        public string SelectedCommand { get; set; } = string.Empty;

        public int WindowWidth { get; set; }

        public int WindowHeight { get; set; }

        /// <summary>Tab the app was on when it last closed, by position. Superseded by
        ///  <see cref="SelectedPageName"/>; kept so an older settings file still opens sensibly.</summary>
        public int SelectedPage { get; set; }

        /// <summary>
        ///  Tab the app was on when it last closed, by name.
        /// </summary>
        /// <remarks>
        ///  By name because positions move: adding the Assistant tab in the middle shifted every
        ///  index after it, so a saved "2" that meant Settings silently became Assistant. A name
        ///  survives any future reordering.
        /// </remarks>
        public string SelectedPageName { get; set; } = string.Empty;

        /// <summary>Stop Windows sleeping while the app is open.</summary>
        public bool KeepAwake { get; set; } = true;

        /// <summary>
        ///  Also hold back the Windows idle clock, so Teams and other chat apps keep showing you as
        ///  available rather than away. Only has an effect while <see cref="KeepAwake"/> is on.
        /// </summary>
        public bool StayAvailable { get; set; } = true;

        /// <summary>
        ///  Whether to offer to install the local AI runtime when the assistant is not reachable.
        ///  Cleared once the user declines, so the offer is made at most once and then lives on the
        ///  Assistant tab's own button instead.
        /// </summary>
        public bool OfferAiSetup { get; set; } = true;

        /// <summary>
        ///  The version whose changelog the user has been shown. A build that differs opens on the
        ///  Changelog page once, so an update always says what it brought.
        /// </summary>
        public string LastSeenVersion { get; set; } = string.Empty;

        /// <summary>
        ///  Endpoint, model and key of the provider in use, mirrored out of <see cref="AiProfiles"/>
        ///  on every save.
        /// </summary>
        /// <remarks>
        ///  Kept as their own properties even though the profiles now hold the same three values,
        ///  so a settings file written by this build still opens in the one before it - and so the
        ///  migration below has something to fold in when the file came from that build.
        /// </remarks>
        public string AiBaseUrl { get; set; } = "http://localhost:11434/v1";

        /// <summary>Model name to ask that endpoint for.</summary>
        public string AiModel { get; set; } = "qwen2.5-coder:7b";

        /// <summary>Which provider the assistant is pointed at, by preset name.</summary>
        public string AiProvider { get; set; } = string.Empty;

        /// <summary>
        ///  One entry per provider the user has touched, each remembering its own endpoint, model
        ///  and key. Switching providers swaps between them instead of overwriting one set.
        /// </summary>
        public List<AiProfile> AiProfiles { get; set; } = [];

        /// <summary>
        ///  Key for the endpoint, in the clear and in memory only. Local models need none.
        /// </summary>
        [JsonIgnore]
        public string AiApiKey { get; set; } = string.Empty;

        /// <summary>
        ///  The only form of the key that reaches the disk: wrapped by DPAPI so a settings file
        ///  copied off this machine, or read by another user on it, does not hand over the key.
        /// </summary>
        [JsonPropertyName("AiApiKeyProtected")]
        public string ProtectedAiApiKey
        {
            get => DataProtection.Protect(AiApiKey);
            set => AiApiKey = DataProtection.Unprotect(value);
        }

        /// <summary>Ask for confirmation before lock / sign out / restart / shut down.</summary>
        public bool ConfirmPowerActions { get; set; } = true;

        /// <summary>Keep the log scrolled to the newest line.</summary>
        public bool AutoScrollLog { get; set; } = true;

        /// <summary>
        ///  Cleanup steps ticked in the memory cleaner, by <see cref="MemoryStep"/> name.
        /// </summary>
        /// <remarks>
        ///  Names rather than numbers, and missing rather than empty for "never chosen": an empty
        ///  list is a real answer - the user unticked everything - and has to survive a restart, so
        ///  it cannot double as the marker for a settings file that predates the feature.
        /// </remarks>
        public List<string>? MemorySteps { get; set; }

        /// <summary>Whether a clean may raise a UAC prompt for the steps that need one.</summary>
        public bool MemoryAllowElevation { get; set; } = true;

        /// <summary>Whether the round memory widget floats on the desktop.</summary>
        public bool MemoryWidgetVisible { get; set; }

        /// <summary>
        ///  Where the widget was left, in screen coordinates.
        /// </summary>
        /// <remarks>
        ///  <see cref="int.MinValue"/> means "never placed", which is not the same as 0: a widget
        ///  really left at the top-left corner has to stay there rather than jumping back to the
        ///  default corner on the next start.
        /// </remarks>
        public int MemoryWidgetX { get; set; } = int.MinValue;

        public int MemoryWidgetY { get; set; } = int.MinValue;

        /// <summary>Widget diameter in pixels. 150 is the middle of the three sizes offered.</summary>
        public int MemoryWidgetSize { get; set; } = 150;

        /// <summary>Widget opacity, 0.35 to 1. Anything below about a third is unclickable.</summary>
        public double MemoryWidgetOpacity { get; set; } = 1.0;

        /// <summary>Pull the widget flush against a screen edge when it is dropped near one.</summary>
        public bool MemoryWidgetSnap { get; set; } = true;

        /// <summary>Clean by itself once memory in use passes <see cref="MemoryAutoThreshold"/>.</summary>
        public bool MemoryAutoClean { get; set; }

        /// <summary>Percentage of physical memory in use that triggers an automatic clean.</summary>
        public int MemoryAutoThreshold { get; set; } = 85;

        /// <summary>
        ///  Shortest gap between two automatic cleans.
        /// </summary>
        /// <remarks>
        ///  Not a nicety. The standby cache is memory doing useful work, and purging it every minute
        ///  means the machine re-reads from disk everything it had already cached - slower overall,
        ///  while the gauge looks better. Ten minutes is a floor that keeps the feature honest.
        /// </remarks>
        public int MemoryAutoCooldownMinutes { get; set; } = 10;

        /// <summary>Only clean automatically once the machine has been left alone.</summary>
        public bool MemoryAutoWhenIdle { get; set; } = true;

        /// <summary>How long the machine must have been idle before an automatic clean runs.</summary>
        public int MemoryAutoIdleSeconds { get; set; } = 60;

        /// <summary>
        ///  Processes never trimmed, never closed, by name without the extension.
        /// </summary>
        /// <remarks>
        ///  Trimming the working set of something that is genuinely busy - a database, a build, an
        ///  IDE holding a large solution - buys a few megabytes and costs a stall while it faults
        ///  every page back in. This is the list of things to leave alone.
        /// </remarks>
        public List<string> MemoryExcluded { get; set; } = [];

        /// <summary>Leave processes that are busy right now out of a trim.</summary>
        public bool MemorySkipBusy { get; set; } = true;

        /// <summary>Show every process in the cleaner's list, not only the ones with a window.</summary>
        public bool MemoryShowAllProcesses { get; set; }

        /// <summary>Keep the process list's memory and CPU figures up to date while it is open.</summary>
        public bool MemoryLiveProcesses { get; set; } = true;

        /// <summary>Clean from anywhere with a hotkey.</summary>
        public bool MemoryHotkeyEnabled { get; set; }

        /// <summary>The hotkey, as <see cref="Keys"/> including its modifiers.</summary>
        public int MemoryHotkey { get; set; } = (int)(Keys.Control | Keys.Alt | Keys.M);

        /// <summary>Show memory in use on the tray icon, instead of the plain app icon.</summary>
        public bool MemoryTrayReadout { get; set; }

        /// <summary>Start DevDeck when Windows starts.</summary>
        public bool StartWithWindows { get; set; }

        /// <summary>
        ///  The last few cleans, newest last, as "when|freed bytes".
        /// </summary>
        /// <remarks>
        ///  Kept so the window can answer "is this feature doing anything for me" across restarts,
        ///  which is the only honest way to judge a memory cleaner. Capped when written.
        /// </remarks>
        public List<string> MemoryHistory { get; set; } = [];

        /// <summary>The saved requests in the HTTP panel.</summary>
        /// <remarks>
        ///  In settings rather than a file of their own because they are configuration in the same
        ///  sense the commands are: a small, hand-made list the user expects to find where they
        ///  left it. Headers are stored as typed, so a request with an Authorization header holds
        ///  whatever was put in it - the same caveat the command bodies carry, and the reason
        ///  <c>{{secret:NAME}}</c> exists for both.
        /// </remarks>
        public List<HttpRequest> Requests { get; set; } = [];

        /// <summary>
        ///  The named environments the HTTP panel switches between.
        /// </summary>
        /// <remarks>
        ///  Local, Staging, Production and whatever else the work needs. Each holds a list of
        ///  values that <c>{{name}}</c> in a URL, header or body resolves from, so one saved
        ///  request serves every environment instead of being copied once per environment and
        ///  left to drift.
        ///
        ///  Secret values are <b>not</b> here. A value marked secret keeps an empty string in this
        ///  file and its real contents in <see cref="SecretVault"/>, wrapped by the platform - so
        ///  this file stays something that can be synced, backed up or attached to a bug report
        ///  without leaking a bearer token. That is the whole reason the flag exists.
        /// </remarks>
        public List<HttpEnvironment> Environments { get; set; } = [];

        /// <summary>
        ///  The environment last selected, by name.
        /// </summary>
        /// <remarks>
        ///  By name rather than by index, so that adding or reordering environments does not
        ///  silently change which one is active. A name that no longer exists selects none, which
        ///  leaves every <c>{{variable}}</c> visibly unresolved rather than quietly sending a
        ///  request somewhere nobody chose.
        /// </remarks>
        public string ActiveEnvironment { get; set; } = string.Empty;

        /// <summary>
        ///  How wide the saved-request list on the HTTP page was left.
        /// </summary>
        /// <remarks>
        ///  The same reasoning as the pane heights: someone with grouped requests and long names
        ///  wants it wide, someone with six wants the room for the request itself. Zero means
        ///  "not set" and takes the default.
        /// </remarks>
        public double HttpTreeWidth { get; set; }

        /// <summary>The groups collapsed in the request list, by name.</summary>
        /// <remarks>
        ///  Collapsed rather than expanded is stored because a new group should arrive open: the
        ///  user has just put something in it and expects to see it there.
        /// </remarks>
        public List<string> HttpCollapsedGroups { get; set; } = [];

        /// <summary>The sections folded shut on the Automation page: "chains", "watches", "deck".</summary>
        /// <remarks>
        ///  Closed rather than open is stored, for the same reason as the groups above: a section
        ///  nobody has touched should arrive open.
        /// </remarks>
        public List<string> AutomationClosed { get; set; } = [];

        /// <summary>
        ///  The rail's sections in the order the user put them, by section name.
        /// </summary>
        /// <remarks>
        ///  Empty until the rail is rearranged, which keeps the shipped order. A section this list
        ///  does not name - one added by a later version - goes at the end rather than being lost.
        /// </remarks>
        public List<string> SectionOrder { get; set; } = [];

        /// <summary>How tall the chains section on the Automation page is, as last dragged.</summary>
        /// <remarks>
        ///  The section used to be as tall as its contents - the step list, the options and a
        ///  320-pixel output log - which pushed the watches and the project deck a screen further
        ///  down than anyone wanted them. Now it is a fixed height the user drags, and the editor
        ///  scrolls inside it. Zero means "not set", so a fresh install takes the view's default.
        /// </remarks>
        public double AutomationChainsHeight { get; set; }

        /// <summary>
        ///  Starter items added after the first release, already offered to this install.
        /// </summary>
        /// <remarks>
        ///  Recorded so each is added once: an install that already existed gets the new example,
        ///  and a user who deletes it does not find it back on the next launch.
        /// </remarks>
        public List<string> SeededExtras { get; set; } = [];

        /// <summary>
        ///  Environment variables set on every command the deck runs.
        /// </summary>
        /// <remarks>
        ///  For the values many scripts want and nobody wants to repeat in each one - a project
        ///  name, a registry, a region. Plain text in settings.json, so the Settings page says to
        ///  keep tokens in the vault instead. A command's own parameters are applied after these,
        ///  so a command can override one for itself.
        /// </remarks>
        public List<CommandArgument> CommandVariables { get; set; } = [];

        /// <summary>The URLs the Running page asks on every refresh.</summary>
        public List<string> HealthChecks { get; set; } = [];

        /// <summary>Whether the Running page re-reads itself on a timer while it is open.</summary>
        public bool RunningAutoRefresh { get; set; } = true;

        /// <summary>
        ///  The icon chosen for each request group, keyed by group name.
        /// </summary>
        /// <remarks>
        ///  Here rather than on a group object because there is no group object: a group is the
        ///  distinct set of Group values across the saved requests, assembled for drawing and
        ///  thrown away. Putting the icon on every request in the group instead would mean the
        ///  same fact stored a dozen times, disagreeing the moment one request is moved.
        ///
        ///  Keyed by name, so renaming a group loses its icon. That is the same trade the grouping
        ///  itself already makes - the name is the identity - and the cost is one icon rather than
        ///  a group of requests going missing.
        /// </remarks>
        public Dictionary<string, string> HttpGroupIcons { get; set; } = [];

        /// <summary>
        ///  How tall the three panes of the HTTP page were left.
        /// </summary>
        /// <remarks>
        ///  Stored because the right split is a property of the work rather than of the app: a
        ///  request with thirty imported headers wants the top pane, a GET that returns a large
        ///  document wants the bottom one, and someone doing either does it all afternoon. A layout
        ///  that resets on every launch asks them to say so again each morning.
        ///
        ///  Kept as heights rather than as splitter positions, because the panel is also resized by
        ///  the window around it, and proportions restored into a window of a different size put
        ///  the splitters somewhere nobody chose.
        ///
        ///  Two of the three panes, because the third is the remainder: the reply takes whatever
        ///  the other two leave, which is what lets a taller window give its extra room to the part
        ///  the request was sent to see. Dragging either splitter still resizes it.
        ///
        ///  Zero means "not set", which is what makes a fresh install take the defaults in the view
        ///  instead of collapsing all three panes to nothing.
        /// </remarks>
        public double HttpHeaderPaneHeight { get; set; }

        /// <inheritdoc cref="HttpHeaderPaneHeight"/>
        public double HttpBodyPaneHeight { get; set; }

        /// <summary>
        ///  Whether the clipboard history records anything at all.
        /// </summary>
        /// <remarks>
        ///  Off until the user turns it on, and deliberately so: a log of everything copied is
        ///  useful and is also a genuinely sensitive file to have created without being asked.
        /// </remarks>
        public bool ClipboardCapture { get; set; }

        /// <summary>
        ///  The chains: named sequences of commands run one after another.
        /// </summary>
        /// <remarks>
        ///  Beside the commands rather than inside them, because a chain names its steps instead of
        ///  copying them - editing a command changes every chain that uses it, which is the whole
        ///  point of storing the name.
        /// </remarks>
        public List<CommandChain> Chains { get; set; } = [];

        /// <summary>The rules that run a command when files change.</summary>
        public List<WatchRule> Watches { get; set; } = [];

        /// <summary>
        ///  The key combination that brings the deck up from anywhere, or empty for none.
        /// </summary>
        /// <remarks>
        ///  Off until asked for. A system-wide key taken without permission is taken from whatever
        ///  else the user had bound to it, and finding out which application stole a shortcut is a
        ///  genuinely unpleasant afternoon.
        /// </remarks>
        public string Hotkey { get; set; } = string.Empty;

        /// <summary>
        ///  The saved prompts the assistant offers.
        /// </summary>
        /// <remarks>
        ///  Here rather than beside the conversations because they are not transcript: they are a
        ///  handful of short strings the user maintains, exactly like the commands, and they want
        ///  to be editable in the same place everything else about the app is.
        /// </remarks>
        public List<SavedPrompt> Prompts { get; set; } = [];

        /// <summary>
        ///  Whether a command that ran for a while announces that it finished.
        /// </summary>
        /// <remarks>
        ///  The point of a long command is that the user goes and does something else, which means
        ///  the app is behind another window when the thing they are waiting for happens.
        /// </remarks>
        public bool NotifyOnFinish { get; set; } = true;

        /// <summary>How long a command must run before finishing is worth saying out loud.</summary>
        /// <remarks>
        ///  Short commands finish while the user is still looking at them, and a notification for
        ///  something already on screen is just noise.
        /// </remarks>
        public int NotifyAfterSeconds { get; set; } = 20;

        /// <summary>Whether to look for a .devdeck.json when the workspace changes.</summary>
        public bool OfferProjectDeck { get; set; } = true;

        /// <summary>
        ///  The stored settings for a provider, or a new entry holding the preset's own defaults.
        /// </summary>
        public AiProfile AiProfileFor(AiPreset preset)
        {
            AiProfile? saved = AiProfiles.FirstOrDefault(
                profile => string.Equals(profile.Provider, preset.Name, StringComparison.OrdinalIgnoreCase));

            if (saved is not null)
            {
                return saved;
            }

            AiProfile created = AiProfile.FromPreset(preset);
            AiProfiles.Add(created);

            return created;
        }

        /// <summary>
        ///  The profile the assistant should start on, creating it from the single endpoint the
        ///  settings file used to hold when this is the first run of a build that has profiles.
        /// </summary>
        public AiProfile CurrentAiProfile()
        {
            AiPreset? chosen = AiPresets.ByName(AiProvider);

            if (chosen is not null)
            {
                AiProfile? saved = AiProfiles.FirstOrDefault(
                    profile => string.Equals(profile.Provider, chosen.Name, StringComparison.OrdinalIgnoreCase));

                if (saved is not null)
                {
                    return saved;
                }
            }

            // Either a file from before providers had their own entries, or a first run. Fold the
            // one endpoint into the profile of whichever preset it came from, so an existing setup
            // - key included - carries over instead of being reset to the defaults.
            AiProfile profile = AiProfileFor(AiPresets.Match(AiBaseUrl));

            if (AiBaseUrl.Length > 0)
            {
                profile.BaseUrl = AiBaseUrl;
                profile.Model = AiModel;
            }

            if (AiApiKey.Length > 0)
            {
                profile.ApiKey = AiApiKey;
            }

            AiProvider = profile.Provider;

            return profile;
        }

        /// <summary>
        ///  The settings file, chosen by <see cref="SettingsPath"/> rather than assumed to sit
        ///  beside the executable - which is what blocked any macOS build.
        /// </summary>
        public static string Path => SettingsPath.Current;

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        /// <summary>
        ///  Loads the settings file, falling back to defaults if it is missing or unreadable.
        ///  Never throws: a bad settings file must not stop the app starting.
        /// </summary>
        public static AppSettings Load()
        {
            if (File.Exists(Path))
            {
                try
                {
                    AppSettings? loaded =
                        JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), Options);

                    if (loaded is not null)
                    {
                        loaded.Mend();
                        return loaded;
                    }
                }
                catch (Exception exception)
                {
                    // Fall through to the recovery below rather than starting with no settings.
                    AppLog.Instance.Failure("settings", "Could not read the settings file", exception);
                }

                // The file is there but unusable - hand-edited, truncated by a crash, or from a
                // newer build. Keep it before writing defaults over the top: a file that cannot be
                // parsed may still hold commands the user wants back.
                Quarantine();
            }
            else
            {
                // First run: adopt V1's settings if its file is sitting next to the executable.
                AppSettings? imported = SettingsMigration.TryImport(SettingsPath.Home);

                if (imported is not null)
                {
                    imported.Save();
                    return imported;
                }
            }

            // Write the defaults out now rather than at shutdown, so the file exists to be looked
            // at and edited from the first run, and a crash on day one does not leave nothing.
            //
            // The starters are seeded only here, on a genuinely fresh start. Seeding on every
            // load would resurrect every one the user had deliberately deleted.
            //
            // All of them together, because a chain whose steps do not exist is not a
            // demonstration of anything: the starters are the only commands a first run has, so
            // they are the only ones a shipped chain can be built from.
            AppSettings defaults = Fresh();
            defaults.Save();

            return defaults;
        }

        /// <summary>
        ///  Repairs shipped commands that were fixed after this file was written.
        /// </summary>
        /// <remarks>
        ///  A settings file is written once and read forever, so a bug in a starter command outlives
        ///  the fix for it: <see cref="Fresh"/> only runs when there is no file at all. Two of the
        ///  starters were in that position - one printed nothing, one froze the app - and no amount
        ///  of correcting the source was ever going to reach an install that already had them.
        ///
        ///  Saved straight away rather than at shutdown, so a run that crashes still leaves the
        ///  repair behind, and logged by name because a command changing under the user is
        ///  something they are entitled to find an explanation for.
        /// </remarks>
        private void Mend()
        {
            List<string> mended = StarterCommands.Refresh(CustomCommands);

            bool firstTime = !SeededExtras.Contains(StarterCommands.SecretExampleName, StringComparer.Ordinal);

            StarterCommands.SeedSecret(firstTime);

            if (StarterCommands.SeedVariables(CommandVariables, firstTime))
            {
                TrySave();
            }

            if (firstTime)
            {
                if (!CustomCommands.Any(command => command.Name == StarterCommands.SecretExampleCommand))
                {
                    CustomCommands.Add(StarterCommands.SecretExample());
                    mended.Add(StarterCommands.SecretExampleCommand);
                }

                SeededExtras = [.. SeededExtras, StarterCommands.SecretExampleName];

                TrySave();
            }

            // The parameter and chain examples, offered once to an install that predates them.
            // By name, so nothing already there is replaced and a deleted one is not brought back.
            if (!SeededExtras.Contains(StarterExamples.SeedKey, StringComparer.Ordinal))
            {
                CustomCommands.AddRange(StarterExamples.Commands()
                    .Where(example => !CustomCommands.Any(command => command.Name == example.Name)));

                Chains.AddRange(StarterExamples.Chains()
                    .Where(example => !Chains.Any(chain => chain.Name == example.Name)));

                SeededExtras = [.. SeededExtras, StarterExamples.SeedKey];

                AppLog.Instance.Info("settings", "Added the Example: commands and chains.");

                TrySave();
            }

            if (mended.Count == 0)
            {
                return;
            }

            AppLog.Instance.Info("settings",
                $"Updated {string.Join(", ", mended)} to the version this build ships - "
                + "the copy on file was one that did not work.");

            try
            {
                Save();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The repair is in memory and works for this run; it will be tried again next time.
                AppLog.Instance.Failure("settings", "Could not save the repaired commands", exception);
            }
        }

        /// <summary>Set by <see cref="Reset"/>: this process's settings are no longer the file's.</summary>
        /// <remarks>
        ///  Static rather than on the instance, because the object that must stop saving is the old
        ///  one every panel is holding, not the fresh one the reset made.
        /// </remarks>
        private static bool superseded;

        /// <summary>
        ///  A settings object as a first run would have made it, saved over whatever is there.
        /// </summary>
        /// <remarks>
        ///  Its own method so that the reset action and the first run cannot drift apart. They had
        ///  to be the same thing or the promise "start again from scratch" quietly becomes "start
        ///  again from scratch, minus whatever the seeder gained since this method was written".
        ///
        ///  The old file is set aside rather than deleted, under the same name a corrupt one gets.
        ///  A reset is the most destructive thing in the app - every command, chain, request and
        ///  environment at once - and it costs nothing to leave the previous file on disk for the
        ///  user who meant to reset something smaller.
        ///
        ///  Secrets are deliberately not touched. They live in the OS credential store rather than
        ///  in this file, they are keyed by environment and variable name, and an environment that
        ///  is seeded again with the same name will find its old value still there - which is the
        ///  helpful outcome. Reaching into the keychain to delete entries the app may not have put
        ///  there is not something a settings reset should do quietly.
        /// </remarks>
        public static AppSettings Reset()
        {
            if (File.Exists(Path))
            {
                Quarantine("replaced", "A reset was asked for.");
            }

            AppSettings fresh = Fresh();
            fresh.Save();

            // From here on this process is only waiting to be replaced, and every panel in it still
            // holds the old object. Any save between now and the exit - a tick box, a chain
            // finishing, a request's last response - would write that stale copy straight back
            // over the file just made, undoing the reset without a word.
            superseded = true;

            return fresh;
        }

        /// <summary>Everything a fresh install starts with, and nothing else.</summary>
        private static AppSettings Fresh()
        {
            StarterCommands.SeedSecret(firstTime: true);

            List<CommandArgument> variables = [];
            StarterCommands.SeedVariables(variables, firstTime: true);

            return new()
            {
                CommandVariables = variables,
                CustomCommands = [.. StarterCommands.For(), .. StarterExamples.Commands()],
                Chains = [.. StarterCommands.Chains(), .. StarterExamples.Chains()],
                Watches = StarterCommands.Watches(),
                Environments = StarterCommands.Environments(),
                SeededExtras = [StarterCommands.SecretExampleName, StarterExamples.SeedKey],
            };
        }

        /// <summary>Saves, and leaves a failure for the next launch to try again.</summary>
        private void TrySave()
        {
            try
            {
                Save();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                AppLog.Instance.Failure("settings", "Could not save the seeded examples", exception);
            }
        }

        /// <summary>Moves the settings file aside instead of overwriting it.</summary>
        /// <remarks>
        ///  Both callers lose the user's settings and both want the old file kept, but they are not
        ///  the same event and the file name should not claim they are. A file set aside because a
        ///  reset was asked for is not broken, and labelling it so sends anyone reading the folder
        ///  looking for a fault that never happened.
        /// </remarks>
        private static void Quarantine(string tag = "broken", string why = "The settings file could not be read.")
        {
            try
            {
                string kept = $"{Path}.{tag}-{DateTime.Now:yyyyMMdd-HHmmss}";
                File.Move(Path, kept, overwrite: true);

                // Said out loud, because the user is about to see an app with none of their
                // commands in it and the kept file is the only way back.
                AppLog.Instance.Warn(
                    "settings",
                    $"{why} It was kept as {System.IO.Path.GetFileName(kept)} and defaults were used.");
            }
            catch (Exception exception)
            {
                // Nothing better to do; the defaults below still give a working app.
                AppLog.Instance.Failure("settings", "Could not set the old settings file aside", exception);
            }
        }

        /// <summary>
        ///  Writes the settings file. Writes to a temporary file first and then replaces, so an
        ///  interrupted save cannot leave a half-written file behind.
        /// </summary>
        public void Save()
        {
            if (superseded)
            {
                return;
            }

            try
            {
                System.IO.Directory.CreateDirectory(SettingsPath.Directory);

                string temp = Path + ".tmp";
                File.WriteAllBytes(temp, JsonSerializer.SerializeToUtf8Bytes(this, Options));
                File.Move(temp, Path, overwrite: true);
            }
            catch (Exception exception)
            {
                // Losing preferences is not worth interrupting the user over - but it should not
                // be invisible either, since everything saved afterwards is lost with it.
                AppLog.Instance.Failure("settings", "Could not save", exception);
            }
        }
    }
}
