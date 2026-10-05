using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>One screenshot on disk.</summary>
    internal sealed class Shot
    {
        private readonly Lazy<(int Width, int Height)?> size;

        public Shot(string path, DateTime at, long bytes, bool own)
        {
            Path = path;
            At = at;
            Bytes = bytes;
            Own = own;

            // Read on first use rather than when the folder is listed: the screenshots folder is
            // often in OneDrive, where opening a file that is only in the cloud downloads it, and
            // only the shots actually drawn should pay for that.
            size = new Lazy<(int, int)?>(() => Shots.ImageSize(path));
        }

        public string Path { get; }

        public DateTime At { get; }

        public long Bytes { get; }

        /// <summary>Saved by DevDeck from the clipboard, rather than found in the system's folder.</summary>
        public bool Own { get; }

        /// <summary>Marked by the user: kept out of the sweep, and what Favourites only shows.</summary>
        public bool Favourite { get; set; }

        public string Name => System.IO.Path.GetFileNameWithoutExtension(Path);

        public string Folder => System.IO.Path.GetDirectoryName(Path) ?? string.Empty;

        public (int Width, int Height)? Size => size.Value;

        public string When
        {
            get
            {
                TimeSpan ago = DateTime.Now - At;

                return ago < TimeSpan.FromMinutes(1) ? "just now"
                    : ago < TimeSpan.FromHours(1) ? $"{(int)ago.TotalMinutes}m ago"
                    : ago < TimeSpan.FromDays(1) ? $"{(int)ago.TotalHours}h ago"
                    : At.ToString("ddd dd MMM HH:mm");
            }
        }

        /// <summary>The date and time in full, for the details list, where "2h ago" sorts nothing.</summary>
        public string Stamp => At.ToString("yyyy-MM-dd HH:mm");

        /// <summary>"245 KB".</summary>
        public string Weight => Bytes >= 1024 * 1024
            ? $"{Bytes / (1024.0 * 1024.0):0.0} MB"
            : $"{Math.Max(1, Bytes / 1024)} KB";

        /// <summary>"1920 × 1080", or empty when the header could not be read.</summary>
        public string Dimensions => Size is { } known ? $"{known.Width} × {known.Height}" : string.Empty;

        /// <summary>"1920 × 1080 · 245 KB", or just the file size when the header could not be read.</summary>
        public string Shape => Size is not null ? $"{Dimensions} · {Weight}" : Weight;
    }

    /// <summary>
    ///  Every screenshot on the machine, in one place.
    /// </summary>
    /// <remarks>
    ///  Two sources, and the first is the one that matters. Windows' Snipping Tool already saves
    ///  every Win+Shift+S capture into the Screenshots folder, so the gallery is mostly a view over
    ///  a folder the system keeps: nothing is copied, nothing is lost when DevDeck was closed, and
    ///  a capture taken a week ago is there on first start.
    ///
    ///  The second is the clipboard, for a capture that never reached a file - Snipping Tool with
    ///  auto-save off, or an image copied out of a browser. Those are written as PNGs into a folder
    ///  of DevDeck's own beside the settings, which is the only folder this class ever sweeps. The
    ///  system's folder belongs to the user: a shot there is only deleted when they press Delete.
    ///
    ///  One capture usually arrives both ways - Snipping Tool puts it on the clipboard and writes
    ///  the file a moment later - so a clipboard image that matches a file of the same size saved
    ///  in the last few seconds is treated as that file and not kept twice. Matched on dimensions
    ///  and time rather than on content: the clipboard holds pixels and the file holds a PNG one
    ///  encoder wrote, and comparing those would mean decoding both.
    ///
    ///  Favourites are the one thing stored, in shots.json beside the settings, by path. Everything else
    ///  is read back off the disk, so a file renamed, moved or deleted in Explorer simply changes
    ///  what the gallery shows.
    /// </remarks>
    internal sealed class Shots
    {
        /// <summary>Beyond this DevDeck's own folder is an archive nobody scrolls through.</summary>
        private const int Cap = 200;

        /// <summary>How long a clipboard capture is kept unless it is a favourite.</summary>
        private static readonly TimeSpan KeepFor = TimeSpan.FromDays(30);

        /// <summary>How close together a clipboard image and a file have to be to be one capture.</summary>
        private static readonly TimeSpan Twin = TimeSpan.FromSeconds(15);

        private static readonly string[] Kinds = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp"];

        private static readonly StringComparer PathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        private readonly string index;

        private readonly HashSet<string> favourites;

        /// <summary>The digest of the last clipboard image kept, so the same one is not kept twice.</summary>
        private string lastKept = string.Empty;

        public Shots(string ownFolder, string? systemFolder, string index)
        {
            OwnFolder = ownFolder;
            SystemFolder = systemFolder;
            this.index = index;
            favourites = new HashSet<string>(LoadFavourites(index), PathComparer);
        }

        public static Shots Instance { get; } = new(
            System.IO.Path.Combine(SettingsPath.Directory, "shots"),
            FindSystemFolder(),
            System.IO.Path.Combine(SettingsPath.Directory, "shots.json"));

        /// <summary>Where clipboard captures are written.</summary>
        public string OwnFolder { get; }

        /// <summary>Where the system saves its screenshots, or null when it has no such folder.</summary>
        public string? SystemFolder { get; }

        /// <summary>Newest first, from both folders.</summary>
        public IReadOnlyList<Shot> All()
        {
            List<Shot> found = [.. Read(SystemFolder, own: false), .. Read(OwnFolder, own: true)];

            foreach (Shot shot in found)
            {
                shot.Favourite = favourites.Contains(shot.Path);
            }

            return [.. found.OrderByDescending(shot => shot.At)];
        }

        /// <summary>Shots whose name or date contains every word of the query, in any order.</summary>
        public IReadOnlyList<Shot> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return All();
            }

            string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return
            [
                .. All().Where(shot => words.All(word =>
                    shot.Name.Contains(word, StringComparison.OrdinalIgnoreCase)
                    || shot.At.ToString("yyyy-MM-dd ddd dd MMM").Contains(word, StringComparison.OrdinalIgnoreCase)))
            ];
        }

        /// <summary>
        ///  Writes an image that arrived on the clipboard, unless it is already here.
        /// </summary>
        /// <returns>The path written, or null when it was a capture the gallery already has.</returns>
        public string? Keep(byte[] png, int width, int height, DateTime now)
        {
            string digest = Convert.ToHexString(SHA256.HashData(png));

            // The same image set again - an app re-copying it, or the user copying it back out of
            // the gallery - is not a new capture.
            if (digest == lastKept)
            {
                return null;
            }

            if (Read(SystemFolder, own: false).Any(shot => Matches(shot, width, height, now)))
            {
                lastKept = digest;

                return null;
            }

            try
            {
                Directory.CreateDirectory(OwnFolder);

                string path = Unique(System.IO.Path.Combine(OwnFolder, $"Clipboard {now:yyyy-MM-dd HHmmss}.png"));

                File.WriteAllBytes(path, png);
                File.SetLastWriteTime(path, now);
                lastKept = digest;

                return path;
            }
            catch (Exception)
            {
                // A read-only folder, or no disk. The capture is still on the clipboard.
                return null;
            }
        }

        /// <summary>
        ///  A file appeared in the system's folder: drops the clipboard copy of the same capture.
        /// </summary>
        /// <remarks>
        ///  The other half of the twin rule, for when the clipboard was read before Snipping Tool
        ///  had finished writing its file. The file wins because it is the one the user can find
        ///  without DevDeck.
        /// </remarks>
        public bool Arrived(string path)
        {
            if (!File.Exists(path) || ImageSize(path) is not { } size)
            {
                return false;
            }

            DateTime at = File.GetLastWriteTime(path);
            bool dropped = false;

            foreach (Shot twin in Read(OwnFolder, own: true)
                         .Where(shot => !favourites.Contains(shot.Path) && Matches(shot, size.Width, size.Height, at)))
            {
                try
                {
                    File.Delete(twin.Path);
                    dropped = true;
                }
                catch (Exception)
                {
                    // Held open by a viewer. It stays, and is swept with the rest later.
                }
            }

            return dropped;
        }

        /// <summary>
        ///  Deletes a shot, to the recycle bin where there is one.
        /// </summary>
        /// <remarks>
        ///  The recycle bin rather than a plain delete because most of these files are the user's
        ///  own, in their Pictures folder, and a screenshot deleted by a mis-click from a grid of
        ///  look-alike thumbnails should be one trip to the bin away from coming back.
        /// </remarks>
        public bool Remove(Shot shot)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        shot.Path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                else
                {
                    File.Delete(shot.Path);
                }
            }
            catch (Exception)
            {
                return false;
            }

            if (favourites.Remove(shot.Path))
            {
                SaveFavourites();
            }

            return true;
        }

        public void MarkFavourite(Shot shot, bool favourite)
        {
            shot.Favourite = favourite;

            if (favourite ? favourites.Add(shot.Path) : favourites.Remove(shot.Path))
            {
                SaveFavourites();
            }
        }

        /// <summary>
        ///  Drops DevDeck's own captures past the cap or the age limit. Never the system's folder.
        /// </summary>
        public int Sweep(DateTime now)
        {
            List<Shot> own = [.. Read(OwnFolder, own: true)
                .Where(shot => !favourites.Contains(shot.Path))
                .OrderByDescending(shot => shot.At)];

            int dropped = 0;

            for (int i = 0; i < own.Count; i++)
            {
                if (i < Cap && now - own[i].At <= KeepFor)
                {
                    continue;
                }

                try
                {
                    File.Delete(own[i].Path);
                    dropped++;
                }
                catch (Exception)
                {
                    // Open in a viewer. Next sweep.
                }
            }

            // A favourite whose file has gone - deleted in Explorer - is no longer anything.
            if (favourites.RemoveWhere(path => !File.Exists(path)) > 0)
            {
                SaveFavourites();
            }

            return dropped;
        }

        /// <summary>
        ///  The width and height from an image's header, without decoding it.
        /// </summary>
        /// <remarks>
        ///  PNG only, which is what every screenshot tool writes and what DevDeck writes itself.
        ///  Anything else answers null, which only costs the twin rule a match and the gallery the
        ///  dimensions in a caption.
        /// </remarks>
        public static (int Width, int Height)? ImageSize(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);

                return ImageSize(stream);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static (int Width, int Height)? ImageSize(Stream stream)
        {
            Span<byte> head = stackalloc byte[24];

            if (stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false) < head.Length)
            {
                return null;
            }

            ReadOnlySpan<byte> signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

            // The signature, then the IHDR chunk, which the format requires to come first.
            if (!head[..8].SequenceEqual(signature) || !head[12..16].SequenceEqual("IHDR"u8))
            {
                return null;
            }

            int width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(head[16..20]);
            int height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(head[20..24]);

            return width > 0 && height > 0 ? (width, height) : null;
        }

        /// <summary>
        ///  Where this system saves screenshots, if it does.
        /// </summary>
        /// <remarks>
        ///  On Windows the Screenshots known folder, which follows a Pictures folder redirected into
        ///  OneDrive, and only then the plain Pictures\Screenshots. On macOS the Desktop, where the
        ///  system puts them unless told otherwise - filtered by name, since a Desktop holds much
        ///  else. Elsewhere Pictures/Screenshots, which is GNOME's and KDE's default.
        /// </remarks>
        public static string? FindSystemFolder()
        {
            if (OperatingSystem.IsWindows() && KnownScreenshots() is { } known && Directory.Exists(known))
            {
                return known;
            }

            if (OperatingSystem.IsMacOS())
            {
                return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            string pictures = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots");

            return Directory.Exists(pictures) ? pictures : null;
        }

        /// <summary>
        ///  A number that changes whenever anything is put on the clipboard. Windows only; zero elsewhere.
        /// </summary>
        /// <remarks>
        ///  What makes watching the clipboard for images affordable: reading an image means
        ///  decoding a screenful of pixels, and doing that every second to find out nothing changed
        ///  would be the most expensive thing the app does while idle. This is one call.
        /// </remarks>
        public static uint ClipboardVersion() => OperatingSystem.IsWindows() ? GetClipboardSequenceNumber() : 0;

        private static bool Matches(Shot shot, int width, int height, DateTime at) =>
            (shot.At - at).Duration() <= Twin && shot.Size == (width, height);

        private IEnumerable<Shot> Read(string? folder, bool own)
        {
            if (folder is null || !Directory.Exists(folder))
            {
                return [];
            }

            try
            {
                bool desktop = OperatingSystem.IsMacOS() && !own;

                return
                [
                    .. new DirectoryInfo(folder).EnumerateFiles()
                        .Where(file => Kinds.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                        .Where(file => !desktop
                            || file.Name.StartsWith("Screenshot", StringComparison.OrdinalIgnoreCase)
                            || file.Name.StartsWith("Screen Shot", StringComparison.OrdinalIgnoreCase))
                        .Select(file => new Shot(file.FullName, file.LastWriteTime, file.Length, own))
                ];
            }
            catch (Exception)
            {
                // Unplugged, renamed or not ours to read. The other folder still shows.
                return [];
            }
        }

        private static string Unique(string path)
        {
            string stem = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(path) ?? string.Empty,
                System.IO.Path.GetFileNameWithoutExtension(path));

            for (int n = 2; File.Exists(path); n++)
            {
                path = $"{stem} ({n}).png";
            }

            return path;
        }

        private static List<string> LoadFavourites(string index)
        {
            try
            {
                if (File.Exists(index)
                    && JsonSerializer.Deserialize<List<string>>(File.ReadAllText(index)) is { } saved)
                {
                    return saved;
                }
            }
            catch (Exception)
            {
                // Unreadable: nothing is a favourite, and every file is still there.
            }

            return [];
        }

        private void SaveFavourites()
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(index) ?? ".");
                File.WriteAllText(index, JsonSerializer.Serialize(favourites.Order(PathComparer).ToList(), Options));
            }
            catch (Exception)
            {
                // Read-only folder. The favourites last for this session.
            }
        }

        private static string? KnownScreenshots()
        {
            // FOLDERID_Screenshots.
            Guid id = new("b7bede81-df94-4682-a7d8-57a52620b86f");

            if (SHGetKnownFolderPath(id, 0, IntPtr.Zero, out IntPtr found) != 0)
            {
                return null;
            }

            try
            {
                return Marshal.PtrToStringUni(found);
            }
            finally
            {
                Marshal.FreeCoTaskMem(found);
            }
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, IntPtr token, out IntPtr path);

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();
    }
}
