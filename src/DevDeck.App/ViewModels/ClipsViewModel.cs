using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>
    ///  Everything copied since the capture was turned on.
    /// </summary>
    /// <remarks>
    ///  Off by default and off again the moment it is switched off, because this panel's file is a
    ///  record of everything the user has copied - which is a genuinely sensitive thing to have
    ///  created on someone's behalf without asking. The switch is the feature; the list is what it
    ///  produces.
    ///
    ///  Polled rather than hooked. There is no cross-platform clipboard notification in Avalonia,
    ///  and the alternative is three native implementations of an event that this panel would use
    ///  to do exactly what a timer does. A second is fast enough that nothing is missed in practice
    ///  and slow enough to cost nothing.
    /// </remarks>
    internal sealed partial class ClipsViewModel : ObservableObject, IDisposable
    {
        private readonly AppSettings settings;
        private readonly DispatcherTimer timer;

        /// <summary>
        ///  The digest of the last value seen, so an unchanged clipboard costs one hash.
        /// </summary>
        /// <remarks>
        ///  Held as a hash rather than as the text: the point of the panel is that the history
        ///  lives in one place that the user can wipe, and a copy of the most recent clip kept in
        ///  a field is a second place they cannot.
        /// </remarks>
        private string last = string.Empty;

        public ClipsViewModel(AppSettings settings)
        {
            this.settings = settings;
            capturing = settings.ClipboardCapture;

            // On the UI thread by construction, which is what the clipboard requires - unlike the
            // Running panel, whose work is all in subprocesses and belongs off it.
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) => Poll();

            Show();
        }

        public ObservableCollection<Clip> Items { get; } = [];

        /// <summary>Set by the view: reading and writing the clipboard is a top-level operation.</summary>
        public Func<string, Task>? Copy { get; set; }

        public Func<Task<string?>>? Paste { get; set; }

        /// <summary>Set by the view, to hand a link to the desktop.</summary>
        public Action<string>? Open { get; set; }

        [ObservableProperty]
        private bool capturing;

        [ObservableProperty]
        private string query = string.Empty;

        [ObservableProperty]
        private Clip? selected;

        [ObservableProperty]
        private string status = string.Empty;

        public bool IsEmpty => Items.Count == 0;

        /// <summary>Nothing recorded at all, as opposed to nothing matching the search or filter.</summary>
        public bool NothingRecorded => Clips.Instance.All.Count == 0;

        public string Where => Clips.Path;

        /// <summary>Set by the view: a question with a heading, a consequence and a button label.</summary>
        public Func<string, string, string, Task<bool>>? Confirm { get; set; }

        partial void OnQueryChanged(string value) => Show();

        /// <summary>Which clips the chips at the top let through: "All", "Pinned" or "Links".</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowingAll), nameof(ShowingPinned), nameof(ShowingLinks))]
        private string filter = "All";

        public bool ShowingAll => Filter == "All";

        public bool ShowingPinned => Filter == "Pinned";

        public bool ShowingLinks => Filter == "Links";

        partial void OnFilterChanged(string value) => Show();

        [RelayCommand]
        private void SetFilter(string? value) => Filter = value is "Pinned" or "Links" ? value : "All";

        /// <summary>The whole of the selected clip, for the preview pane - the list only has room for a line.</summary>
        public string SelectedText => Selected?.Text ?? string.Empty;

        public bool HasSelection => Selected is not null;

        /// <summary>"1,204 characters · 18 lines", under the preview.</summary>
        public string SelectedFacts => Selected is { } clip
            ? Strings.Format("ClipsFacts", clip.Text.Length, clip.Text.Split('\n').Length)
            : string.Empty;

        partial void OnSelectedChanged(Clip? value)
        {
            OnPropertyChanged(nameof(SelectedText));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedFacts));
        }

        partial void OnCapturingChanged(bool value)
        {
            settings.ClipboardCapture = value;
            settings.Save();

            if (value)
            {
                timer.Start();
                Status = Strings.Text("ClipsRecordingOn");
            }
            else
            {
                timer.Stop();
                Status = Strings.Text("ClipsRecordingOff");
            }
        }

        /// <summary>Starts polling if the setting says to, called when the panel appears.</summary>
        public void Start()
        {
            if (Capturing)
            {
                timer.Start();
            }
        }

        /// <summary>
        ///  Stops polling while the panel is not on screen.
        /// </summary>
        /// <remarks>
        ///  A deliberate limit rather than an oversight: the history records what was copied while
        ///  the panel was open, not everything the machine has ever seen. A capture that runs
        ///  whether or not the app is being used is a keylogger with extra steps, and that is not
        ///  what this is for.
        /// </remarks>
        public void Stop() => timer.Stop();

        /// <summary>Reads the clipboard and records anything new.</summary>
        private async void Poll()
        {
            if (!Capturing || Paste is not { } paste)
            {
                return;
            }

            string? text;

            try
            {
                text = await paste();
            }
            catch (Exception)
            {
                // The clipboard is shared, and another process holding it is ordinary. The next
                // tick is a second away.
                return;
            }

            string digest = Clips.Digest(text);

            if (digest == last)
            {
                return;
            }

            last = digest;

            if (Clips.Instance.Add(text))
            {
                Show();
            }
        }

        /// <summary>Refills the list from the store, through the search box.</summary>
        private void Show()
        {
            Clip? was = Selected;

            Items.Clear();

            foreach (Clip clip in Clips.Instance.Search(Query).Where(clip => Filter switch
            {
                "Pinned" => clip.Pinned,
                "Links" => clip.IsLink,
                _ => true,
            }))
            {
                Items.Add(clip);
            }

            // Restored by reference, since these are the same objects the store holds rather than
            // copies of them. With nothing to restore, the newest is shown, so the preview pane
            // is never an empty box beside a full list.
            Selected = was is not null && Items.Contains(was) ? was : Items.FirstOrDefault();

            // Pinning changes the clip in place, which the preview's facts do not see otherwise.
            OnSelectedChanged(Selected);
            OnPropertyChanged(nameof(Selected));

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(NothingRecorded));
        }

        [RelayCommand]
        private async Task Use(Clip? clip)
        {
            if ((clip ?? Selected) is not { } wanted || Copy is not { } copy)
            {
                return;
            }

            await copy(wanted.Text);

            // The write is about to come back round as a new clipboard value; recording it would
            // move the entry to the top of the list for no reason the user asked for.
            last = Clips.Digest(wanted.Text);

            Status = Strings.Text("ClipsCopied");
        }

        /// <summary>
        ///  Opens a clip that is a web address.
        /// </summary>
        /// <remarks>
        ///  The check is repeated here rather than trusted from the view. A disabled button is a
        ///  hint, not a guard, and this one ends in a shell execute - so what actually decides is
        ///  the model's own rule about what counts as a link, read again at the moment of use.
        /// </remarks>
        [RelayCommand]
        private void Follow(Clip? clip)
        {
            if ((clip ?? Selected)?.Link is not { } link)
            {
                return;
            }

            Open?.Invoke(link);
            Status = Strings.Text("ClipsOpened");
        }

        /// <summary>The recording switch as one button: start it, or pause it.</summary>
        [RelayCommand]
        private void ToggleCapture() => Capturing = !Capturing;

        [RelayCommand]
        private void Remove(Clip? clip)
        {
            if ((clip ?? Selected) is not { } doomed)
            {
                return;
            }

            Clips.Instance.Remove(doomed);
            Show();
        }

        [RelayCommand]
        private void Pin(Clip? clip)
        {
            if ((clip ?? Selected) is not { } wanted)
            {
                return;
            }

            Clips.Instance.Pin(wanted, !wanted.Pinned);
            Show();
        }

        /// <summary>
        ///  Wipes the history, pinned entries included - after asking, because pins are the clips
        ///  someone chose to keep, and one stray click used to take them too.
        /// </summary>
        [RelayCommand]
        private async Task Clear()
        {
            int count = Clips.Instance.All.Count;

            if (count == 0)
            {
                return;
            }

            bool sure = Confirm is { } ask && await ask(
                Strings.Text("ClipsClearConfirm"),
                Strings.Format("ClipsClearConfirmDetail", count),
                Strings.Text("ClipsClearAll"));

            if (!sure)
            {
                return;
            }

            Clips.Instance.Clear();
            Show();

            Status = Strings.Text("ClipsCleared");
        }

        public void Dispose() => timer.Stop();
    }
}
