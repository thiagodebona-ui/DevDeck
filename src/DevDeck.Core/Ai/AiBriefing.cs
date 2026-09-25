namespace DevDeck.Core
{
    /// <summary>
    ///  The system message that tells the model what it is plugged into.
    /// </summary>
    /// <remarks>
    ///  This is the difference between "a chat box that happens to be in a dev tool" and an
    ///  assistant that hands back something the deck can actually run. Without it the model has no
    ///  idea that its fence label decides how the code is executed, so it labels a PowerShell
    ///  script ```bash and the command fails on the first line - which is exactly what V3 did
    ///  before this existed, because it sent no system message at all.
    ///
    ///  V2's briefing assumed Windows and named three fences. V3 runs on three platforms and has a
    ///  fourth kind, so the briefing is built per machine rather than being a constant: telling a
    ///  model on Linux that it may emit ```bat is how you get a batch file that cannot run.
    /// </remarks>
    internal static class AiBriefing
    {
        /// <summary>
        ///  The briefing, with the workspace folder appended when there is one.
        /// </summary>
        /// <remarks>
        ///  The workspace is the one piece of context that changes what a good answer looks like -
        ///  paths, whether a repository is present, which package manager is in front of it.
        /// </remarks>
        public static string For(string workspace)
        {
            string briefing = $"""
                You are built into "DevDeck", a small desktop app a developer uses to keep a list of
                named shell commands and scripts.

                A saved command is one of these, and the fence you use decides which:
                {Fences()}

                Either way it runs with the working directory set to the workspace folder chosen in
                the app.

                When you are asked for a command:
                - Give one or two sentences of explanation, then exactly one fenced code block
                  holding only the command or script itself, ready to run.
                - Label the fence correctly. A multi-line PowerShell script fenced as shell will fail.
                - Do not put a shell prompt, or "cmd /c", inside the block.
                - Reach for a script when the job needs several steps, variables or error handling,
                  and a one-line command when it does not.
                - Assume a normal {Machine()} developer machine: git, node, npm, yarn, dotnet and the
                  usual built-in commands.
                - If the command deletes or overwrites anything, say so plainly in the explanation.

                Answer anything else briefly and normally.
                """;

            return workspace.Length > 0
                ? $"{briefing}{Environment.NewLine}{Environment.NewLine}The current workspace folder is: {workspace}"
                : briefing;
        }

        /// <summary>Only the fences that this machine can actually run.</summary>
        private static string Fences()
        {
            List<string> lines =
            [
                OperatingSystem.IsWindows()
                    ? "- ```shell        a single command line, run by cmd.exe"
                    : "- ```shell        a single command line, run by /bin/sh",
                "- ```powershell   a whole PowerShell script, saved and run as a .ps1 file",
            ];

            if (OperatingSystem.IsWindows())
            {
                lines.Add("- ```bat          a whole batch script, saved and run as a .bat file");
            }

            if (ScriptFile.FindBash() is not null)
            {
                lines.Add("- ```bash         a whole shell script, saved and run as a .sh file");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string Machine()
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }

            return OperatingSystem.IsMacOS() ? "macOS" : "Linux";
        }

        /// <summary>
        ///  A failure turned into something the user can act on.
        /// </summary>
        /// <remarks>
        ///  V2's DescribeAiFailure. A bare "connection refused" tells a developer nothing they did
        ///  not already suspect; naming the endpoint and the command that would fix it does.
        /// </remarks>
        public static string Describe(Exception exception, AiClient client)
        {
            if (exception is OperationCanceledException)
            {
                return $"{client.Endpoint} did not answer in time.";
            }

            if (exception is HttpRequestException && IsUnreachable(exception))
            {
                string model = client.Model.Length > 0 ? client.Model : "qwen2.5-coder:7b";

                return client.IsLocal
                    ? $"Nothing is listening on {client.BaseUrl}."
                        + $"{Environment.NewLine}{Environment.NewLine}Install Ollama from ollama.com, then run:"
                        + $"{Environment.NewLine}    ollama pull {model}"
                    : $"Could not reach {client.BaseUrl}. Check the endpoint, and whether this network allows it.";
            }

            return exception.Message;
        }

        /// <summary>Whether a request failed because nothing answered, rather than answered badly.</summary>
        private static bool IsUnreachable(Exception exception)
        {
            for (Exception? cause = exception; cause is not null; cause = cause.InnerException)
            {
                if (cause is System.Net.Sockets.SocketException)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
