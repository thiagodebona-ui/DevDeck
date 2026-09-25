using System.Diagnostics;

namespace DevDeck.Core
{
    /// <summary>What registering the scheme did, and what the user still has to do.</summary>
    internal sealed record SchemeResult(bool Done, string Message);

    /// <summary>
    ///  Registers <c>devdeck://</c> with the desktop, so a link anywhere opens the deck.
    /// </summary>
    /// <remarks>
    ///  Only ever for the current user, and only when asked. A scheme handler is a machine-wide
    ///  claim on a name, and an app that silently takes one during a first run is an app that has
    ///  decided something on the user's behalf that it has no business deciding - especially from a
    ///  build they may have unzipped to try out.
    ///
    ///  Three platforms, three entirely different mechanisms, and one of them cannot be done at
    ///  all from a running process:
    ///
    ///  <list type="bullet">
    ///   <item>Windows - keys under <c>HKCU\Software\Classes</c>, written through <c>reg.exe</c>
    ///   rather than the registry API, which keeps this file free of a platform-gated dependency
    ///   and of the analyser suppressions that come with one.</item>
    ///   <item>Linux - a desktop entry declaring the <c>x-scheme-handler</c> MIME type, which is
    ///   the freedesktop mechanism every environment implements.</item>
    ///   <item>macOS - declared in an app bundle's Info.plist and read by Launch Services when the
    ///   bundle is installed. Nothing a process can do to itself while running, so this says so
    ///   plainly instead of failing quietly.</item>
    ///  </list>
    /// </remarks>
    internal static class UrlScheme
    {
        /// <summary>The executable a handler should point at.</summary>
        /// <remarks>
        ///  <see cref="Environment.ProcessPath"/> rather than the assembly location, because under a
        ///  single-file publish the assembly has no path on disk and the launcher is what the
        ///  desktop must actually start.
        /// </remarks>
        public static string Executable => Environment.ProcessPath ?? string.Empty;

        /// <summary>Whether this platform can be set up from here at all.</summary>
        public static bool CanRegister => OperatingSystem.IsWindows() || OperatingSystem.IsLinux();

        public static SchemeResult Register()
        {
            if (Executable.Length == 0)
            {
                return new SchemeResult(false, "This build cannot tell where its own executable is.");
            }

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    return Windows();
                }

                if (OperatingSystem.IsLinux())
                {
                    return Linux();
                }

                return new SchemeResult(
                    false,
                    "On macOS a URL scheme belongs to an app bundle's Info.plist and cannot be "
                    + "registered by a running process. The command line works either way: "
                    + "devdeck run \"Build\".");
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("link", "Could not register devdeck://", exception);

                return new SchemeResult(false, $"Could not register: {exception.Message}");
            }
        }

        private static SchemeResult Windows()
        {
            const string key = @"HKCU\Software\Classes\devdeck";

            // URL Protocol is an empty-valued name rather than a value with content; its presence is
            // what marks the key as a scheme handler.
            if (!Reg(key, "/ve", "/d", "URL:DevDeck Protocol")
                || !Reg(key, "/v", "URL Protocol", "/d", string.Empty)
                || !Reg($@"{key}\shell\open\command", "/ve", "/d", $"\"{Executable}\" \"%1\""))
            {
                return new SchemeResult(false, "The registry would not accept the handler keys.");
            }

            AppLog.Instance.Good("link", "Registered devdeck:// for this user.");

            return new SchemeResult(true, "Registered. devdeck://run/Build now opens this deck.");
        }

        private static bool Reg(string key, params string[] rest)
        {
            List<string> arguments = ["add", key, .. rest, "/f"];

            return Run("reg", arguments) == 0;
        }

        private static SchemeResult Linux()
        {
            string applications = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "applications");

            Directory.CreateDirectory(applications);

            string file = Path.Combine(applications, "devdeck.desktop");

            // NoDisplay, because this entry exists to claim the scheme rather than to put a second
            // copy of the app in the launcher menu beside however it was actually installed.
            File.WriteAllText(file, string.Join('\n',
            [
                "[Desktop Entry]",
                "Type=Application",
                "Name=DevDeck",
                "Comment=Developer command deck",
                $"Exec=\"{Executable}\" %u",
                "Terminal=false",
                "NoDisplay=true",
                "MimeType=x-scheme-handler/devdeck;",
                string.Empty,
            ]));

            // Best effort: the entry is written either way, and on a session that indexes these
            // itself the refresh is redundant rather than required.
            Run("update-desktop-database", [applications]);
            Run("xdg-mime", ["default", "devdeck.desktop", "x-scheme-handler/devdeck"]);

            AppLog.Instance.Good("link", $"Wrote {file}");

            return new SchemeResult(true, $"Registered through {file}. You may need to log out once.");
        }

        private static int Run(string file, IReadOnlyList<string> arguments)
        {
            try
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
                    return -1;
                }

                // Bounded: this runs on a button press, and a helper that never returns must not
                // take the window with it.
                if (!process.WaitForExit(5000))
                {
                    return -1;
                }

                return process.ExitCode;
            }
            catch (Exception)
            {
                // A missing helper - update-desktop-database on a minimal system - is not an error
                // in itself; the caller judges the result from the exit code it wanted.
                return -1;
            }
        }
    }
}
