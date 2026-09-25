using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  What a chain step is told about the step before it.
    /// </summary>
    /// <remarks>
    ///  These are the app's side of a contract whose other side is a shell script somebody wrote,
    ///  which means a rename here breaks something no compiler will ever look at. Pinning the
    ///  variable names down in a test is the only thing that makes them hard to change by accident.
    /// </remarks>
    public class ChainContextTests
    {
        [Fact]
        public void TheFirstStepIsToldTheChainButNotAPrevious()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain("Ship it", 1, 3);

            Assert.Equal("chain", given[RunContext.Trigger]);
            Assert.Equal("Ship it", given[RunContext.ChainName]);
            Assert.Equal("1", given[RunContext.StepNumber]);
            Assert.Equal("3", given[RunContext.StepCount]);

            // Absent rather than empty, so a script can ask whether it is being fed at all.
            Assert.False(given.ContainsKey(RunContext.PreviousName));
            Assert.False(given.ContainsKey(RunContext.Previous));
        }

        /// <summary>The watch name stays populated, for the scripts that already read it.</summary>
        [Fact]
        public void AChainStillAnswersToTheWatchName()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain("Ship it");

            Assert.Equal("Ship it", given[RunContext.WatchName]);
        }

        [Fact]
        public void ALaterStepIsHandedTheWholeOutputAndItsLastLine()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain(
                "Ship it", 2, 3, "Build", "compiling\nlinking\n/tmp/app.exe\n", 0);

            Assert.Equal("Build", given[RunContext.PreviousName]);
            Assert.Equal("compiling\nlinking\n/tmp/app.exe\n", given[RunContext.Previous]);
            Assert.Equal("/tmp/app.exe", given[RunContext.PreviousLine]);
            Assert.Equal("0", given[RunContext.PreviousExit]);
        }

        /// <summary>A step that failed still hands on what it said, and what it exited with.</summary>
        /// <remarks>
        ///  The case that matters for a chain which carries on regardless: the step after a failure
        ///  is usually the one reporting it, and it cannot report what it was not given.
        /// </remarks>
        [Fact]
        public void AFailedStepIsStillPassedOn()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain(
                "Ship it", 2, 3, "Build", "error CS1002: ; expected", 1);

            Assert.Equal("1", given[RunContext.PreviousExit]);
            Assert.Equal("error CS1002: ; expected", given[RunContext.PreviousLine]);
        }

        [Fact]
        public void AStepThatPrintedNothingHandsOnEmptyRatherThanNothing()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain(
                "Ship it", 2, 3, "Build", string.Empty, 0);

            Assert.Equal(string.Empty, given[RunContext.Previous]);
            Assert.Equal(string.Empty, given[RunContext.PreviousLine]);
        }

        /// <summary>Steps are only counted when there is a count to give.</summary>
        [Fact]
        public void AChainRunWithoutStepNumbersOmitsThem()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain("Ship it");

            Assert.False(given.ContainsKey(RunContext.StepNumber));
            Assert.False(given.ContainsKey(RunContext.StepCount));
        }

        [Theory]
        [InlineData("one\ntwo\n", "two")]
        [InlineData("one\ntwo", "two")]
        [InlineData("one\r\ntwo\r\n", "two")]
        [InlineData("only", "only")]
        [InlineData("   padded   \n", "padded")]
        [InlineData("", "")]
        [InlineData("\n\n\n", "")]
        public void TheLastLineIsTheLastOneWithAnythingOnIt(string output, string expected)
        {
            Assert.Equal(expected, RunContext.LastLine(output));
        }

        /// <summary>
        ///  The trailing blank lines nearly every script ends with are skipped.
        /// </summary>
        /// <remarks>
        ///  The single most likely way for this to be wrong in practice: `echo x` writes a newline,
        ///  so naively taking "the text after the last newline" hands the next step an empty string
        ///  for every well-behaved script there is.
        /// </remarks>
        [Fact]
        public void TrailingBlankLinesAreSkippedRatherThanReturned()
        {
            Assert.Equal("the value", RunContext.LastLine("noise\nthe value\n\n   \n\n"));
        }

        [Fact]
        public void OutputThatFitsIsPassedOnUntouched()
        {
            string output = new('x', RunContext.MostPassedOn);

            Assert.Equal(output, RunContext.Clip(output));
        }

        /// <summary>
        ///  Too much output keeps its end, and says that it dropped the rest.
        /// </summary>
        /// <remarks>
        ///  Both halves matter. The tail, because a script's answer is what it printed last - the
        ///  front of a long log is startup chatter. The note, because output that silently loses
        ///  its first half looks like a script that silently stopped working.
        /// </remarks>
        [Fact]
        public void TooMuchOutputKeepsItsTailAndSaysSo()
        {
            string output = new string('x', RunContext.MostPassedOn * 2) + "the answer";
            string clipped = RunContext.Clip(output);

            Assert.Equal(RunContext.MostPassedOn, clipped.Length);
            Assert.EndsWith("the answer", clipped, StringComparison.Ordinal);
            Assert.Contains("too long to pass on", clipped, StringComparison.Ordinal);
        }

        /// <summary>
        ///  A clipped output must still be usable, which means its last line must survive.
        /// </summary>
        /// <remarks>
        ///  The two helpers are used together on the same string, and clipping from the front is
        ///  what makes that safe - the value a step means to hand on is at the end, which is the
        ///  end that is kept.
        /// </remarks>
        [Fact]
        public void TheValueSurvivesBeingClipped()
        {
            string output = new string('x', RunContext.MostPassedOn * 2) + "\nthe answer\n";

            Assert.Equal("the answer", RunContext.LastLine(RunContext.Clip(output)));
        }

        /// <summary>
        ///  Everything passed on has to fit in an environment block.
        /// </summary>
        /// <remarks>
        ///  Windows caps the whole block at 32,767 characters and the user's own variables are in
        ///  there too, so the cap is not a nicety - past it the process does not start at all.
        /// </remarks>
        /// <summary>
        ///  A value too long to be a value is cut down, keeping its front.
        /// </summary>
        /// <remarks>
        ///  The front, because the useful end of a value is its beginning - a truncated path still
        ///  says which folder it was in, where a truncated tail says only that something ends in
        ///  ".cs". The opposite of how the whole output is treated, and deliberately so: one is a
        ///  log, whose answer is at the end, and this is a value.
        /// </remarks>
        [Fact]
        public void AValueTooLongToBeAValueIsCutDownKeepingItsFront()
        {
            // Short enough that the output cap does not apply, so this exercises the line cap
            // alone rather than the two of them at once.
            string one = "/start/" + new string('x', RunContext.MostOfALine);

            IReadOnlyDictionary<string, string> given = RunContext.Chain(
                "Ship it", 2, 3, "Build", one, 0);

            string line = given[RunContext.PreviousLine];

            Assert.Equal(RunContext.MostOfALine, line.Length);
            Assert.StartsWith("/start/", line, StringComparison.Ordinal);
            Assert.EndsWith("[line truncated]", line, StringComparison.Ordinal);
        }

        /// <summary>
        ///  Half a megabyte printed without a single newline still fits.
        /// </summary>
        /// <remarks>
        ///  The case the output cap cannot cover on its own: clipping keeps the tail of the
        ///  output, and when the whole output is one line, that tail is still one line of sixteen
        ///  thousand characters. Both caps have to apply for the environment block to hold.
        /// </remarks>
        [Fact]
        public void OneEnormousLineIsCappedTwice()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain(
                "Ship it", 2, 3, "Build", new string('x', 500_000), 0);

            Assert.Equal(RunContext.MostPassedOn, given[RunContext.Previous].Length);
            Assert.Equal(RunContext.MostOfALine, given[RunContext.PreviousLine].Length);
        }

        [Fact]
        public void TheWholeContextStaysInsideWhatAProcessWillTake()
        {
            IReadOnlyDictionary<string, string> given = RunContext.Chain(
                "Ship it", 2, 3, "Build", new string('x', 500_000), 0);

            int total = given.Sum(pair => pair.Key.Length + pair.Value.Length + 2);

            Assert.True(total < 20_000, $"a chain step was handed {total} characters");
        }
    }
}
