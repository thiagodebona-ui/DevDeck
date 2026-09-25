using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>
    ///  The plumbing shared by every local runtime the app can set up: finding programs, running
    ///  them, and waiting for a server to come up.
    /// </summary>
    internal static partial class AiSetup
    {
        /// <summary>Models are large; refuse to start a download that cannot finish.</summary>
        private const long RequiredFreeBytes = 12L * 1024 * 1024 * 1024;

        /// <summary>How long to wait for a server to answer after starting it.</summary>
        private static readonly TimeSpan ServerStartTimeout = TimeSpan.FromSeconds(60);

        [GeneratedRegex(@"\x1B\[[0-9;?]*[a-zA-Z]")]
        private static partial Regex AnsiCodes();

        /// <summary>True when something answers the model list at the configured endpoint.</summary>
        public static async Task<bool> IsReachableAsync(AiClient client, CancellationToken token)
        {
            try
            {
                using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(4));

                await client.ListModelsAsync(timeout.Token);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Model list, or an empty list when the endpoint will not say.</summary>
        public static async Task<List<string>> SafeListAsync(AiClient client, CancellationToken token)
        {
            try
            {
                return await client.ListModelsAsync(token);
            }
            catch (Exception)
            {
                return [];
            }
        }

        /// <summary>
        ///  Finds a program by checking known install locations first and PATH afterwards.
        /// </summary>
        /// <remarks>
        ///  Known locations first because a fresh install updates the environment for processes
        ///  started afterwards, and this one is already running with the copy it inherited.
        /// </remarks>
        public static string? FindProgram(string executableName, params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (FileExists(candidate))
                {
                    return candidate;
                }
            }

            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(';'))
            {
                string trimmed = directory.Trim();

                if (trimmed.Length > 0 && FileExists(Path.Combine(trimmed, executableName)))
                {
                    return Path.Combine(trimmed, executableName);
                }
            }

            return null;
        }

        /// <summary>Path to winget.exe, or null when it is unavailable on this machine.</summary>
        public static string? FindWinget() => FindProgram(
            "winget.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "winget.exe"));

        /// <summary>Starts a program and leaves it running after this one exits.</summary>
        public static void StartDetached(string fileName, string arguments, bool shell = false)
        {
            try
            {
                ProcessStartInfo start = new(fileName)
                {
                    CreateNoWindow = !shell,
                    UseShellExecute = shell
                };

                if (arguments.Length > 0)
                {
                    start.Arguments = arguments;
                }

                using Process? started = Process.Start(start);
            }
            catch (Exception)
            {
                // Already running, or blocked - the reachability poll is the real test either way.
            }
        }

        /// <summary>Polls until the endpoint answers, or the timeout runs out.</summary>
        public static async Task<bool> WaitForServerAsync(
            AiClient client,
            Action<string> status,
            CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow + ServerStartTimeout;

            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();

                if (await IsReachableAsync(client, token))
                {
                    return true;
                }

                status($"Waiting for the server to start… {(deadline - DateTime.UtcNow).TotalSeconds:F0}s left");
                await Task.Delay(TimeSpan.FromSeconds(2), token);
            }

            return await IsReachableAsync(client, token);
        }

        /// <summary>Refuses a multi-gigabyte download that the disk cannot hold.</summary>
        public static void EnsureDiskSpace()
        {
            try
            {
                // Local runtimes keep their models under the user profile.
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                DriveInfo drive = new(Path.GetPathRoot(home) ?? "C:\\");

                if (drive.AvailableFreeSpace < RequiredFreeBytes)
                {
                    throw new InvalidOperationException(
                        $"Only {drive.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1} GB free on {drive.Name}. "
                        + "A model needs several gigabytes, so the download would fail part way through.");
                }
            }
            catch (Exception exception) when (exception is not InvalidOperationException)
            {
                // Cannot measure the drive - let the download try rather than block on a guess.
            }
        }

        private static bool FileExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        ///  Runs a console tool, reporting each line of its output.
        /// </summary>
        /// <remarks>
        ///  Separate from the form's own process runner because these tools draw progress bars:
        ///  they rewrite one line using carriage returns and ANSI codes, which a line-oriented
        ///  reader would turn into thousands of entries.
        /// </remarks>
        public static async Task<int> RunProcessAsync(
            string fileName,
            string arguments,
            Action<string> onLine,
            CancellationToken token)
        {
            using Process process = new();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            process.Start();

            using CancellationTokenRegistration cancelKill = token.Register(() =>
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // Finished between the cancel and this callback.
                }
            });

            await Task.WhenAll(
                PumpAsync(process.StandardOutput, onLine),
                PumpAsync(process.StandardError, onLine));

            await process.WaitForExitAsync(CancellationToken.None);

            token.ThrowIfCancellationRequested();

            return process.ExitCode;
        }

        /// <summary>
        ///  Reads a stream a line at a time, treating a bare carriage return as a line break so a
        ///  redrawn progress bar arrives as successive updates rather than one enormous line.
        /// </summary>
        private static async Task PumpAsync(TextReader reader, Action<string> onLine)
        {
            char[] buffer = new char[1024];
            StringBuilder line = new();
            int read;

            while ((read = await reader.ReadAsync(buffer, CancellationToken.None)) > 0)
            {
                for (int index = 0; index < read; index++)
                {
                    char character = buffer[index];

                    if (character is '\r' or '\n')
                    {
                        Flush(line, onLine);
                        continue;
                    }

                    line.Append(character);
                }
            }

            Flush(line, onLine);
        }

        private static void Flush(StringBuilder line, Action<string> onLine)
        {
            if (line.Length == 0)
            {
                return;
            }

            string text = AnsiCodes().Replace(line.ToString(), string.Empty).Trim();
            line.Clear();

            if (text.Length > 0)
            {
                onLine(text);
            }
        }
    }
}
