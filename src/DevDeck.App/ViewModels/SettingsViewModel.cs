using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  The settings that V3 actually honours, and nothing it does not.
    /// </summary>
    /// <remarks>
    ///  Deliberately shorter than V2's settings tab. What is not built is absent rather than
    ///  present and inert - a switch that does nothing is worse than a missing one.
    /// </remarks>
    internal sealed partial class SettingsViewModel : ObservableObject
    {
        private readonly AppSettings settings;

        public SettingsViewModel(AppSettings settings)
        {
            this.settings = settings;

            theme = AppTheme.Parse(settings.Theme);
            effect = Effects.FirstOrDefault(choice => string.Equals(choice.Id, settings.ThemeEffect, StringComparison.OrdinalIgnoreCase))
                ?? Effects[0];
            language = AppLanguage.Parse(settings.Language);
            keepAwake = settings.KeepAwake;
            stayAvailable = settings.StayAvailable;
            confirmPowerActions = settings.ConfirmPowerActions;
            autoScrollLog = settings.AutoScrollLog;
            notifyOnFinish = settings.NotifyOnFinish;
            notifyAfterSeconds = settings.NotifyAfterSeconds;

            // The real file, rather than the folder the executable happens to be in. Those are the
            // same thing on a portable Windows install and nowhere else, and the old readout was
            // therefore wrong on exactly the platforms where the answer is hard to guess.
            SettingsPath = Core.SettingsPath.Current;
            SecretsPath = SecretVault.Path;

            Secrets = new ObservableCollection<string>(SecretVault.Instance.Names);

            Variables = new ObservableCollection<ArgumentRow>(settings.CommandVariables.Select(Watch));

            hotkey = settings.Hotkey;
            hotkeyStatus = settings.Hotkey.Length > 0 && GlobalHotkey.Current.Length > 0
                ? Strings.Format("SetHotkeyHeld", settings.Hotkey)
                : string.Empty;
        }

        /// <summary>
        ///  The assistant's endpoint, which the AI card edits in place.
        /// </summary>
        /// <remarks>
        ///  The assistant itself rather than a second copy of its settings. Settings are not read
        ///  back after startup, so two panels each holding their own copy would disagree until
        ///  the next launch - and the one the user had just changed would not be the one asked.
        /// </remarks>
        public AssistantViewModel? Ai { get; init; }

        /// <summary>The environment variables every command gets, as editable rows.</summary>
        public ObservableCollection<ArgumentRow> Variables { get; }

        public bool HasVariables => Variables.Count > 0;

        /// <summary>A row that saves itself as it is typed into, like every other setting here.</summary>
        private ArgumentRow Watch(CommandArgument variable)
        {
            ArgumentRow row = new(variable);
            row.PropertyChanged += (_, _) => settings.Save();

            return row;
        }

        [RelayCommand]
        private void AddVariable()
        {
            CommandArgument added = new();

            settings.CommandVariables.Add(added);
            Variables.Add(Watch(added));
            settings.Save();

            OnPropertyChanged(nameof(HasVariables));
        }

        [RelayCommand]
        private void RemoveVariable(ArgumentRow? row)
        {
            if (row is null)
            {
                return;
            }

            settings.CommandVariables.Remove(row.Source);
            Variables.Remove(row);
            settings.Save();

            OnPropertyChanged(nameof(HasVariables));
        }

        public IReadOnlyList<ThemePalette> Themes { get; } = AppTheme.All;

        /// <summary>"Match the theme", "None", then each effect, in the app's language.</summary>
        public IReadOnlyList<EffectChoice> Effects { get; } =
            [.. ThemeEffect.Choices.Select(id => new EffectChoice(id))];

        /// <summary>
        ///  The background effect, applied the moment it is picked - for the theme's reason: an
        ///  effect you cannot see until you press a button is one you cannot choose.
        /// </summary>
        [ObservableProperty]
        private EffectChoice effect;

        partial void OnEffectChanged(EffectChoice value)
        {
            settings.ThemeEffect = value.Id == ThemeEffect.MatchTheme ? null : value.Id;
            ThemeManager.SetEffect(settings.ThemeEffect);
            Save();
        }

        public IReadOnlyList<LanguageChoice> Languages { get; } = AppLanguage.All;

        /// <summary>Where the file lives, because "portable app" is only obvious once you see it.</summary>
        public string SettingsPath { get; }

        /// <summary>Where the secrets live, which is deliberately not the same file.</summary>
        public string SecretsPath { get; }

        /// <summary>Whether this install keeps its settings beside the executable.</summary>
        public bool IsPortable => Core.SettingsPath.IsPortable;

        public string Storage => IsPortable
            ? Strings.Text("SetStoragePortable")
            : Strings.Text("SetStorageConfig");

        /// <summary>
        ///  The names of the stored secrets. Never the values.
        /// </summary>
        /// <remarks>
        ///  A settings page that will show a secret back is a settings page that leaks one over a
        ///  shoulder or a screen share. Setting one is write-only here: the value goes in and the
        ///  only thing that can read it back is a command that names it.
        /// </remarks>
        public ObservableCollection<string> Secrets { get; }

        public bool HasSecrets => Secrets.Count > 0;

        [ObservableProperty]
        private string secretName = string.Empty;

        [ObservableProperty]
        private string secretValue = string.Empty;

        [ObservableProperty]
        private string? selectedSecret;

        [ObservableProperty]
        private string secretNote = string.Empty;

        public string Version => AppVersion.Display;

        [ObservableProperty]
        private ThemePalette theme;

        [ObservableProperty]
        private LanguageChoice language;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AwakeDetail))]
        private bool keepAwake;

        /// <summary>
        ///  Whether to hold back the idle clock as well as the power request.
        /// </summary>
        /// <remarks>
        ///  A second switch rather than part of the first, because the two do different things and
        ///  one of them types. Injecting a keypress is a reasonable thing to want and an
        ///  unreasonable thing to do without being asked.
        /// </remarks>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AwakeDetail))]
        private bool stayAvailable;

        /// <summary>Whether holding back the idle clock is possible on this system at all.</summary>
        public bool CanStayAvailable => Core.KeepAwake.IsPresenceAvailable;

        /// <summary>What keep-awake is actually doing, rather than what was asked for.</summary>
        public string AwakeDetail => Core.KeepAwake.Detail;

        [ObservableProperty]
        private bool confirmPowerActions;

        [ObservableProperty]
        private bool autoScrollLog;

        [ObservableProperty]
        private bool notifyOnFinish;

        [ObservableProperty]
        private int notifyAfterSeconds;

        [ObservableProperty]
        private string status = Strings.Text("SetSavedAutomatically");

        partial void OnThemeChanged(ThemePalette value)
        {
            // Applied at once rather than on save: a theme you cannot see until you press a button
            // is a theme you cannot choose. ThemeManager swaps the palette dictionary, and every
            // view reads it through DynamicResource, so the whole window repaints in place.
            if (Application.Current is { } app)
            {
                ThemeManager.Apply(app, value);
            }

            settings.Theme = value.Id;
            Save();
        }

        /// <summary>
        ///  Switches the app's language, at once and everywhere.
        /// </summary>
        /// <remarks>
        ///  The same argument as the theme: a language you cannot see until you restart is one
        ///  people try, see nothing happen, and set back. Strings.Use raises a change for every
        ///  string, and every label in every panel is bound through that.
        ///
        ///  What is stored is the choice rather than what it resolved to, so "follow the system"
        ///  stays a live rule and not a snapshot of whatever the desktop was set to on the day.
        /// </remarks>
        partial void OnLanguageChanged(LanguageChoice value)
        {
            Strings.Use(value);

            // The notes on this page are computed properties reading the table, and nothing else
            // tells them the table underneath them has changed. Raised by hand rather than through
            // Strings.Changed, because this view model is the only thing that can cause it.
            OnPropertyChanged(nameof(Storage));
            OnPropertyChanged(nameof(SchemeNote));
            OnPropertyChanged(nameof(HotkeyNote));
            OnPropertyChanged(nameof(AutoStartNote));

            settings.Language = value.Id;
            Save();
        }

        partial void OnKeepAwakeChanged(bool value)
        {
            settings.KeepAwake = value;
            ApplyAwake();
            Save();
        }

        partial void OnStayAvailableChanged(bool value)
        {
            settings.StayAvailable = value;
            ApplyAwake();
            Save();
        }

        /// <summary>
        ///  Pushes both halves of keep-awake at the platform.
        /// </summary>
        /// <remarks>
        ///  Called on every change rather than only on the way up, because turning it off has to
        ///  reach the OS as surely as turning it on: a request left asserted after the tick box was
        ///  cleared is a machine that never sleeps again for reasons the user has no way to find.
        ///
        ///  The readout is refreshed a beat later. The keeper thread does the asking, and what it
        ///  reports is what actually happened rather than what was requested - which is the whole
        ///  point of showing it.
        /// </remarks>
        private void ApplyAwake()
        {
            Core.KeepAwake.Set(KeepAwake, StayAvailable);

            _ = Task.Delay(250).ContinueWith(
                _ => Dispatcher.UIThread.Post(() => OnPropertyChanged(nameof(AwakeDetail))),
                TaskScheduler.Default);
        }

        partial void OnConfirmPowerActionsChanged(bool value)
        {
            settings.ConfirmPowerActions = value;
            Save();
        }

        partial void OnAutoScrollLogChanged(bool value)
        {
            settings.AutoScrollLog = value;
            Save();
        }

        partial void OnNotifyOnFinishChanged(bool value)
        {
            settings.NotifyOnFinish = value;
            Save();
        }

        partial void OnNotifyAfterSecondsChanged(int value)
        {
            // Clamped rather than validated: a spinner is the wrong place to refuse an answer, and
            // zero would announce every command the moment it finished.
            settings.NotifyAfterSeconds = Math.Clamp(value, 1, 3600);
            Save();
        }

        /// <summary>
        ///  Stores a secret, and forgets the typed value immediately.
        /// </summary>
        /// <remarks>
        ///  The box is cleared as part of storing rather than left for the user to clear, so the
        ///  value is not sitting on screen behind whatever they do next.
        /// </remarks>
        [RelayCommand]
        private void AddSecret()
        {
            if (string.IsNullOrWhiteSpace(SecretName) || SecretValue.Length == 0)
            {
                SecretNote = Strings.Text("SetSecretNeedsBoth");

                return;
            }

            string name = SecretName.Trim();

            SecretVault.Instance.Set(name, SecretValue);

            if (!Secrets.Contains(name))
            {
                Secrets.Add(name);
            }

            SecretName = string.Empty;
            SecretValue = string.Empty;

            OnPropertyChanged(nameof(HasSecrets));

            SecretNote = Strings.Format("SetSecretSaved", name);
        }

        [RelayCommand]
        private void RemoveSecret()
        {
            if (SelectedSecret is not { } doomed)
            {
                return;
            }

            SecretVault.Instance.Remove(doomed);
            Secrets.Remove(doomed);
            SelectedSecret = null;

            OnPropertyChanged(nameof(HasSecrets));

            SecretNote = Strings.Format("SetSecretRemoved", doomed);
        }

        /// <summary>Where the terminal launcher would go, shown whether or not one is there yet.</summary>
        public string ShimPath => CliShim.File;

        [ObservableProperty]
        private bool hasShim = CliShim.Exists;

        /// <summary>Whether this platform can claim devdeck:// from a running process at all.</summary>
        public bool CanRegisterScheme => UrlScheme.CanRegister;

        public string SchemeNote => CanRegisterScheme
            ? Strings.Text("SetSchemeCan")
            : Strings.Text("SetSchemeCannot");

        [ObservableProperty]
        private string linkStatus = string.Empty;

        [RelayCommand]
        private void RegisterScheme() => LinkStatus = UrlScheme.Register().Message;

        [RelayCommand]
        private void InstallShim()
        {
            LinkStatus = CliShim.Install().Message;
            HasShim = CliShim.Exists;
        }

        [RelayCommand]
        private void RemoveShim()
        {
            LinkStatus = CliShim.Remove().Message;
            HasShim = CliShim.Exists;
        }

        #region Starting with the system
        /// <summary>Whether this platform can be set to start the deck at sign-in.</summary>
        public bool CanAutoStart => AutoStart.IsAvailable;

        public string AutoStartNote => CanAutoStart
            ? Strings.Text("SetAutoStartNote")
            : Strings.Text("SetAutoStartCannot");

        /// <summary>Read from the system, not the settings file, so it shows what is true.</summary>
        [ObservableProperty]
        private bool startWithSystem = AutoStart.IsEnabled;

        [ObservableProperty]
        private string autoStartStatus = string.Empty;

        partial void OnStartWithSystemChanged(bool value)
        {
            SchemeResult result = AutoStart.Set(value);

            AutoStartStatus = result.Message;

            // Put the tick back to what the system now says, so a refusal does not leave a box
            // claiming something that did not happen. SetProperty raises nothing when it matches.
            bool actual = AutoStart.IsEnabled;

            if (actual != value)
            {
                Dispatcher.UIThread.Post(() => StartWithSystem = actual);
            }
        }
        #endregion

        #region The key that works from anywhere
        /// <summary>Claims the hotkey. Set by the app, which owns the window to raise.</summary>
        /// <remarks>
        ///  Through a callback rather than called here because registering a key means nothing on
        ///  its own - what the key does is bring a window up, and a settings page has no window.
        /// </remarks>
        public Func<string, bool>? ClaimHotkey { get; set; }

        public bool CanUseHotkey => GlobalHotkey.IsAvailable;

        /// <summary>The honest explanation, and what to do instead, on the platforms that cannot.</summary>
        public string HotkeyNote => CanUseHotkey
            ? Strings.Text("SetHotkeyNote")
            : GlobalHotkey.Unavailable;

        [ObservableProperty]
        private string hotkey = string.Empty;

        [ObservableProperty]
        private string hotkeyStatus = string.Empty;

        [RelayCommand]
        private void SetHotkey()
        {
            string wanted = Hotkey.Trim();

            if (wanted.Length == 0)
            {
                ClearHotkey();
                return;
            }

            if (!GlobalHotkey.Parse(wanted, out _, out _))
            {
                HotkeyStatus = Strings.Format("SetHotkeyUnreadable", wanted);
                return;
            }

            if (ClaimHotkey?.Invoke(wanted) != true)
            {
                // Almost always another application holding it. Saying which one is not something
                // Windows will tell us, so the advice is the useful part.
                HotkeyStatus = Strings.Format("SetHotkeyTaken", wanted);
                return;
            }

            settings.Hotkey = wanted;
            settings.Save();

            HotkeyStatus = Strings.Format("SetHotkeyClaimed", wanted);
        }

        [RelayCommand]
        private void ClearHotkey()
        {
            GlobalHotkey.Unregister();

            settings.Hotkey = string.Empty;
            settings.Save();

            Hotkey = string.Empty;
            HotkeyStatus = Strings.Text("SetHotkeyNone");
        }
        #endregion

        #region Updates
        /// <summary>What the update check found, or how it failed.</summary>
        [ObservableProperty]
        private string updateStatus = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CheckForUpdateCommand))]
        private bool isChecking;

        /// <summary>Where a newer build was found, if there is one.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUpdate))]
        private string updateUrl = string.Empty;

        public bool HasUpdate => UpdateUrl.Length > 0;

        /// <summary>What every version between this build and the newest one changed.</summary>
        public ObservableCollection<ChangelogEntry> Changes { get; } = [];

        public bool HasChanges => Changes.Count > 0;

        /// <summary>Opens the release page. Set by the view, which owns the top level.</summary>
        public Action<string>? OpenUrl { get; set; }

        [RelayCommand(CanExecute = nameof(CanCheckForUpdate))]
        private async Task CheckForUpdate()
        {
            IsChecking = true;
            UpdateStatus = "Asking…";
            UpdateUrl = string.Empty;
            Changes.Clear();
            OnPropertyChanged(nameof(HasChanges));

            try
            {
                Release? found = await Updates.LatestAsync(CancellationToken.None);

                if (found is null)
                {
                    UpdateStatus = Strings.Text("SetNoRelease");
                }
                else if (found.IsNewerThan(AppVersion.Current))
                {
                    UpdateUrl = found.Url;
                    UpdateStatus = Strings.Format("SetUpdateAvailable", found.Version, AppVersion.Display);

                    IReadOnlyList<ChangelogEntry> changes = await Changelog.SinceAsync(found.Version, CancellationToken.None);

                    // The release's own notes when the changelog could not be read or does not
                    // mention it, so a newer build never arrives with nothing said about it.
                    if (changes.Count == 0 && found.Notes.Trim().Length > 0)
                    {
                        changes = [new ChangelogEntry(found.Version.TrimStart('v', 'V'), Changelog.Plain(found.Notes))];
                    }

                    foreach (ChangelogEntry change in changes)
                    {
                        Changes.Add(change);
                    }

                    OnPropertyChanged(nameof(HasChanges));
                }
                else
                {
                    UpdateStatus = Strings.Format("SetUpToDate", AppVersion.Display);
                }
            }
            catch (Exception exception)
            {
                UpdateStatus = Strings.Format("SetCouldNotCheck", exception.Message);
            }
            finally
            {
                IsChecking = false;
            }
        }

        private bool CanCheckForUpdate() => !IsChecking;

        [RelayCommand]
        private void GetUpdate()
        {
            if (UpdateUrl.Length > 0)
            {
                OpenUrl?.Invoke(UpdateUrl);
            }
        }
        #endregion

        [RelayCommand]
        private void Save()
        {
            settings.Save();
            Status = Strings.Format("SetSavedAt", DateTime.Now.ToString("HH:mm:ss"));
        }

        /// <summary>Set by the view, to put an irreversible question in front of a window.</summary>
        public Func<string, string, string, Task<bool>>? Confirm { get; set; }

        /// <summary>
        ///  Replaces this process with a fresh copy. Set by the app, which owns the lifetime.
        /// </summary>
        /// <remarks>
        ///  Returns false when no new copy could be started, in which case this one keeps running:
        ///  a reset that also closed the only window would leave nothing on screen at all.
        /// </remarks>
        public Func<bool>? Restart { get; set; }

        /// <summary>
        ///  Throws the settings file away and writes the one a first run would have made.
        /// </summary>
        /// <remarks>
        ///  Asks first, through the view, and does nothing at all if the answer is no or if no
        ///  asker was ever attached. That second case is the important one: a view model whose
        ///  callback is missing must fail closed here, because failing open means a misconfigured
        ///  build wipes settings on a single click with no question asked.
        ///
        ///  What is on screen afterwards is still the old settings, and the status line says so
        ///  rather than pretending otherwise. <see cref="AppSettings.Load"/> runs once, at startup,
        ///  and every panel in the app is holding the object it returned - the http requests, the
        ///  chains, the watches, the tray. Swapping that object out underneath them would mean
        ///  rebuilding the entire window, and the first thing that saved afterwards would write the
        ///  stale copy it was still holding back over the fresh file. A restart is both the honest
        ///  answer and the correct one, so the app does it straight away through
        ///  <see cref="Restart"/>. Only when that cannot start a new copy does the status line fall
        ///  back to asking the user to do it by hand.
        /// </remarks>
        [RelayCommand]
        private async Task Reset()
        {
            if (Confirm is not { } ask)
            {
                return;
            }

            bool sure = await ask(
                Strings.Text("SettingsResetAsk"),
                Strings.Text("SettingsResetDetail"),
                Strings.Text("SettingsResetNow"));

            if (!sure)
            {
                return;
            }

            try
            {
                AppSettings.Reset();

                Status = Restart?.Invoke() == true
                    ? Strings.Text("SettingsResetRestarting")
                    : Strings.Text("SettingsResetDone");
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A settings file held open by something, or a folder that has been made read-only
                // by a policy. Neither is a crash, and neither has left anything half-written: the
                // quarantine either moved the file or it did not.
                Status = Strings.Format("SettingsResetFailed", exception.Message);
            }
        }
    }
}
