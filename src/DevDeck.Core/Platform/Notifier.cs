using System.Diagnostics;

namespace DevDeck.Core
{
    /// <summary>
    ///  Says something to the desktop, when the desktop has a way of being told.
    /// </summary>
    /// <remarks>
    ///  The point of a notification here is that the user walked away - so an in-app banner, which
    ///  is the only thing a UI framework can offer portably, is exactly the wrong answer: it
    ///  appears on a window nobody is looking at. What is wanted is the system's own channel.
    ///
    ///  macOS and Linux both have one that can be reached from a subprocess, so those are used
    ///  directly. Windows does not: a real toast needs a registered application identity, which is
    ///  a per-install arrangement this app deliberately does not make. <see cref="Send"/> therefore
    ///  reports whether it managed anything, and the caller is expected to fall back to something
    ///  visible in the app - a banner and a flashing taskbar button - rather than assume it worked.
    /// </remarks>
    internal static class Notifier
    {
        /// <summary>
        ///  Shows a desktop notification, and says whether it could.
        /// </summary>
        /// <returns>False when this platform has no channel, so the caller can fall back.</returns>
        public static bool Send(string title, string body)
        {
            try
            {
                if (OperatingSystem.IsMacOS())
                {
                    // Through osascript because the alternative, terminal-notifier, is a thing the
                    // user would have to install first.
                    return Run("osascript",
                    [
                        "-e",
                        $"display notification \"{Clean(body)}\" with title \"{Clean(title)}\"",
                    ]);
                }

                if (OperatingSystem.IsLinux())
                {
                    // Present wherever there is a notification daemon at all, which is the only
                    // case where a notification would have been shown anyway.
                    return Run("notify-send", ["--app-name=DevDeck", title, body]);
                }
            }
            catch (Exception)
            {
                // A notification that could not be delivered is not worth an error - the run it was
                // reporting on already succeeded or failed on its own terms.
            }

            return false;
        }

        private static bool Run(string file, string[] arguments)
        {
            ProcessStartInfo start = new()
            {
                FileName = file,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(start);

            if (process is null)
            {
                return false;
            }

            // Bounded, because this is called from a finished run and a notification daemon that
            // has wedged must not take the run's reporting down with it.
            if (!process.WaitForExit(3000))
            {
                return false;
            }

            return process.ExitCode == 0;
        }

        /// <summary>Makes a string safe to paste into an AppleScript literal.</summary>
        /// <remarks>
        ///  A command name is user-supplied text, and a quote in it would otherwise end the string
        ///  and leave the rest of the name being read as AppleScript.
        /// </remarks>
        private static string Clean(string text) => text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }
}
