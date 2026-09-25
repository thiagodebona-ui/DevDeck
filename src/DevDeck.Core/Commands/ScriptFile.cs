using System.Text;

namespace DevDeck.Core
{
    /// <summary>
    ///  Turns a command into something the host OS can start, writing script bodies to a temporary
    ///  file first.
    /// </summary>
    /// <remarks>
    ///  This is what lets a whole .ps1, .bat or .sh live inside the settings file rather than as a
    ///  loose file next to the executable. The app used to depend on scripts sitting in its own
    ///  folder, which broke the moment it was copied somewhere else; a command now carries its own
    ///  body.
    ///
    ///  Diverged from V2: every launch used to be cmd.exe or powershell.exe. The interpreter is now
    ///  chosen per platform, and a kind with no interpreter on this OS is reported rather than
    ///  started - see <see cref="Create"/>.
    /// </remarks>
    internal sealed class ScriptFile : IDisposable
    {
        /// <summary>
        ///  Pins PowerShell's output to UTF-8 before the user's script runs.
        /// </summary>
        /// <remarks>
        ///  Windows PowerShell 5.1 writes redirected output in the console's OEM code page, so
        ///  anything accented came back through the log as mojibake. The app reads the pipe as
        ///  UTF-8, so the script is told to write UTF-8. Harmless under pwsh, which is UTF-8
        ///  already.
        /// </remarks>
        private const string PowerShellPreamble =
            "[Console]::OutputEncoding = [System.Text.Encoding]::UTF8\r\n";

        /// <summary>The same for cmd, which reads a batch file in the OEM code page by default.</summary>
        private const string BatchPreamble = "@chcp 65001>nul\r\n";

        /// <summary>Unix shells need no encoding coaxing, only a line telling the kernel who runs this.</summary>
        private const string ShellPreamble = "#!/bin/sh\n";

        /// <summary>Temporary file backing this run, or null for a plain shell command.</summary>
        private readonly string? path;

        public string FileName { get; }

        public string Arguments { get; }

        private ScriptFile(string fileName, string arguments, string? path)
        {
            FileName = fileName;
            Arguments = arguments;
            this.path = path;
        }

        /// <summary>Prepares <paramref name="command"/> for launching.</summary>
        /// <exception cref="PlatformNotSupportedException">
        ///  The command's kind has no interpreter on this OS - a batch file away from Windows, or a
        ///  shell script on it. Thrown rather than silently run by the wrong interpreter, which
        ///  would fail further away from the cause.
        /// </exception>
        public static ScriptFile Create(CustomCommand command)
        {
            bool windows = OperatingSystem.IsWindows();

            switch (command.Kind)
            {
                case CommandKind.PowerShell:
                {
                    // A BOM, because Windows PowerShell 5.1 reads a script without one as ANSI and
                    // mangles every accented character and box-drawing glyph in it. pwsh assumes
                    // UTF-8 without one, and tolerates it with.
                    string file = Write(command.Name, ".ps1", PowerShellPreamble + command.Command,
                        new UTF8Encoding(windows));

                    return new ScriptFile(
                        windows ? "powershell.exe" : "pwsh",
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{file}\"{ArgumentsFor(command)}",
                        file);
                }

                case CommandKind.Batch:
                {
                    if (!windows)
                    {
                        throw new PlatformNotSupportedException(
                            "Batch scripts only run on Windows. Use a shell script instead.");
                    }

                    // No BOM here: cmd.exe treats the bytes as part of the first command and fails
                    // with a confusing error about an unrecognised command.
                    string file = Write(command.Name, ".bat", BatchPreamble + command.Command,
                        new UTF8Encoding(false));

                    return new ScriptFile("cmd.exe", $"/c \"{file}\"{ArgumentsFor(command)}", file);
                }

                case CommandKind.Bash:
                {
                    string? bash = windows ? FindBash() : "/bin/sh";

                    if (bash is null)
                    {
                        throw new PlatformNotSupportedException(
                            "No bash found. Install Git for Windows, or enable WSL, then try again.");
                    }

                    // No BOM: the kernel reads the first two bytes looking for "#!", and a BOM in
                    // front of it means the script is handed to the wrong interpreter. LF endings
                    // for the same reason - bash reads a trailing CR as part of the command and
                    // fails with the baffling "command not found: 'foo\r'".
                    string body = (ShellPreamble + command.Command).Replace("\r\n", "\n");
                    string file = Write(command.Name, ".sh", body, new UTF8Encoding(false));

                    if (!windows)
                    {
                        File.SetUnixFileMode(file,
                            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    }

                    return new ScriptFile(bash, $"\"{ScriptPathFor(bash, file)}\"{ArgumentsFor(command)}", file);
                }

                default:
                    return windows
                        ? new ScriptFile("cmd.exe", $"/c {command.Command}{ArgumentsFor(command)}", null)
                        : new ScriptFile("/bin/sh", $"-c \"{Escape(command.Command + ArgumentsFor(command))}\"", null);
            }
        }

        /// <summary>
        ///  Finds a bash on Windows, or null if there is none.
        /// </summary>
        /// <remarks>
        ///  Git for Windows is preferred over WSL's bash even when both are installed. WSL runs in
        ///  its own filesystem namespace and its own distro, so a script would run against
        ///  different tools than the rest of the command deck; Git bash shares the Windows
        ///  filesystem and is what someone typing a shell one-liner on Windows usually means.
        /// </remarks>
        /// <summary>
        ///  The command's parameters as they go on the end of its command line, with a leading
        ///  space - or nothing, when it has none switched on.
        /// </summary>
        /// <remarks>
        ///  <para>
        ///   PowerShell gets them by name, <c>-Name "value"</c>, which binds to a <c>param()</c>
        ///   block and otherwise lands in <c>$args</c>. An unnamed row, or a name PowerShell could
        ///   not bind, goes positionally. Batch, shell scripts and plain command lines have no
        ///   named arguments, so every value goes in order: <c>%1 %2</c>, <c>$1 $2</c>.
        ///  </para>
        ///  <para>
        ///   Every value is quoted, so a path with spaces is one argument, and a quote inside a
        ///   value is escaped the way that interpreter expects rather than ending the argument.
        ///   The same values are also in the environment, which <see cref="EnvironmentFor"/> builds.
        ///  </para>
        /// </remarks>
        public static string ArgumentsFor(CustomCommand command)
        {
            List<CommandArgument> on = [.. command.Arguments.Where(Passed)];

            if (on.Count == 0)
            {
                return string.Empty;
            }

            bool windows = OperatingSystem.IsWindows();

            IEnumerable<string> parts = command.Kind switch
            {
                CommandKind.PowerShell => on.Select(argument => Bindable(argument.Name)
                    ? $"-{argument.Name.Trim()} {Quote(argument.Value)}"
                    : Quote(argument.Value)),
                CommandKind.Batch => on.Select(argument => CmdQuote(argument.Value)),
                CommandKind.Shell when windows => on.Select(argument => CmdQuote(argument.Value)),
                CommandKind.Shell => on.Select(argument => ShQuote(argument.Value)),
                _ => on.Select(argument => Quote(argument.Value)),
            };

            return " " + string.Join(' ', parts);
        }

        /// <summary>
        ///  The same parameters as environment variables, for a script that would rather read
        ///  those: <c>DEVDECK_ARG_NAME</c> for each named one, and <c>DEVDECK_ARG_1</c> onwards
        ///  for all of them in order, plus <c>DEVDECK_ARG_COUNT</c>.
        /// </summary>
        public static IReadOnlyDictionary<string, string> EnvironmentFor(CustomCommand command)
        {
            Dictionary<string, string> variables = new(StringComparer.OrdinalIgnoreCase);
            int at = 0;

            foreach (CommandArgument argument in command.Arguments.Where(Passed))
            {
                at++;
                variables[$"DEVDECK_ARG_{at}"] = argument.Value;

                string name = string.Concat(argument.Name.Trim()
                    .Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToUpperInvariant(c) : '_'));

                if (name.Length > 0)
                {
                    variables[$"DEVDECK_ARG_{name}"] = argument.Value;
                }
            }

            if (at > 0)
            {
                variables["DEVDECK_ARG_COUNT"] = at.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return variables;
        }

        /// <summary>On, and with something in it. A blank row is one the user has not filled in yet.</summary>
        private static bool Passed(CommandArgument argument) =>
            argument.Enabled && (argument.Name.Trim().Length > 0 || argument.Value.Length > 0);

        /// <summary>Whether PowerShell could bind a parameter of this name: a plain identifier.</summary>
        private static bool Bindable(string name)
        {
            string trimmed = name.Trim();

            return trimmed.Length > 0
                && (char.IsAsciiLetter(trimmed[0]) || trimmed[0] == '_')
                && trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c == '_');
        }

        /// <summary>Double quotes, with the backslash escaping a process command line uses.</summary>
        private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

        /// <summary>cmd.exe's rule: a quote inside quotes is doubled.</summary>
        private static string CmdQuote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

        /// <summary>Single quotes for sh, where nothing inside them is special but the quote itself.</summary>
        private static string ShQuote(string value) => $"'{value.Replace("'", "'\\''")}'";

        public static string? FindBash()
        {
            if (!OperatingSystem.IsWindows())
            {
                return "/bin/sh";
            }

            string[] candidates =
            [
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Git", "bin", "bash.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    "Git", "bin", "bash.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Git", "bin", "bash.exe"),
            ];

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            // Anything else on PATH, which is where a scoop or chocolatey install turns up, and
            // where WSL's bash.exe lives as a last resort.
            foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                try
                {
                    string candidate = Path.Combine(directory.Trim(), "bash.exe");

                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch (ArgumentException)
                {
                    // A malformed PATH entry. Skip it rather than refusing to find bash at all.
                }
            }

            return null;
        }

        /// <summary>
        ///  The path to hand the chosen bash, translated if that bash cannot read a Windows one.
        /// </summary>
        /// <remarks>
        ///  Git bash accepts C:/Users/... as-is. WSL's bash sees the Windows drives mounted under
        ///  /mnt, so the same path has to be rewritten or the script is simply not found.
        /// </remarks>
        private static string ScriptPathFor(string bash, string file)
        {
            if (!OperatingSystem.IsWindows())
            {
                return file;
            }

            string forward = file.Replace('\\', '/');

            bool wsl = bash.Contains(@"\System32\", StringComparison.OrdinalIgnoreCase)
                || bash.Contains(@"\Sysnative\", StringComparison.OrdinalIgnoreCase);

            if (!wsl || forward.Length < 2 || forward[1] != ':')
            {
                return forward;
            }

            return $"/mnt/{char.ToLowerInvariant(forward[0])}{forward[2..]}";
        }

        /// <summary>
        ///  Quotes a one-liner for <c>sh -c "..."</c>.
        /// </summary>
        /// <remarks>
        ///  Only the characters the double quotes leave live need escaping. The user's command is
        ///  meant to be a command - this is not a sandbox, and the Windows branch has never been
        ///  one either.
        /// </remarks>
        private static string Escape(string command)
        {
            return command
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("$", "\\$")
                .Replace("`", "\\`");
        }

        private static string Write(string name, string extension, string body, Encoding encoding)
        {
            string directory = Path.Combine(Path.GetTempPath(), "DevDeck");
            Directory.CreateDirectory(directory);

            // The name is only there to make the temp folder readable; the guid keeps two runs of
            // the same command from fighting over one file.
            string safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string file = Path.Combine(directory, $"{safe}-{Guid.NewGuid():N}{extension}");

            File.WriteAllText(file, body, encoding);

            return file;
        }

        public void Dispose()
        {
            if (path is null)
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // Still running, or locked by a virus scanner. It is in the temp folder either way.
            }
        }
    }
}
