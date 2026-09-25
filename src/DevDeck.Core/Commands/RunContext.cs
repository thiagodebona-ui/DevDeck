namespace DevDeck.Core
{
    /// <summary>
    ///  What a command is told about why it is running.
    /// </summary>
    /// <remarks>
    ///  A command started by hand knows everything it needs from its working directory. One started
    ///  by a file watch does not: the interesting part is which files changed, and without it the
    ///  script has to re-derive from the filesystem what the watcher already knew - usually by
    ///  rebuilding everything, which is the cost the watch was meant to avoid.
    ///
    ///  Passed as environment variables rather than as arguments. A command here is a script body
    ///  rather than an argv, so there is nowhere to put $1; and splicing paths into the text before
    ///  running it breaks on the first path containing a space or an ampersand. Every shell this
    ///  app runs - cmd, PowerShell, sh - reads variables the same way and needs no quoting to do it.
    ///
    ///  Every name is prefixed DEVDECK_ so that nothing here can collide with a variable the user's
    ///  own environment already relies on.
    /// </remarks>
    internal static class RunContext
    {
        /// <summary>Why this run started: "manual", "watch" or "chain".</summary>
        public const string Trigger = "DEVDECK_TRIGGER";

        /// <summary>The name of the watch rule that fired, when one did.</summary>
        public const string WatchName = "DEVDECK_WATCH_NAME";

        /// <summary>The folder being watched.</summary>
        public const string WatchFolder = "DEVDECK_WATCH_FOLDER";

        /// <summary>The full path of the first file in the burst.</summary>
        public const string ChangedFile = "DEVDECK_CHANGED_FILE";

        /// <summary>Just the file name of that first file, with no folder in front of it.</summary>
        public const string ChangedName = "DEVDECK_CHANGED_NAME";

        /// <summary>How many distinct files the burst touched.</summary>
        public const string ChangedCount = "DEVDECK_CHANGED_COUNT";

        /// <summary>What happened: Changed, Created, Deleted or Renamed.</summary>
        public const string ChangedKind = "DEVDECK_CHANGED_KIND";

        /// <summary>When the burst settled, as ISO-8601.</summary>
        public const string ChangedAt = "DEVDECK_CHANGED_AT";

        /// <summary>
        ///  Every changed path, newline-separated.
        /// </summary>
        /// <remarks>
        ///  A newline rather than a semicolon or a space, because a path may legally contain both
        ///  of those and may not contain a newline. That makes this safe to split on in every shell
        ///  without quoting rules entering into it.
        /// </remarks>
        public const string ChangedFiles = "DEVDECK_CHANGED_FILES";

        /// <summary>The variables a manually started run is given.</summary>
        public static IReadOnlyDictionary<string, string> Manual() => new Dictionary<string, string>
        {
            [Trigger] = "manual",
        };

        /// <summary>The name of the chain running this step.</summary>
        public const string ChainName = "DEVDECK_CHAIN";

        /// <summary>Which step this is, counting from one.</summary>
        public const string StepNumber = "DEVDECK_STEP";

        /// <summary>How many steps the chain has in total.</summary>
        public const string StepCount = "DEVDECK_STEPS";

        /// <summary>The name of the step that ran immediately before this one.</summary>
        public const string PreviousName = "DEVDECK_PREVIOUS_NAME";

        /// <summary>
        ///  Everything the previous step printed, newlines and all.
        /// </summary>
        /// <remarks>
        ///  The whole output rather than a value the step nominated, because a script has no way
        ///  to nominate one - it writes to stdout and that is the whole of its vocabulary. Making
        ///  it declare a return value would mean inventing a protocol ("write DEVDECK_OUT=x on the
        ///  last line") that every step would have to opt into, and the steps people already have
        ///  would all pass nothing.
        ///
        ///  Capped, because this is an environment variable: Windows will refuse a process whose
        ///  whole block is too large, and a step that greps a large tree can print megabytes. The
        ///  tail is kept rather than the head - a script's answer is what it printed last.
        /// </remarks>
        public const string Previous = "DEVDECK_PREVIOUS";

        /// <summary>
        ///  The last non-empty line the previous step printed.
        /// </summary>
        /// <remarks>
        ///  The one that is actually usable without parsing. A step written to feed another ends
        ///  by echoing one value - a branch name, a version, a path - and this is that value,
        ///  already trimmed, ready to be used directly.
        /// </remarks>
        public const string PreviousLine = "DEVDECK_PREVIOUS_LINE";

        /// <summary>What the previous step exited with. "0" is success.</summary>
        public const string PreviousExit = "DEVDECK_PREVIOUS_EXIT";

        /// <summary>
        ///  How much of the previous step's output is passed on, in characters.
        /// </summary>
        /// <remarks>
        ///  Windows caps a process's whole environment block at 32,767 characters, and that block
        ///  has to hold the user's own PATH as well as everything here. Sixteen thousand leaves
        ///  room for an ordinary environment and is far past what any step written to feed another
        ///  one actually prints. Past it the tail is kept and the front is dropped, with a marker
        ///  saying so - a truncation nobody is told about is a bug report about missing data.
        /// </remarks>
        public const int MostPassedOn = 16000;

        /// <summary>
        ///  How much of the previous step's last line is passed on, in characters.
        /// </summary>
        /// <remarks>
        ///  Far smaller than the output cap, because this one is a value rather than a log - a
        ///  path, a version, a count, a branch name. A "value" two thousand characters long is a
        ///  script that printed its results on one line, and truncating it costs nothing that
        ///  DEVDECK_PREVIOUS is not already carrying.
        ///
        ///  It needs its own cap rather than inheriting the output's: a step that prints half a
        ///  megabyte with no newline in it has a last line half a megabyte long, and clipping the
        ///  output would not have clipped that.
        /// </remarks>
        public const int MostOfALine = 2000;

        /// <summary>
        ///  The variables a run started by a chain is given.
        /// </summary>
        /// <remarks>
        ///  Each step sees the step before it, not every step so far. A chain is a pipeline - the
        ///  shell equivalent is <c>a | b | c</c>, where b reads a and c reads b - and accumulating
        ///  all of it would mean the fourth step receiving three outputs with no way to tell which
        ///  is which. Anything a step needs to send further than one hop, it re-prints.
        ///
        ///  The first step gets the chain variables and no previous anything, so a script can
        ///  check whether it is being fed by testing DEVDECK_PREVIOUS_NAME for empty.
        /// </remarks>
        public static IReadOnlyDictionary<string, string> Chain(
            string chain,
            int step = 0,
            int steps = 0,
            string? previousName = null,
            string? previousOutput = null,
            int previousExit = 0)
        {
            Dictionary<string, string> given = new()
            {
                [Trigger] = "chain",
                [ChainName] = chain,

                // Kept for the watch-shaped scripts that already read it, and because a step that
                // wants "what started me" should not have to know which of two names to look at.
                [WatchName] = chain,
            };

            if (steps > 0)
            {
                given[StepNumber] = step.ToString(System.Globalization.CultureInfo.InvariantCulture);
                given[StepCount] = steps.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            if (previousName is not { Length: > 0 })
            {
                return given;
            }

            string output = previousOutput ?? string.Empty;

            string passed = Clip(output);

            given[PreviousName] = previousName;
            given[Previous] = passed;

            // Taken from the clipped text rather than the original, so that what a step is told
            // its predecessor's last line was is a line that is actually in what it was handed.
            given[PreviousLine] = Shorten(LastLine(passed));
            given[PreviousExit] = previousExit.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return given;
        }

        /// <summary>Cuts an over-long value down, saying that it did so.</summary>
        /// <remarks>
        ///  The front is kept here, unlike the output, because the front of a value is the useful
        ///  end of it - a truncated path still says which folder it was in.
        /// </remarks>
        internal static string Shorten(string line)
        {
            if (line.Length <= MostOfALine)
            {
                return line;
            }

            const string note = " [line truncated]";

            return line[..(MostOfALine - note.Length)] + note;
        }

        /// <summary>Keeps the tail of a long output, saying that it did so.</summary>
        internal static string Clip(string output)
        {
            if (output.Length <= MostPassedOn)
            {
                return output;
            }

            const string note = "[earlier output dropped - too long to pass on]"
                + "\n";

            return note + output[^(MostPassedOn - note.Length)..];
        }

        /// <summary>
        ///  The last line with something on it.
        /// </summary>
        /// <remarks>
        ///  Searched from the end rather than splitting the whole output, because the output may
        ///  be thousands of lines and only the end of it is being asked about. Blank lines are
        ///  skipped: a script that ends with a newline - which most do - would otherwise hand the
        ///  next step an empty string.
        /// </remarks>
        internal static string LastLine(string output)
        {
            for (int at = output.Length - 1; at >= 0;)
            {
                int start = output.LastIndexOf('\n', at);
                string line = output[(start + 1)..(at + 1)].Trim();

                if (line.Length > 0)
                {
                    return line;
                }

                if (start < 0)
                {
                    return string.Empty;
                }

                at = start - 1;
            }

            return string.Empty;
        }

        /// <summary>
        ///  The variables a run started by a file watch is given.
        /// </summary>
        /// <remarks>
        ///  The count is written with the invariant culture: it is read back by a script, and a
        ///  number that renders differently depending on the machine's regional settings is a bug
        ///  waiting for the first user outside the developer's locale.
        /// </remarks>
        public static IReadOnlyDictionary<string, string> Watch(
            string rule,
            string folder,
            WatchedChange change) => new Dictionary<string, string>
            {
                [Trigger] = "watch",
                [WatchName] = rule,
                [WatchFolder] = folder,
                [ChangedFile] = change.First,
                [ChangedName] = System.IO.Path.GetFileName(change.First),
                [ChangedCount] = change.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [ChangedKind] = change.Kind,
                [ChangedAt] = change.At.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                [ChangedFiles] = string.Join("\n", change.Paths),
            };
    }
}
