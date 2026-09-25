using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The parser's contract with the output panel.
    /// </summary>
    /// <remarks>
    ///  The first test is the one that matters, and it is the same invariant the syntax highlighter
    ///  has: the spans must reassemble into exactly the visible text. If they do not, the panel
    ///  shows output that differs from what the process actually printed - characters eaten by a
    ///  half-parsed escape, or escape bytes leaking through as glyphs - and either one makes the
    ///  log untrustworthy in a way that looks like the tool's fault rather than the parser's.
    /// </remarks>
    public class AnsiTests
    {
        private const string Escape = "\u001B";

        [Theory]
        [InlineData("plain text with no escapes at all")]
        [InlineData(Escape + "[32mgreen" + Escape + "[0m")]
        [InlineData(Escape + "[1;31mbold red" + Escape + "[0m normal")]
        [InlineData("before " + Escape + "[38;5;196mred" + Escape + "[39m after")]
        [InlineData("truecolour " + Escape + "[38;2;255;0;0mred" + Escape + "[0m")]
        [InlineData(Escape + "[2K" + Escape + "[1Gcarriage stuff")]
        [InlineData(Escape + "]0;a window title" + Escape + "\\text after")]
        [InlineData(Escape + "[m reset written short")]
        public void SpansReassembleIntoTheStrippedText(string line)
        {
            IReadOnlyList<AnsiSpan> spans = Ansi.Parse(line);

            Assert.Equal(Ansi.Strip(line), string.Concat(spans.Select(span => span.Text)));
        }

        [Fact]
        public void ReadsTheBasicForegroundColours()
        {
            IReadOnlyList<AnsiSpan> spans = Ansi.Parse($"{Escape}[31mred{Escape}[32mgreen{Escape}[0mplain");

            Assert.Collection(
                spans,
                span => Assert.Equal((AnsiColor.Red, "red"), (span.Foreground, span.Text)),
                span => Assert.Equal((AnsiColor.Green, "green"), (span.Foreground, span.Text)),
                span => Assert.Equal((AnsiColor.None, "plain"), (span.Foreground, span.Text)));
        }

        [Fact]
        public void BrightColoursAreDistinctFromTheirBaseColour()
        {
            AnsiSpan bright = Ansi.Parse($"{Escape}[91mx")[0];
            AnsiSpan plain = Ansi.Parse($"{Escape}[31mx")[0];

            Assert.Equal(AnsiColor.BrightRed, bright.Foreground);
            Assert.Equal(AnsiColor.Red, plain.Foreground);
        }

        [Fact]
        public void BoldAndFaintSurviveAndAreClearedTogether()
        {
            IReadOnlyList<AnsiSpan> spans = Ansi.Parse($"{Escape}[1mbold{Escape}[22mnormal");

            Assert.True(spans[0].Bold);
            Assert.False(spans[1].Bold);
        }

        /// <summary>
        ///  A tool that opens a colour on one line and closes it on the next is common - anything
        ///  that prints a coloured heading followed by an indented body does it - and getting this
        ///  wrong shows up as the colour stopping at the first newline.
        /// </summary>
        [Fact]
        public void ColourCarriesFromOneLineToTheNext()
        {
            Ansi.AnsiState state = Ansi.AnsiState.Clear;

            Ansi.Parse($"{Escape}[33mopened here", ref state);
            IReadOnlyList<AnsiSpan> second = Ansi.Parse("still yellow", ref state);

            Assert.Equal(AnsiColor.Yellow, second[0].Foreground);

            Ansi.Parse($"{Escape}[0m", ref state);
            Assert.Equal(AnsiColor.None, Ansi.Parse("plain again", ref state)[0].Foreground);
        }

        /// <summary>
        ///  Cursor and erase sequences are what progress bars are built from, and they arrive in
        ///  npm, pip and cargo output constantly. They must vanish without taking text with them.
        /// </summary>
        [Fact]
        public void NonColourSequencesAreRemovedWithoutAffectingAttributes()
        {
            IReadOnlyList<AnsiSpan> spans = Ansi.Parse($"{Escape}[31mred{Escape}[2K{Escape}[1Gstill red");

            Assert.All(spans, span => Assert.Equal(AnsiColor.Red, span.Foreground));
            Assert.Equal("redstill red", string.Concat(spans.Select(span => span.Text)));
        }

        [Fact]
        public void BackgroundColoursAreConsumedButDoNotChangeTheForeground()
        {
            AnsiSpan span = Ansi.Parse($"{Escape}[32m{Escape}[48;5;236mtext")[^1];

            Assert.Equal(AnsiColor.Green, span.Foreground);
            Assert.Equal("text", span.Text);
        }

        [Fact]
        public void TextWithNoEscapesIsNotCopied()
        {
            const string line = "nothing to do here";

            Assert.False(Ansi.Has(line));
            Assert.Same(line, Ansi.Strip(line));
        }

        /// <summary>A line that is only an escape still yields one span, so callers need no null check.</summary>
        [Fact]
        public void AlwaysReturnsAtLeastOneSpan()
        {
            Assert.NotEmpty(Ansi.Parse($"{Escape}[0m"));
            Assert.NotEmpty(Ansi.Parse(string.Empty));
        }
    }
}
