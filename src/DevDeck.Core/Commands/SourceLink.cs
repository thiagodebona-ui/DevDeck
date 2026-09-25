using System.Diagnostics;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>A file, and optionally a line and column, named somewhere in a line of output.</summary>
    internal sealed record SourceLink(string Path, int Line, int Column, int Start, int Length)
    {
        /// <summary>How the link reads in a tooltip.</summary>
        public string Display => Line > 0 ? $"{Path}:{Line}" : Path;
    }

    /// <summary>
    ///  Finds the file references in compiler and test output, so they can be opened.
    /// </summary>
    /// <remarks>
    ///  The single most repeated action after a failed build is reading the error, finding the path
    ///  in it, and going there by hand. Every compiler already prints the answer; nothing in the
    ///  app was reading it.
    ///
    ///  Three shapes cover essentially all of it: MSBuild and C# write "Path(12,5): error CS1002",
    ///  gcc, eslint, tsc, python and most Unix tools write "path:12:5", and stack traces write
    ///  "in path:line 12". A match is only offered once the path resolves to a file that exists
    ///  relative to the workspace, which is what keeps "Program.cs" in a sentence from becoming a
    ///  link, and keeps a link from ever pointing at nothing.
    /// </remarks>
    internal static partial class SourceLinks
    {
        /// <summary>MSBuild and the C# compiler: <c>Foo.cs(12,5): error CS1002</c>.</summary>
        [GeneratedRegex(@"(?<path>[A-Za-z]:[\\/][^\s(){}""'<>|*?]+|[^\s(){}""'<>|*?:]+\.[A-Za-z0-9]+)\((?<line>\d+)(?:,(?<col>\d+))?\)")]
        private static partial Regex MsBuild();

        /// <summary>gcc, tsc, eslint, pytest and friends: <c>src/foo.ts:12:5</c>.</summary>
        [GeneratedRegex(@"(?<path>[A-Za-z]:[\\/][^\s(){}""'<>|*?]+|[^\s(){}""'<>|*?:]+\.[A-Za-z0-9]+):(?<line>\d+)(?::(?<col>\d+))?")]
        private static partial Regex Colon();

        /// <summary>.NET stack traces: <c>in C:\src\Foo.cs:line 42</c>.</summary>
        [GeneratedRegex(@"(?<path>[A-Za-z]:[\\/][^\s(){}""'<>|*?]+|/[^\s(){}""'<>|*?]+):line (?<line>\d+)")]
        private static partial Regex StackTrace();

        /// <summary>
        ///  Every file reference in one line of output, in the order they appear and never
        ///  overlapping.
        /// </summary>
        /// <param name="line">The output line, with any ANSI already removed.</param>
        /// <param name="workspace">
        ///  The folder relative paths are resolved against. A link is only returned if the file is
        ///  really there, so an empty or missing workspace yields nothing rather than dead links.
        /// </param>
        public static IReadOnlyList<SourceLink> Find(string line, string workspace)
        {
            if (line.Length == 0 || line.Length > 4000)
            {
                return [];
            }

            List<SourceLink> found = [];

            // Stack traces first: their "…:line 42" tail would otherwise be half-claimed by the
            // colon form, which would take the path and lose the line number.
            foreach (Regex pattern in new[] { StackTrace(), MsBuild(), Colon() })
            {
                foreach (Match match in pattern.Matches(line))
                {
                    if (found.Any(link => match.Index < link.Start + link.Length
                        && link.Start < match.Index + match.Length))
                    {
                        continue;
                    }

                    if (Resolve(match.Groups["path"].Value, workspace) is not { } path)
                    {
                        continue;
                    }

                    found.Add(new SourceLink(
                        path,
                        Number(match, "line"),
                        Number(match, "col"),
                        match.Index,
                        match.Length));
                }
            }

            return [.. found.OrderBy(link => link.Start)];
        }

        private static int Number(Match match, string group) =>
            match.Groups[group].Success && int.TryParse(match.Groups[group].Value, out int value)
                ? value
                : 0;

        /// <summary>
        ///  The full path this reference names, or nothing if no such file exists.
        /// </summary>
        /// <remarks>
        ///  Existence is the whole filter. Output is full of things shaped like paths - package
        ///  names with dots, version numbers, URLs - and the only reliable way to tell a real file
        ///  reference from one of those is to go and look.
        /// </remarks>
        private static string? Resolve(string candidate, string workspace)
        {
            if (candidate.Length == 0)
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(candidate))
                {
                    return Exists(candidate) ? Path.GetFullPath(candidate) : null;
                }

                if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
                {
                    return null;
                }

                string full = Path.GetFullPath(Path.Combine(workspace, candidate));

                return Exists(full) ? full : null;
            }
            catch (Exception)
            {
                // A path too long, or with characters this platform will not accept. Not a link.
                return null;
            }
        }

        /// <summary>The answers already given, so the same path is only looked for once.</summary>
        private static readonly Dictionary<string, bool> Seen = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///  Anything past this and the table is emptied rather than grown without limit.
        /// </summary>
        /// <remarks>
        ///  Generous, because the thing being bounded is a dictionary of short strings and the
        ///  thing being avoided costs milliseconds each. A run that manages to name more distinct
        ///  paths than this has stopped being output anyone is reading.
        /// </remarks>
        private const int MostRemembered = 20_000;

        /// <summary>
        ///  <see cref="File.Exists(string)"/>, asked at most once per path.
        /// </summary>
        /// <remarks>
        ///  This is the freeze. Every line of output is searched for links, every candidate is
        ///  confirmed by going to the disk, and a grep across a large checkout produces tens of
        ///  thousands of lines naming a few hundred distinct files - so the same few hundred
        ///  questions were asked tens of thousands of times. On a local SSD that is slow; on a
        ///  synced folder - OneDrive, a network share, anything with a filter driver in the way -
        ///  each one is a round trip, and the measured cost was around a fifth of a millisecond a
        ///  line. Twenty thousand lines of `git grep` is therefore several seconds of a thread
        ///  doing nothing but re-asking a question it already had the answer to.
        ///
        ///  Cached for the life of the process rather than per run, because the answer is about a
        ///  path rather than about a run, and the common case is the same tree being searched over
        ///  and over.
        ///
        ///  The staleness this buys is deliberate and small: a file created after its name first
        ///  appeared in output will not become a link until the app is restarted. A link that is
        ///  briefly missing is a far cheaper wrong answer than a window that stops responding, and
        ///  the reverse - a link to a file that has since been deleted - is already handled, since
        ///  opening one has always had to cope with the file being gone by the time it is clicked.
        /// </remarks>
        private static bool Exists(string path)
        {
            lock (Seen)
            {
                if (Seen.TryGetValue(path, out bool known))
                {
                    return known;
                }
            }

            // Outside the lock: this is the slow part, and holding a lock across it would serialise
            // every caller behind one disk round trip - which is most of what this exists to avoid.
            bool there = File.Exists(path);

            lock (Seen)
            {
                if (Seen.Count >= MostRemembered)
                {
                    Seen.Clear();
                }

                Seen[path] = there;
            }

            return there;
        }

        /// <summary>
        ///  Forgets what is on disk, so the next line of output asks again.
        /// </summary>
        /// <remarks>
        ///  Called when a run starts. A build that has just written the files its errors name is
        ///  the one case where the cache above would be confidently wrong for the whole session,
        ///  and clearing it costs one dictionary allocation per run.
        /// </remarks>
        public static void Forget()
        {
            lock (Seen)
            {
                Seen.Clear();
            }
        }

        /// <summary>
        ///  Opens a link in the user's editor, falling back to whatever the desktop associates.
        /// </summary>
        /// <remarks>
        ///  VS Code and its forks take a "file:line:column" argument and land the cursor on the
        ///  error, which is the entire point of the feature, so they are tried first. Anything else
        ///  gets the plain path through the shell, which at least opens the right file.
        /// </remarks>
        public static void Open(SourceLink link)
        {
            foreach (string editor in Editors())
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = editor,
                        Arguments = link.Line > 0
                            ? $"--goto \"{link.Path}:{link.Line}:{Math.Max(link.Column, 1)}\""
                            : $"\"{link.Path}\"",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                    });

                    return;
                }
                catch (Exception)
                {
                    // Not installed, or not on PATH under that name. Try the next one.
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo { FileName = link.Path, UseShellExecute = true });
            }
            catch (Exception)
            {
                // Nothing is registered for this extension. There is nothing further to try, and a
                // failed click is not worth an error dialog.
            }
        }

        /// <summary>
        ///  The editors to try, most specific first.
        /// </summary>
        /// <remarks>
        ///  VISUAL and EDITOR are honoured before the guesses, because someone who has set them has
        ///  already answered this question.
        /// </remarks>
        private static IEnumerable<string> Editors()
        {
            foreach (string variable in new[] { "DEVDECK_EDITOR", "VISUAL", "EDITOR" })
            {
                string? set = Environment.GetEnvironmentVariable(variable);

                if (!string.IsNullOrWhiteSpace(set))
                {
                    yield return set;
                }
            }

            yield return "code";
            yield return "cursor";
            yield return "codium";
            yield return "subl";
        }
    }
}
