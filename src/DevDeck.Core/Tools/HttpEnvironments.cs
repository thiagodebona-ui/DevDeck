using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>One named value in an environment.</summary>
    /// <remarks>
    ///  A variable is either held here or held in the vault, and which one is a property of the
    ///  variable rather than of the value: <see cref="IsSecret"/> decides whether
    ///  <see cref="Value"/> means anything at all. For a secret it is always empty and the real
    ///  value lives under <see cref="SecretName"/> in <see cref="SecretVault"/>, wrapped, so that
    ///  settings.json can be synced, backed up or pasted into a bug report without carrying a
    ///  bearer token with it.
    /// </remarks>
    internal sealed class EnvironmentValue
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>The value, for a variable that is not a secret. Empty for one that is.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Whether the real value is in the vault rather than in this file.</summary>
        public bool IsSecret { get; set; }

        public bool Enabled { get; set; } = true;

        /// <summary>
        ///  Where a secret variable's value is kept in the vault.
        /// </summary>
        /// <remarks>
        ///  Qualified by the environment it belongs to, so that a Staging token and a Production
        ///  token can both be called API_KEY without one overwriting the other. Built rather than
        ///  stored, so renaming an environment does not leave a dangling name behind.
        /// </remarks>
        public static string VaultName(string environment, string variable) =>
            $"http.{environment.Trim()}.{variable.Trim()}";
    }

    /// <summary>A named set of values - Local, Staging, Production.</summary>
    internal sealed class HttpEnvironment
    {
        public string Name { get; set; } = "New environment";

        public List<EnvironmentValue> Values { get; set; } = [];

        /// <summary>The variable of that name, or null.</summary>
        public EnvironmentValue? Find(string name) =>
            Values.FirstOrDefault(value =>
                string.Equals(value.Name.Trim(), name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///  Moves the credentials out of an imported request and into an environment.
    /// </summary>
    /// <remarks>
    ///  The reason the panel ships with three environments that each have an empty Authorization
    ///  in them. A curl copied out of devtools carries a live bearer token in a header, and what
    ///  happens to that token next is decided entirely here: left alone it is written into
    ///  settings.json the moment the request is saved, where it will be synced, backed up, and
    ///  eventually pasted into a bug report by someone who has forgotten it is there.
    ///
    ///  So the value is lifted into the selected environment - into the credential store, since the
    ///  seeded variable is marked secret - and the header keeps <c>{{Authorization}}</c> in its
    ///  place. The request still works, the same request now works against the other two
    ///  environments as soon as their tokens are filled in, and the file on disk has a reference in
    ///  it rather than a credential.
    ///
    ///  Only into a variable that already exists and is still empty. Creating variables from
    ///  whatever headers an import happened to carry would fill an environment with noise, and
    ///  overwriting one that has a value in it would silently replace a working token with one
    ///  pasted from somewhere else - the case where being wrong is expensive and invisible.
    /// </remarks>
    internal static class Adoption
    {
        /// <summary>What was moved, so the panel can say so.</summary>
        public readonly record struct Result(IReadOnlyList<string> Moved)
        {
            public bool Any => Moved.Count > 0;
        }

        /// <summary>
        ///  Replaces header values with references, and stores what they were.
        /// </summary>
        /// <remarks>
        ///  Mutates the request it is given, which is safe here and nowhere else: it is called on a
        ///  request that has just been parsed out of a curl command and has not been saved,
        ///  selected or shown yet.
        /// </remarks>
        public static Result Take(HttpRequest request, HttpEnvironment? environment)
        {
            if (environment is null)
            {
                return new Result([]);
            }

            List<string> moved = [];

            foreach (HeaderLine header in request.Headers)
            {
                string value = header.Value.Trim();

                // Already a reference, or empty. Nothing to lift, and re-lifting a reference would
                // store the literal text "{{Authorization}}" as the token.
                if (value.Length == 0 || HttpVariables.Any(value))
                {
                    continue;
                }

                if (environment.Find(header.Name) is not { } variable || !variable.Enabled)
                {
                    continue;
                }

                if (Held(environment, variable).Length > 0)
                {
                    // Occupied. The request keeps the value it was imported with, which is the
                    // conservative half of the trade: one import carries a token in settings.json,
                    // where overwriting would have cost the user the token they had set up.
                    continue;
                }

                Store(environment, variable, header.Value);

                header.Value = $"{{{{{variable.Name.Trim()}}}}}";

                moved.Add(variable.Name.Trim());
            }

            return new Result(moved);
        }

        /// <summary>What a variable currently holds, wherever it is held.</summary>
        private static string Held(HttpEnvironment environment, EnvironmentValue variable) =>
            variable.IsSecret
                ? SecretVault.Instance.Value(EnvironmentValue.VaultName(environment.Name, variable.Name)) ?? string.Empty
                : variable.Value;

        private static void Store(HttpEnvironment environment, EnvironmentValue variable, string value)
        {
            if (variable.IsSecret)
            {
                SecretVault.Instance.Set(
                    EnvironmentValue.VaultName(environment.Name, variable.Name), value);

                // Belt and braces: a secret variable's own Value must stay empty, or the token is
                // in settings.json after all and the vault was for nothing.
                variable.Value = string.Empty;

                return;
            }

            variable.Value = value;
        }
    }

    /// <summary>
    ///  Variables in a request, and the environment that supplies them.
    /// </summary>
    /// <remarks>
    ///  The thing that makes a saved request worth saving. Without it the same call against local,
    ///  staging and production is three near-identical entries that drift apart, and the usual way
    ///  that drift is discovered is a request that was meant for staging arriving at production.
    ///  One request with <c>{{baseUrl}}</c> in it and a picker above the panel is the fix.
    ///
    ///  Substitution happens at send time and nowhere else. What is stored, shown and exported as
    ///  curl is always the text with the braces still in it - so the panel never quietly becomes a
    ///  place a token is displayed, and copying a request to a colleague sends them the template
    ///  rather than this machine's credentials.
    ///
    ///  <b>Unknown names are left alone.</b> A <c>{{typo}}</c> stays as the literal text
    ///  <c>{{typo}}</c> rather than becoming an empty string, because a URL that silently loses a
    ///  path segment is far harder to diagnose than one that visibly still has braces in it. The
    ///  same reasoning covers a secret whose vault entry has gone missing.
    ///
    ///  Not recursive: a value containing <c>{{other}}</c> is used as written. One pass is what
    ///  everyone expects, and resolving further invites both a cycle and a way to smuggle one
    ///  variable's contents into another's position.
    /// </remarks>
    internal static partial class HttpVariables
    {
        /// <summary>
        ///  A variable reference: <c>{{name}}</c>, with optional spaces inside the braces.
        /// </summary>
        /// <remarks>
        ///  Deliberately not matching <c>{{secret:NAME}}</c>, which the command parameters already
        ///  own: the two syntaxes live in one settings file and a pattern loose enough to swallow
        ///  both would make each one's behaviour depend on which ran first.
        /// </remarks>
        [GeneratedRegex(@"\{\{\s*(?<name>[A-Za-z_][A-Za-z0-9_\-.]*)\s*\}\}")]
        private static partial Regex Reference();

        /// <summary>Whether this text has anything in it to resolve.</summary>
        public static bool Any(string text) =>
            !string.IsNullOrEmpty(text) && Reference().IsMatch(text);

        /// <summary>The names referenced in a piece of text, in the order they appear.</summary>
        public static IReadOnlyList<string> Used(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return [];
            }

            List<string> names = [];

            foreach (Match match in Reference().Matches(text))
            {
                string name = match.Groups["name"].Value;

                if (!names.Contains(name, StringComparer.Ordinal))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        /// <summary>
        ///  Replaces every known reference in <paramref name="text"/> from the environment.
        /// </summary>
        /// <remarks>
        ///  A null or nameless environment resolves nothing, which leaves the text exactly as
        ///  typed - the right answer for someone who has not set an environment up rather than an
        ///  error about a feature they are not using.
        /// </remarks>
        public static string Resolve(string text, HttpEnvironment? environment)
        {
            if (string.IsNullOrEmpty(text) || environment is null || !Any(text))
            {
                return text;
            }

            return Reference().Replace(text, match =>
            {
                string name = match.Groups["name"].Value;

                EnvironmentValue? found = environment.Values.FirstOrDefault(value =>
                    value.Enabled && string.Equals(value.Name.Trim(), name, StringComparison.Ordinal));

                if (found is null)
                {
                    // Left as written. An unresolved name that still reads as {{name}} is a
                    // question the user can answer; an empty string in its place is not.
                    return match.Value;
                }

                if (!found.IsSecret)
                {
                    return found.Value;
                }

                return SecretVault.Instance.Value(EnvironmentValue.VaultName(environment.Name, found.Name))
                    ?? match.Value;
            });
        }

        /// <summary>
        ///  A copy of the request with every reference resolved, ready to send.
        /// </summary>
        /// <remarks>
        ///  A copy, emphatically: resolving in place would write the resolved values back into the
        ///  saved request the moment anything persisted it, which is how a token ends up in
        ///  settings.json by accident. The original keeps its braces.
        /// </remarks>
        public static HttpRequest Resolved(HttpRequest request, HttpEnvironment? environment)
        {
            if (environment is null)
            {
                return request;
            }

            return new HttpRequest
            {
                Name = request.Name,
                Method = request.Method,
                Url = Resolve(request.Url, environment),
                Body = Resolve(request.Body, environment),
                Colour = request.Colour,
                Group = request.Group,
                Headers =
                [
                    .. request.Headers.Select(header => new HeaderLine
                    {
                        Name = Resolve(header.Name, environment),
                        Value = Resolve(header.Value, environment),
                        Enabled = header.Enabled,
                    }),
                ],
            };
        }

        /// <summary>
        ///  The names a request refers to that the environment cannot supply.
        /// </summary>
        /// <remarks>
        ///  What the panel warns with before a send. The common failure is not a missing feature
        ///  but a misspelling, or the right variable defined in the other environment, and both are
        ///  cheap to say out loud and expensive to find by reading a 404.
        /// </remarks>
        public static IReadOnlyList<string> Missing(HttpRequest request, HttpEnvironment? environment)
        {
            List<string> wanted = [];

            foreach (string name in Used(request.Url).Concat(Used(request.Body)))
            {
                if (!wanted.Contains(name, StringComparer.Ordinal))
                {
                    wanted.Add(name);
                }
            }

            foreach (HeaderLine header in request.Headers.Where(header => header.Enabled))
            {
                foreach (string name in Used(header.Name).Concat(Used(header.Value)))
                {
                    if (!wanted.Contains(name, StringComparer.Ordinal))
                    {
                        wanted.Add(name);
                    }
                }
            }

            if (wanted.Count == 0)
            {
                return [];
            }

            return
            [
                .. wanted.Where(name => environment is null || !environment.Values.Any(value =>
                    value.Enabled && string.Equals(value.Name.Trim(), name, StringComparison.Ordinal))),
            ];
        }
    }
}
