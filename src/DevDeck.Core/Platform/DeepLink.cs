namespace DevDeck.Core
{
    /// <summary>What a link or a command line is asking the app to do.</summary>
    internal enum Intent
    {
        /// <summary>Nothing recognisable; the caller should say so rather than guess.</summary>
        None,

        /// <summary>Bring the window up, optionally on a named section.</summary>
        Show,

        /// <summary>Run one command by name.</summary>
        Run,

        /// <summary>Run one chain by name.</summary>
        Chain,

        /// <summary>Point the deck at a folder.</summary>
        Workspace,
    }

    /// <summary>One parsed request.</summary>
    /// <remarks>
    ///  <see cref="Target"/> is the command, chain, section or folder the intent applies to, and
    ///  <see cref="Values"/> carries <c>{{placeholder}}</c> values so a link can run a parameterised
    ///  command without stopping to ask - which is the whole point of linking to one.
    /// </remarks>
    internal sealed record LinkRequest(Intent Intent, string Target, IReadOnlyDictionary<string, string> Values)
    {
        public static LinkRequest Nothing { get; } = new(Intent.None, string.Empty, new Dictionary<string, string>());

        public bool IsSomething => Intent != Intent.None;

        /// <summary>A sentence for the log, so an arriving link is visible rather than mysterious.</summary>
        public string Describe() => Intent switch
        {
            Intent.Run => $"run \"{Target}\"",
            Intent.Chain => $"run the chain \"{Target}\"",
            Intent.Workspace => $"open {Target}",
            Intent.Show => Target.Length > 0 ? $"show {Target}" : "show the window",
            _ => "nothing",
        };
    }

    /// <summary>
    ///  Turns a <c>devdeck://</c> link, or the command line, into something to do.
    /// </summary>
    /// <remarks>
    ///  The deck is a good place to keep a command and a bad place to start one from when your
    ///  hands are already in a terminal, an editor task, a README or a browser. A URL scheme fixes
    ///  both ends at once: <c>devdeck://run/Build</c> is a thing a README can contain, a CI page can
    ///  link to and a shell can open, and the same strings work as arguments so
    ///  <c>devdeck run Build</c> does exactly what the link does.
    ///
    ///  Names are matched loosely later on, but they are parsed strictly here: percent-decoded once
    ///  and never twice, because a command name that arrives already containing a percent sign
    ///  should stay that way.
    ///
    ///  A link cannot supply a command body - only name one that the user has already saved. That
    ///  is the security boundary, and it is deliberate: a scheme handler is reachable from any web
    ///  page, and one that accepted a script to run would be a remote execution hole with a
    ///  friendly icon. The worst a hostile link can do is start something the user themselves wrote
    ///  and would recognise in the window that comes up.
    /// </remarks>
    internal static class DeepLink
    {
        public const string Scheme = "devdeck";

        public const string Prefix = Scheme + "://";

        /// <summary>Parses a link, or returns <see cref="LinkRequest.Nothing"/>.</summary>
        public static LinkRequest Parse(string? link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return LinkRequest.Nothing;
            }

            string text = link.Trim();

            if (!text.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return LinkRequest.Nothing;
            }

            string rest = text[Prefix.Length..].TrimEnd('/');

            // The query is split off first: a value may legitimately contain a slash, and a target
            // may legitimately contain an equals sign.
            string query = string.Empty;
            int mark = rest.IndexOf('?');

            if (mark >= 0)
            {
                query = rest[(mark + 1)..];
                rest = rest[..mark];
            }

            string[] parts = rest.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
            string verb = parts.Length > 0 ? Decode(parts[0]) : string.Empty;
            string target = parts.Length > 1 ? Decode(parts[1]) : string.Empty;

            switch (verb.ToLowerInvariant())
            {
                case "run":
                case "chain":
                case "workspace":
                case "open":
                    // A verb that needs something to act on and was given nothing is not a request
                    // to do that thing to whatever happens to be selected.
                    if (target.Length == 0)
                    {
                        return LinkRequest.Nothing;
                    }

                    break;

                case "show":
                case "":
                    break;

                default:
                    // "devdeck://Commands" - a section named on its own, which is friendlier than
                    // requiring "show/" and is the only reading that leaves a word here meaning
                    // anything. A word with something after it is a verb this build does not have.
                    if (target.Length > 0)
                    {
                        return LinkRequest.Nothing;
                    }

                    target = verb;
                    break;
            }

            Intent intent = verb.ToLowerInvariant() switch
            {
                "run" => Intent.Run,
                "chain" => Intent.Chain,
                "workspace" or "open" => Intent.Workspace,
                _ => Intent.Show,
            };

            return new LinkRequest(intent, target, Query(query));
        }

        /// <summary>
        ///  Parses the command line the same way, so the CLI and the scheme cannot drift apart.
        /// </summary>
        /// <remarks>
        ///  Accepts either shape. A link handed over by the desktop arrives as a single argument, so
        ///  that is checked first; otherwise the arguments are read as
        ///  <c>run &lt;name&gt; [name=value ...]</c>.
        /// </remarks>
        public static LinkRequest FromArguments(IReadOnlyList<string> arguments)
        {
            if (arguments.Count == 0)
            {
                return LinkRequest.Nothing;
            }

            if (Parse(arguments[0]) is { IsSomething: true } link)
            {
                return link;
            }

            Intent intent = arguments[0].TrimStart('-').ToLowerInvariant() switch
            {
                "run" => Intent.Run,
                "chain" => Intent.Chain,
                "workspace" or "open" => Intent.Workspace,
                "show" => Intent.Show,
                _ => Intent.None,
            };

            if (intent == Intent.None)
            {
                return LinkRequest.Nothing;
            }

            string target = arguments.Count > 1 ? arguments[1] : string.Empty;

            if (intent is Intent.Run or Intent.Chain or Intent.Workspace && target.Length == 0)
            {
                return LinkRequest.Nothing;
            }

            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

            foreach (string argument in arguments.Skip(2))
            {
                int equals = argument.IndexOf('=');

                if (equals > 0)
                {
                    values[argument[..equals].Trim()] = argument[(equals + 1)..];
                }
            }

            return new LinkRequest(intent, target, values);
        }

        /// <summary>Builds the link that runs a command, for the "copy link" button.</summary>
        public static string For(Intent intent, string target)
        {
            string verb = intent switch
            {
                Intent.Run => "run",
                Intent.Chain => "chain",
                Intent.Workspace => "open",
                _ => "show",
            };

            return $"{Prefix}{verb}/{Uri.EscapeDataString(target)}";
        }

        private static Dictionary<string, string> Query(string query)
        {
            Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

            foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                int equals = pair.IndexOf('=');

                if (equals <= 0)
                {
                    continue;
                }

                values[Decode(pair[..equals])] = Decode(pair[(equals + 1)..]);
            }

            return values;
        }

        /// <summary>
        ///  Percent-decoding that treats a malformed escape as text.
        /// </summary>
        /// <remarks>
        ///  <see cref="Uri.UnescapeDataString"/> throws on some malformed input, and a link typed by
        ///  hand into a README is exactly where malformed input comes from. A stray percent sign
        ///  should leave the name alone, not take the whole request down.
        /// </remarks>
        private static string Decode(string text)
        {
            try
            {
                return Uri.UnescapeDataString(text.Replace('+', ' '));
            }
            catch (Exception)
            {
                return text;
            }
        }
    }
}
