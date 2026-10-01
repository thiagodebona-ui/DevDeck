using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>A command a model's reply defines, by name, for a chain to run.</summary>
    internal sealed record PlannedCommand(string Name, string Body, CommandKind Kind)
    {
        public CustomCommand ToCommand() => new() { Name = Name, Command = Body, Kind = Kind };
    }

    /// <summary>
    ///  A chain a model's reply asks for: its steps, and the commands it wrote for them.
    /// </summary>
    /// <remarks>
    ///  The format the briefing teaches is markdown rather than JSON, and that is the whole
    ///  design. Each command stays an ordinary fenced block - the same thing the model already
    ///  writes for a single command, readable in the transcript, with only a name added to its
    ///  fence line - and the chain is a short block listing those names. A JSON document would put
    ///  every script inside a string, and a small local model escaping a forty-line PowerShell
    ///  script's quotes and newlines correctly is not something to depend on.
    ///
    ///  <code>
    ///  ```powershell name="Count files"
    ///  (Get-ChildItem -Recurse -File).Count
    ///  ```
    ///
    ///  ```devdeck-chain
    ///  name: Repository report
    ///  stop-on-failure: false
    ///  steps:
    ///  - Count files
    ///  - Report
    ///  ```
    ///  </code>
    ///
    ///  A step may also name a command that is already in the deck rather than one in the reply;
    ///  which is which is only known when the chain is created, against the deck as it is then.
    /// </remarks>
    internal sealed record ChainPlan(
        string Name,
        IReadOnlyList<string> Steps,
        bool StopOnFailure,
        IReadOnlyList<PlannedCommand> Commands)
    {
        /// <summary>The fence label that marks the chain block.</summary>
        public const string Fence = "devdeck-chain";

        private static readonly Regex NameAttribute = new(
            """\bname\s*=\s*(?:"(?<name>[^"]*)"|'(?<name>[^']*)'|(?<name>\S+))""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ListItem = new(
            @"^\s*(?:[-*+]|\d+[.)])\s+(?<item>.+?)\s*$",
            RegexOptions.CultureInvariant);

        /// <summary>The language half of a fence line: "powershell" out of powershell name="x".</summary>
        public static string LanguageOf(string info)
        {
            string trimmed = info.Trim();
            int space = trimmed.IndexOfAny([' ', '\t', '{']);

            return space < 0 ? trimmed : trimmed[..space];
        }

        /// <summary>The name a fence line gives its block, or an empty string.</summary>
        public static string NameOf(string info)
        {
            Match match = NameAttribute.Match(info);

            return match.Success ? match.Groups["name"].Value.Trim() : string.Empty;
        }

        /// <summary>Whether a fence line opens a chain block.</summary>
        public static bool IsChainFence(string info) =>
            LanguageOf(info).Equals(Fence, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///  The chain in a reply, or null when the reply does not define one.
        /// </summary>
        /// <remarks>
        ///  The last chain block wins, for the same reason the last code block does in
        ///  <see cref="CodeBlock"/>: a model that corrects itself ends on the version it means.
        ///  A command defined twice under one name likewise keeps its last body.
        /// </remarks>
        public static ChainPlan? Parse(string markdown)
        {
            List<(string Info, string Body)> blocks = Blocks(markdown);

            (string Info, string Body) chain = blocks.LastOrDefault(block => IsChainFence(block.Info));

            if (chain.Info is null)
            {
                return null;
            }

            Dictionary<string, PlannedCommand> commands = new(StringComparer.OrdinalIgnoreCase);

            foreach ((string info, string body) in blocks)
            {
                string name = NameOf(info);
                string script = body.Trim('\r', '\n');

                if (IsChainFence(info) || name.Length == 0 || script.Trim().Length == 0)
                {
                    continue;
                }

                commands[name] = new PlannedCommand(name, script, CodeBlock.KindFor(LanguageOf(info), script));
            }

            string title = string.Empty;
            bool stop = true;
            List<string> steps = [];

            foreach (string line in chain.Body.Split('\n'))
            {
                string text = line.Trim();

                if (ListItem.Match(line) is { Success: true } item)
                {
                    string step = Unquote(item.Groups["item"].Value);

                    if (step.Length > 0)
                    {
                        steps.Add(Canonical(step, commands));
                    }

                    continue;
                }

                int colon = text.IndexOf(':');

                if (colon <= 0)
                {
                    continue;
                }

                string key = text[..colon].Trim().Replace(' ', '-').Replace('_', '-').ToLowerInvariant();
                string value = Unquote(text[(colon + 1)..]);

                if (key == "name")
                {
                    title = value;
                }
                else if (key == "stop-on-failure")
                {
                    stop = !(value.Equals("false", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("no", StringComparison.OrdinalIgnoreCase)
                        || value == "0");
                }
            }

            if (steps.Count == 0)
            {
                return null;
            }

            // Only the commands the chain uses, in the order it first uses them. A block the model
            // named but did not put in the chain is still in the transcript, with its own Add button.
            List<PlannedCommand> used = [];

            foreach (string step in steps)
            {
                if (commands.TryGetValue(step, out PlannedCommand? command) && !used.Contains(command))
                {
                    used.Add(command);
                }
            }

            return new ChainPlan(title.Length > 0 ? title : "New chain", steps, stop, used);
        }

        /// <summary>A step spelled the way its command is, so "count files" finds "Count files".</summary>
        private static string Canonical(string step, Dictionary<string, PlannedCommand> commands) =>
            commands.TryGetValue(step, out PlannedCommand? command) ? command.Name : step;

        /// <summary>Strips the quotes and backticks a model puts round a name it is citing.</summary>
        private static string Unquote(string value) => value.Trim().Trim('"', '\'', '`').Trim();

        /// <summary>Every fenced block, with its whole fence line and its body.</summary>
        private static List<(string Info, string Body)> Blocks(string markdown)
        {
            List<(string, string)> blocks = [];
            System.Text.StringBuilder body = new();
            string? info = null;

            foreach (string line in markdown.ReplaceLineEndings("\n").Split('\n'))
            {
                string trimmed = line.TrimStart();

                if (trimmed.StartsWith("```", StringComparison.Ordinal))
                {
                    if (info is null)
                    {
                        info = trimmed[3..].Trim();
                    }
                    else
                    {
                        blocks.Add((info, body.ToString()));
                        body.Clear();
                        info = null;
                    }

                    continue;
                }

                if (info is not null)
                {
                    body.Append(line).Append('\n');
                }
            }

            return blocks;
        }
    }
}
