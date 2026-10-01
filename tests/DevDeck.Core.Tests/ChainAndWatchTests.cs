using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Chains: a list of command names that has to survive the commands being edited.
    /// </summary>
    public class CommandChainTests
    {
        private static readonly CustomCommand[] Deck =
        [
            new() { Name = "pull", Command = "git pull" },
            new() { Name = "build", Command = "dotnet build" },
            new() { Name = "test", Command = "dotnet test" },
        ];

        [Fact]
        public void ResolvesItsStepsInOrder()
        {
            CommandChain chain = new() { Name = "ship", Steps = ["pull", "build", "test"] };

            Assert.Equal(["pull", "build", "test"], chain.Resolve(Deck).Select(command => command.Name));
        }

        [Fact]
        public void TheOrderIsTheChainsNotTheDecks()
        {
            // The point of a chain. Resolving through the deck's own order would silently run the
            // steps in whatever order the list happened to be sorted in.
            CommandChain chain = new() { Name = "ship", Steps = ["test", "pull"] };

            Assert.Equal(["test", "pull"], chain.Resolve(Deck).Select(command => command.Name));
        }

        [Fact]
        public void ARepeatedStepRunsTwice()
        {
            CommandChain chain = new() { Name = "twice", Steps = ["build", "build"] };

            Assert.Equal(2, chain.Resolve(Deck).Count);
        }

        [Fact]
        public void NamesTheStepsThatNoLongerExist()
        {
            CommandChain chain = new() { Name = "ship", Steps = ["pull", "deploy", "smoke"] };

            Assert.Equal(["deploy", "smoke"], chain.Missing(Deck));
        }

        [Fact]
        public void ABrokenStepIsSkippedRatherThanFaked()
        {
            CommandChain chain = new() { Name = "ship", Steps = ["pull", "deploy", "test"] };

            Assert.Equal(["pull", "test"], chain.Resolve(Deck).Select(command => command.Name));
        }

        [Fact]
        public void TheCasingOfAStepDoesNotMatter()
        {
            CommandChain chain = new() { Name = "ship", Steps = ["BUILD"] };

            Assert.Empty(chain.Missing(Deck));
            Assert.Single(chain.Resolve(Deck));
        }

        [Fact]
        public void FollowsACommandThatWasRenamed()
        {
            CommandChain chain = new() { Name = "ship", Steps = ["pull", "build"] };

            Assert.True(chain.Rename("build", "compile"));
            Assert.Equal(["pull", "compile"], chain.Steps);
        }

        [Fact]
        public void ARenameOfSomethingElseChangesNothing()
        {
            CommandChain chain = new() { Name = "ship", Steps = ["pull"] };

            Assert.False(chain.Rename("build", "compile"));
            Assert.Equal(["pull"], chain.Steps);
        }

        [Fact]
        public void ARenameFollowsEveryUseOfTheName()
        {
            CommandChain chain = new() { Name = "twice", Steps = ["build", "test", "build"] };

            chain.Rename("build", "compile");

            Assert.Equal(["compile", "test", "compile"], chain.Steps);
        }

        [Fact]
        public void StoppingOnFailureIsTheDefault()
        {
            // Because a chain is nearly always a pipeline, and testing what did not build reports
            // a second failure caused by the first.
            Assert.True(new CommandChain().StopOnFailure);
        }

        [Fact]
        public void AChainThatWouldRunItselfIsCaught()
        {
            CommandChain one = new() { Name = "one", Steps = ["two"] };
            CommandChain two = new() { Name = "two", Steps = ["one"] };

            Assert.True(one.Cycles([one, two]));
        }

        [Fact]
        public void AnOrdinaryChainDoesNotLookLikeALoop()
        {
            CommandChain one = new() { Name = "one", Steps = ["build", "two"] };
            CommandChain two = new() { Name = "two", Steps = ["test"] };

            Assert.False(one.Cycles([one, two]));
        }

        [Fact]
        public void AChainNamingItselfIsALoop()
        {
            CommandChain chain = new() { Name = "ship", Steps = ["ship"] };

            Assert.True(chain.Cycles([chain]));
        }

        [Fact]
        public void TheSameChainReachedTwoWaysIsNotALoop()
        {
            // A diamond, not a cycle. Getting this wrong would refuse a perfectly good chain, and
            // the difference is whether the frame is removed on the way back up.
            CommandChain shared = new() { Name = "shared", Steps = ["build"] };
            CommandChain left = new() { Name = "left", Steps = ["shared"] };
            CommandChain right = new() { Name = "right", Steps = ["shared"] };
            CommandChain top = new() { Name = "top", Steps = ["left", "right"] };

            Assert.False(top.Cycles([top, left, right, shared]));
        }
    }

    /// <summary>
    ///  Watches: what counts as a change worth running a command for.
    /// </summary>
    /// <remarks>
    ///  Only the filtering is tested here. The timing - debouncing a burst into one run - is a
    ///  property of a real filesystem and a real clock, and a test that asserts it by sleeping is
    ///  a test that fails on a loaded build machine for no reason anyone can act on.
    /// </remarks>
    public class WatchRuleTests
    {
        private static FileWatch Watching(string pattern) =>
            new(new WatchRule { Pattern = pattern }, Path.GetTempPath(), _ => { }, () => false);

        [Fact]
        public void MatchesWhatThePatternAsksFor()
        {
            using FileWatch watch = Watching("*.cs");

            Assert.True(watch.Wanted("/work/app/Program.cs"));
            Assert.False(watch.Wanted("/work/app/notes.md"));
        }

        [Fact]
        public void ReadsSeveralPatternsAtOnce()
        {
            using FileWatch watch = Watching("*.cs; *.axaml ;*.json");

            Assert.True(watch.Wanted("/work/app/MainWindow.axaml"));
            Assert.True(watch.Wanted("/work/app/appsettings.json"));
            Assert.False(watch.Wanted("/work/app/readme.md"));
        }

        [Fact]
        public void NoPatternMeansEverything()
        {
            using FileWatch watch = Watching("   ");

            Assert.True(watch.Wanted("/work/app/anything.at.all"));
        }

        [Theory]
        [InlineData("/work/app/bin/Debug/app.dll")]
        [InlineData("/work/app/obj/project.assets.json")]
        [InlineData("/work/.git/index")]
        [InlineData("/work/ui/node_modules/react/index.js")]
        [InlineData("C:\\work\\app\\bin\\Debug\\app.pdb")]
        public void IgnoresWhatABuildWrites(string path)
        {
            // The check that stops the loop: a watch on a .NET project sees bin and obj rewritten
            // by the very command it just started, so without this the build triggers the build.
            using FileWatch watch = Watching("");

            Assert.False(watch.Wanted(path));
        }

        [Fact]
        public void AFolderMerelyNamedLikeOneOfThoseIsFine()
        {
            using FileWatch watch = Watching("*.cs");

            Assert.True(watch.Wanted("/work/binding/Thing.cs"));
            Assert.True(watch.Wanted("/work/app/Objects/Thing.cs"));
        }

        [Fact]
        public void TheCaseOfTheNameDoesNotMatter()
        {
            using FileWatch watch = Watching("*.cs");

            Assert.True(watch.Wanted("/work/app/PROGRAM.CS"));
        }

        [Fact]
        public void WaitsForTheWritingToStopByDefault()
        {
            // Zero here would mean running on the first of the several events one save produces.
            Assert.True(new WatchRule().QuietMilliseconds > 0);
        }

        /// <summary>A chain saved before steps could be switched off runs every step, as it did.</summary>
        [Fact]
        public void EveryStepIsOnUnlessSwitchedOff()
        {
            CommandChain chain = new() { Steps = ["Build", "Test", "Build"] };

            Assert.True(chain.IsOn(0) && chain.IsOn(1) && chain.IsOn(2));

            CommandChain read = System.Text.Json.JsonSerializer.Deserialize<CommandChain>("""{"Name":"Old","Steps":["A"]}""")!;

            Assert.Empty(read.SkippedSteps);
            Assert.True(read.IsOn(0));
        }

        /// <summary>By position, so one of two runs of the same command can be off.</summary>
        [Fact]
        public void OneOfTwoRunsOfACommandCanBeOff()
        {
            CommandChain chain = new() { Steps = ["Build", "Test", "Build"], SkippedSteps = [2] };

            Assert.True(chain.IsOn(0));
            Assert.False(chain.IsOn(2));
        }

        /// <summary>A step that is off and names nothing does not stop the chain from running.</summary>
        [Fact]
        public void AnOffStepThatIsMissingDoesNotBlockARun()
        {
            CommandChain chain = new() { Steps = ["Build", "Gone"], SkippedSteps = [1] };
            CustomCommand[] deck = [new() { Name = "Build" }];

            Assert.Equal(["Gone"], chain.Missing(deck));
            Assert.Empty(chain.Missing(deck, onlyOn: true));
        }
    }
}
