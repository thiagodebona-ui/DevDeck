namespace DevDeck.Core
{
    /// <summary>One thing the app did.</summary>
    internal sealed record LogEntry(DateTime At, LogLevel Level, string Source, string Text)
    {
        public string Stamp => At.ToString("HH:mm:ss");

        public bool IsError => Level == LogLevel.Error;

        public bool IsWarning => Level == LogLevel.Warning;

        public bool IsNotice => Level == LogLevel.Success;
    }

    /// <summary>
    ///  What the app itself has done this session.
    /// </summary>
    /// <remarks>
    ///  Distinct from a command's own output, which belongs to that command and is shown beside it.
    ///  This is the other half: the app deciding things on the user's behalf - a watch firing, a
    ///  settings file being migrated, a secret that could not be unwrapped, a request that failed
    ///  before it was sent. Until now all of that was either swallowed by a catch or shown once in
    ///  a status line and then lost.
    ///
    ///  In memory only, and deliberately. A log file that accumulates on disk is a thing the user
    ///  has to be told about, manage and eventually clear, and the questions this answers - why did
    ///  that just run, what happened when I pressed that - are all about the session in front of
    ///  them. Saving one is the user's choice, through <see cref="AsText"/>.
    /// </remarks>
    internal sealed class AppLog
    {
        /// <summary>
        ///  How much is kept.
        /// </summary>
        /// <remarks>
        ///  A bound rather than a guess at what is enough: a watch on a busy folder writes here
        ///  every time it fires, and an unbounded list in a long-running app is a leak with a
        ///  friendly name.
        /// </remarks>
        public const int Cap = 2000;

        private readonly LinkedList<LogEntry> entries = new();

        private readonly object gate = new();

        public static AppLog Instance { get; } = new();

        /// <summary>Raised on whichever thread wrote the entry, so a listener must marshal.</summary>
        public event Action<LogEntry>? Added;

        /// <summary>Everything kept, oldest first.</summary>
        public IReadOnlyList<LogEntry> All
        {
            get
            {
                lock (gate)
                {
                    return [.. entries];
                }
            }
        }

        public int Count
        {
            get
            {
                lock (gate)
                {
                    return entries.Count;
                }
            }
        }

        public void Write(string source, string text, LogLevel level = LogLevel.Info)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            LogEntry entry = new(DateTime.Now, level, source, text.Trim());

            lock (gate)
            {
                entries.AddLast(entry);

                while (entries.Count > Cap)
                {
                    entries.RemoveFirst();
                }
            }

            // Outside the lock: a handler that logs something itself would otherwise deadlock on a
            // lock this thread already holds.
            Added?.Invoke(entry);
        }

        public void Info(string source, string text) => Write(source, text);

        public void Good(string source, string text) => Write(source, text, LogLevel.Success);

        public void Warn(string source, string text) => Write(source, text, LogLevel.Warning);

        public void Bad(string source, string text) => Write(source, text, LogLevel.Error);

        /// <summary>
        ///  Records something that went wrong, without the stack trace.
        /// </summary>
        /// <remarks>
        ///  The type and the message, because that is what a user can act on. A stack trace through
        ///  the framework tells them nothing and buries the one line that does.
        /// </remarks>
        public void Failure(string source, string what, Exception exception) =>
            Bad(source, $"{what}: {exception.GetType().Name} - {exception.Message}");

        public void Clear()
        {
            lock (gate)
            {
                entries.Clear();
            }
        }

        /// <summary>The log as plain text, for saving or pasting into a bug report.</summary>
        public string AsText() => string.Join(
            Environment.NewLine,
            All.Select(entry => $"{entry.At:yyyy-MM-dd HH:mm:ss}  {entry.Level,-7}  {entry.Source,-12}  {entry.Text}"));
    }
}
