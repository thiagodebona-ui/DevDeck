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

                {Chains()}

                Answer anything else briefly and normally.
                """;

            return workspace.Length > 0
                ? $"{briefing}{Environment.NewLine}{Environment.NewLine}The current workspace folder is: {workspace}"
                : briefing;
        }

        /// <summary>
        ///  How to hand back a chain: several commands, and the order to run them in.
        /// </summary>
        /// <remarks>
        ///  The variables are spelled out because they are the only way one step can see another,
        ///  and a model left to guess invents a pipe or a temp file of its own that the chain does
        ///  not provide. The format is <see cref="ChainPlan"/>'s, and the example is in the
        ///  machine's own script language so the model copies something that runs here.
        /// </remarks>
        private static string Chains()
        {
            bool windows = OperatingSystem.IsWindows();
            string fence = windows ? "powershell" : (ScriptFile.FindBash() is not null ? "bash" : "shell");

            string first = windows
                ? "(Get-ChildItem -Recurse -File).Count"
                : "find . -type f | wc -l";

            string report = windows
                ? """
                  $folder = $env:DEVDECK_CHAIN_OUTPUTS
                  $files = Get-Content (Join-Path $folder 'step1.txt') -Raw
                  $todos = Get-Content (Join-Path $folder 'step2.txt') -Raw
                  "Files: $($files.Trim())"
                  "TODOs: $($todos.Trim())"
                  """
                : """
                  folder="$DEVDECK_CHAIN_OUTPUTS"
                  echo "Files: $(cat "$folder/step1.txt")"
                  echo "TODOs: $(cat "$folder/step2.txt")"
                  """;

            string todos = windows
                ? "(Get-ChildItem -Recurse -File | Select-String -Pattern 'TODO').Count"
                : "grep -r TODO . | wc -l";

            return $"""
                When you are asked for a chain - several commands run one after another, an
                automation, or commands whose output feeds another - answer with:
                - One fenced block per command, with its name on the fence line: ```{fence} name="Count files"
                  Give every command a short, distinct name.
                - Then exactly one ```{ChainPlan.Fence} block: a "name:" line, a "stop-on-failure:"
                  line (true or false), and a "steps:" list of command names in the order they run.
                  A step may also name a command the user already has.

                A step can read what earlier steps printed, through environment variables:
                - DEVDECK_PREVIOUS        everything the step just before printed
                - DEVDECK_PREVIOUS_LINE   the last non-empty line the step just before printed
                - DEVDECK_PREVIOUS_EXIT   that step's exit code, "0" for success
                - DEVDECK_CHAIN_OUTPUTS   a folder holding every earlier step's output, as
                                          step1.txt, step2.txt and so on, numbered by position
                - DEVDECK_STEP            this step's position, counting from 1
                A step that combines the output of several others must read DEVDECK_CHAIN_OUTPUTS;
                DEVDECK_PREVIOUS only ever holds the one step before. Steps that produce values
                should print just the value, so the step that reads them does not have to parse.

                For example:

                ```{fence} name="Count files"
                {first}
                ```

                ```{fence} name="Count TODOs"
                {todos}
                ```

                ```{fence} name="Report"
                {report}
                ```

                ```{ChainPlan.Fence}
                name: Repository report
                stop-on-failure: true
                steps:
                - Count files
                - Count TODOs
                - Report
                ```
                """;
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
