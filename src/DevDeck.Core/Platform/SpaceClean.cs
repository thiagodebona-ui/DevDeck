using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DevDeck.Core
{
    /// <summary>One place disk space can be won back from.</summary>
    /// <remarks>Stored by name, like <see cref="MemoryStep"/>, so the numbers are free to move.</remarks>
    internal enum SpaceTarget
    {
        UserTemp,
        RecycleBin,
        CrashDumps,
        NuGetHttpCache,
        NpmCache,
        YarnCache,
        PipCache,
    }

    /// <summary>How much a target is holding: bytes, and the files they are in.</summary>
    internal readonly record struct SpaceMeasure(long Bytes, int Files)
    {
        public static readonly SpaceMeasure None = new(0, 0);
    }

    /// <summary>What freeing one target did.</summary>
    internal readonly record struct SpaceOutcome(SpaceTarget Target, long Freed, int Removed, int Skipped, string? Error)
    {
        public bool Failed => Error is not null;
    }

    /// <summary>
    ///  The disk half of the cleaner: temporary files, the recycle bin, crash dumps, and the
    ///  download caches a developer's package managers keep.
    /// </summary>
    /// <remarks>
    ///  Everything here is something that comes back by itself when it is needed - a cache is
    ///  downloaded again, a temp file is written again - except the recycle bin, which is why it
    ///  is never ticked by default and says "permanently" in its description.
    ///
    ///  What is deliberately not here: NuGet's global packages folder and similar package stores,
    ///  because emptying them turns the next restore of every solution into a full download; the
    ///  browsers' caches, which belong to programs that are usually running; and anything under
    ///  the Windows folder, which needs an administrator for little gain.
    ///
    ///  Deleting is cautious in three ways. Nothing is followed through a junction or symbolic
    ///  link, so a link inside a cache cannot lead the sweep somewhere else on the disk. A file in
    ///  use is skipped and counted rather than treated as an error. And temporary files are only
    ///  taken once they are a day old, because a program that is running now may still want them.
    /// </remarks>
    internal static class SpaceClean
    {
        #region Windows imports
        [StructLayout(LayoutKind.Sequential)]
        private struct RecycleBinInfo
        {
            public int Size;
            public long Bytes;
            public long Items;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHQueryRecycleBinW")]
        private static extern int QueryRecycleBin(string? root, ref RecycleBinInfo info);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHEmptyRecycleBinW")]
        private static extern int EmptyRecycleBin(IntPtr owner, string? root, uint flags);

        // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND: the tick box was the confirmation.
        private const uint QuietEmpty = 0x1 | 0x2 | 0x4;
        #endregion

        /// <summary>How old a temporary file must be before it is fair game.</summary>
        public static readonly TimeSpan TempAge = TimeSpan.FromDays(1);

        public static readonly IReadOnlyList<SpaceTarget> Order = Enum.GetValues<SpaceTarget>();

        /// <summary>Ticked on a first run: everything that refills itself. Not the recycle bin.</summary>
        public static readonly IReadOnlyList<SpaceTarget> Defaults =
        [
            SpaceTarget.UserTemp,
            SpaceTarget.CrashDumps,
            SpaceTarget.NuGetHttpCache,
            SpaceTarget.NpmCache,
            SpaceTarget.YarnCache,
            SpaceTarget.PipCache,
        ];

        /// <summary>
        ///  Whether this platform has the target at all.
        /// </summary>
        /// <remarks>
        ///  Temporary files are Windows only. On Linux the temp folder is /tmp, shared by every
        ///  user and full of sockets running programs depend on; on macOS it is per user but just
        ///  as busy. A day-old socket is not an abandoned file.
        /// </remarks>
        public static bool Supported(SpaceTarget target) => target switch
        {
            SpaceTarget.UserTemp or SpaceTarget.CrashDumps => OperatingSystem.IsWindows(),
            _ => true,
        };

        public static string Title(SpaceTarget target) => Strings.Text("Space" + target);

        public static string Explain(SpaceTarget target) => Supported(target)
            ? Strings.Text("Space" + target + "Why")
            : Strings.Text("SpaceWindowsOnly");

        /// <summary>Reads a stored list of target names back, as <see cref="MemoryClean.ParseSteps"/> does.</summary>
        public static List<SpaceTarget> ParseTargets(IReadOnlyCollection<string>? names)
        {
            if (names is null)
            {
                return Defaults.Where(Supported).ToList();
            }

            HashSet<SpaceTarget> parsed = [];

            foreach (string name in names)
            {
                if (Enum.TryParse(name, ignoreCase: true, out SpaceTarget target))
                {
                    parsed.Add(target);
                }
            }

            return Order.Where(parsed.Contains).ToList();
        }

        /// <summary>
        ///  The folders a target sweeps, those that exist and pass <see cref="IsSafeRoot"/>.
        /// </summary>
        public static IReadOnlyList<string> Folders(SpaceTarget target)
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            bool windows = OperatingSystem.IsWindows();
            bool mac = OperatingSystem.IsMacOS();

            string xdgCache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(home, ".cache");

            IEnumerable<string> candidates = target switch
            {
                SpaceTarget.UserTemp => [Path.GetTempPath()],
                SpaceTarget.RecycleBin when mac => [Path.Combine(home, ".Trash")],
                // The two halves of the freedesktop trash, swept separately so both folders stay.
                SpaceTarget.RecycleBin when !windows =>
                [
                    Path.Combine(home, ".local", "share", "Trash", "files"),
                    Path.Combine(home, ".local", "share", "Trash", "info"),
                ],
                SpaceTarget.CrashDumps =>
                [
                    Path.Combine(local, "CrashDumps"),
                    Path.Combine(local, "Microsoft", "Windows", "WER", "ReportArchive"),
                    Path.Combine(local, "Microsoft", "Windows", "WER", "ReportQueue"),
                ],
                SpaceTarget.NuGetHttpCache =>
                [
                    Environment.GetEnvironmentVariable("NUGET_HTTP_CACHE_PATH") is { Length: > 0 } custom
                        ? custom
                        : Path.Combine(windows ? local : Path.Combine(home, ".local", "share"), "NuGet", "v3-cache"),
                    Path.Combine(windows ? local : Path.Combine(home, ".local", "share"), "NuGet", "plugins-cache"),
                ],
                // _cacache only: the rest of the npm folder holds logs and a lock npm expects.
                SpaceTarget.NpmCache => [Path.Combine(windows ? Path.Combine(local, "npm-cache") : Path.Combine(home, ".npm"), "_cacache")],
                SpaceTarget.YarnCache =>
                [
                    windows ? Path.Combine(local, "Yarn", "Cache")
                    : mac ? Path.Combine(home, "Library", "Caches", "Yarn")
                    : Path.Combine(xdgCache, "yarn"),
                ],
                SpaceTarget.PipCache =>
                [
                    windows ? Path.Combine(local, "pip", "Cache")
                    : mac ? Path.Combine(home, "Library", "Caches", "pip")
                    : Path.Combine(xdgCache, "pip"),
                ],
                _ => [],
            };

            return candidates
                .Select(path => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)))
                .Where(path => Directory.Exists(path) && IsSafeRoot(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        ///  Refuses a folder that is too close to the top of the disk to sweep.
        /// </summary>
        /// <remarks>
        ///  Every folder here comes from a known location, but two of them can be redirected by an
        ///  environment variable, and a TEMP that someone pointed at C:\ must not become a sweep of
        ///  the whole drive. The folder has to be at least two levels below its root and must not
        ///  be the profile or the Windows folder themselves.
        /// </remarks>
        public static bool IsSafeRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                return false;
            }

            string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            string root = Path.GetPathRoot(full) ?? string.Empty;

            string[] below = full[root.Length..].Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            if (below.Length < 2)
            {
                return false;
            }

            string[] forbidden =
            [
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ];

            return !forbidden.Any(other => other.Length > 0
                && string.Equals(Path.TrimEndingDirectorySeparator(other), full, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>How much a target would free right now. Never throws.</summary>
        public static SpaceMeasure Measure(SpaceTarget target, CancellationToken token)
        {
            if (!Supported(target))
            {
                return SpaceMeasure.None;
            }

            try
            {
                if (target == SpaceTarget.RecycleBin && OperatingSystem.IsWindows())
                {
                    return MeasureRecycleBin();
                }

                long bytes = 0;
                int files = 0;

                foreach (string folder in Folders(target))
                {
                    foreach (FileInfo file in Candidates(target, folder, token))
                    {
                        bytes += file.Length;
                        files++;
                    }
                }

                return new SpaceMeasure(bytes, files);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return SpaceMeasure.None;
            }
        }

        /// <summary>Deletes what <see cref="Measure"/> counted. Never throws but for cancellation.</summary>
        public static SpaceOutcome Free(SpaceTarget target, CancellationToken token)
        {
            if (!Supported(target))
            {
                return new SpaceOutcome(target, 0, 0, 0, Strings.Text("SpaceWindowsOnly"));
            }

            try
            {
                if (target == SpaceTarget.RecycleBin && OperatingSystem.IsWindows())
                {
                    return EmptyRecycleBinWindows();
                }

                long freed = 0;
                int removed = 0;
                int skipped = 0;

                foreach (string folder in Folders(target))
                {
                    foreach (FileInfo file in Candidates(target, folder, token).ToList())
                    {
                        token.ThrowIfCancellationRequested();

                        try
                        {
                            long length = file.Length;

                            // Caches mark some of their files read-only, which File.Delete refuses.
                            if (file.IsReadOnly)
                            {
                                file.IsReadOnly = false;
                            }

                            file.Delete();
                            freed += length;
                            removed++;
                        }
                        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                        {
                            // In use, or not ours. Either way it stays, and is counted.
                            skipped++;
                        }
                    }

                    RemoveEmptyFolders(target, folder);
                }

                return new SpaceOutcome(target, freed, removed, skipped, null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return new SpaceOutcome(target, 0, 0, 0, exception.Message);
            }
        }

        /// <summary>Every file in <paramref name="folder"/> a sweep of <paramref name="target"/> may take.</summary>
        private static IEnumerable<FileInfo> Candidates(SpaceTarget target, string folder, CancellationToken token)
        {
            DateTime? cutoff = target == SpaceTarget.UserTemp ? DateTime.UtcNow - TempAge : null;
            string? spared = Spared(target, folder);

            foreach (FileInfo file in new DirectoryInfo(folder).EnumerateFiles("*", Walk))
            {
                token.ThrowIfCancellationRequested();

                if (spared is not null && file.FullName.StartsWith(spared, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (cutoff is { } before && file.LastWriteTimeUtc >= before)
                {
                    continue;
                }

                yield return file;
            }
        }

        /// <summary>
        ///  Folders left empty by the sweep, deepest first. The swept folder itself always stays.
        /// </summary>
        private static void RemoveEmptyFolders(SpaceTarget target, string folder)
        {
            DateTime? cutoff = target == SpaceTarget.UserTemp ? DateTime.UtcNow - TempAge : null;
            string? spared = Spared(target, folder);

            List<DirectoryInfo> folders;

            try
            {
                folders = [.. new DirectoryInfo(folder).EnumerateDirectories("*", Walk)
                    .OrderByDescending(directory => directory.FullName.Length)];
            }
            catch (Exception)
            {
                return;
            }

            foreach (DirectoryInfo directory in folders)
            {
                if (spared is not null
                    && (directory.FullName + Path.DirectorySeparatorChar).StartsWith(spared, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // A temp folder made a minute ago is probably about to be filled.
                if (cutoff is { } before && directory.LastWriteTimeUtc >= before)
                {
                    continue;
                }

                try
                {
                    // Not recursive: a folder that still has something in it keeps it.
                    directory.Delete(recursive: false);
                }
                catch (Exception)
                {
                    // Not empty, or in use.
                }
            }
        }

        /// <summary>
        ///  DevDeck's own folder in temp, which holds the script of every command running now.
        /// </summary>
        /// <remarks>
        ///  cmd.exe reads a batch file a line at a time as it runs, so deleting a long-running
        ///  command's script - a dev server started yesterday - breaks it the next time it reads.
        /// </remarks>
        private static string? Spared(SpaceTarget target, string folder) =>
            target == SpaceTarget.UserTemp
                ? Path.Combine(folder, "DevDeck") + Path.DirectorySeparatorChar
                : null;

        /// <summary>
        ///  Recurse, skip nothing for being hidden, and never step through a link.
        /// </summary>
        private static readonly EnumerationOptions Walk = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        [SupportedOSPlatform("windows")]
        private static SpaceMeasure MeasureRecycleBin()
        {
            RecycleBinInfo info = new() { Size = Marshal.SizeOf<RecycleBinInfo>() };

            return QueryRecycleBin(null, ref info) == 0
                ? new SpaceMeasure(info.Bytes, (int)Math.Min(info.Items, int.MaxValue))
                : SpaceMeasure.None;
        }

        [SupportedOSPlatform("windows")]
        private static SpaceOutcome EmptyRecycleBinWindows()
        {
            SpaceMeasure before = MeasureRecycleBin();

            if (before.Files == 0)
            {
                return new SpaceOutcome(SpaceTarget.RecycleBin, 0, 0, 0, null);
            }

            int result = EmptyRecycleBin(IntPtr.Zero, null, QuietEmpty);

            if (result != 0)
            {
                return new SpaceOutcome(SpaceTarget.RecycleBin, 0, 0, 0, $"HRESULT 0x{result:X8}");
            }

            SpaceMeasure after = MeasureRecycleBin();

            return new SpaceOutcome(
                SpaceTarget.RecycleBin,
                before.Bytes - after.Bytes,
                before.Files - after.Files,
                after.Files,
                null);
        }
    }
}
