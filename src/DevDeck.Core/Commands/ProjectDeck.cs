using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevDeck.Core
{
    /// <summary>One command as a project file writes it.</summary>
    /// <remarks>
    ///  Deliberately not <see cref="CustomCommand"/>. This shape is a file format that other people
    ///  commit and edit by hand, so it holds only what a project can meaningfully say - a name, a
    ///  body, how to run it - and none of the per-machine state the app keeps alongside its own
    ///  commands. Keeping them separate means the app's storage can change without breaking every
    ///  .devdeck.json in every repository.
    /// </remarks>
    internal sealed class DeckEntry
    {
        public string Name { get; set; } = string.Empty;

        public string Command { get; set; } = string.Empty;

        /// <summary>"shell", "powershell", "batch" or "bash". Absent means shell.</summary>
        public string? Kind { get; set; }

        /// <summary>What this command is for, shown where the app has room for it.</summary>
        public string? Description { get; set; }

        /// <summary>Started and forgotten rather than watched - a dev server, an editor.</summary>
        public bool Detached { get; set; }

        [JsonIgnore]
        public CommandKind Parsed => Kind?.ToLowerInvariant() switch
        {
            "powershell" or "pwsh" or "ps1" => CommandKind.PowerShell,
            "batch" or "bat" or "cmd" => CommandKind.Batch,
            "bash" or "sh" or "shell-script" => CommandKind.Bash,
            _ => CommandKind.Shell,
        };
    }

    /// <summary>The whole of a .devdeck.json.</summary>
    internal sealed class DeckFile
    {
        /// <summary>What to call this set of commands, if not the folder's own name.</summary>
        public string? Name { get; set; }

        public List<DeckEntry> Commands { get; set; } = [];
    }

    /// <summary>
    ///  A deck committed alongside the code it operates on.
    /// </summary>
    /// <remarks>
    ///  The answer to the question this app otherwise leaves open: a new person clones the
    ///  repository and still has to be told how to build it. A .devdeck.json puts that in the
    ///  repository, so the commands arrive with the code and stay correct as it changes - the same
    ///  argument as a Makefile or a package.json scripts block, which is why those are what this
    ///  reads when there is no deck file to read.
    ///
    ///  Nothing here runs anything. The file is data, it is read, and the user decides what to
    ///  import - because a file fetched from a repository that silently became runnable commands
    ///  is a supply chain problem, not a feature.
    /// </remarks>
    internal static class ProjectDeck
    {
        public const string FileName = ".devdeck.json";

        private static readonly JsonSerializerOptions Reading = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly JsonSerializerOptions Writing = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Where the deck file for a folder would be.</summary>
        public static string PathIn(string folder) => Path.Combine(folder, FileName);

        public static bool Exists(string folder) =>
            !string.IsNullOrWhiteSpace(folder) && File.Exists(PathIn(folder));

        /// <summary>
        ///  Reads the deck file in a folder, or nothing.
        /// </summary>
        /// <remarks>
        ///  Never throws. A malformed file is a file someone is in the middle of editing, and the
        ///  panel that calls this has no better answer than carrying on without it.
        /// </remarks>
        public static DeckFile? Read(string folder)
        {
            if (!Exists(folder))
            {
                return null;
            }

            try
            {
                DeckFile? deck = JsonSerializer.Deserialize<DeckFile>(
                    File.ReadAllText(PathIn(folder)), Reading);

                if (deck is null)
                {
                    return null;
                }

                // A file may list nothing, or list entries with no body; neither is worth showing
                // as an importable command.
                deck.Commands = [.. deck.Commands.Where(entry =>
                    !string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrWhiteSpace(entry.Command))];

                return deck;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Turns a deck entry into something the app can run.</summary>
        public static CustomCommand ToCommand(DeckEntry entry) => new()
        {
            Name = entry.Name.Trim(),
            Command = entry.Command,
            Kind = entry.Parsed,
            Detached = entry.Detached,
        };

        /// <summary>
        ///  Writes the given commands out as a deck file.
        /// </summary>
        /// <remarks>
        ///  The other direction, and the one that makes the format worth having: a deck worked out
        ///  interactively can be committed, rather than described in a README and retyped by the
        ///  next person.
        /// </remarks>
        public static string Write(string folder, string? name, IEnumerable<CustomCommand> commands)
        {
            DeckFile deck = new()
            {
                Name = name,
                Commands =
                [
                    .. commands.Select(command => new DeckEntry
                    {
                        Name = command.Name,
                        Command = command.Command,
                        Kind = command.Kind switch
                        {
                            CommandKind.PowerShell => "powershell",
                            CommandKind.Batch => "batch",
                            CommandKind.Bash => "bash",
                            _ => null,
                        },
                        Detached = command.Detached,
                    })
                ],
            };

            string path = PathIn(folder);

            File.WriteAllText(path, JsonSerializer.Serialize(deck, Writing));

            return path;
        }

        /// <summary>A starter file, for a project that has none.</summary>
        public static string Sample(string folder, IEnumerable<CustomCommand> commands)
        {
            List<CustomCommand> some = [.. commands.Take(6)];

            return Write(
                folder,
                new DirectoryInfo(folder).Name,
                some.Count > 0
                    ? some
                    : [new CustomCommand { Name = "build", Command = "dotnet build" }]);
        }
    }
}
