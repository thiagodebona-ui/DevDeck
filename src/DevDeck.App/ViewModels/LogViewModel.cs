using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  What the app itself has done this session.
    /// </summary>
    /// <remarks>
    ///  The answer to "why did that just run" and "what happened when I pressed that". A command's
    ///  own output lives beside the command; this is everything else - a watch firing, a settings
    ///  file that could not be read, a secret that could not be unwrapped - all of which used to
    ///  disappear into a catch block.
    ///
    ///  Subscribed for the life of the app rather than while the panel is open, because the point
    ///  of a log is that it was already recording when the thing you are now investigating
    ///  happened.
    /// </remarks>
    internal sealed partial class LogViewModel : ObservableObject
    {
        public LogViewModel()
        {
            foreach (LogEntry entry in AppLog.Instance.All)
            {
                Items.Add(entry);
            }

            // Onto the UI thread: entries are written from watcher callbacks, timers and whatever
            // thread a failed save happened on.
            AppLog.Instance.Added += entry => Dispatcher.UIThread.Post(() => Take(entry));
        }

        public ObservableCollection<LogEntry> Items { get; } = [];

        [ObservableProperty]
        private string query = string.Empty;

        /// <summary>Whether to show only what went wrong.</summary>
        [ObservableProperty]
        private bool problemsOnly;

        [ObservableProperty]
        private string status = string.Empty;

        public bool IsEmpty => Items.Count == 0;

        partial void OnQueryChanged(string value) => Show();

        partial void OnProblemsOnlyChanged(bool value) => Show();

        private void Take(LogEntry entry)
        {
            if (!Wanted(entry))
            {
                return;
            }

            Items.Add(entry);

            // The store is already bounded; this keeps the bound on what is displayed in step with
            // it rather than growing a second unbounded list of the same entries.
            while (Items.Count > AppLog.Cap)
            {
                Items.RemoveAt(0);
            }

            OnPropertyChanged(nameof(IsEmpty));
        }

        private bool Wanted(LogEntry entry)
        {
            if (ProblemsOnly && entry.Level is not (LogLevel.Warning or LogLevel.Error))
            {
                return false;
            }

            return Query.Length == 0
                || entry.Text.Contains(Query, StringComparison.OrdinalIgnoreCase)
                || entry.Source.Contains(Query, StringComparison.OrdinalIgnoreCase);
        }

        private void Show()
        {
            Items.Clear();

            foreach (LogEntry entry in AppLog.Instance.All.Where(Wanted))
            {
                Items.Add(entry);
            }

            OnPropertyChanged(nameof(IsEmpty));
        }

        /// <summary>Set by the view, which is the only thing that can reach the clipboard.</summary>
        public Func<string, Task>? Copy { get; set; }

        [RelayCommand]
        private async Task CopyAll()
        {
            if (Copy is not { } copy)
            {
                return;
            }

            await copy(AppLog.Instance.AsText());

            Status = Strings.Format("LogCopiedLines", AppLog.Instance.Count);
        }

        [RelayCommand]
        private void Clear()
        {
            AppLog.Instance.Clear();
            Show();

            Status = "Cleared.";
        }
    }
}
