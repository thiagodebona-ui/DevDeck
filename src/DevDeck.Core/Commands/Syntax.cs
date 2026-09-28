using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>What a run of source text is, as far as colouring it goes.</summary>
    internal enum TokenKind
    {
        Plain,
        Keyword,
        Text,
        Comment,
        Number,
        Variable,
    }

    /// <summary>One coloured run: an offset, a length, and what it is.</summary>
    internal readonly record struct Token(int Start, int Length, TokenKind Kind);

    /// <summary>
    ///  A small syntax highlighter for the three script languages the deck can run.
    /// </summary>
    /// <remarks>
    ///  Deliberately a lexer and not a parser. The editor re-colours on every keystroke against
    ///  text that is usually half-written, so the only useful behaviour on something malformed is
    ///  to colour what it can and leave the rest plain - which a regex sweep does by construction
    ///  and a grammar does not.
    ///
    ///  Order matters and is the whole trick: comments and strings are matched first and everything
    ///  inside them is left alone, so a keyword in a comment or a "$" in a string stays the colour
    ///  of its surroundings rather than lighting up.
    /// </remarks>
    internal static class Syntax
    {
        private const RegexOptions Options =
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline;

        /// <summary>Comments and strings, matched before anything else can claim their contents.</summary>
        private static readonly Regex PowerShellSpans = new(
            @"(?<comment><\#[\s\S]*?\#>|\#.*?$)|(?<text>@""[\s\S]*?""@|@'[\s\S]*?'@|""(?:`.|[^""])*""|'(?:''|[^'])*')",
            Options);

        private static readonly Regex BatchSpans = new(
            @"(?<comment>^\s*(?:rem\b|::).*?$)|(?<text>""[^""\r\n]*"")",
            Options | RegexOptions.IgnoreCase);

        private static readonly Regex ShellSpans = new(
            @"(?<comment>\#.*?$)|(?<text>""(?:\\.|[^""])*""|'[^']*')",
            Options);

        private static readonly Regex PowerShellWords = new(
            @"(?<variable>\$[A-Za-z_][\w:]*)"
            + @"|\b(?<keyword>if|elseif|else|foreach|for|while|do|switch|function|filter|param|"
            + @"return|try|catch|finally|throw|break|continue|begin|process|end|in|exit)\b"
            + @"|(?<number>\b\d+(?:\.\d+)?\b)"
            + @"|(?<keyword2>-\w+)",
            Options | RegexOptions.IgnoreCase);

        private static readonly Regex BatchWords = new(
            @"(?<variable>%[^%\s]+%|%%?\w)"
            + @"|\b(?<keyword>echo|set|setlocal|endlocal|if|else|for|in|do|goto|call|exit|shift|"
            + @"pause|pushd|popd|start|not|defined|errorlevel|exist)\b"
            + @"|(?<number>\b\d+\b)"
            + @"|(?<keyword2>^\s*:\w+)",
            Options | RegexOptions.IgnoreCase);

        private static readonly Regex ShellWords = new(
            @"(?<variable>\$\{[^}]*\}|\$[A-Za-z_]\w*|\$[\d@*#?])"
            + @"|\b(?<keyword>if|then|elif|else|fi|for|while|until|do|done|case|esac|function|"
            + @"return|exit|export|local|readonly|shift|break|continue|source|echo|set|unset)\b"
            + @"|(?<number>\b\d+\b)",
            Options);

        /// <summary>
        ///  Splits <paramref name="source"/> into coloured runs, in order and without gaps.
        /// </summary>
        /// <remarks>
        ///  Every character of the input appears in exactly one token, plain ones included, so the
        ///  caller can build its inlines by walking the list without tracking where it has got to.
        /// </remarks>
        public static IReadOnlyList<Token> Tokenize(string source, CommandKind kind)
        {
            if (string.IsNullOrEmpty(source))
            {
                return [];
            }

            (Regex spans, Regex words) = kind switch
            {
                CommandKind.PowerShell => (PowerShellSpans, PowerShellWords),
                CommandKind.Batch => (BatchSpans, BatchWords),
                _ => (ShellSpans, ShellWords),
            };

            List<Token> found = [];

            // Comments and strings first. Whatever they cover is settled and is not offered to the
            // word pass at all.
            foreach (Match match in spans.Matches(source))
            {
                TokenKind claimed = match.Groups["comment"].Success ? TokenKind.Comment : TokenKind.Text;

                found.Add(new Token(match.Index, match.Length, claimed));
            }

            found.Sort((left, right) => left.Start.CompareTo(right.Start));

            List<Token> built = [];
            int at = 0;

            foreach (Token span in found)
            {
                // Spans can nest when a string opens inside a comment; the outer one already covers
                // it, so anything starting behind the cursor is dropped rather than double-counted.
                if (span.Start < at)
                {
                    continue;
                }

                Words(source, at, span.Start, words, built);

                built.Add(span);
                at = span.Start + span.Length;
            }

            Words(source, at, source.Length, words, built);

            return built;
        }

        /// <summary>
        ///  The text to draw for <paramref name="token"/>: its slice of the source, with every
        ///  CRLF - including one split between this token and the next - drawn as one break.
        /// </summary>
        /// <remarks>
        ///  A comment runs to the end of its line, so on a CRLF body it ends with the CR and the
        ///  next token starts with the LF. A TextBlock given those as two runs breaks the line
        ///  twice, while the TextBox over it reads CRLF as one break. Every comment then pushed the
        ///  painted text a line further down than the text being edited, and a click on the code
        ///  you could see landed past the end of the box - so only the comments at the top could
        ///  take the caret. Dropping the CR costs nothing: it has no width, at the end of a line.
        /// </remarks>
        public static string Drawn(string source, Token token)
        {
            string text = source.Substring(token.Start, token.Length);
            int end = token.Start + token.Length;

            if (text.EndsWith('\r') && end < source.Length && source[end] == '\n')
            {
                text = text[..^1];
            }

            return text.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        /// <summary>Colours keywords, variables and numbers between two offsets.</summary>
        private static void Words(string source, int from, int to, Regex words, List<Token> into)
        {
            if (to <= from)
            {
                return;
            }

            int at = from;

            foreach (Match match in words.Matches(source[from..to]))
            {
                int start = from + match.Index;

                if (start < at)
                {
                    continue;
                }

                if (start > at)
                {
                    into.Add(new Token(at, start - at, TokenKind.Plain));
                }

                into.Add(new Token(start, match.Length, KindOf(match)));
                at = start + match.Length;
            }

            if (at < to)
            {
                into.Add(new Token(at, to - at, TokenKind.Plain));
            }
        }

        private static TokenKind KindOf(Match match)
        {
            if (match.Groups["variable"].Success)
            {
                return TokenKind.Variable;
            }

            if (match.Groups["number"].Success)
            {
                return TokenKind.Number;
            }

            // keyword2 is the language's second shape of keyword - a PowerShell parameter, a batch
            // label - which reads as a keyword but cannot share the alternation group.
            return TokenKind.Keyword;
        }

        /// <summary>What to call this language in the editor's gutter.</summary>
        public static string Describe(CommandKind kind) => kind switch
        {
            CommandKind.PowerShell => "powershell",
            CommandKind.Batch => "batch",
            CommandKind.Bash => "bash",
            _ => OperatingSystem.IsWindows() ? "cmd" : "sh",
        };
    }
}
