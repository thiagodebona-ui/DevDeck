using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevDeck.Core;

namespace DevDeck.App.ViewModels
{
    /// <summary>One tile in the gallery: the shot, and its thumbnail once it has been decoded.</summary>
    internal sealed partial class ShotItem(Shot shot) : ObservableObject
    {
        public Shot Shot { get; } = shot;

        public string Path => Shot.Path;

        public string Name => Shot.Name;

        public string When => Shot.When;

        public string Stamp => Shot.Stamp;

        public string Weight => Shot.Weight;

        public bool Own => Shot.Own;

        /// <summary>Where it came from, for the details list.</summary>
        public string Source => Strings.Text(Own ? "ShotsSourceClipboard" : "ShotsSourceFolder");

        [ObservableProperty]
        private bool favourite = shot.Favourite;

        [ObservableProperty]
        private Bitmap? thumbnail;

        /// <summary>Dimensions and weight, filled in with the thumbnail rather than read on the UI thread.</summary>
        [ObservableProperty]
        private string caption = string.Empty;

        /// <summary>Just the dimensions, for the details list's own column. Filled in with the caption.</summary>
        [ObservableProperty]
        private string dimensions = string.Empty;
    }

    /// <summary>
    ///  The Screenshots page: every capture on the machine, newest first.
    /// </summary>
    /// <remarks>
    ///  The folder is read when the page is shown, and again when a file appears in it while the
    ///  page is open - so a Win+Shift+S lands in the grid as it is taken. Nothing is decoded until
    ///  then: the deck starts with no thumbnails in memory, and a page of them is a few megabytes.
    ///
    ///  Clipboard images are watched whether or not the page is open, unlike the text history.
    ///  The text history records while it is being watched because what is copied as text is
    ///  routinely a secret; an image on the clipboard is almost always a capture the user just
    ///  took on purpose, and missing the one taken while DevDeck was in the tray is the whole
    ///  problem this page exists to solve. It is still off until switched on.
    /// </remarks>
    internal sealed partial class ScreenshotsViewModel : ObservableObject, IDisposable
    {
        /// <summary>How many tiles are drawn before "Show more" is needed.</summary>
        private const int Page = 60;

        /// <summary>
        ///  How long a clipboard image waits before it is kept.
        /// </summary>
        /// <remarks>
        ///  Snipping Tool puts a capture on the clipboard and writes its file a moment later.
        ///  Waiting lets the file land first, so the clipboard copy is recognised as a twin and
        ///  never written, rather than written and deleted again.
        /// </remarks>
        private static readonly TimeSpan Settle = TimeSpan.FromSeconds(3);

        private readonly AppSettings settings;

        private readonly DispatcherTimer clipboardTimer;

        /// <summary>Gathers a burst of file events into one refresh.</summary>
        private readonly DispatcherTimer refreshTimer;

        private readonly FileSystemWatcher? watcher;

        private readonly Dictionary<string, (DateTime At, Bitmap Thumb)> thumbs = new(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> arrived = [];

        /// <summary>Two decodes at a time: enough to fill a page quickly without starving the UI.</summary>
        private readonly SemaphoreSlim decoding = new(2);

        private List<Shot> found = [];

        private int shown = Page;

        private bool visible;

        private bool stale = true;

        /// <summary>The clipboard version last looked at, so an unchanged clipboard costs one call.</summary>
        private uint seen;

        private bool polling;

        private (byte[] Png, int Width, int Height, DateTime At)? pending;

        public ScreenshotsViewModel(AppSettings settings)
        {
            this.settings = settings;
            capturing = settings.ScreenshotsFromClipboard;
            layout = settings.ScreenshotsLayout;

            // Whatever is on the clipboard at start-up was put there before; it is not a new capture.
            seen = Shots.ClipboardVersion();

            clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clipboardTimer.Tick += (_, _) => Poll();

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            refreshTimer.Tick += (_, _) => Settled();

            if (Shots.Instance.SystemFolder is { } folder)
            {
                try
                {
                    watcher = new FileSystemWatcher(folder)
                    {
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    };

                    watcher.Created += (_, e) => Dispatcher.UIThread.Post(() => Changed(e.FullPath));
                    watcher.Deleted += (_, _) => Dispatcher.UIThread.Post(() => Changed(null));
                    watcher.Renamed += (_, e) => Dispatcher.UIThread.Post(() => Changed(e.FullPath));
                    watcher.EnableRaisingEvents = true;
                }
                catch (Exception)
                {
                    // A folder that cannot be watched is still read each time the page is shown.
                    watcher = null;
                }
            }

            if (capturing && CanCapture)
            {
                clipboardTimer.Start();
            }

            // Off the UI thread and out of the way of start-up: it touches the disk, and nothing
            // needs it done before the page is first shown.
            _ = Task.Run(() => Shots.Instance.Sweep(DateTime.Now));
        }

        public ObservableCollection<ShotItem> Items { get; } = [];

        [ObservableProperty]
        private bool capturing;

        [ObservableProperty]
        private string query = string.Empty;

        [ObservableProperty]
        private ShotItem? selected;

        [ObservableProperty]
        private Bitmap? preview;

        [ObservableProperty]
        private string status = string.Empty;

        [ObservableProperty]
        private bool favouritesOnly;

        /// <summary>"Thumbnails", "List" or "Details", as stored in the settings.</summary>
        [ObservableProperty]
        private string layout;

        public bool IsThumbnails => Layout is not ("List" or "Details");

        public bool IsList => Layout == "List";

        public bool IsDetails => Layout == "Details";

        /// <summary>The narrowest the preview can be dragged: still wide enough for its buttons.</summary>
        public double PreviewFloor => 220;

        /// <summary>
        ///  The preview's width, as the divider left it.
        /// </summary>
        /// <remarks>
        ///  Set on every step of a drag and written to disk only when the drag ends, by
        ///  <see cref="KeepLayout"/>: a drag is dozens of steps, and each would be a settings file.
        /// </remarks>
        public double PreviewWidth
        {
            get => settings.ScreenshotsPreviewWidth > 0 ? Math.Max(PreviewFloor, settings.ScreenshotsPreviewWidth) : 360;
            set => settings.ScreenshotsPreviewWidth = Math.Round(Math.Max(PreviewFloor, value));
        }

        public void KeepLayout() => settings.Save();

        /// <summary>What the toolbar's favourite button would do to the selection.</summary>
        public string FavouriteLabel => Strings.Text(
            Targets(null) is { Count: > 0 } all && all.All(shot => shot.Favourite) ? "ShotsUnfavourite" : "ShotsFavourite");

        /// <summary>
        ///  Every shot selected, in the order they appear, set by the view as the selection changes.
        /// </summary>
        /// <remarks>
        ///  Kept beside <see cref="Selected"/> rather than instead of it: Selected is the one the
        ///  preview shows - the last one clicked - and this is what the toolbar acts on. Ctrl and
        ///  Shift add to it the way they do in Explorer.
        /// </remarks>
        public IReadOnlyList<ShotItem> Picked { get; private set; } = [];

        public void Pick(IEnumerable<ShotItem> items)
        {
            HashSet<ShotItem> chosen = [.. items];

            Picked = [.. Items.Where(chosen.Contains)];

            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(FavouriteLabel));
            OnPropertyChanged(nameof(Count));
        }

        /// <summary>
        ///  What a command acts on: the one row it was pressed on, or else everything selected.
        /// </summary>
        /// <remarks>
        ///  A star on a tile is about that tile even when others are selected; the toolbar is
        ///  about the selection.
        /// </remarks>
        private List<ShotItem> Targets(ShotItem? item) =>
            item is not null ? [item]
            : Picked.Count > 0 ? [.. Picked]
            : Selected is { } one ? [one]
            : [];

        /// <summary>Whether this system can watch its clipboard for images: Windows only, for now.</summary>
        public bool CanCapture => OperatingSystem.IsWindows();

        public bool IsEmpty => Items.Count == 0;

        public bool HasSelection => Picked.Count > 0 || Selected is not null;

        public bool HasMore => found.Count > Items.Count;

        public bool HasSystemFolder => Shots.Instance.SystemFolder is not null;

        public string Count
        {
            get
            {
                string total = found.Count > Items.Count
                    ? Strings.Format("ShotsShowing", Items.Count, found.Count)
                    : Strings.Format("ShotsCount", found.Count);

                return Picked.Count > 1 ? $"{Strings.Format("ShotsSelected", Picked.Count)} · {total}" : total;
            }
        }

        /// <summary>What the info box says about where shots come from on this system.</summary>
        public string Where => Shots.Instance.SystemFolder is not { } folder
            ? Strings.Text("ShotsNoFolder")
            : OperatingSystem.IsWindows()
                ? Strings.Format("ShotsWindowsHint", folder)
                : Strings.Format("ShotsOtherHint", folder);

        partial void OnQueryChanged(string value)
        {
            shown = Page;
            Refresh();
        }

        partial void OnFavouritesOnlyChanged(bool value)
        {
            shown = Page;
            Refresh();
        }

        partial void OnLayoutChanged(string value)
        {
            settings.ScreenshotsLayout = value;
            settings.Save();

            OnPropertyChanged(nameof(IsThumbnails));
            OnPropertyChanged(nameof(IsList));
            OnPropertyChanged(nameof(IsDetails));
        }

        partial void OnSelectedChanged(ShotItem? value)
        {
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(FavouriteLabel));
            _ = LoadPreviewAsync(value);
        }

        partial void OnCapturingChanged(bool value)
        {
            settings.ScreenshotsFromClipboard = value;
            settings.Save();

            if (value)
            {
                seen = Shots.ClipboardVersion();
                clipboardTimer.Start();
                Status = Strings.Format("ShotsKeepingOn", Shots.Instance.OwnFolder);
            }
            else
            {
                clipboardTimer.Stop();
                pending = null;
                Status = Strings.Text("ShotsKeepingOff");
            }
        }

        /// <summary>The page came on screen: read the folders, if anything changed since last time.</summary>
        public void Start()
        {
            visible = true;

            if (stale)
            {
                Refresh();
            }
        }

        public void Stop() => visible = false;

        /// <summary>A file came or went. Gathered, because a save is several events in a row.</summary>
        private void Changed(string? path)
        {
            if (path is not null)
            {
                arrived.Add(path);
            }

            refreshTimer.Stop();
            refreshTimer.Start();
        }

        private void Settled()
        {
            refreshTimer.Stop();

            foreach (string path in arrived)
            {
                Shots.Instance.Arrived(path);
            }

            arrived.Clear();

            if (visible)
            {
                Refresh();
            }
            else
            {
                stale = true;
            }
        }

        /// <summary>
        ///  Looks at the clipboard, and keeps an image that has settled there.
        /// </summary>
        private async void Poll()
        {
            if (polling)
            {
                return;
            }

            polling = true;

            try
            {
                if (pending is { } waiting && DateTime.Now - waiting.At >= Settle)
                {
                    pending = null;

                    if (Shots.Instance.Keep(waiting.Png, waiting.Width, waiting.Height, waiting.At) is not null)
                    {
                        Changed(null);
                    }
                }

                uint version = Shots.ClipboardVersion();

                if (version == seen || Clipboard() is not { } clipboard)
                {
                    return;
                }

                seen = version;

                IReadOnlyList<DataFormat> formats = await clipboard.GetDataFormatsAsync();

                if (!formats.Contains(DataFormat.Bitmap))
                {
                    return;
                }

                using Bitmap? image = await clipboard.TryGetBitmapAsync();

                if (image is null)
                {
                    return;
                }

                using MemoryStream png = new();
                image.Save(png, PngBitmapEncoderOptions.Default);

                // A newer image replaces one still settling: two captures in three seconds are the
                // user retaking a shot, and the last one is the one they meant.
                pending = (png.ToArray(), image.PixelSize.Width, image.PixelSize.Height, DateTime.Now);
            }
            catch (Exception)
            {
                // Another process holding the clipboard is ordinary. The next tick is a second away.
            }
            finally
            {
                polling = false;
            }
        }

        /// <summary>
        ///  Rereads the folders and brings the grid in line, keeping the selection and thumbnails.
        /// </summary>
        /// <remarks>
        ///  Reconciled tile by tile rather than cleared and refilled: a refill scrolls the grid back
        ///  to the top, and this runs every time a capture lands - which would yank the page out
        ///  from under someone scrolled halfway down it looking for last week's shot.
        /// </remarks>
        private void Refresh()
        {
            stale = false;

            string? was = Selected?.Path;

            found = [.. Shots.Instance.Search(Query).Where(shot => !FavouritesOnly || shot.Favourite)];

            List<Shot> wanted = [.. found.Take(shown)];
            HashSet<string> keys = new(wanted.Select(Key), StringComparer.OrdinalIgnoreCase);

            for (int i = Items.Count - 1; i >= 0; i--)
            {
                if (!keys.Contains(Key(Items[i].Shot)))
                {
                    Items.RemoveAt(i);
                }
            }

            for (int i = 0; i < wanted.Count; i++)
            {
                string key = Key(wanted[i]);

                if (i < Items.Count && string.Equals(Key(Items[i].Shot), key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int at = -1;

                for (int j = i + 1; j < Items.Count; j++)
                {
                    if (string.Equals(Key(Items[j].Shot), key, StringComparison.OrdinalIgnoreCase))
                    {
                        at = j;
                        break;
                    }
                }

                if (at >= 0)
                {
                    Items.Move(at, i);
                }
                else
                {
                    Items.Insert(i, new ShotItem(wanted[i]));
                }
            }

            // Thumbnails of files no longer listed are let go of; the rest are reused.
            HashSet<string> listed = new(Items.Select(item => item.Path), StringComparer.OrdinalIgnoreCase);

            foreach (string gone in thumbs.Keys.Where(path => !listed.Contains(path)).ToList())
            {
                thumbs[gone].Thumb.Dispose();
                thumbs.Remove(gone);
            }

            foreach (ShotItem item in Items.Where(item => item.Thumbnail is null))
            {
                if (thumbs.TryGetValue(item.Path, out var cached) && cached.At == item.Shot.At)
                {
                    item.Thumbnail = cached.Thumb;
                    item.Caption = item.Shot.Shape;
                    item.Dimensions = item.Shot.Dimensions;
                }
                else
                {
                    _ = LoadThumbnailAsync(item);
                }
            }

            if (Selected?.Path != was || !Items.Contains(Selected!))
            {
                Selected = Items.FirstOrDefault(item => string.Equals(item.Path, was, StringComparison.OrdinalIgnoreCase));
            }

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(HasMore));
            OnPropertyChanged(nameof(Count));
        }

        private async Task LoadThumbnailAsync(ShotItem item)
        {
            await decoding.WaitAsync();

            try
            {
                (Bitmap thumb, string caption, string dimensions) = await Task.Run(() =>
                {
                    using FileStream stream = File.OpenRead(item.Path);

                    Bitmap decoded = Bitmap.DecodeToWidth(stream, 320, BitmapInterpolationMode.MediumQuality);

                    return (decoded, item.Shot.Shape, item.Shot.Dimensions);
                });

                // A refresh while this decoded can have replaced the tile, or decoded the same file
                // first. Either way the bitmap already drawn must be the one that is kept.
                if (!Items.Contains(item))
                {
                    thumb.Dispose();

                    return;
                }

                if (thumbs.TryGetValue(item.Path, out var cached) && cached.At == item.Shot.At)
                {
                    thumb.Dispose();
                    thumb = cached.Thumb;
                }
                else if (thumbs.Remove(item.Path, out var old))
                {
                    old.Thumb.Dispose();
                }

                thumbs[item.Path] = (item.Shot.At, thumb);
                item.Thumbnail = thumb;
                item.Caption = caption;
                item.Dimensions = dimensions;
            }
            catch (Exception)
            {
                // Still being written, or not an image after all. The next refresh tries again.
            }
            finally
            {
                decoding.Release();
            }
        }

        private async Task LoadPreviewAsync(ShotItem? item)
        {
            Bitmap? old = Preview;
            Preview = null;
            old?.Dispose();

            if (item is null)
            {
                return;
            }

            try
            {
                Bitmap full = await Task.Run(() => new Bitmap(item.Path));

                // The user may have moved on while it decoded.
                if (Selected == item)
                {
                    Preview = full;
                }
                else
                {
                    full.Dispose();
                }
            }
            catch (Exception)
            {
                // The thumbnail is still showing in the grid.
            }
        }

        private static IClipboard? Clipboard() =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;

        private static IStorageProvider? Storage() =>
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow?.StorageProvider;

        [RelayCommand]
        private void ShowMore()
        {
            shown += Page;
            Refresh();
        }

        /// <summary>
        ///  Copies the picture, or - for several - the files, which is the only way more than one
        ///  image fits on a clipboard.
        /// </summary>
        /// <remarks>
        ///  As files they paste into Teams, Outlook, a Jira comment or an Explorer folder all the
        ///  same, which is what copying several screenshots at once is for.
        /// </remarks>
        [RelayCommand]
        private async Task CopyImage(ShotItem? item)
        {
            List<ShotItem> targets = Targets(item);

            if (targets.Count == 0 || Clipboard() is not { } clipboard)
            {
                return;
            }

            try
            {
                if (targets.Count == 1)
                {
                    using Bitmap image = await Task.Run(() => new Bitmap(targets[0].Path));

                    await clipboard.SetBitmapAsync(image);
                    Status = Strings.Text("ShotsImageCopied");
                }
                else if (Storage() is { } storage)
                {
                    List<IStorageItem> files = [];

                    foreach (ShotItem target in targets)
                    {
                        if (await storage.TryGetFileFromPathAsync(target.Path) is { } file)
                        {
                            files.Add(file);
                        }
                    }

                    await clipboard.SetFilesAsync(files);
                    Status = Strings.Format("ShotsImagesCopied", files.Count);
                }

                // Coming back round as a new clipboard image is not a new capture.
                seen = Shots.ClipboardVersion();
            }
            catch (Exception ex)
            {
                Status = ex.Message;
            }
        }

        /// <summary>The paths, one per line, so several paste as a list.</summary>
        [RelayCommand]
        private async Task CopyPath(ShotItem? item)
        {
            List<ShotItem> targets = Targets(item);

            if (targets.Count == 0 || Clipboard() is not { } clipboard)
            {
                return;
            }

            await clipboard.SetTextAsync(string.Join(Environment.NewLine, targets.Select(target => target.Path)));

            Status = targets.Count == 1
                ? Strings.Text("ShotsPathCopied")
                : Strings.Format("ShotsPathsCopied", targets.Count);
        }

        /// <summary>Opens the shot in whatever the desktop opens images with.</summary>
        [RelayCommand]
        private void Open(ShotItem? item)
        {
            if ((item ?? Selected) is { } wanted)
            {
                Launch(wanted.Path);
            }
        }

        /// <summary>Opens the folder with the shot selected in it, where the platform allows.</summary>
        [RelayCommand]
        private void Reveal(ShotItem? item)
        {
            if ((item ?? Selected) is not { } wanted)
            {
                return;
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Process.Start("explorer.exe", $"/select,\"{wanted.Path}\"")?.Dispose();
                }
                else if (OperatingSystem.IsMacOS())
                {
                    Process.Start("open", ["-R", wanted.Path])?.Dispose();
                }
                else
                {
                    Launch(wanted.Shot.Folder);
                }
            }
            catch (Exception)
            {
                // No file manager to hand it to. The path is one Copy path away.
            }
        }

        [RelayCommand]
        private void OpenFolder()
        {
            if (Shots.Instance.SystemFolder is { } folder)
            {
                Launch(folder);
            }
        }

        /// <summary>
        ///  Marks a shot as a favourite, or stops it being one. It stays where it is in the grid.
        /// </summary>
        /// <remarks>
        ///  A favourite does not jump to the top: the gallery is in the order the shots were taken,
        ///  which is how they are remembered, and the star plus Favourites only is how one is
        ///  found again.
        /// </remarks>
        [RelayCommand]
        private void ToggleFavourite(ShotItem? item)
        {
            List<ShotItem> targets = Targets(item);

            if (targets.Count == 0)
            {
                return;
            }

            // One way for all of them, as Explorer does: unless every one is already a favourite,
            // they all become one. Flipping each would leave a mixed selection still mixed.
            bool favourite = !targets.All(target => target.Favourite);

            foreach (ShotItem target in targets)
            {
                Shots.Instance.MarkFavourite(target.Shot, favourite);
                target.Favourite = favourite;
            }

            OnPropertyChanged(nameof(FavouriteLabel));

            if (FavouritesOnly)
            {
                Refresh();
            }
        }

        [RelayCommand]
        private void SetLayout(string? name) => Layout = name ?? "Thumbnails";

        /// <summary>A file's identity in the grid: the same path rewritten is a different shot.</summary>
        private static string Key(Shot shot) => $"{shot.Path}|{shot.At.Ticks}";

        [RelayCommand]
        private void Remove(ShotItem? item)
        {
            List<ShotItem> targets = Targets(item);

            if (targets.Count == 0)
            {
                return;
            }

            // Let go of the preview first: the recycle bin is fussier than a plain delete about a
            // file anything has open.
            if (Selected is { } previewed && targets.Contains(previewed))
            {
                Selected = null;
            }

            int removed = targets.Count(target => Shots.Instance.Remove(target.Shot));

            Status = removed < targets.Count ? Strings.Text("ShotsCannotDelete")
                : removed == 1 ? Strings.Text(OperatingSystem.IsWindows() ? "ShotsRecycled" : "ShotsDeleted")
                : Strings.Format(OperatingSystem.IsWindows() ? "ShotsRecycledMany" : "ShotsDeletedMany", removed);

            Refresh();
        }

        private static void Launch(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
            }
            catch (Exception)
            {
                // Nothing registered to open it. The panel keeps working.
            }
        }

        public void Dispose()
        {
            clipboardTimer.Stop();
            refreshTimer.Stop();
            watcher?.Dispose();
        }
    }
}
