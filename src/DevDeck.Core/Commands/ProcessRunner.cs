using System.Diagnostics;
using System.Text;

namespace DevDeck.Core
{
    /// <summary>
    ///  Starts console processes for the rest of the app: either watched, with their output streamed
    ///  back line by line, or detached and left to run on their own.
    /// </summary>
    /// <remarks>
    ///  This was a private helper on MainForm until a command run became a panel of its own. Two
    ///  unrelated places start processes now, and neither of them should own the plumbing.
    /// </remarks>
    internal static class ProcessRunner
    {
        /// <summary>
        ///  Runs a console process, streaming both stdout and stderr into <paramref name="log"/>,
        ///  and returns its exit code.
        /// </summary>
        public static async Task<int> RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            Action<string, LogLevel> log,
            Action<Process>? onStarted = null,
            CancellationToken cancellationToken = default,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            using Process process = new();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDirectory;

            Apply(process.StartInfo, environment);
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            // V1 only captured stdout, so every error message a command produced was thrown away.
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            process.StartInfo.StandardErrorEncoding = Encoding.UTF8;

            process.OutputDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    log(args.Data, LogLevel.Info);
                }
            };

            process.ErrorDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    log(args.Data, LogLevel.Error);
                }
            };

            process.Start();
            onStarted?.Invoke(process);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // Handing the token to WaitForExitAsync would only stop us waiting - cmd.exe and the
            // yarn/node children underneath it would carry on writing into node_modules unseen. Kill
            // the whole tree instead, then wait for it to actually be gone before reporting back.
            using CancellationTokenRegistration cancelKill = cancellationToken.Register(() =>
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception)
                {
                    // The process finished on its own between the cancel and this callback.
                }
            });

            await process.WaitForExitAsync(CancellationToken.None);

            cancellationToken.ThrowIfCancellationRequested();

            return process.ExitCode;
        }

        /// <summary>
        ///  Starts a process and hands it back without waiting for it, with nothing redirected and a
        ///  console of its own.
        /// </summary>
        /// <remarks>
        ///  Redirecting the output of something interactive is what makes the app look hung: it
        ///  holds the pipes and has nothing to report until they close, and a session the user is
        ///  meant to type into never closes them. A detached command gets a real window to be typed
        ///  into instead, and the app stops caring about it the moment it starts.
        /// </remarks>
        public static Process StartDetached(
            string fileName,
            string arguments,
            string workingDirectory,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            Process process = new();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.WorkingDirectory = workingDirectory;

            Apply(process.StartInfo, environment);

            // UseShellExecute is what gives it its own console. It also makes redirection impossible,
            // which is the point rather than a limitation.
            process.StartInfo.UseShellExecute = true;
            process.StartInfo.CreateNoWindow = false;

            // Set before Start, so a caller can hook Exited to clean up after it - notably to delete
            // the temporary script file the process is reading from.
            process.EnableRaisingEvents = true;

            process.Start();

            return process;
        }

        /// <summary>
        ///  Adds variables to the environment the child process will be started with.
        /// </summary>
        /// <remarks>
        ///  Environment rather than command-line arguments, which is the decision worth recording.
        ///  A command in this app is a script body, not an argv: there is nowhere to put $1, and
        ///  pasting a list of file paths into the text before running it would break the moment a
        ///  path contained a space, a quote or an ampersand - which on Windows is most of them,
        ///  starting with "C:\Program Files". A variable carries the value through untouched, is
        ///  read the same way by cmd, PowerShell and sh, and needs no quoting to survive the trip.
        ///  <para>
        ///   The child inherits this process's environment by default, and adding to
        ///   <see cref="ProcessStartInfo.Environment"/> keeps that: these are added to what is
        ///   already there rather than replacing it, so PATH still works.
        ///  </para>
        /// </remarks>
        private static void Apply(ProcessStartInfo start, IReadOnlyDictionary<string, string>? environment)
        {
            if (environment is null)
            {
                return;
            }

            foreach (KeyValuePair<string, string> variable in environment)
            {
                start.Environment[variable.Key] = variable.Value;
            }
        }
    }
}
