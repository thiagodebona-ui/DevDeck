using System.Text;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>One of the sixteen terminal colours, or none.</summary>
    /// <remarks>
    ///  Deliberately the classic sixteen rather than an RGB value. Tools pick these by index and
    ///  the terminal supplies the actual colour, which is what lets the same output read correctly
    ///  against a light theme and a dark one - so the index travels and the theme resolves it. A
    ///  256-colour or true-colour escape is mapped to the nearest of these rather than dropped,
    ///  because the distinction almost never carries meaning in build output.
    /// </remarks>
    internal enum AnsiColor
    {
        None = 0,
        Black,
        Red,
        Green,
        Yellow,
        Blue,
        Magenta,
        Cyan,
        White,
        BrightBlack,
        BrightRed,
        BrightGreen,
        BrightYellow,
        BrightBlue,
        BrightMagenta,
        BrightCyan,
        BrightWhite,
    }

    /// <summary>A run of text that shares one set of terminal attributes.</summary>
    internal sealed record AnsiSpan(string Text, AnsiColor Foreground, bool Bold, bool Faint);

    /// <summary>
    ///  Turns a line containing ANSI escape sequences into coloured spans.
    /// </summary>
    /// <remarks>
    ///  Every modern build tool colours its output, and until now those escapes arrived in the
    ///  panel as literal noise, which is worse than no colour at all: it buries the very word the
    ///  colour was meant to pick out.
    ///
    ///  A parser rather than a stripper. The colours in cargo, npm, dotnet, pytest and git output
    ///  are the fastest way to find the one failing line in two thousand, and throwing them away
    ///  to get clean text loses exactly the signal the user is scanning for.
    ///
    ///  State is carried across calls because a tool may open a colour on one line and close it on
    ///  the next. Anything that is not SGR - cursor movement, erase, the bracketed-paste and title
    ///  sequences - is removed without interpretation: this renders into a list, not a grid, so
    ///  there is nowhere for a cursor to move to.
    /// </remarks>
    internal static partial class Ansi
    {
        /// <summary>The attributes in force, which outlive the line that set them.</summary>
        internal record struct AnsiState(AnsiColor Foreground, bool Bold, bool Faint)
        {
            public static AnsiState Clear => new(AnsiColor.None, false, false);
        }

        /// <summary>
        ///  Any escape sequence: CSI with its parameters, OSC up to its terminator, and the short
        ///  two-character forms.
        /// </summary>
        [GeneratedRegex("\u001B\\[[0-9;?]*[ -/]*[@-~]|\u001B\\][^\u0007\u001B]*(?:\u0007|\u001B\\\\)|\u001B[@-Z\\\\-_]")]
        private static partial Regex Sequences();

        /// <summary>Whether this text holds anything worth parsing, which is usually no.</summary>
        public static bool Has(string text) => text.Contains('\u001B');

        /// <summary>Removes every escape sequence, for the places that want plain text.</summary>
        public static string Strip(string text) =>
            Has(text) ? Sequences().Replace(text, string.Empty) : text;

        /// <summary>Splits one line into spans, starting from a clean slate.</summary>
        public static IReadOnlyList<AnsiSpan> Parse(string line)
        {
            AnsiState state = AnsiState.Clear;

            return Parse(line, ref state);
        }

        /// <summary>Splits one line into spans, carrying the attributes in and back out again.</summary>
        public static IReadOnlyList<AnsiSpan> Parse(string line, ref AnsiState state)
        {
            if (!Has(line))
            {
                return [new AnsiSpan(line, state.Foreground, state.Bold, state.Faint)];
            }

            List<AnsiSpan> spans = [];
            StringBuilder run = new();

            int at = 0;

            foreach (Match match in Sequences().Matches(line))
            {
                run.Append(line, at, match.Index - at);
                at = match.Index + match.Length;

                // Only SGR changes how text looks. Everything else is addressed at a screen this
                // panel does not have, and is dropped by not being handled.
                if (match.Value.Length > 2 && match.Value[1] == '[' && match.Value[^1] == 'm')
                {
                    if (run.Length > 0)
                    {
                        spans.Add(new AnsiSpan(run.ToString(), state.Foreground, state.Bold, state.Faint));
                        run.Clear();
                    }

                    Apply(match.Value[2..^1], ref state);
                }
            }

            run.Append(line, at, line.Length - at);

            if (run.Length > 0 || spans.Count == 0)
            {
                spans.Add(new AnsiSpan(run.ToString(), state.Foreground, state.Bold, state.Faint));
            }

            return spans;
        }

        /// <summary>Applies the parameters of one SGR sequence to the running state.</summary>
        private static void Apply(string parameters, ref AnsiState state)
        {
            // The empty parameter list is the reset written short.
            if (parameters.Length == 0)
            {
                state = AnsiState.Clear;
                return;
            }

            string[] codes = parameters.Split(';');

            for (int i = 0; i < codes.Length; i++)
            {
                if (!int.TryParse(codes[i], out int code))
                {
                    continue;
                }

                switch (code)
                {
                    case 0:
                        state = AnsiState.Clear;
                        break;

                    case 1:
                        state.Bold = true;
                        break;

                    case 2:
                        state.Faint = true;
                        break;

                    case 22:
                        state.Bold = false;
                        state.Faint = false;
                        break;

                    case >= 30 and <= 37:
                        state.Foreground = (AnsiColor)(code - 30 + 1);
                        break;

                    case >= 90 and <= 97:
                        state.Foreground = (AnsiColor)(code - 90 + 9);
                        break;

                    case 39:
                        state.Foreground = AnsiColor.None;
                        break;

                    // Extended colour: a 256-colour index or a true-colour triple. Both are folded
                    // to the nearest of the sixteen, so the theme keeps control of what the colour
                    // actually looks like.
                    case 38:
                        i = Extended(codes, i, ref state);
                        break;

                    // Background colours are parsed only to be consumed: painting a background
                    // behind a line of build output fights the panel's own surface for no gain.
                    case 48:
                    {
                        AnsiState ignored = AnsiState.Clear;
                        i = Extended(codes, i, ref ignored);
                        break;
                    }
                }
            }
        }

        /// <summary>Reads a 38/48 extended-colour run and returns the index of its last parameter.</summary>
        private static int Extended(string[] codes, int i, ref AnsiState state)
        {
            if (i + 1 >= codes.Length || !int.TryParse(codes[i + 1], out int mode))
            {
                return i;
            }

            if (mode == 5 && i + 2 < codes.Length && int.TryParse(codes[i + 2], out int index))
            {
                state.Foreground = FromCube(index);
                return i + 2;
            }

            if (mode == 2 && i + 4 < codes.Length
                && int.TryParse(codes[i + 2], out int r)
                && int.TryParse(codes[i + 3], out int g)
                && int.TryParse(codes[i + 4], out int b))
            {
                state.Foreground = Nearest(r, g, b);
                return i + 4;
            }

            return i + 1;
        }

        /// <summary>Maps a 256-colour index onto the sixteen.</summary>
        private static AnsiColor FromCube(int index)
        {
            // The first sixteen are the sixteen.
            if (index is >= 0 and < 16)
            {
                return (AnsiColor)(index + 1);
            }

            // A 6x6x6 cube, then a greyscale ramp.
            if (index is >= 16 and <= 231)
            {
                int offset = index - 16;

                return Nearest(offset / 36 * 51, offset / 6 % 6 * 51, offset % 6 * 51);
            }

            if (index is >= 232 and <= 255)
            {
                return (index - 232) * 11 < 96 ? AnsiColor.BrightBlack : AnsiColor.White;
            }

            return AnsiColor.None;
        }

        /// <summary>
        ///  The closest of the sixteen to an RGB triple.
        /// </summary>
        /// <remarks>
        ///  Nearest by squared distance against the xterm palette. Approximate by design: the point
        ///  is that a tool's "red means broken" survives, not that the shade is reproduced.
        /// </remarks>
        private static AnsiColor Nearest(int r, int g, int b)
        {
            (int R, int G, int B, AnsiColor Colour)[] palette =
            [
                (0, 0, 0, AnsiColor.Black),
                (205, 0, 0, AnsiColor.Red),
                (0, 205, 0, AnsiColor.Green),
                (205, 205, 0, AnsiColor.Yellow),
                (0, 0, 238, AnsiColor.Blue),
                (205, 0, 205, AnsiColor.Magenta),
                (0, 205, 205, AnsiColor.Cyan),
                (229, 229, 229, AnsiColor.White),
                (127, 127, 127, AnsiColor.BrightBlack),
                (255, 0, 0, AnsiColor.BrightRed),
                (0, 255, 0, AnsiColor.BrightGreen),
                (255, 255, 0, AnsiColor.BrightYellow),
                (92, 92, 255, AnsiColor.BrightBlue),
                (255, 0, 255, AnsiColor.BrightMagenta),
                (0, 255, 255, AnsiColor.BrightCyan),
                (255, 255, 255, AnsiColor.BrightWhite),
            ];

            AnsiColor best = AnsiColor.None;
            int closest = int.MaxValue;

            foreach ((int pr, int pg, int pb, AnsiColor colour) in palette)
            {
                int distance = ((pr - r) * (pr - r)) + ((pg - g) * (pg - g)) + ((pb - b) * (pb - b));

                if (distance < closest)
                {
                    closest = distance;
                    best = colour;
                }
            }

            return best;
        }
    }
}
