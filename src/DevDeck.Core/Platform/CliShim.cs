namespace DevDeck.Core
{
    /// <summary>
    ///  Writes a small <c>devdeck</c> launcher, so the deck can be driven from a terminal.
    /// </summary>
    /// <remarks>
    ///  The deck is the wrong shape for half the moments it is wanted in. You are in a terminal in
    ///  the repository already; reaching for the mouse to press a button that runs a command in
    ///  that same repository is slower than typing the command was. What makes the deck worth
    ///  having there is not the button, it is that the command is long, exact and hard to remember
    ///  - so <c>devdeck run "Publish all"</c> is the shape that actually helps.
    ///
    ///  A script rather than a second executable: a real CLI would be a second project, a second
    ///  build and a second thing to ship, all to spell one argument list. What is written here is
    ///  four lines that pass their arguments to the app that is already installed.
    ///
    ///  The shim does not wait and does not print. The run happens in the window, with its output,
    ///  its history and its Stop button - which is the point. A version that streamed output back
    ///  to the terminal would be a different feature, and would want the run to happen in the
    ///  terminal's process rather than the app's.
    /// </remarks>
    internal static class CliShim
    {
        /// <summary>Where the shim goes, by convention for this platform.</summary>
        /// <remarks>
        ///  <c>~/.local/bin</c> on Unix, which every current distribution and macOS shell puts on
        ///  PATH by default. On Windows there is no such convention, so it goes beside the settings
        ///  file, where the app already writes, and the user is told to add that folder themselves
        ///  rather than having their PATH edited for them.
        /// </remarks>
        public static string Folder => OperatingSystem.IsWindows()
            ? Path.GetDirectoryName(SettingsPath.Current) ?? SettingsPath.Home
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local",
                "bin");

        public static string File => Path.Combine(
            Folder,
            OperatingSystem.IsWindows() ? "devdeck.cmd" : "devdeck");

        public static bool Exists => System.IO.File.Exists(File);

        /// <summary>Writes the shim, and says what happened.</summary>
        public static SchemeResult Install()
        {
            string executable = Environment.ProcessPath ?? string.Empty;

            if (executable.Length == 0)
            {
                return new SchemeResult(false, "This build cannot tell where its own executable is.");
            }

            try
            {
                Directory.CreateDirectory(Folder);

                System.IO.File.WriteAllText(File, OperatingSystem.IsWindows()
                    ? Windows(executable)
                    : Unix(executable));

                if (!OperatingSystem.IsWindows())
                {
                    // Written rather than assumed: a file created by WriteAllText is not executable,
                    // and a shim that cannot be run is a puzzle rather than a tool.
                    System.IO.File.SetUnixFileMode(
                        File,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                        | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                        | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }

                AppLog.Instance.Good("cli", $"Wrote {File}");

                return new SchemeResult(true, OnPath()
                    ? $"Written to {File}. Try: devdeck run \"Build\""
                    : $"Written to {File}. Add that folder to your PATH to use it by name.");
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("cli", "Could not write the launcher", exception);

                return new SchemeResult(false, $"Could not write it: {exception.Message}");
            }
        }

        public static SchemeResult Remove()
        {
            try
            {
                if (Exists)
                {
                    System.IO.File.Delete(File);
                    AppLog.Instance.Info("cli", $"Removed {File}");
                }

                return new SchemeResult(true, "Removed.");
            }
            catch (Exception exception)
            {
                return new SchemeResult(false, $"Could not remove it: {exception.Message}");
            }
        }

        /// <summary>Whether the folder the shim went into is somewhere a shell will look.</summary>
        public static bool OnPath()
        {
            string? path = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            char separator = OperatingSystem.IsWindows() ? ';' : ':';

            // Compared as paths rather than as strings, so a trailing slash or a different spelling
            // of the home folder does not read as a different folder.
            return path
                .Split(separator, StringSplitOptions.RemoveEmptyEntries)
                .Any(entry => Same(entry, Folder));
        }

        private static bool Same(string left, string right)
        {
            try
            {
                return string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(left.Trim().Trim('"'))),
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
            }
            catch (Exception)
            {
                // A PATH entry that is not a usable path at all - which is common - is simply not
                // this folder.
                return false;
            }
        }

        /// <summary>
        ///  The Windows shim.
        /// </summary>
        /// <remarks>
        ///  <c>start ""</c> rather than calling the executable directly, because a GUI application
        ///  started from cmd leaves the prompt attached to a process that will not exit until the
        ///  window is closed. The empty pair of quotes is the window title <c>start</c> demands
        ///  before it will accept a quoted path as the program.
        /// </remarks>
        private static string Windows(string executable) => string.Join("\r\n",
        [
            "@echo off",
            "rem Written by DevDeck. Hands its arguments to the running deck.",
            $"start \"\" \"{executable}\" %*",
            string.Empty,
        ]);

        /// <summary>
        ///  The Unix shim.
        /// </summary>
        /// <remarks>
        ///  <c>exec</c> so the shell does not keep a process around whose only job was to start
        ///  another, and <c>"$@"</c> quoted so a command name with a space in it stays one argument.
        /// </remarks>
        private static string Unix(string executable) => string.Join('\n',
        [
            "#!/bin/sh",
            "# Written by DevDeck. Hands its arguments to the running deck.",
            $"exec \"{executable}\" \"$@\"",
            string.Empty,
        ]);
    }
}
