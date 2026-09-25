using System.Text;

namespace DevDeck.Core
{
    /// <summary>
    ///  Pulls the runnable command out of a model's markdown reply.
    /// </summary>
    /// <remarks>
    ///  The point of the assistant is that the answer goes straight into the command list, so the
    ///  fenced block has to survive the round trip intact - including the shell prompts and the
    ///  "```bash" hints models sprinkle in out of habit.
    /// </remarks>
    internal static class CodeBlock
    {
        private const string Fence = "```";

        private const char NewLine = '\n';

        /// <summary>
        ///  Returns the last fenced code block in <paramref name="markdown"/>, cleaned up, or an
        ///  empty string when there is nothing that looks like a command.
        /// </summary>
        public static string LastCommand(string markdown) => Last(markdown).Command;

        /// <summary>
        ///  The last fenced block together with how it needs to be run, so an answer written as
        ///  PowerShell is saved as a PowerShell command rather than pasted into cmd.
        /// </summary>
        /// <remarks>
        ///  The last block rather than the first: when a model shows a wrong turn before the answer,
        ///  or offers a simple version and then a better one, the one it ends on is the one it means.
        /// </remarks>
        public static (string Command, CommandKind Kind) Last(string markdown)
        {
            List<(string Body, string Language)> blocks = [];
            StringBuilder current = new();
            string language = string.Empty;
            bool inside = false;

            foreach (string line in markdown.ReplaceLineEndings("\n").Split('\n'))
            {
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith(Fence, StringComparison.Ordinal))
                {
                    if (inside)
                    {
                        blocks.Add((current.ToString(), language));
                        current.Clear();
                        language = string.Empty;
                    }
                    else
                    {
                        language = trimmed[Fence.Length..].Trim();
                    }

                    inside = !inside;
                    continue;
                }

                if (inside)
                {
                    current.AppendLine(line);
                }
            }

            // A reply cut off mid-block still holds a usable command.
            if (inside && current.Length > 0)
            {
                blocks.Add((current.ToString(), language));
            }

            if (blocks.Count == 0)
            {
                return (string.Empty, CommandKind.Shell);
            }

            (string body, string fence) = blocks[^1];
            string cleaned = Clean(body);

            return (cleaned, KindFor(fence, cleaned));
        }

        /// <summary>
        ///  Maps a fence label onto how the body has to be run.
        /// </summary>
        /// <remarks>
        ///  Falls back to reading the body when the fence says nothing useful: models label
        ///  PowerShell as "shell" often enough that trusting the label alone gets it wrong, and a
        ///  PowerShell script fed to cmd.exe fails in a way that looks like the app is broken.
        /// </remarks>
        public static CommandKind KindFor(string language, string body)
        {
            switch (language.ToLowerInvariant())
            {
                case "powershell":
                case "pwsh":
                case "posh":
                case "ps":
                case "ps1":
                    return CommandKind.PowerShell;

                case "bat":
                case "batch":
                case "cmd":
                case "dos":
                    // Only when there is really a script here. Models fence ordinary one-liners as
                    // cmd all the time, and writing "yarn install" out to a .bat just to run it
                    // would be a temporary file for nothing.
                    return body.Contains(NewLine) ? CommandKind.Batch : CommandKind.Shell;
            }

            // A single line is a command line whatever it is written in. PowerShell is the
            // exception handled above: a one-line cmdlet still has to reach powershell.exe.
            if (!body.Contains('\n'))
            {
                return CommandKind.Shell;
            }

            bool powershell =
                body.Contains("$") ||
                body.Contains("Write-Host", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("Get-", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("param(", StringComparison.OrdinalIgnoreCase) ||
                body.Contains("foreach (", StringComparison.OrdinalIgnoreCase);

            return powershell ? CommandKind.PowerShell : CommandKind.Batch;
        }

        /// <summary>Strips the shell prompts and blank padding that come with copied snippets.</summary>
        private static string Clean(string block)
        {
            IEnumerable<string> lines = block
                .ReplaceLineEndings("\n")
                .Split('\n')
                .Select(StripPrompt);

            return string.Join(Environment.NewLine, lines).Trim();
        }

        private static string StripPrompt(string line)
        {
            string trimmed = line.TrimStart();

            foreach (string prompt in (string[])["PS>", "C:\\>", "$ ", "> ", "# "])
            {
                if (trimmed.StartsWith(prompt, StringComparison.Ordinal))
                {
                    return trimmed[prompt.Length..].TrimStart();
                }
            }

            // "PS C:\path> command" - a prompt only if the line really starts one.
            if (trimmed.StartsWith("PS ", StringComparison.Ordinal))
            {
                int end = trimmed.IndexOf("> ", StringComparison.Ordinal);

                if (end > 0)
                {
                    return trimmed[(end + 2)..].TrimStart();
                }
            }

            return line;
        }
    }
}
