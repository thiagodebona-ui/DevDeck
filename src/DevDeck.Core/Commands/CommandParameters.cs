using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>One placeholder found in a command body.</summary>
    /// <remarks>
    ///  <see cref="Name"/> is what the prompt asks for and what the value is remembered under.
    ///  <see cref="Default"/> is the text after a colon in the placeholder, which is both a
    ///  starting value and the documentation of what the parameter expects.
    /// </remarks>
    internal sealed record CommandParameter(string Name, string Default, bool IsSecret)
    {
        /// <summary>The placeholder as it appears in the body, for substitution.</summary>
        public string Token => IsSecret ? $"{{{{secret:{Name}}}}}" : Default.Length > 0
            ? $"{{{{{Name}:{Default}}}}}"
            : $"{{{{{Name}}}}}";

        /// <summary>A human label: "base branch" out of "base_branch".</summary>
        public string Label
        {
            get
            {
                string spaced = Name.Replace('_', ' ').Replace('-', ' ').Trim();

                return spaced.Length == 0
                    ? Name
                    : char.ToUpperInvariant(spaced[0]) + spaced[1..];
            }
        }
    }

    /// <summary>
    ///  Finds and fills the <c>{{placeholders}}</c> in a command body.
    /// </summary>
    /// <remarks>
    ///  A command was a fixed string, so the things people run most often could not be buttons at
    ///  all: "check out a branch", "kill whatever is on a port", "run one test" all take an
    ///  argument, and a deck that cannot take one can only hold the commands that never vary.
    ///  This is what turns ten starters into a deck worth keeping.
    ///
    ///  The syntax is deliberately the one everything else uses - Warp workflows, VS Code tasks,
    ///  GitHub Actions - so it needs no explaining: <c>{{name}}</c>, or <c>{{name:default}}</c>
    ///  where the default doubles as the hint. <c>{{secret:NAME}}</c> is looked up in the vault
    ///  instead of being asked for, so a token never sits in the settings file as plain text.
    ///
    ///  Substitution is textual and deliberately not quoted: the body is a script the user wrote,
    ///  and the placeholder may just as well sit inside a string literal or a path as stand alone
    ///  as an argument. Quoting it here would corrupt those. What is refused is a value that could
    ///  end the current command and start another - see <see cref="Unsafe"/>.
    /// </remarks>
    internal static partial class CommandParameters
    {
        /// <summary>A placeholder, with an optional default after the first colon.</summary>
        [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z_][A-Za-z0-9_\-]*)\s*(?::(?<default>[^}]*))?\}\}")]
        private static partial Regex Placeholder();

        /// <summary>A secret reference, which is filled from the vault rather than a prompt.</summary>
        [GeneratedRegex(@"\{\{\s*secret:(?<name>[A-Za-z_][A-Za-z0-9_\-]*)\s*\}\}")]
        private static partial Regex Secret();

        /// <summary>Whether this body needs anything filling in before it can run.</summary>
        public static bool Any(string body) => Placeholder().IsMatch(body);

        /// <summary>
        ///  The values a run already has without asking: the command's own parameter rows, then
        ///  whatever a link or the command line supplied, which wins over a row of the same name.
        /// </summary>
        /// <remarks>
        ///  A row and a placeholder of the same name are the same parameter as far as anyone
        ///  filling them in is concerned. They used to be two unrelated mechanisms - a row was only
        ///  ever passed to the script as an argument, and a placeholder always asked - so a user who
        ///  filled in Name under Parameters was then asked for Name again in a box, with their
        ///  value nowhere in it.
        ///
        ///  Only rows that are switched on, named and not empty count. An empty row is a parameter
        ///  the user has not decided on yet, and that is exactly what the box is for.
        /// </remarks>
        public static Dictionary<string, string> Given(
            IEnumerable<CommandArgument> rows,
            IReadOnlyDictionary<string, string>? supplied = null)
        {
            Dictionary<string, string> given = new(StringComparer.OrdinalIgnoreCase);

            foreach (CommandArgument row in rows)
            {
                string name = row.Name.Trim();

                if (row.Enabled && name.Length > 0 && row.Value.Length > 0)
                {
                    given.TryAdd(name, row.Value);
                }
            }

            foreach ((string name, string value) in supplied ?? new Dictionary<string, string>())
            {
                given[name] = value;
            }

            return given;
        }

        /// <summary>
        ///  Whether every placeholder already has a value, so the run need not ask at all.
        /// </summary>
        /// <remarks>
        ///  A secret counts as answered when the vault has it, since it is filled from there and
        ///  never typed. One the vault lacks still needs the box, which is where that is said.
        /// </remarks>
        public static bool Answered(
            IReadOnlyList<CommandParameter> parameters,
            IReadOnlyDictionary<string, string> given,
            Func<string, string?> secrets) =>
            parameters.All(parameter => parameter.IsSecret
                ? secrets(parameter.Name) is not null
                : given.ContainsKey(parameter.Name));

        /// <summary>The parameters with what is already known put in as their starting values.</summary>
        public static IReadOnlyList<CommandParameter> Prefill(
            IReadOnlyList<CommandParameter> parameters,
            IReadOnlyDictionary<string, string> given) =>
        [
            .. parameters.Select(parameter => !parameter.IsSecret && given.TryGetValue(parameter.Name, out string? value)
                ? parameter with { Default = value }
                : parameter),
        ];

        /// <summary>
        ///  The parameters in a body, in the order they first appear and without repeats.
        /// </summary>
        /// <remarks>
        ///  Deduplicated by name: a body that mentions <c>{{branch}}</c> three times asks once and
        ///  substitutes three times, which is the only behaviour that makes sense to the person
        ///  answering. The first occurrence wins the default, so a later bare <c>{{branch}}</c>
        ///  does not blank the hint set by the first.
        /// </remarks>
        public static IReadOnlyList<CommandParameter> Find(string body)
        {
            if (body.Length == 0)
            {
                return [];
            }

            List<CommandParameter> found = [];

            foreach (Match match in Placeholder().Matches(body))
            {
                string name = match.Groups["name"].Value;

                // "secret" is the marker, not a parameter name of its own.
                if (string.Equals(name, "secret", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (found.Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                found.Add(new CommandParameter(name, match.Groups["default"].Value.Trim(), false));
            }

            foreach (Match match in Secret().Matches(body))
            {
                string name = match.Groups["name"].Value;

                if (found.Any(p => p.IsSecret && string.Equals(p.Name, name, StringComparison.Ordinal)))
                {
                    continue;
                }

                found.Add(new CommandParameter(name, string.Empty, true));
            }

            return found;
        }

        /// <summary>
        ///  Replaces every placeholder with the value supplied for it.
        /// </summary>
        /// <param name="body">The command body as saved.</param>
        /// <param name="values">Values by parameter name; a name with no value falls back to its default.</param>
        /// <param name="secrets">Resolves a secret name to its value, or null if the vault has none.</param>
        public static string Fill(
            string body,
            IReadOnlyDictionary<string, string> values,
            Func<string, string?>? secrets = null)
        {
            if (body.Length == 0)
            {
                return body;
            }

            string filled = Secret().Replace(body, match =>
                secrets?.Invoke(match.Groups["name"].Value) ?? string.Empty);

            return Placeholder().Replace(filled, match =>
            {
                string name = match.Groups["name"].Value;

                if (values.TryGetValue(name, out string? value) && value.Length > 0)
                {
                    return value;
                }

                return match.Groups["default"].Value.Trim();
            });
        }

        /// <summary>
        ///  Whether a value would turn one command into two.
        /// </summary>
        /// <remarks>
        ///  Not a sandbox, and it is not pretending to be one: the body itself is arbitrary script
        ///  the user wrote, and no amount of checking the arguments changes that. What this stops
        ///  is the narrower and much likelier accident - a value pasted from somewhere with a
        ///  newline or a semicolon in it, silently appending a second command to a saved one the
        ///  user believed they were only parameterising. Asking first is cheap; finding out
        ///  afterwards is not.
        /// </remarks>
        public static bool Unsafe(string value) =>
            value.AsSpan().ContainsAny("\n\r;&|`");

        /// <summary>The characters that made <see cref="Unsafe"/> say so, for the warning text.</summary>
        public static string Offending(string value) =>
            string.Join(" ", "\n\r;&|`"
                .Where(value.Contains)
                .Select(c => c switch
                {
                    '\n' => "a new line",
                    '\r' => "a carriage return",
                    ';' => "a semicolon",
                    '&' => "an ampersand",
                    '|' => "a pipe",
                    _ => "a backtick",
                })
                .Distinct());
    }
}
