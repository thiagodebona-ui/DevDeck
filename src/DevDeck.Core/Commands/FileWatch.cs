using System.IO.Enumeration;

namespace DevDeck.Core
{
    /// <summary>A command to run when files under a folder change.</summary>
    internal sealed class WatchRule
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>The command to run, by name.</summary>
        public string Command { get; set; } = string.Empty;

        /// <summary>The folder to watch. Empty means the current workspace.</summary>
        public string Folder { get; set; } = string.Empty;

        /// <summary>A semicolon-separated list of globs: "*.cs;*.axaml". Empty means everything.</summary>
        public string Pattern { get; set; } = "*.cs";

        /// <summary>
        ///  How long the changes must stop before the command runs.
        /// </summary>
        /// <remarks>
        ///  Not a nicety. A single save in most editors produces several filesystem events - a
        ///  temporary file, a rename, a write, sometimes an attribute change - and a build started
        ///  on the first of them races the rest. Waiting for quiet turns a burst into one run,
        ///  which is what the user meant by "when I save".
        /// </remarks>
        public int QuietMilliseconds { get; set; } = 700;

        public bool Enabled { get; set; } = true;

        /// <summary>
        ///  Whether firing is announced.
        /// </summary>
        /// <remarks>
        ///  On by default, which is the opposite of how a notification setting usually starts. A
        ///  watch runs a command the user did not press, often while they are in another
        ///  application entirely - so the first time one fires, silence is indistinguishable from
        ///  the feature not working, and the second time it is a build they did not know had
        ///  started. Anyone who finds it noisy can turn it off per rule, which is the granularity
        ///  that matters: one chatty watch should not silence the others.
        /// </remarks>
        public bool Notify { get; set; } = true;

        public override string ToString() => Name;

        /// <summary>The globs, split and cleaned.</summary>
        public IReadOnlyList<string> Patterns =>
        [
            .. Pattern.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ];
    }

    /// <summary>
    ///  What one settled burst of changes touched, handed to the command that runs because of it.
    /// </summary>
    /// <remarks>
    ///  A burst rather than a single file, because that is what a save actually is - and what makes
    ///  this worth passing on at all. A formatter run over a folder, a git checkout or a find and
    ///  replace across a project all arrive as one settled burst touching many files, and a command
    ///  told only "something changed" has to go and work out what, which is the work it was started
    ///  to avoid.
    ///
    ///  Paths are absolute. The relative form depends on what you measure from, and the watched
    ///  folder is not necessarily the folder the command runs in.
    /// </remarks>
    internal sealed record WatchedChange(
        IReadOnlyList<string> Paths,
        string Kind,
        DateTime At)
    {
        /// <summary>The file that set the burst off, which is the one worth naming in a sentence.</summary>
        public string First => Paths.Count > 0 ? Paths[0] : string.Empty;

        public int Count => Paths.Count;

        /// <summary>Just the file names, for saying out loud rather than for passing to a script.</summary>
        public IEnumerable<string> Names => Paths.Select(System.IO.Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!);
    }

    /// <summary>
    ///  Watches a folder and calls back once the writing has stopped.
    /// </summary>
    /// <remarks>
    ///  Everything hard about this is timing rather than watching. FileSystemWatcher is fine at
    ///  telling you something changed and terrible at telling you what a human did: a save is a
    ///  burst, a build is thousands of events, and a git checkout is a flood. So this debounces
    ///  hard, ignores the directories that generate noise rather than signal, and refuses to
    ///  restart while its own command is still running - without that last part, a watch on a
    ///  folder that its own build writes into is an infinite loop that looks exactly like the app
    ///  having crashed.
    /// </remarks>
    internal sealed class FileWatch : IDisposable
    {
        /// <summary>
        ///  Folders whose contents change because a build ran, not because anyone edited anything.
        /// </summary>
        /// <remarks>
        ///  The list that stops the loop. A watch on a .NET project sees bin and obj rewritten by
        ///  the very command it just started; without skipping them the build triggers the build.
        /// </remarks>
        private static readonly string[] Noise =
        [
            "bin", "obj", ".git", ".vs", ".idea", "node_modules", "target", "dist",
            "__pycache__", ".pytest_cache", ".gradle", "packages", ".next", ".venv",
        ];

        private readonly FileSystemWatcher watcher;
        private readonly Timer quiet;
        private readonly WatchRule rule;
        private readonly Action<WatchedChange> fire;
        private readonly Func<bool> busy;
        private readonly object gate = new();

        /// <summary>
        ///  The paths touched since the last run, in the order they were first seen.
        /// </summary>
        /// <remarks>
        ///  Ordered and de-duplicated, which a plain list or a plain set each get half right. One
        ///  save produces several events for the same file - a write, then a size change, then an
        ///  attribute change - and reporting it three times would be noise; but the first file in
        ///  the burst is the one a message names, so the order has to survive too.
        /// </remarks>
        private readonly List<string> touched = [];

        private readonly HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        private bool pending;
        private bool stopped;

        /// <summary>The kind of the most recent event in the burst - Changed, Created, Renamed.</summary>
        private string kind = nameof(WatcherChangeTypes.Changed);

        /// <summary>What the last settled burst touched, or null before the first one.</summary>
        public WatchedChange? Last { get; private set; }

        /// <summary>The last thing that set this off, for the panel to show.</summary>
        public string? LastChange { get; private set; }

        public DateTime? LastFired { get; private set; }

        /// <param name="busy">
        ///  Whether the command is already running. A watch that fires while its own command is
        ///  mid-run has nothing useful to start, and starting it anyway is how a save turns into
        ///  two concurrent builds fighting over the same output folder.
        /// </param>
        public FileWatch(WatchRule rule, string folder, Action<WatchedChange> fire, Func<bool> busy)
        {
            this.rule = rule;
            this.fire = fire;
            this.busy = busy;

            quiet = new Timer(_ => Settle(), null, Timeout.Infinite, Timeout.Infinite);

            watcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,

                // Deliberately not LastAccess, which fires when anything merely reads a file - a
                // search, an indexer, a backup - and would keep the watch permanently awake.
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size,

                // A checkout can outrun the default buffer, and an overflow loses events silently.
                InternalBufferSize = 64 * 1024,
            };

            watcher.Changed += Touched;
            watcher.Created += Touched;
            watcher.Deleted += Touched;
            watcher.Renamed += Touched;

            // Without this the whole thing is inert and looks like it is working.
            watcher.EnableRaisingEvents = true;
        }

        /// <summary>Whether a path is one this rule cares about.</summary>
        public bool Wanted(string path)
        {
            string relative = path.Replace('\\', '/');

            foreach (string skip in Noise)
            {
                if (relative.Contains($"/{skip}/", StringComparison.OrdinalIgnoreCase)
                    || relative.EndsWith($"/{skip}", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            IReadOnlyList<string> patterns = rule.Patterns;

            if (patterns.Count == 0)
            {
                return true;
            }

            string name = Path.GetFileName(path);

            foreach (string pattern in patterns)
            {
                // The framework's own glob matcher, so the syntax is the one the user already knows
                // from the shell rather than a second dialect invented here.
                if (FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true))
                {
                    return true;
                }
            }

            return false;
        }

        private void Touched(object sender, FileSystemEventArgs e)
        {
            if (stopped || !Wanted(e.FullPath))
            {
                return;
            }

            lock (gate)
            {
                pending = true;
                LastChange = Path.GetFileName(e.FullPath);

                if (seen.Add(e.FullPath))
                {
                    touched.Add(e.FullPath);
                }

                // The last thing to happen to the burst, which is the closest thing to a verb the
                // filesystem gives us. A rename is reported as Renamed rather than as a create and
                // a delete, so it is worth keeping distinct.
                kind = e.ChangeType.ToString();

                // Each event pushes the deadline out, so the run happens once the burst ends
                // rather than once per event in it.
                quiet.Change(rule.QuietMilliseconds, Timeout.Infinite);
            }
        }

        /// <summary>The burst has ended. Run, unless the last run has not.</summary>
        private void Settle()
        {
            lock (gate)
            {
                if (stopped || !pending)
                {
                    return;
                }

                if (busy())
                {
                    // Try again shortly rather than dropping the change: the edit that arrived
                    // mid-build is precisely the one worth rebuilding for.
                    quiet.Change(rule.QuietMilliseconds, Timeout.Infinite);

                    return;
                }

                pending = false;
                LastFired = DateTime.Now;

                Last = new WatchedChange([.. touched], kind, LastFired.Value);

                // Cleared here rather than after the callback, so files that change while the
                // command is running belong to the next burst instead of being reported twice.
                touched.Clear();
                seen.Clear();
            }

            fire(Last);
        }

        public void Dispose()
        {
            stopped = true;

            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            quiet.Dispose();
        }
    }
}
