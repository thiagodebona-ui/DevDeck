using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevDeck.Core
{
    /// <summary>One finished run of one command.</summary>
    internal sealed class RunRecord
    {
        public string Command { get; set; } = string.Empty;

        public DateTime At { get; set; }

        public int ExitCode { get; set; }

        /// <summary>How long it took, in milliseconds.</summary>
        public long Milliseconds { get; set; }

        /// <summary>Null for a run that was stopped rather than finished.</summary>
        public bool Stopped { get; set; }

        /// <summary>The workspace it ran against, so a duration is comparable to the right ones.</summary>
        public string Workspace { get; set; } = string.Empty;

        [JsonIgnore]
        public bool Ok => !Stopped && ExitCode == 0;

        [JsonIgnore]
        public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Milliseconds);

        /// <summary>The duration, at the precision a human reads it at.</summary>
        [JsonIgnore]
        public string Duration => Milliseconds < 1000
            ? $"{Milliseconds} ms"
            : Milliseconds < 60_000
                ? $"{Milliseconds / 1000.0:F1} s"
                : $"{Milliseconds / 60_000}m {Milliseconds % 60_000 / 1000}s";

        [JsonIgnore]
        public string When => At.ToString("ddd HH:mm");

        [JsonIgnore]
        public string Outcome => Stopped ? "stopped" : ExitCode == 0 ? "ok" : $"exit {ExitCode}";
    }

    /// <summary>
    ///  What each command has done, across restarts.
    /// </summary>
    /// <remarks>
    ///  A terminal forgets. That makes one question unanswerable in the place it matters most: is
    ///  this build getting slower? The durations are already measured on every run and were being
    ///  thrown away a second later, so keeping them is nearly free and turns the panel into
    ///  something a terminal cannot be.
    ///
    ///  Capped per command rather than overall, so a chatty command cannot push a rarely-run one
    ///  out of its own history. Kept beside the settings file rather than inside it: it is written
    ///  on every single run, and settings.json holds the things that would actually hurt to lose.
    /// </remarks>
    internal sealed class RunHistory
    {
        /// <summary>Runs kept per command. Enough for a trend, not enough to be a log file.</summary>
        private const int PerCommand = 20;

        private const string FileName = "history.json";

        private readonly Dictionary<string, List<RunRecord>> byCommand = new(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        private RunHistory()
        {
        }

        public static RunHistory Instance { get; } = Load();

        public static string Path => System.IO.Path.Combine(SettingsPath.Directory, FileName);

        /// <summary>Every run of a command, newest first.</summary>
        public IReadOnlyList<RunRecord> For(string command) =>
            byCommand.TryGetValue(command, out List<RunRecord>? runs)
                ? [.. runs.OrderByDescending(run => run.At)]
                : [];

        /// <summary>Records a finished run and writes the file.</summary>
        public void Add(RunRecord record)
        {
            if (record.Command.Length == 0)
            {
                return;
            }

            if (!byCommand.TryGetValue(record.Command, out List<RunRecord>? runs))
            {
                runs = [];
                byCommand[record.Command] = runs;
            }

            runs.Add(record);

            while (runs.Count > PerCommand)
            {
                runs.RemoveAt(0);
            }

            Save();
        }

        /// <summary>Forgets a command's history, for when the command itself is deleted.</summary>
        public void Forget(string command)
        {
            if (byCommand.Remove(command))
            {
                Save();
            }
        }

        /// <summary>Carries history across a rename, so editing a name does not lose the trend.</summary>
        public void Rename(string from, string to)
        {
            if (from == to || !byCommand.Remove(from, out List<RunRecord>? runs))
            {
                return;
            }

            foreach (RunRecord run in runs)
            {
                run.Command = to;
            }

            // A name that already has history keeps it, with the renamed runs folded in - the
            // alternative is silently discarding one of the two sets.
            if (byCommand.TryGetValue(to, out List<RunRecord>? existing))
            {
                existing.AddRange(runs);

                while (existing.Count > PerCommand)
                {
                    existing.RemoveAt(0);
                }
            }
            else
            {
                byCommand[to] = runs;
            }

            Save();
        }

        /// <summary>
        ///  The median duration of the successful runs, which is what a new run is worth comparing
        ///  against.
        /// </summary>
        /// <remarks>
        ///  Median rather than mean, and successes only. One cold first build or one run that
        ///  failed in two seconds drags a mean far enough to make the comparison useless, and it is
        ///  precisely the unusual run that the user is trying to notice.
        /// </remarks>
        public TimeSpan? Typical(string command)
        {
            long[] durations = [.. For(command).Where(run => run.Ok).Select(run => run.Milliseconds).Order()];

            if (durations.Length < 3)
            {
                return null;
            }

            return TimeSpan.FromMilliseconds(durations[durations.Length / 2]);
        }

        /// <summary>
        ///  How this run compares to the usual, in words, or nothing when there is no useful answer.
        /// </summary>
        public string? Compare(string command, TimeSpan elapsed)
        {
            if (Typical(command) is not { } typical || typical.TotalMilliseconds < 500)
            {
                return null;
            }

            double ratio = elapsed.TotalMilliseconds / typical.TotalMilliseconds;

            // Only remark on a difference big enough to be real rather than noise.
            return ratio switch
            {
                > 1.5 => $"{ratio:F1}x slower than usual",
                < 0.6 => $"{1 / ratio:F1}x faster than usual",
                _ => null,
            };
        }

        private static RunHistory Load()
        {
            RunHistory history = new();

            try
            {
                if (!File.Exists(Path))
                {
                    return history;
                }

                Dictionary<string, List<RunRecord>>? read =
                    JsonSerializer.Deserialize<Dictionary<string, List<RunRecord>>>(
                        File.ReadAllText(Path), Options);

                foreach ((string command, List<RunRecord> runs) in read ?? [])
                {
                    history.byCommand[command] = runs;
                }
            }
            catch (Exception)
            {
                // History is the most expendable thing the app stores. Start empty.
            }

            return history;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsPath.Directory);

                string temp = Path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(byCommand, Options));
                File.Move(temp, Path, overwrite: true);
            }
            catch (Exception)
            {
                // As above.
            }
        }
    }
}
