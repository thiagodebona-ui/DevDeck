namespace DevDeck.Core
{
    /// <summary>
    ///  Asks a model to rewrite a command in another language, and reads the script out of its reply.
    /// </summary>
    /// <remarks>
    ///  Used when a command's Run with is changed: the body was written for the old interpreter,
    ///  and handing PowerShell to bash is a command that fails on its first line. Rewriting it by
    ///  hand is exactly the chore a model does well, so the deck asks one when one is set up.
    ///
    ///  The instructions are about what must survive the trip rather than about style, because those
    ///  are the parts a model gets wrong unprompted: the placeholders DevDeck fills in, and how
    ///  parameters arrive, which differs per language and is easy to translate literally and break.
    /// </remarks>
    internal static class ScriptConversion
    {
        /// <summary>The fence label for a kind, which is also how the model is told what to write.</summary>
        public static string Fence(CommandKind kind) => kind switch
        {
            CommandKind.PowerShell => "powershell",
            CommandKind.Batch => "bat",
            CommandKind.Bash => "bash",
            _ => "shell",
        };

        /// <summary>A kind in words, for the request.</summary>
        public static string Describe(CommandKind kind) => kind switch
        {
            CommandKind.PowerShell => "a PowerShell script, run as a .ps1 file",
            CommandKind.Batch => "a Windows batch script, run as a .bat file",
            CommandKind.Bash => "a bash script, run as a .sh file",
            _ => OperatingSystem.IsWindows()
                ? "a single command line run by cmd.exe"
                : "a single command line run by /bin/sh",
        };

        /// <summary>The request: what to convert, from what, to what, and what has to be kept.</summary>
        public static IReadOnlyList<ChatMessage> Messages(string body, CommandKind from, CommandKind to)
        {
            string system = $"""
                You convert scripts for "DevDeck", a desktop app that keeps a list of named commands.
                Reply with only the converted script, in exactly one fenced code block labelled
                {Fence(to)}, and nothing before or after it.

                Rules:
                - Keep what the script does and what it prints. Keep its comments, translated to the
                  new comment syntax.
                - Keep every {"{{name}}"}, {"{{name:default}}"} and {"{{secret:NAME}}"} placeholder exactly as
                  written. DevDeck replaces them with text before the script runs.
                - Parameters arrive differently per language: PowerShell as -Name "value" read with
                  param(), batch as %1 %2, bash and shell as $1 $2. Every one is also in the
                  environment as DEVDECK_ARG_NAME and DEVDECK_ARG_1. Convert how the script reads
                  them to the new language's way.
                - Keep any DEVDECK_ environment variable the script reads, under the same name.
                - If something cannot be done in the new language, do the nearest thing and say so in
                  a comment inside the script.
                """;

            string user = $"""
                Convert this from {Describe(from)} to {Describe(to)}:

                ```{Fence(from)}
                {body}
                ```
                """;

            return [new ChatMessage("system", system), new ChatMessage("user", user)];
        }

        /// <summary>
        ///  The script in a reply: its last fenced block, or the whole reply when there is none.
        /// </summary>
        /// <remarks>
        ///  Read raw rather than through <see cref="CodeBlock"/>, which strips what look like shell
        ///  prompts - and in a bash or PowerShell script a line starting "# " is a comment, which
        ///  that would turn into code. Null when there is nothing usable, so the body is left alone.
        /// </remarks>
        public static string? Extract(string reply)
        {
            List<string> blocks = [];
            System.Text.StringBuilder current = new();
            bool inside = false;

            foreach (string line in reply.ReplaceLineEndings("\n").Split('\n'))
            {
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (inside)
                    {
                        blocks.Add(current.ToString());
                        current.Clear();
                    }

                    inside = !inside;

                    continue;
                }

                if (inside)
                {
                    current.Append(line).Append('\n');
                }
            }

            // A reply cut off inside its block holds the script so far - but a script that stops
            // half-way is worse than the original, so only a closed block is taken. A reply with no
            // fence at all is a model that ignored the format, and its whole text is the script.
            if (blocks.Count == 0 && inside)
            {
                return null;
            }

            string script = blocks.Count > 0 ? blocks[^1] : reply;
            string trimmed = script.Trim('\n', '\r');

            return trimmed.Trim().Length > 0 ? trimmed.ReplaceLineEndings(Environment.NewLine) : null;
        }
    }
}
