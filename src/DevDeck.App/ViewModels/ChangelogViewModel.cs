using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One version on the Changelog page.</summary>
    internal sealed record ChangelogRow(string Version, string Notes, bool IsCurrent, bool IsNewer)
    {
        /// <summary>The badge beside the version, if it has one.</summary>
        public string Badge => IsCurrent
            ? Strings.Text("ChangelogThisVersion")
            : IsNewer ? Strings.Text("ChangelogNotInstalled") : string.Empty;

        public bool HasBadge => Badge.Length > 0;
    }

    /// <summary>
    ///  What each version brought: the build's own changelog, and anything newer on request.
    /// </summary>
    /// <remarks>
    ///  The history comes from the changelog embedded in this build, so the page is never empty
    ///  offline and always matches the version running. Newer versions are only looked up when the
    ///  button is pressed, for the same reason the update check in Settings is never automatic.
    /// </remarks>
    internal sealed partial class ChangelogViewModel : ObservableObject
    {
        public ChangelogViewModel()
        {
            foreach (ChangelogEntry entry in Changelog.Bundled)
            {
                Entries.Add(new ChangelogRow(
                    entry.Version,
                    entry.Notes,
                    IsCurrent: entry.Version == AppVersion.Number,
                    IsNewer: false));
            }
        }

        public ObservableCollection<ChangelogRow> Entries { get; } = [];

        public bool IsEmpty => Entries.Count == 0;

        public string Version => AppVersion.Display;

        [ObservableProperty]
        private string status = string.Empty;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CheckCommand))]
        private bool isChecking;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUpdate))]
        private string updateUrl = string.Empty;

        public bool HasUpdate => UpdateUrl.Length > 0;

        /// <summary>Opens the release page. Set by the view, which owns the top level.</summary>
        public Action<string>? OpenUrl { get; set; }

        /// <summary>
        ///  Asks for the newest release and puts every version between it and this one on top.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCheck))]
        private async Task Check()
        {
            IsChecking = true;
            Status = Strings.Text("ChangelogAsking");
            UpdateUrl = string.Empty;

            // Anything a previous press added comes off first, so pressing twice does not list a
            // version twice.
            foreach (ChangelogRow newer in Entries.Where(row => row.IsNewer).ToList())
            {
                Entries.Remove(newer);
            }

            try
            {
                Release? found = await Updates.LatestAsync(CancellationToken.None);

                if (found is null || !found.IsNewerThan(AppVersion.Current))
                {
                    Status = Strings.Format("SetUpToDate", AppVersion.Display);
                    return;
                }

                UpdateUrl = found.Url;

                IReadOnlyList<ChangelogEntry> changes = await Changelog.SinceAsync(found.Version, CancellationToken.None);

                if (changes.Count == 0 && found.Notes.Trim().Length > 0)
                {
                    changes = [new ChangelogEntry(found.Version.TrimStart('v', 'V'), Changelog.Plain(found.Notes))];
                }

                for (int i = 0; i < changes.Count; i++)
                {
                    Entries.Insert(i, new ChangelogRow(changes[i].Version, changes[i].Notes, IsCurrent: false, IsNewer: true));
                }

                Status = Strings.Format("SetUpdateAvailable", found.Version, AppVersion.Display);
            }
            catch (Exception exception)
            {
                Status = Strings.Format("SetCouldNotCheck", exception.Message);
            }
            finally
            {
                IsChecking = false;
                OnPropertyChanged(nameof(IsEmpty));
            }
        }

        private bool CanCheck() => !IsChecking;

        [RelayCommand]
        private void GetUpdate()
        {
            if (UpdateUrl.Length > 0)
            {
                OpenUrl?.Invoke(UpdateUrl);
            }
        }
    }
}
