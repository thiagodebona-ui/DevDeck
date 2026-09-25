using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>A port something is listening on, and what that something is.</summary>
    internal sealed record ListeningPort(int Port, int ProcessId, string Process, string Address)
    {
        /// <summary>
        ///  A guess at what this is, from the port number alone.
        /// </summary>
        /// <remarks>
        ///  Worth having because the question is almost never "what is on 5432" in the abstract -
        ///  it is "which of the three things I started is this", and the conventional owner of a
        ///  port answers that faster than a process name like "node" or "docker-proxy" does.
        /// </remarks>
        public string Note => Port switch
        {
            80 or 8080 or 8000 => "http",
            443 or 8443 => "https",
            3000 or 5173 or 4200 or 5000 => "dev server",
            5432 => "postgres",
            3306 => "mysql",
            6379 => "redis",
            27017 => "mongo",
            1433 => "sql server",
            5672 or 15672 => "rabbitmq",
            9200 => "elasticsearch",
            2375 or 2376 => "docker",
            _ => string.Empty,
        };

        public bool HasNote => Note.Length > 0;

        /// <summary>What a browser would need, for the ports where that makes sense.</summary>
        public string Url => $"http://localhost:{Port}";

        public bool IsWeb => Port is 80 or 443 or 3000 or 4200 or 5000 or 5173 or 8000 or 8080 or 8443 or 9000;
    }

    /// <summary>
    ///  What is listening on this machine, and the means to stop it.
    /// </summary>
    /// <remarks>
    ///  "Address already in use" is a daily event and the fix is always the same three steps:
    ///  find out what has the port, decide whether it matters, kill it. Those steps are a netstat
    ///  invocation nobody remembers the flags for, a pipe into findstr or grep, and a kill by an
    ///  id read off the screen - which is exactly the kind of thing a panel should do.
    ///
    ///  The listing is read from the framework where it can be, because IPGlobalProperties knows
    ///  the listeners on every platform without shelling out. What it does not know is which
    ///  process owns each one, and that genuinely does need platform-specific help.
    /// </remarks>
    internal static partial class Ports
    {
        /// <summary>Everything listening, lowest port first.</summary>
        public static IReadOnlyList<ListeningPort> Listening()
        {
            IReadOnlyDictionary<int, (int Id, string Name)> owners = Owners();

            List<ListeningPort> found = [];

            try
            {
                foreach (System.Net.IPEndPoint endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                {
                    if (found.Any(port => port.Port == endpoint.Port))
                    {
                        // A service bound to both stacks appears twice. One row per port is what
                        // the question "what has 3000" wants answering.
                        continue;
                    }

                    // Not every listener has an owner: netstat reports the ports it can see, and
                    // one held by another user's process or started between the two calls is not
                    // among them. The miss leaves a default tuple whose name is null, not empty -
                    // and reading Length off it once emptied this entire panel.
                    owners.TryGetValue(endpoint.Port, out (int Id, string Name) owner);

                    found.Add(new ListeningPort(
                        endpoint.Port,
                        owner.Id,
                        string.IsNullOrEmpty(owner.Name) ? "unknown" : owner.Name,
                        endpoint.Address.ToString()));
                }
            }
            catch (Exception exception)
            {
                // No permission to enumerate, or no network stack to ask. An empty list is the
                // honest answer - but it is also what a defect in the loop above looks like, so it
                // is said out loud rather than returned as though nothing were listening.
                AppLog.Instance.Write("ports", $"Could not list what is listening: {exception.Message}", LogLevel.Warning);

                return [];
            }

            return [.. found.OrderBy(port => port.Port)];
        }

        /// <summary>
        ///  Which process owns each listening port.
        /// </summary>
        /// <remarks>
        ///  netstat on Windows and lsof on Unix, both of which are present by default. Failure here
        ///  is not fatal: the ports still list, they just say "unknown" - which is still enough to
        ///  answer whether something has the port.
        /// </remarks>
        private static IReadOnlyDictionary<int, (int, string)> Owners()
        {
            try
            {
                return OperatingSystem.IsWindows() ? FromNetstat() : FromLsof();
            }
            catch (Exception exception)
            {
                // Non-fatal: every port still lists, they just say "unknown".
                AppLog.Instance.Write("ports", $"Could not identify what owns each port: {exception.Message}", LogLevel.Warning);

                return new Dictionary<int, (int, string)>();
            }
        }

        [GeneratedRegex(@"^\s*TCP\s+\S+:(?<port>\d+)\s+\S+\s+LISTENING\s+(?<pid>\d+)", RegexOptions.Multiline)]
        private static partial Regex NetstatLine();

        private static IReadOnlyDictionary<int, (int, string)> FromNetstat()
        {
            Dictionary<int, (int, string)> owners = [];

            foreach (Match match in NetstatLine().Matches(Run("netstat", "-ano -p TCP")))
            {
                if (int.TryParse(match.Groups["port"].Value, out int port)
                    && int.TryParse(match.Groups["pid"].Value, out int pid)
                    && !owners.ContainsKey(port))
                {
                    owners[port] = (pid, Name(pid));
                }
            }

            return owners;
        }

        [GeneratedRegex(@"^(?<name>\S+)\s+(?<pid>\d+)\s+.*:(?<port>\d+)\s+\(LISTEN\)", RegexOptions.Multiline)]
        private static partial Regex LsofLine();

        private static IReadOnlyDictionary<int, (int, string)> FromLsof()
        {
            Dictionary<int, (int, string)> owners = [];

            foreach (Match match in LsofLine().Matches(Run("lsof", "-nP -iTCP -sTCP:LISTEN")))
            {
                if (int.TryParse(match.Groups["port"].Value, out int port)
                    && int.TryParse(match.Groups["pid"].Value, out int pid)
                    && !owners.ContainsKey(port))
                {
                    owners[port] = (pid, match.Groups["name"].Value);
                }
            }

            return owners;
        }

        private static string Name(int pid)
        {
            try
            {
                return Process.GetProcessById(pid).ProcessName;
            }
            catch (Exception)
            {
                // Exited between the listing and the lookup, or owned by another user.
                return string.Empty;
            }
        }

        /// <summary>
        ///  Runs a short informational command and returns its output.
        /// </summary>
        /// <remarks>
        ///  Bounded, because this runs on a panel refresh: a tool that hangs would otherwise hang
        ///  the refresh with it, and there is nothing here worth waiting five seconds for.
        /// </remarks>
        internal static string Run(string file, string arguments, int timeoutMs = 5000, bool withErrors = false)
        {
            try
            {
                using Process? process = Process.Start(new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                if (process is null)
                {
                    return string.Empty;
                }

                // Read both before waiting, and read error on another thread: a process that fills
                // one pipe while nothing drains the other blocks forever, and the timeout below
                // would never be reached because the read above never returns.
                // Always drained, even when it is not wanted: error is redirected either way, and a
                // process that fills a pipe nobody reads stops dead.
                Task<string> errors = process.StandardError.ReadToEndAsync();

                string output = process.StandardOutput.ReadToEnd();

                if (withErrors && errors.Wait(timeoutMs))
                {
                    // Many tools log to stderr by convention rather than to report a failure -
                    // container engines above all - so the caller asks for it and gets it appended.
                    output += errors.Result;
                }

                if (!process.WaitForExit(timeoutMs))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception)
                    {
                        // Already gone.
                    }
                }

                return output;
            }
            catch (Exception)
            {
                // Not installed, or not on PATH.
                return string.Empty;
            }
        }

        /// <summary>
        ///  Ends whatever holds a port.
        /// </summary>
        /// <remarks>
        ///  The whole tree, not just the named process. A dev server is usually a shell that
        ///  spawned a runtime that spawned a watcher, and killing the parent alone leaves the child
        ///  holding the socket - which looks like the kill silently failed.
        /// </remarks>
        public static string Kill(ListeningPort port)
        {
            if (port.ProcessId <= 0)
            {
                return "Nothing to stop - the owning process could not be identified.";
            }

            try
            {
                using Process process = Process.GetProcessById(port.ProcessId);
                string name = process.ProcessName;

                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);

                return $"Stopped {name} ({port.ProcessId}), which held port {port.Port}.";
            }
            catch (ArgumentException)
            {
                return "That process has already exited.";
            }
            catch (Exception exception)
            {
                return $"Could not stop it: {exception.Message}";
            }
        }

        /// <summary>Whether a port is free, which is the question a dev server is really asking.</summary>
        public static bool Free(int port) =>
            !IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners().Any(e => e.Port == port);
    }
}
