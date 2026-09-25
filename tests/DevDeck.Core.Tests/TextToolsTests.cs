using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The offline conversions.
    /// </summary>
    /// <remarks>
    ///  These are the tools people currently paste tokens and customer records into a website to
    ///  run, so the bar is not "mostly right": a decoder that quietly mangles a payload is worse
    ///  than no decoder, because the user has no reason to doubt it. The cases pinned here are the
    ///  ones where a naive implementation is wrong - unpadded base64, a run of capitals in an
    ///  identifier, a pattern that backtracks forever.
    /// </remarks>
    public class TextToolsTests
    {
        [Fact]
        public void FormatsJsonAndReportsWhereItBroke()
        {
            Assert.Contains("\"a\": 1", TextTools.FormatJson("""{"a":1}""").Text);

            ToolResult bad = TextTools.FormatJson("""{"a": }""");

            Assert.True(bad.Failed);
            Assert.Contains("Line 1", bad.Error!);
        }

        /// <summary>An empty box is not an error, it is an empty box.</summary>
        [Fact]
        public void EmptyInputIsNotAFailure()
        {
            Assert.False(TextTools.FormatJson("   ").Failed);
            Assert.False(TextTools.FromBase64(string.Empty).Failed);
            Assert.False(TextTools.DecodeJwt(string.Empty).Failed);
        }

        [Fact]
        public void MinifyingRemovesTheWhitespace()
        {
            Assert.Equal("""{"a":1,"b":[2,3]}""", TextTools.MinifyJson("""
                {
                  "a": 1,
                  "b": [2, 3]
                }
                """).Text);
        }

        /// <summary>
        ///  Trailing commas and comments turn up constantly in hand-written config, and refusing
        ///  them would mean failing on the files people most want formatted.
        /// </summary>
        [Fact]
        public void AcceptsTheJsonPeopleActuallyWrite()
        {
            Assert.False(TextTools.FormatJson("""{"a": 1, /* note */ "b": 2,}""").Failed);
        }

        [Fact]
        public void Base64RoundTrips()
        {
            const string original = "hello, wörld — with punctuation";

            Assert.Equal(original, TextTools.FromBase64(TextTools.ToBase64(original).Text).Text);
        }

        /// <summary>
        ///  JWT segments are URL-safe and unpadded by specification. A decoder that rejects them
        ///  fails on the single most common thing anyone pastes into it.
        /// </summary>
        [Theory]
        [InlineData("eyJhIjoxfQ")]        // unpadded
        [InlineData("eyJhIjoxfQ==")]      // padded
        public void DecodesUnpaddedAndUrlSafeBase64(string encoded)
        {
            Assert.Equal("""{"a":1}""", TextTools.FromBase64(encoded).Text);
        }

        [Fact]
        public void RejectsTextThatIsNotBase64()
        {
            Assert.True(TextTools.FromBase64("not base64 !!!").Failed);
        }

        [Fact]
        public void UrlAndHtmlEncodingRoundTrip()
        {
            const string original = "a b&c=d?e<f>";

            Assert.Equal(original, TextTools.UrlDecode(TextTools.UrlEncode(original).Text).Text);
            Assert.Equal(original, TextTools.HtmlDecode(TextTools.HtmlEncode(original).Text).Text);
        }

        /// <summary>
        ///  The token below is the canonical example from jwt.io, signed with the secret "your-256-bit-secret".
        ///  Nothing here checks that signature - and the output has to say so, because a user who
        ///  reads a decoded payload and assumes it was verified has been actively misled.
        /// </summary>
        [Fact]
        public void DecodesAJwtAndSaysItHasNotVerifiedIt()
        {
            const string token =
                "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9."
                + "eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ."
                + "SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

            ToolResult result = TextTools.DecodeJwt(token);

            Assert.False(result.Failed);
            Assert.Contains("HS256", result.Text);
            Assert.Contains("John Doe", result.Text);
            Assert.Contains("does not verify", result.Text);
        }

        [Fact]
        public void RejectsSomethingThatIsNotAToken()
        {
            Assert.True(TextTools.DecodeJwt("just a string").Failed);
        }

        /// <summary>The empty-string digests, which are the ones that can be checked by eye.</summary>
        [Theory]
        [InlineData("MD5", "d41d8cd98f00b204e9800998ecf8427e")]
        [InlineData("SHA1", "da39a3ee5e6b4b0d3255bfef95601890afd80709")]
        [InlineData("SHA256", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855")]
        public void HashesMatchTheKnownValues(string algorithm, string expected)
        {
            Assert.Equal(expected, TextTools.Hash(string.Empty, algorithm).Text);
        }

        [Fact]
        public void AnUnknownAlgorithmIsReported()
        {
            Assert.True(TextTools.Hash("x", "sha3").Failed);
        }

        [Fact]
        public void EveryOfferedAlgorithmActuallyWorks()
        {
            Assert.All(
                TextTools.HashAlgorithms,
                algorithm => Assert.False(TextTools.Hash("x", algorithm).Failed));
        }

        [Fact]
        public void GuidsAreDistinctAndFormatted()
        {
            Assert.NotEqual(TextTools.NewGuid(), TextTools.NewGuid());
            Assert.StartsWith("{", TextTools.NewGuid(braces: true));
            string upper = TextTools.NewGuid(uppercase: true);
            Assert.Equal(upper.ToUpperInvariant(), upper);
        }

        [Fact]
        public void ReadsASecondsTimestamp()
        {
            ToolResult result = TextTools.Timestamp("1516239022");

            Assert.Contains("2018-01-18", result.Text);
            Assert.Contains("1516239022000", result.Text);
        }

        /// <summary>
        ///  Thirteen digits is milliseconds and ten is seconds. Guessing wrong puts the date in
        ///  1970 or 49000, which is the one failure mode of this tool that looks like a bug in
        ///  whatever produced the timestamp.
        /// </summary>
        [Fact]
        public void TellsMillisecondsFromSecondsByMagnitude()
        {
            Assert.Contains("2018-01-18", TextTools.Timestamp("1516239022000").Text);
            Assert.Contains("2018-01-18", TextTools.Timestamp("1516239022").Text);
        }

        [Fact]
        public void ReadsAnIsoDateBack()
        {
            Assert.Contains("1516239022", TextTools.Timestamp("2018-01-18T01:30:22Z").Text);
        }

        [Fact]
        public void RejectsTextThatIsNotADate()
        {
            Assert.True(TextTools.Timestamp("yesterdayish").Failed);
        }

        [Fact]
        public void ConvertsBetweenNamingConventions()
        {
            (TextTools.CaseStyle Style, string Expected)[] cases =
            [
                (TextTools.CaseStyle.Camel, "helloWorldAgain"),
                (TextTools.CaseStyle.Pascal, "HelloWorldAgain"),
                (TextTools.CaseStyle.Snake, "hello_world_again"),
                (TextTools.CaseStyle.Kebab, "hello-world-again"),
                (TextTools.CaseStyle.Constant, "HELLO_WORLD_AGAIN"),
                (TextTools.CaseStyle.Title, "Hello World Again"),
                (TextTools.CaseStyle.Lower, "hello_world again"),
                (TextTools.CaseStyle.Upper, "HELLO_WORLD AGAIN"),
            ];

            Assert.All(
                cases,
                item => Assert.Equal(item.Expected, TextTools.ChangeCase("hello_world again", item.Style).Text));
        }

        /// <summary>
        ///  The case that catches naive splitters: in "parseXMLFile" the boundary falls before the
        ///  last capital, not after the first.
        /// </summary>
        [Fact]
        public void SplitsARunOfCapitalsAtTheRightPlace()
        {
            Assert.Equal("parse_xml_file", TextTools.ChangeCase("parseXMLFile", TextTools.CaseStyle.Snake).Text);
            Assert.Equal("http_server", TextTools.ChangeCase("HTTPServer", TextTools.CaseStyle.Snake).Text);
        }

        [Fact]
        public void CaseConversionSurvivesEmptyAndSymbolOnlyInput()
        {
            Assert.Equal(string.Empty, TextTools.ChangeCase(string.Empty, TextTools.CaseStyle.Camel).Text);
            Assert.Equal("---", TextTools.ChangeCase("---", TextTools.CaseStyle.Camel).Text);
        }

        [Fact]
        public void FindsRegexMatchesAndTheirGroups()
        {
            ToolResult result = TextTools.TestRegex(
                @"(\w+)@(\w+)\.com",
                "a@b.com and c@d.com",
                ignoreCase: false,
                multiline: false,
                out IReadOnlyList<TextTools.RegexMatch> matches);

            Assert.Equal("2 matches.", result.Text);
            Assert.Equal(2, matches.Count);
            Assert.Equal(new[] { "a", "b" }, matches[0].Groups);
            Assert.Equal(0, matches[0].Index);
        }

        [Fact]
        public void ReportsABrokenPatternRatherThanThrowing()
        {
            ToolResult result = TextTools.TestRegex("([unclosed", "x", false, false, out _);

            Assert.True(result.Failed);
        }

        /// <summary>
        ///  A user-written pattern against user-supplied text is the textbook backtracking bomb.
        ///  Without the timeout this test would not fail - it would never finish, which is exactly
        ///  what it would do to the window.
        /// </summary>
        [Fact]
        public void ACatastrophicPatternTimesOutInsteadOfHanging()
        {
            ToolResult result = TextTools.TestRegex(
                "(a+)+$",
                new string('a', 40) + "!",
                ignoreCase: false,
                multiline: false,
                out _);

            Assert.True(result.Failed);
            Assert.Contains("backtracking", result.Error!);
        }

        [Fact]
        public void MatchingNothingSaysSoRatherThanFailing()
        {
            ToolResult result = TextTools.TestRegex("zzz", "abc", false, false, out IReadOnlyList<TextTools.RegexMatch> matches);

            Assert.False(result.Failed);
            Assert.Empty(matches);
        }

        [Fact]
        public void SortsAndDeduplicatesLines()
        {
            Assert.Equal(
                string.Join(Environment.NewLine, "a", "b", "c"),
                TextTools.SortLines("c\nb\na\nb", unique: true).Text);

            Assert.StartsWith("c", TextTools.SortLines("a\nb\nc", descending: true).Text);
        }

        [Fact]
        public void CountsWhatIsInTheBox()
        {
            Assert.Equal("11 characters · 2 words · 2 lines", TextTools.Statistics("hello\nworld"));
            Assert.Equal("0 characters · 0 words · 0 lines", TextTools.Statistics(string.Empty));
        }

        [Fact]
        public void FormatsXmlAndReportsWhereItBroke()
        {
            Assert.False(TextTools.FormatXml("<a><b>1</b></a>").Failed);
            Assert.True(TextTools.FormatXml("<a><b></a>").Failed);
        }
    }
}
