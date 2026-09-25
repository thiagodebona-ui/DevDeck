using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>
    ///  A small highlighter for the three structured things a server sends back.
    /// </summary>
    /// <remarks>
    ///  The companion to <see cref="Syntax"/>, and deliberately a separate class rather than more
    ///  cases in that one: a command body is a script and is coloured by keyword, where a response
    ///  body is data and is coloured by position - the same word is a field name in one place and a
    ///  value in another, and a keyword list has nothing to say about which.
    ///
    ///  It reuses <see cref="Token"/> and <see cref="TokenKind"/> so that the painted pane is one
    ///  control taking one shape of input, whichever side of the request it is showing. The kinds
    ///  are borrowed rather than extended: a JSON field name is not a shell variable, but it plays
    ///  the same part on screen - the thing on the left that names the thing on the right - so it
    ///  takes that colour and the palette stays the size it was.
    ///
    ///  A lexer, for the same reason the script one is: a response is often truncated by a timeout
    ///  or is an error page that was never well-formed to begin with, and the only useful behaviour
    ///  on a half-written document is to colour as far as it can and leave the remainder plain.
    /// </remarks>
    internal static class Markup
    {
        private const RegexOptions Options =
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline;

        /// <summary>
        ///  JSON, in one sweep.
        /// </summary>
        /// <remarks>
        ///  A string followed by a colon is a field name and everything else quoted is a value,
        ///  which is the whole of the distinction and is why the lookahead is here rather than in a
        ///  second pass. The escape clause inside the quotes matters more than it looks: without
        ///  it, a value ending in a backslash swallows the rest of the document.
        /// </remarks>
        private static readonly Regex Json = new(
            @"(?<variable>""(?:\\.|[^""\\])*""(?=\s*:))"
            + @"|(?<text>""(?:\\.|[^""\\])*"")"
            + @"|(?<keyword>\b(?:true|false|null)\b)"
            + @"|(?<number>-?\b\d+(?:\.\d+)?(?:[eE][-+]?\d+)?\b)",
            Options);

        /// <summary>
        ///  XML and HTML, which are close enough here to share a pass.
        /// </summary>
        /// <remarks>
        ///  Comments, CDATA and doctypes first, so that markup quoted inside one of them is not
        ///  taken for markup. Then the tag as a whole, with its attributes picked out inside it -
        ///  text between tags is content and stays plain, which is what makes a page readable as a
        ///  page rather than as a wall of one colour.
        /// </remarks>
        private static readonly Regex Tags = new(
            @"(?<comment><!--[\s\S]*?-->|<!\[CDATA\[[\s\S]*?\]\]>)"
            + @"|(?<doctype><!(?:DOCTYPE|doctype)[^>]*>|<\?[\s\S]*?\?>)"
            + @"|(?<tag><[^>]*>)",
            Options);

        /// <summary>Inside one tag: the attribute names and their quoted values.</summary>
        private static readonly Regex Attributes = new(
            @"(?<text>""[^""]*""|'[^']*')"
            + @"|(?<variable>[A-Za-z_:][-\w:.]*)(?=\s*=)",
            Options);

        /// <summary>
        ///  JavaScript, for the responses that are a script rather than data.
        /// </summary>
        /// <remarks>
        ///  Ordered the way every lexer here is ordered: the things that quote other things come
        ///  first, so that a keyword inside a comment or an apostrophe inside a string is read as
        ///  what encloses it rather than as itself. Template literals are matched whole - the
        ///  interpolations inside one are expressions and would want their own pass, and colouring
        ///  the whole literal as a string is both cheaper and closer to how it reads.
        ///
        ///  Regex literals are not matched at all, and that is deliberate: telling one from a
        ///  division needs to know whether what came before it was a value, which a single regular
        ///  expression cannot. An uncoloured regex is a small loss; a division sign that starts a
        ///  string and swallows the next two lines is a large one.
        ///
        ///  The keyword list is the reserved words plus the handful of globals - console, JSON,
        ///  Promise - that a response body is actually likely to contain.
        /// </remarks>
        private static readonly Regex Script = new(
            @"(?<comment>//[^\n]*|/\*[\s\S]*?\*/)"
            + @"|(?<text>""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)"
            + @"|(?<keyword>\b(?:async|await|break|case|catch|class|const|continue|debugger|"
            + @"default|delete|do|else|export|extends|finally|for|from|function|get|if|import|in|"
            + @"instanceof|let|new|of|return|set|static|super|switch|this|throw|try|typeof|var|"
            + @"void|while|with|yield|true|false|null|undefined|NaN|Infinity|console|JSON|Promise|"
            + @"Math|Object|Array|String|Number|Boolean|Symbol|Error|window|document)\b)"
            + @"|(?<variable>\b[A-Za-z_$][\w$]*(?=\s*\())"
            + @"|(?<number>\b0[xX][0-9a-fA-F]+\b|-?\b\d+(?:\.\d+)?(?:[eE][-+]?\d+)?\b)",
            Options);

        /// <summary>Whether this kind of body is one this class can colour at all.</summary>
        public static bool Handles(BodyKind kind) =>
            kind is BodyKind.Json or BodyKind.Xml or BodyKind.Html or BodyKind.JavaScript;

        /// <summary>
        ///  Splits a response body into coloured runs, in order and without gaps.
        /// </summary>
        /// <remarks>
        ///  The same contract as <see cref="Syntax.Tokenize"/>: every character appears in exactly
        ///  one token, so the pane can build its inlines by walking the list. A kind with no
        ///  highlighter returns one plain token rather than an empty list, because an empty list
        ///  from a non-empty body would paint nothing at all.
        /// </remarks>
        public static IReadOnlyList<Token> Tokenize(string source, BodyKind kind)
        {
            if (string.IsNullOrEmpty(source))
            {
                return [];
            }

            if (!Handles(kind))
            {
                return [new Token(0, source.Length, TokenKind.Plain)];
            }

            List<Token> built = [];

            if (kind == BodyKind.Json)
            {
                Sweep(source, 0, source.Length, Json, built);
            }
            else if (kind == BodyKind.JavaScript)
            {
                Sweep(source, 0, source.Length, Script, built);
            }
            else
            {
                Markupwise(source, built);
            }

            return built;
        }

        /// <summary>
        ///  Walks a document tag by tag, colouring inside each one and leaving content alone.
        /// </summary>
        private static void Markupwise(string source, List<Token> into)
        {
            int at = 0;

            foreach (Match match in Tags.Matches(source))
            {
                if (match.Index < at)
                {
                    continue;
                }

                if (match.Index > at)
                {
                    // Between tags: the page's own words, which are the part being read.
                    into.Add(new Token(at, match.Index - at, TokenKind.Plain));
                }

                if (match.Groups["comment"].Success)
                {
                    into.Add(new Token(match.Index, match.Length, TokenKind.Comment));
                }
                else if (match.Groups["doctype"].Success)
                {
                    into.Add(new Token(match.Index, match.Length, TokenKind.Comment));
                }
                else
                {
                    Tagwise(source, match.Index, match.Length, into);
                }

                at = match.Index + match.Length;
            }

            if (at < source.Length)
            {
                into.Add(new Token(at, source.Length - at, TokenKind.Plain));
            }
        }

        /// <summary>
        ///  One tag: the angle brackets and element name as keyword, its attributes picked out.
        /// </summary>
        private static void Tagwise(string source, int start, int length, List<Token> into)
        {
            int at = start;
            int end = start + length;

            foreach (Match match in Attributes.Matches(source.Substring(start, length)))
            {
                int where = start + match.Index;

                if (where < at)
                {
                    continue;
                }

                if (where > at)
                {
                    // The element name and the punctuation around it.
                    into.Add(new Token(at, where - at, TokenKind.Keyword));
                }

                into.Add(new Token(
                    where,
                    match.Length,
                    match.Groups["text"].Success ? TokenKind.Text : TokenKind.Variable));

                at = where + match.Length;
            }

            if (at < end)
            {
                into.Add(new Token(at, end - at, TokenKind.Keyword));
            }
        }

        /// <summary>Colours one regex's matches between two offsets, plain between them.</summary>
        private static void Sweep(string source, int from, int to, Regex pattern, List<Token> into)
        {
            int at = from;

            foreach (Match match in pattern.Matches(source[from..to]))
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

            if (match.Groups["text"].Success)
            {
                return TokenKind.Text;
            }

            if (match.Groups["number"].Success)
            {
                return TokenKind.Number;
            }

            return TokenKind.Keyword;
        }
    }
}
