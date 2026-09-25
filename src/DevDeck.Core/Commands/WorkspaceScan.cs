using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>A runnable thing found in a project, ready to become a command.</summary>
    /// <remarks>
    ///  <see cref="Source"/> is what found it - "package.json", "Makefile" - and is shown rather
    ///  than hidden: the same name can come from two tools in one repository, and knowing which one
    ///  a button will run is the difference between trusting the list and ignoring it.
    /// </remarks>
    internal sealed record Runnable(string Name, string Command, string Source, CommandKind Kind)
    {
        /// <summary>The command this becomes when the user accepts it.</summary>
        public CustomCommand ToCommand() => new()
        {
            Name = Name,
            Command = Command,
            Kind = Kind,
        };
    }

    /// <summary>
    ///  Reads a project folder and reports what can be run in it.
    /// </summary>
    /// <remarks>
    ///  The honest problem with a command deck is the empty one. Ten starters help, but they are
    ///  guesses about a project the app has not looked at - while the project itself already
    ///  contains the answer, written down, in the file the team keeps up to date. Every editor
    ///  worth using reads these; nothing was reading them here.
    ///
    ///  Scanning is shallow on purpose. Only the workspace root and one level down, and never
    ///  inside node_modules, target, bin or their kin: a deep scan of a large monorepo takes long
    ///  enough to need a progress bar, and the runnables that matter are at the top anyway.
    ///
    ///  Nothing here runs anything. It reads files and proposes; the user picks what to keep.
    /// </remarks>
    internal static partial class WorkspaceScan
    {
        /// <summary>Folders never descended into, because nothing in them is a project of ours.</summary>
        private static readonly string[] Skipped =
        [
            "node_modules", ".git", ".svn", ".hg", "bin", "obj", "target", "dist", "build",
            "out", "vendor", ".venv", "venv", "__pycache__", ".next", ".nuxt", ".gradle",
            ".idea", ".vs", "packages",
        ];

        /// <summary>A Makefile target: a name at the start of a line, before a colon.</summary>
        [GeneratedRegex(@"^(?<name>[A-Za-z0-9][A-Za-z0-9_\-.]*)\s*:(?!=)", RegexOptions.Multiline)]
        private static partial Regex MakeTarget();

        /// <summary>A just recipe, which is a Makefile target with optional parameters after it.</summary>
        [GeneratedRegex(@"^(?<name>[A-Za-z0-9][A-Za-z0-9_\-]*)(?<args>(?:\s+[A-Za-z_][A-Za-z0-9_]*)*)\s*:(?!=)", RegexOptions.Multiline)]
        private static partial Regex JustRecipe();

        /// <summary>Everything runnable in a workspace, deduplicated and in a sensible order.</summary>
        public static IReadOnlyList<Runnable> Scan(string workspace)
        {
            if (string.IsNullOrWhiteSpace(workspace) || !Directory.Exists(workspace))
            {
                return [];
            }

            List<Runnable> found = [];

            foreach (string directory in Roots(workspace))
            {
                // The prefix keeps a nested project's "build" from colliding with the root's, and
                // says where it came from at a glance.
                string prefix = Prefix(workspace, directory);

                found.AddRange(Node(directory, prefix));
                found.AddRange(Make(directory, prefix));
                found.AddRange(Just(directory, prefix));
                found.AddRange(Dotnet(directory, prefix));
                found.AddRange(Compose(directory, prefix));
                found.AddRange(Python(directory, prefix));
                found.AddRange(Rust(directory, prefix));
                found.AddRange(Go(directory, prefix));
                found.AddRange(Scripts(directory, prefix));
            }

            // Same name from the same source twice is a scan artefact, not two things to run.
            return [.. found
                .GroupBy(r => (r.Name, r.Source), StringComparer.OrdinalIgnoreCase.Wrap())
                .Select(group => group.First())
                .OrderBy(r => r.Source, StringComparer.Ordinal)
                .ThenBy(r => r.Name, StringComparer.Ordinal)];
        }

        /// <summary>The workspace root, then one level of child folders worth looking in.</summary>
        private static IEnumerable<string> Roots(string workspace)
        {
            yield return workspace;

            IEnumerable<string> children;

            try
            {
                children = Directory.EnumerateDirectories(workspace);
            }
            catch (Exception)
            {
                yield break;
            }

            foreach (string child in children)
            {
                string name = System.IO.Path.GetFileName(child);

                if (name.StartsWith('.') || Skipped.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return child;
            }
        }

        private static string Prefix(string workspace, string directory) =>
            string.Equals(
                System.IO.Path.GetFullPath(workspace).TrimEnd(System.IO.Path.DirectorySeparatorChar),
                System.IO.Path.GetFullPath(directory).TrimEnd(System.IO.Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : System.IO.Path.GetFileName(directory) + ": ";

        /// <summary>
        ///  npm scripts, with the package manager the lockfile actually names.
        /// </summary>
        /// <remarks>
        ///  The lockfile rather than a guess: running `npm run build` in a pnpm workspace does not
        ///  merely differ in speed, it resolves a different dependency tree and often fails outright.
        /// </remarks>
        private static IEnumerable<Runnable> Node(string directory, string prefix)
        {
            string manifest = System.IO.Path.Combine(directory, "package.json");

            if (!File.Exists(manifest))
            {
                yield break;
            }

            string runner =
                File.Exists(System.IO.Path.Combine(directory, "pnpm-lock.yaml")) ? "pnpm"
                : File.Exists(System.IO.Path.Combine(directory, "yarn.lock")) ? "yarn"
                : File.Exists(System.IO.Path.Combine(directory, "bun.lockb")) ? "bun"
                : "npm";

            string[] names;

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));

                if (!document.RootElement.TryGetProperty("scripts", out JsonElement scripts)
                    || scripts.ValueKind != JsonValueKind.Object)
                {
                    yield break;
                }

                names = [.. scripts.EnumerateObject().Select(property => property.Name)];
            }
            catch (Exception)
            {
                // Malformed or half-written package.json. Nothing to offer from it.
                yield break;
            }

            foreach (string name in names)
            {
                // yarn takes the script name bare; the rest want "run".
                string verb = runner == "yarn" ? name : $"run {name}";

                yield return new Runnable(
                    $"{prefix}{name}",
                    In(directory, prefix, $"{runner} {verb}"),
                    "package.json",
                    CommandKind.Shell);
            }
        }

        private static IEnumerable<Runnable> Make(string directory, string prefix)
        {
            foreach (string file in new[] { "Makefile", "makefile", "GNUmakefile" })
            {
                string path = System.IO.Path.Combine(directory, file);

                if (!File.Exists(path))
                {
                    continue;
                }

                foreach (string name in Targets(path, MakeTarget()))
                {
                    yield return new Runnable(
                        $"{prefix}make {name}",
                        In(directory, prefix, $"make {name}"),
                        "Makefile",
                        CommandKind.Shell);
                }

                yield break;
            }
        }

        private static IEnumerable<Runnable> Just(string directory, string prefix)
        {
            foreach (string file in new[] { "justfile", "Justfile", ".justfile" })
            {
                string path = System.IO.Path.Combine(directory, file);

                if (!File.Exists(path))
                {
                    continue;
                }

                foreach (string name in Targets(path, JustRecipe()))
                {
                    yield return new Runnable(
                        $"{prefix}just {name}",
                        In(directory, prefix, $"just {name}"),
                        "justfile",
                        CommandKind.Shell);
                }

                yield break;
            }
        }

        /// <summary>
        ///  Target names out of a make-style file.
        /// </summary>
        /// <remarks>
        ///  Special targets and the ones conventionally marked private are dropped: .PHONY is a
        ///  declaration rather than something to run, and a leading underscore is the usual signal
        ///  for an internal recipe.
        /// </remarks>
        private static IEnumerable<string> Targets(string path, Regex pattern)
        {
            string text;

            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception)
            {
                yield break;
            }

            HashSet<string> seen = new(StringComparer.Ordinal);

            foreach (Match match in pattern.Matches(text))
            {
                string name = match.Groups["name"].Value;

                if (name.StartsWith('.') || name.StartsWith('_') || !seen.Add(name))
                {
                    continue;
                }

                yield return name;
            }
        }

        private static IEnumerable<Runnable> Dotnet(string directory, string prefix)
        {
            string[] projects;

            try
            {
                projects =
                [
                    .. Directory.EnumerateFiles(directory, "*.sln"),
                    .. Directory.EnumerateFiles(directory, "*.csproj"),
                    .. Directory.EnumerateFiles(directory, "*.fsproj"),
                ];
            }
            catch (Exception)
            {
                yield break;
            }

            if (projects.Length == 0)
            {
                yield break;
            }

            // The solution if there is one, otherwise let dotnet find the single project itself.
            string? solution = projects.FirstOrDefault(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
            string target = solution is null ? string.Empty : $" \"{System.IO.Path.GetFileName(solution)}\"";

            yield return new Runnable($"{prefix}dotnet build", In(directory, prefix, $"dotnet build{target}"), ".NET", CommandKind.Shell);
            yield return new Runnable($"{prefix}dotnet test", In(directory, prefix, $"dotnet test{target}"), ".NET", CommandKind.Shell);
            yield return new Runnable($"{prefix}dotnet restore", In(directory, prefix, $"dotnet restore{target}"), ".NET", CommandKind.Shell);
        }

        private static IEnumerable<Runnable> Compose(string directory, string prefix)
        {
            foreach (string file in new[] { "docker-compose.yml", "docker-compose.yaml", "compose.yml", "compose.yaml" })
            {
                if (!File.Exists(System.IO.Path.Combine(directory, file)))
                {
                    continue;
                }

                yield return new Runnable($"{prefix}compose up", In(directory, prefix, "docker compose up -d"), "Compose", CommandKind.Shell);
                yield return new Runnable($"{prefix}compose down", In(directory, prefix, "docker compose down"), "Compose", CommandKind.Shell);
                yield return new Runnable($"{prefix}compose logs", In(directory, prefix, "docker compose logs -f --tail=100"), "Compose", CommandKind.Shell);
                yield return new Runnable($"{prefix}compose ps", In(directory, prefix, "docker compose ps"), "Compose", CommandKind.Shell);

                yield break;
            }
        }

        private static IEnumerable<Runnable> Python(string directory, string prefix)
        {
            bool poetry = File.Exists(System.IO.Path.Combine(directory, "poetry.lock"));
            bool uv = File.Exists(System.IO.Path.Combine(directory, "uv.lock"));
            bool project = File.Exists(System.IO.Path.Combine(directory, "pyproject.toml"));
            bool requirements = File.Exists(System.IO.Path.Combine(directory, "requirements.txt"));

            if (!project && !requirements)
            {
                yield break;
            }

            string run = uv ? "uv run " : poetry ? "poetry run " : string.Empty;

            yield return new Runnable($"{prefix}pytest", In(directory, prefix, $"{run}pytest -q"), "Python", CommandKind.Shell);

            if (requirements && run.Length == 0)
            {
                yield return new Runnable(
                    $"{prefix}pip install",
                    In(directory, prefix, "pip install -r requirements.txt"),
                    "Python",
                    CommandKind.Shell);
            }
        }

        private static IEnumerable<Runnable> Rust(string directory, string prefix)
        {
            if (!File.Exists(System.IO.Path.Combine(directory, "Cargo.toml")))
            {
                yield break;
            }

            yield return new Runnable($"{prefix}cargo build", In(directory, prefix, "cargo build"), "Cargo", CommandKind.Shell);
            yield return new Runnable($"{prefix}cargo test", In(directory, prefix, "cargo test"), "Cargo", CommandKind.Shell);
            yield return new Runnable($"{prefix}cargo clippy", In(directory, prefix, "cargo clippy --all-targets"), "Cargo", CommandKind.Shell);
        }

        private static IEnumerable<Runnable> Go(string directory, string prefix)
        {
            if (!File.Exists(System.IO.Path.Combine(directory, "go.mod")))
            {
                yield break;
            }

            yield return new Runnable($"{prefix}go build", In(directory, prefix, "go build ./..."), "Go", CommandKind.Shell);
            yield return new Runnable($"{prefix}go test", In(directory, prefix, "go test ./..."), "Go", CommandKind.Shell);
        }

        /// <summary>
        ///  Loose scripts in the conventional folders.
        /// </summary>
        /// <remarks>
        ///  Run through their own interpreter rather than through the shell, so a .ps1 keeps working
        ///  when the workspace's default kind is Shell - and so the app never hands a .sh to cmd.
        /// </remarks>
        private static IEnumerable<Runnable> Scripts(string directory, string prefix)
        {
            foreach (string folder in new[] { "scripts", "script", "tools", "bin" })
            {
                string path = System.IO.Path.Combine(directory, folder);

                if (!Directory.Exists(path))
                {
                    continue;
                }

                string[] files;

                try
                {
                    files = [.. Directory.EnumerateFiles(path)
                        .Where(f => f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
                        .Take(30)];
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (string file in files)
                {
                    bool powershell = file.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase);

                    // A script is only offered where it can actually run.
                    if (!powershell && OperatingSystem.IsWindows() && ScriptFile.FindBash() is null)
                    {
                        continue;
                    }

                    string relative = System.IO.Path.GetRelativePath(directory, file).Replace('\\', '/');

                    yield return new Runnable(
                        $"{prefix}{System.IO.Path.GetFileNameWithoutExtension(file)}",
                        In(directory, prefix, powershell
                            ? $"powershell -NoProfile -ExecutionPolicy Bypass -File \"{relative}\""
                            : $"sh \"{relative}\""),
                        folder + "/",
                        CommandKind.Shell);
                }
            }
        }

        /// <summary>
        ///  Prefixes a cd for anything found below the workspace root.
        /// </summary>
        /// <remarks>
        ///  Commands run against the workspace, so a nested project's build has to step down into
        ///  its own folder first or it builds the wrong thing - or nothing. The && is portable
        ///  between cmd and sh, and means a failed cd does not go on to run the command in the
        ///  wrong place.
        /// </remarks>
        private static string In(string directory, string prefix, string command) =>
            prefix.Length == 0 ? command : $"cd \"{System.IO.Path.GetFileName(directory)}\" && {command}";
    }

    /// <summary>Lets a string comparer be used on a tuple key without writing a comparer class.</summary>
    internal static class ComparerExtensions
    {
        public static IEqualityComparer<(string, string)> Wrap(this StringComparer comparer) =>
            new TupleComparer(comparer);

        private sealed class TupleComparer(StringComparer inner) : IEqualityComparer<(string, string)>
        {
            public bool Equals((string, string) x, (string, string) y) =>
                inner.Equals(x.Item1, y.Item1) && inner.Equals(x.Item2, y.Item2);

            public int GetHashCode((string, string) value) =>
                HashCode.Combine(inner.GetHashCode(value.Item1), inner.GetHashCode(value.Item2));
        }
    }
}
