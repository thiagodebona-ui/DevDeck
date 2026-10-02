namespace DevDeck.Core
{
    /// <summary>One run of a body line, in one colour.</summary>
    internal readonly record struct BodyRun(string Text, TokenKind Kind);

    /// <summary>One line of a body as it is drawn.</summary>
    internal sealed record BodyLine(IReadOnlyList<BodyRun> Runs);

    /// <summary>
    ///  A body cut into coloured lines, so the pane can draw only the ones in view.
    /// </summary>
    /// <remarks>
    ///  The pane used to be one text block holding the whole body as coloured runs. Text layout
    ///  is not virtualised: every change of the body, every show of the pane, measured all of it,
    ///  and a few megabytes of pretty-printed JSON is hundreds of thousands of runs - seconds of
    ///  the UI thread each time, which is what froze the panel on a large response and again on
    ///  anything that redrew it, adding a request included.
    ///
    ///  As lines, a list draws the forty on screen and nothing else. The colouring is still worked
    ///  out over the whole body - a string's colour depends on where it started - and then cut at
    ///  the line breaks. This is plain data and runs off the UI thread.
    ///
    ///  A line longer than <paramref name="widest"/> is broken into pieces, each its own row: a
    ///  minified megabyte of JSON is one line, and one row holding it would be the same freeze
    ///  in a different shape.
    /// </remarks>
    internal static class BodyLines
    {
        public static List<BodyLine> Split(string source, BodyKind kind, int colourCeiling = 4_000_000, int widest = 2000)
        {
            List<BodyLine> lines = [];

            if (source.Length == 0)
            {
                return lines;
            }

            IEnumerable<Token> tokens = source.Length <= colourCeiling && Markup.Handles(kind)
                ? Markup.Tokenize(source, kind)
                : [new Token(0, source.Length, TokenKind.Plain)];

            List<BodyRun> current = [];
            int width = 0;

            void Break()
            {
                lines.Add(new BodyLine(current));
                current = [];
                width = 0;
            }

            foreach (Token token in tokens)
            {
                int at = token.Start;
                int end = token.Start + token.Length;

                while (at < end)
                {
                    int newline = source.IndexOf('\n', at, end - at);
                    int stop = newline < 0 ? end : newline;

                    // What fits on this row before it is too wide.
                    int take = Math.Min(stop - at, widest - width);

                    if (take > 0)
                    {
                        current.Add(new BodyRun(source.Substring(at, take).TrimEnd('\r'), token.Kind));
                        width += take;
                        at += take;
                    }

                    if (at == newline)
                    {
                        Break();
                        at++;
                    }
                    else if (width >= widest)
                    {
                        Break();
                    }
                }
            }

            if (current.Count > 0)
            {
                Break();
            }

            return lines;
        }
    }
}
