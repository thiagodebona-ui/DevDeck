using System.Diagnostics;
using System.Globalization;

namespace DevDeck.Core
{
    /// <summary>
    ///  Starts a fresh copy of the app to take over from this one, for the moments where carrying on
    ///  in the same process would be wrong.
    /// </summary>
    /// <remarks>
    ///  The reset is the case this exists for. Every panel holds the settings object that was loaded
    ///  at startup, so after the file is replaced the only honest way to show the new set is a new
    ///  process - and asking the user to close and reopen it themselves is a chore the app can do.
    ///
    ///  The successor cannot simply be started and left to it. <see cref="SingleInstance"/> would see
    ///  this copy still holding the pipe and hand it a "show" instead of opening, which is a restart
    ///  that quietly does nothing. So the new copy is told this one's process id, and waits for it to
    ///  be gone before it goes on to the single-instance check at all.
    /// </remarks>
    internal static class Relaunch
    {
        /// <summary>The argument that marks a copy started by <see cref="Start"/>.</summary>
        public const string Switch = "--relaunched-after";

        /// <summary>How long a successor waits for its predecessor before starting regardless.</summary>
        /// <remarks>
        ///  Generous, because shutdown waits on the tray, the hotkey thread and the keep-awake
        ///  inhibitor. But bounded: a predecessor that hangs on its way out must not leave the user
        ///  with no window at all, and two windows for a moment is the lesser failure.
        /// </remarks>
        private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

        /// <summary>
        ///  Starts the successor. The caller shuts this copy down afterwards.
        /// </summary>
        /// <returns>False when there was nothing to start, so the caller can say so instead.</returns>
        /// <remarks>
        ///  <see cref="Environment.ProcessPath"/> rather than the assembly location, for the same
        ///  reason as <see cref="UrlScheme"/>: under a single-file publish the assembly has no path of
        ///  its own, and under <c>dotnet run</c> the process path is the apphost, which is still the
        ///  thing that starts the app.
        /// </remarks>
        public static bool Start()
        {
            string executable = Environment.ProcessPath ?? string.Empty;

            if (executable.Length == 0 || !File.Exists(executable))
            {
                return false;
            }

            try
            {
                ProcessStartInfo start = new(executable)
                {
                    UseShellExecute = false,
                    WorkingDirectory = SettingsPath.Home,
                };

                start.ArgumentList.Add(Switch);
                start.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

                using Process? successor = Process.Start(start);

                return successor is not null;
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("relaunch", "Could not start a new copy", exception);

                return false;
            }
        }

        /// <summary>
        ///  In a copy started by <see cref="Start"/>, waits for the one that started it to exit, and
        ///  hands back the arguments with the switch taken off.
        /// </summary>
        /// <remarks>
        ///  A predecessor that has already gone, or an id that is not a number, is simply nothing to
        ///  wait for. Neither is a reason to refuse to start.
        /// </remarks>
        public static string[] AwaitPredecessor(string[] args)
        {
            if (args.Length < 2 || args[0] != Switch)
            {
                return args;
            }

            if (int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                try
                {
                    using Process predecessor = Process.GetProcessById(id);

                    predecessor.WaitForExit(Patience);
                }
                catch (Exception)
                {
                    // Already gone, which is the outcome being waited for.
                }
            }

            return args[2..];
        }
    }
}
