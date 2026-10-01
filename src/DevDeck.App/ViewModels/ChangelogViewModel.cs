using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One change on the Changelog page, with the mark drawn beside it.</summary>
    internal sealed record ChangeLine(ChangelogItem Item)
    {
        public string Title => Item.Title;

        public string Body => Item.Body;

        public bool HasTitle => Item.Title.Length > 0;

        /// <summary>A tick for a repair, a star for something new. See <see cref="ChangelogItem.IsFix"/>.</summary>
        public Avalonia.Media.Geometry Icon => Item.IsFix ? Icons.Check : Icons.Star;

        public bool IsFix => Item.IsFix;
    }

    /// <summary>One version on the Changelog page.</summary>
    internal sealed record ChangelogRow(string Version, string Notes, bool IsCurrent, bool IsNewer)
    {
        /// <summary>When the version came out, from its heading in the changelog.</summary>
        public DateTime? Released { get; init; }

        /// <summary>Its changes one by one; falls back to the notes as one change for an older format.</summary>
        public IReadOnlyList<ChangeLine> Lines { get; init; } = [];

        public bool HasReleased => Released is not null;

        /// <summary>"Released 1 Oct 2026, 18:47", in the language the app is in.</summary>
        public string ReleasedText => Released is { } at
            ? Strings.Format("ChangelogReleased", at.ToString("d MMM yyyy, HH:mm",
                System.Globalization.CultureInfo.GetCultureInfo(Strings.Language.Id)))
            : string.Empty;

        /// <summary>A row for one entry of the changelog.</summary>
        public static ChangelogRow From(ChangelogEntry entry, bool isCurrent, bool isNewer) =>
            new(entry.Version, entry.Notes, isCurrent, isNewer)
            {
                Released = entry.Released,
                Lines = [.. (entry.Items is { Count: > 0 } items ? items : Changelog.Items(entry.Notes)).Select(item => new ChangeLine(item))],
            };

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
                Entries.Add(ChangelogRow.From(entry, isCurrent: entry.Version == AppVersion.Number, isNewer: false));
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
                    Entries.Insert(i, ChangelogRow.From(changes[i], isCurrent: false, isNewer: true));
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
