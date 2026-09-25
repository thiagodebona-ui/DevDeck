using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>One container, as the engine describes it.</summary>
    internal sealed record ContainerInfo(
        string Id,
        string Name,
        string Image,
        string State,
        string Status,
        string Ports)
    {
        public bool IsRunning => State.Equals("running", StringComparison.OrdinalIgnoreCase);

        /// <summary>The id at the length everyone actually types.</summary>
        public string Short => Id.Length > 12 ? Id[..12] : Id;

        /// <summary>
        ///  The image without its registry prefix.
        /// </summary>
        /// <remarks>
        ///  A row reading "myregistry.azurecr.io/team/service:1.4.2-rc" tells you nothing the column
        ///  beside it does not, and pushes the state off the edge. The tail is the part that
        ///  identifies the thing.
        /// </remarks>
        public string ImageName
        {
            get
            {
                int slash = Image.LastIndexOf('/');

                return slash >= 0 && slash < Image.Length - 1 ? Image[(slash + 1)..] : Image;
            }
        }

        /// <summary>The first published host port, which is what a browser would need.</summary>
        public int? Published
        {
            get
            {
                // "0.0.0.0:8080->80/tcp, :::8080->80/tcp" - the host port is what precedes the arrow.
                foreach (string part in Ports.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    int arrow = part.IndexOf("->", StringComparison.Ordinal);

                    if (arrow < 0)
                    {
                        continue;
                    }

                    string host = part[..arrow];
                    int colon = host.LastIndexOf(':');

                    if (colon >= 0 && int.TryParse(host[(colon + 1)..], out int port))
                    {
                        return port;
                    }
                }

                return null;
            }
        }

        public bool HasPublished => Published is not null;

        public string Url => $"http://localhost:{Published}";
    }

    /// <summary>
    ///  The containers on this machine, and the four things anyone does to one.
    /// </summary>
    /// <remarks>
    ///  Modelled on lazydocker rather than on Docker Desktop: not an engine manager, just the list
    ///  and start/stop/restart/logs, which is the whole of what a developer does to a container
    ///  between one build and the next.
    ///
    ///  The engine is spoken to through its CLI rather than its socket. A socket client would be
    ///  faster and would mean an HTTP stack, an API version to track, and a second implementation
    ///  for the named pipe on Windows - in exchange for information this panel does not display.
    ///  The CLI is present wherever the engine is, and podman answers to the same verbs, which is
    ///  why it is tried second rather than ignored.
    /// </remarks>
    internal static class Containers
    {
        /// <summary>
        ///  Which CLI is available, decided once.
        /// </summary>
        /// <remarks>
        ///  Cached because it is asked on every refresh and the answer cannot change without the
        ///  user installing something, at which point restarting the app is a reasonable ask.
        /// </remarks>
        private static string? engine;

        private static bool looked;

        /// <summary>The engine in use, or null if neither is installed.</summary>
        public static string? Engine
        {
            get
            {
                if (looked)
                {
                    return engine;
                }

                looked = true;

                foreach (string candidate in new[] { "docker", "podman" })
                {
                    if (Ports.Run(candidate, "version --format {{.Client.Version}}", 4000).Trim().Length > 0)
                    {
                        engine = candidate;

                        break;
                    }
                }

                return engine;
            }
        }

        public static bool Available => Engine is not null;

        /// <summary>
        ///  Why the list is empty, when the CLI is installed but could not answer.
        /// </summary>
        /// <remarks>
        ///  Docker Desktop leaves its CLI on the PATH when the engine is stopped, so "docker
        ///  version" answers and "docker ps" does not. Without this the panel says "Containers,
        ///  through docker" over an empty list and gives the user nothing to act on, when the
        ///  actual answer is that the engine needs starting.
        /// </remarks>
        public static string? Trouble { get; private set; }

        /// <summary>
        ///  Every container, running ones first.
        /// </summary>
        /// <remarks>
        ///  JSON lines rather than the table format, because a container whose name or status
        ///  contains a space turns the table into something that can only be parsed by guessing.
        /// </remarks>
        public static IReadOnlyList<ContainerInfo> List()
        {
            if (Engine is not { } cli)
            {
                return [];
            }

            List<ContainerInfo> containers = [];

            // Errors are wanted here, not just output: a stopped engine says nothing at all on
            // stdout, and an empty listing with no explanation is what sent the user looking.
            string said = Ports.Run(cli, "ps --all --no-trunc --format {{json .}}", 8000, withErrors: true);

            foreach (string line in said.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (Parse(line) is { } container)
                {
                    containers.Add(container);
                }
            }

            Trouble = containers.Count == 0 && Complaint(said) is { } complaint ? complaint : null;

            return
            [
                .. containers
                    .OrderByDescending(container => container.IsRunning)
                    .ThenBy(container => container.Name, StringComparer.OrdinalIgnoreCase)
            ];
        }

        /// <summary>
        ///  What the engine said, when what it said was not a container.
        /// </summary>
        /// <remarks>
        ///  Trimmed to the first line: the daemon's refusal runs to a sentence about named pipes
        ///  and socket paths that is no use above a list. The common case is worth recognising
        ///  outright, because "start Docker Desktop" is the whole of the fix.
        /// </remarks>
        private static string? Complaint(string said)
        {
            string first = said
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(line => line.Trim().Length > 0)
                ?.Trim()
                ?? string.Empty;

            if (first.Length == 0)
            {
                return null;
            }

            if (first.Contains("daemon", StringComparison.OrdinalIgnoreCase)
                || first.Contains("connect", StringComparison.OrdinalIgnoreCase)
                || first.Contains("pipe", StringComparison.OrdinalIgnoreCase)
                || first.Contains("socket", StringComparison.OrdinalIgnoreCase))
            {
                return $"{engine} is installed but its engine is not running. Start it to see containers here.";
            }

            return first.Length > 160 ? first[..160] + "\u2026" : first;
        }

        /// <summary>
        ///  One JSON line into a container.
        /// </summary>
        /// <remarks>
        ///  Field names differ between the two engines - podman has used lower-case keys and an
        ///  array for Names - so each is read by trying the spellings rather than by binding to a
        ///  type. A line that cannot be read is skipped rather than failing the listing.
        /// </remarks>
        private static ContainerInfo? Parse(string line)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                string id = Text(root, "ID", "Id");

                if (id.Length == 0)
                {
                    return null;
                }

                string state = Text(root, "State", "state");

                return new ContainerInfo(
                    id,
                    Text(root, "Names", "Name", "names"),
                    Text(root, "Image", "image"),
                    state.Length > 0 ? state : "unknown",
                    Text(root, "Status", "status"),
                    Text(root, "Ports", "ports"));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>The first of several possible spellings of a field, as a string.</summary>
        private static string Text(JsonElement root, params string[] names)
        {
            foreach (string name in names)
            {
                if (!root.TryGetProperty(name, out JsonElement value))
                {
                    continue;
                }

                switch (value.ValueKind)
                {
                    case JsonValueKind.String:
                        return value.GetString() ?? string.Empty;

                    case JsonValueKind.Number:
                        return value.ToString();

                    case JsonValueKind.Array:
                        // podman's Names is a list; the first is the one the user gave it.
                        return string.Join(", ", value.EnumerateArray()
                            .Select(item => item.GetString())
                            .Where(item => !string.IsNullOrEmpty(item)));
                }
            }

            return string.Empty;
        }

        /// <summary>Runs one verb against one container and reports what happened.</summary>
        public static string Do(string verb, ContainerInfo container)
        {
            if (Engine is not { } cli)
            {
                return "No container engine is installed.";
            }

            string output = Ports.Run(cli, $"{verb} {container.Short}", 30_000).Trim();

            return output.Length > 0
                ? output
                : $"{verb} {container.Name} - done.";
        }

        /// <summary>
        ///  The tail of a container's log.
        /// </summary>
        /// <remarks>
        ///  Bounded and one-shot rather than followed. A live follow belongs in the output pane with
        ///  a process behind it, and the deck can already do that - "docker logs -f" is a command
        ///  like any other, and one this panel can offer to create.
        /// </remarks>
        public static string Logs(ContainerInfo container, int lines = 200)
        {
            if (Engine is not { } cli)
            {
                return "No container engine is installed.";
            }

            // Logs go to stderr for a great many images, so both streams are wanted here.
            string output = Ports.Run(cli, $"logs --tail {lines} --timestamps {container.Short}", 15_000, withErrors: true);

            return output.Trim().Length > 0 ? output : "This container has logged nothing.";
        }

        /// <summary>The command that would follow this container's log, for the deck to save.</summary>
        public static string FollowCommand(ContainerInfo container) =>
            $"{Engine ?? "docker"} logs -f --tail 100 {container.Name}";
    }
}
