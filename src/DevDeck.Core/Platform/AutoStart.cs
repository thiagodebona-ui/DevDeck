using System.Diagnostics;

namespace DevDeck.Core
{
    /// <summary>
    ///  Starts DevDeck when the user signs in, for the current user only and only when asked.
    /// </summary>
    /// <remarks>
    ///  The answer to "is it on?" is read back from the system every time rather than kept in the
    ///  settings file. The entry lives outside the app - Task Manager can remove it, a reset of the
    ///  settings file does not - and a tick box that remembers what it was told rather than what is
    ///  true is the switch-that-does-nothing this page refuses to show.
    ///
    ///  <list type="bullet">
    ///   <item>Windows - a value under <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>,
    ///   written through <c>reg.exe</c> for the same reason as <see cref="UrlScheme"/>.</item>
    ///   <item>Linux - an XDG autostart entry in <c>~/.config/autostart</c>, which every desktop
    ///   environment honours.</item>
    ///   <item>macOS - a login item belongs to an app bundle, so this says so instead.</item>
    ///  </list>
    ///
    ///  The entry passes <see cref="Switch"/>, so a copy started by a sign-in opens minimised rather
    ///  than putting a window in front of whatever the user logged in to do.
    /// </remarks>
    internal static class AutoStart
    {
        /// <summary>The argument that marks a copy started by signing in.</summary>
        public const string Switch = "--autostart";

        private const string RunKey = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run";

        private const string ValueName = "DevDeck";

        /// <summary>Whether this platform can be set up from here at all.</summary>
        public static bool IsAvailable => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

        private static string Executable => Environment.ProcessPath ?? string.Empty;

        /// <summary>What the entry runs, quoted so a path with spaces survives.</summary>
        private static string Line => $"\"{Executable}\" {Switch}";

        private static string DesktopFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "autostart",
            "devdeck.desktop");

        /// <summary>
        ///  Whether signing in starts this executable.
        /// </summary>
        /// <remarks>
        ///  An entry pointing at a different copy - one unzipped somewhere else last month - counts
        ///  as off, so turning it on here points sign-in at the copy the user is actually looking at.
        /// </remarks>
        public static bool IsEnabled
        {
            get
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        (int code, string output) = Run("reg", ["query", RunKey, "/v", ValueName]);

                        return code == 0
                            && output.Contains(Executable, StringComparison.OrdinalIgnoreCase);
                    }

                    if (OperatingSystem.IsLinux())
                    {
                        return File.Exists(DesktopFile)
                            && File.ReadAllText(DesktopFile).Contains(Executable, StringComparison.Ordinal);
                    }
                }
                catch (Exception)
                {
                    // Unreadable is reported as off, which is what the user can act on.
                }

                return false;
            }
        }

        /// <summary>Adds or removes the entry, and reports what actually happened.</summary>
        public static SchemeResult Set(bool enabled)
        {
            if (!IsAvailable)
            {
                return new SchemeResult(false, Strings.Text("SetAutoStartCannot"));
            }

            if (Executable.Length == 0)
            {
                return new SchemeResult(false, "This build cannot tell where its own executable is.");
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    (int code, _) = enabled
                        ? Run("reg", ["add", RunKey, "/v", ValueName, "/t", "REG_SZ", "/d", Line, "/f"])
                        : Run("reg", ["delete", RunKey, "/v", ValueName, "/f"]);

                    // Deleting a value that is not there exits 1; for "off" that is still success.
                    if (enabled && code != 0)
                    {
                        return new SchemeResult(false, "The registry would not accept the startup entry.");
                    }
                }
                else if (enabled)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(DesktopFile)!);

                    File.WriteAllText(DesktopFile, string.Join('\n',
                    [
                        "[Desktop Entry]",
                        "Type=Application",
                        "Name=DevDeck",
                        "Comment=Developer command deck",
                        $"Exec={Line}",
                        "Terminal=false",
                        "X-GNOME-Autostart-enabled=true",
                        string.Empty,
                    ]));
                }
                else if (File.Exists(DesktopFile))
                {
                    File.Delete(DesktopFile);
                }

                AppLog.Instance.Good("startup", enabled
                    ? "DevDeck now starts when you sign in."
                    : "DevDeck no longer starts when you sign in.");

                return new SchemeResult(true, Strings.Text(enabled ? "SetAutoStartOn" : "SetAutoStartOff"));
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("startup", "Could not change the startup entry", exception);

                return new SchemeResult(false, Strings.Format("SetAutoStartFailed", exception.Message));
            }
        }

        /// <summary>
        ///  Takes <see cref="Switch"/> off the arguments, saying whether it was there.
        /// </summary>
        public static string[] Strip(string[] args, out bool atSignIn)
        {
            atSignIn = args.Contains(Switch, StringComparer.OrdinalIgnoreCase);

            return atSignIn
                ? args.Where(argument => !string.Equals(argument, Switch, StringComparison.OrdinalIgnoreCase)).ToArray()
                : args;
        }

        private static (int Code, string Output) Run(string file, IReadOnlyList<string> arguments)
        {
            ProcessStartInfo start = new()
            {
                FileName = file,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (string argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            using Process? process = Process.Start(start);

            if (process is null)
            {
                return (-1, string.Empty);
            }

            string output = process.StandardOutput.ReadToEnd();

            // Bounded, for the same reason as UrlScheme: a stuck helper must not take the window.
            return process.WaitForExit(5000) ? (process.ExitCode, output) : (-1, output);
        }
    }
}
