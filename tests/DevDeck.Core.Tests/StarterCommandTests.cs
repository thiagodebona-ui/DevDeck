using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The deck a new install starts with. These ship to every user without being reviewed again,
    ///  so the safety properties are worth pinning down.
    /// </summary>
    public class StarterCommandTests
    {
        private static readonly List<CustomCommand> Starters = StarterCommands.For();

        private static readonly List<CommandChain> Chains = StarterCommands.Chains();

        private static readonly List<WatchRule> Watches = StarterCommands.Watches();

        [Fact]
        public void ThereAreFifteen()
        {
            Assert.Equal(15, Starters.Count);
        }

        /// <summary>
        ///  The chain that demonstrates passing values must be a genuine pipeline.
        /// </summary>
        /// <remarks>
        ///  Its whole purpose is to show a value travelling, so a later step reading the previous
        ///  step's output is the one property that cannot be allowed to rot - and it is invisible
        ///  in the chain itself, which is just three names. It lives in the command bodies, so
        ///  that is where it is checked.
        /// </remarks>
        [Fact]
        public void TheWorkedChainActuallyReadsWhatItIsHanded()
        {
            CommandChain chain = Assert.Single(
                Chains.Where(one => one.Steps.Count > 1 && one.StopOnFailure));

            IEnumerable<CustomCommand> fed = chain.Steps
                .Skip(1)
                .Select(step => Starters.First(command => command.Name == step));

            Assert.All(fed, command => Assert.Contains(RunContext.PreviousName, command.Command));
        }

        [Fact]
        public void EveryOneIsNamedAndHasABody()
        {
            Assert.All(Starters, command =>
            {
                Assert.False(string.IsNullOrWhiteSpace(command.Name));
                Assert.False(string.IsNullOrWhiteSpace(command.Command));
            });
        }

        [Fact]
        public void NamesAreUnique()
        {
            Assert.Equal(Starters.Count, Starters.Select(c => c.Name).Distinct().Count());
        }

        /// <summary>
        ///  Nothing that ships with the app may change anything the first time it is clicked.
        /// </summary>
        /// <remarks>
        ///  Deliberately a blunt text search rather than anything clever. It is a tripwire for the
        ///  next person adding a starter command, not a sandbox - the point is that adding
        ///  `git push` or `rm -rf` to this list fails the build rather than shipping.
        /// </remarks>
        [Theory]
        [InlineData("rm ")]
        [InlineData("del ")]
        [InlineData("rmdir")]
        [InlineData("Remove-Item")]
        [InlineData("git push")]
        [InlineData("git reset")]
        [InlineData("git clean")]
        [InlineData("git checkout")]
        [InlineData("npm install")]
        [InlineData("npm publish")]
        // With the trailing space, so it catches "format c:" and not PowerShell's Format-Table.
        [InlineData("format ")]
        [InlineData("shutdown")]
        public void NothingDestructiveShips(string forbidden)
        {
            CustomCommand? offender = Starters.FirstOrDefault(
                command => command.Command.Contains(forbidden, StringComparison.OrdinalIgnoreCase));

            Assert.True(
                offender is null,
                $"starter command '{offender?.Name}' contains '{forbidden}'");
        }

        /// <summary>Only the one that opens an editor should be left to run unwatched.</summary>
        [Fact]
        public void OnlyTheEditorIsDetached()
        {
            CustomCommand[] detached = Starters.Where(c => c.Detached).ToArray();

            CustomCommand only = Assert.Single(detached);
            Assert.Contains("code", only.Command, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///  A starter command whose kind has no interpreter here would fail the moment it was
        ///  clicked, with an error about the platform rather than about the command.
        /// </summary>
        [Fact]
        public void EveryKindCanActuallyRunOnThisMachine()
        {
            Assert.All(Starters, command =>
            {
                if (command.Kind == CommandKind.Batch)
                {
                    Assert.True(OperatingSystem.IsWindows());
                }

                if (command.Kind == CommandKind.Bash)
                {
                    Assert.NotNull(ScriptFile.FindBash());
                }
            });
        }

        /// <summary>
        ///  Windows gets PowerShell for the scripted ones; everywhere else gets shell scripts.
        /// </summary>
        [Fact]
        public void ScriptedCommandsUseThePlatformsOwnShell()
        {
            CommandKind scripted = OperatingSystem.IsWindows() ? CommandKind.PowerShell : CommandKind.Bash;

            Assert.Contains(Starters, command => command.Kind == scripted);
            Assert.DoesNotContain(Starters, command => command.Kind == CommandKind.Batch);
        }

        /// <summary>
        ///  A Shell command is passed to cmd.exe inline on Windows, where a percent sign is a
        ///  variable reference. Anything needing one has to be a script instead.
        /// </summary>
        [Fact]
        public void PlainShellCommandsAvoidPercentSigns()
        {
            Assert.All(
                Starters.Where(command => command.Kind == CommandKind.Shell),
                command => Assert.DoesNotContain('%', command.Command));
        }

        /// <summary>
        ///  Every step of every shipped chain names a command that ships with it.
        /// </summary>
        /// <remarks>
        ///  The one that matters. A first run has exactly the starter commands and nothing else, so
        ///  a chain with a step pointing at anything not in that list is broken for every new user
        ///  on the first click - and a step is matched by name, so this breaks silently the moment
        ///  someone renames a starter without looking at the chains.
        /// </remarks>
        [Fact]
        public void EveryChainStepNamesAStarterCommand()
        {
            HashSet<string> known = [.. Starters.Select(command => command.Name)];

            Assert.All(Chains, chain => Assert.All(chain.Steps, step => Assert.Contains(step, known)));
        }

        [Fact]
        public void EveryChainIsNamedAndHasSteps()
        {
            Assert.All(Chains, chain =>
            {
                Assert.False(string.IsNullOrWhiteSpace(chain.Name));
                Assert.NotEmpty(chain.Steps);
            });

            Assert.Equal(Chains.Count, Chains.Select(chain => chain.Name).Distinct().Count());
        }

        [Fact]
        public void EveryShippedWatchNamesAStarterCommand()
        {
            HashSet<string> known = [.. Starters.Select(command => command.Name)];

            Assert.All(Watches, watch => Assert.Contains(watch.Command, known));
        }

        /// <summary>
        ///  Nothing that ships starts running on its own.
        /// </summary>
        /// <remarks>
        ///  A watch that arrived switched on would start running a command against whatever folder
        ///  the user first opened, before they had read what it does - which is the one thing a
        ///  seeded default must never do.
        /// </remarks>
        [Fact]
        public void NoShippedWatchIsArmed()
        {
            Assert.All(Watches, watch => Assert.False(watch.Enabled));
        }

        [Fact]
        public void EveryScriptCanBeTurnedIntoSomethingLaunchable()
        {
            foreach (CustomCommand command in Starters)
            {
                using ScriptFile script = ScriptFile.Create(command);

                Assert.False(string.IsNullOrWhiteSpace(script.FileName));
            }
        }

        /// <summary>
        ///  The exact body that shipped as "Today's commits" and was reported as producing no
        ///  output. Written out rather than read from anywhere, because the point of the test is
        ///  that this literal string is recognised on a real user's disk.
        /// </summary>
        private const string OldToday = "git log --since=midnight --oneline --stat";

        /// <summary>And the uncapped grep that froze the panel.</summary>
        private const string OldTodos = "git grep -n -E \"TODO|FIXME|HACK|XXX\"";

        [Fact]
        public void RefreshReplacesTheCommandThatPrintedNothing()
        {
            List<CustomCommand> deck =
            [
                new() { Name = "Today's commits", Command = OldToday, Kind = CommandKind.Batch },
            ];

            List<string> mended = StarterCommands.Refresh(deck);

            Assert.Equal(["Today's commits"], mended);
            Assert.NotEqual(OldToday, deck[0].Command);

            // The replacement has to say something on a day with no commits - that was the bug.
            Assert.Contains("No commits yet today", deck[0].Command, StringComparison.Ordinal);
        }

        [Fact]
        public void RefreshReplacesTheGrepThatFroze()
        {
            List<CustomCommand> deck =
            [
                new() { Name = "TODOs and FIXMEs", Command = OldTodos, Kind = CommandKind.Shell },
            ];

            Assert.Equal(["TODOs and FIXMEs"], StarterCommands.Refresh(deck));
            Assert.Contains("200", deck[0].Command, StringComparison.Ordinal);
        }

        /// <summary>
        ///  The one that matters. A command the user has changed is theirs, and an upgrade that
        ///  quietly threw their edit away would be far worse than the bug it was fixing.
        /// </summary>
        [Fact]
        public void RefreshLeavesAnEditedCommandAlone()
        {
            const string mine = "git log --since=midnight --oneline --author=me";

            List<CustomCommand> deck =
            [
                new() { Name = "Today's commits", Command = mine, Kind = CommandKind.Shell },
            ];

            Assert.Empty(StarterCommands.Refresh(deck));
            Assert.Equal(mine, deck[0].Command);
            Assert.Equal(CommandKind.Shell, deck[0].Kind);
        }

        /// <summary>A command the user deleted stays deleted - repair is not reseeding.</summary>
        [Fact]
        public void RefreshDoesNotBringBackADeletedCommand()
        {
            List<CustomCommand> deck = [];

            Assert.Empty(StarterCommands.Refresh(deck));
            Assert.Empty(deck);
        }

        /// <summary>
        ///  A file that has been through JSON and two operating systems has whatever line endings
        ///  it has. That is not a user edit.
        /// </summary>
        [Fact]
        public void RefreshIgnoresLineEndingsAndPadding()
        {
            List<CustomCommand> deck =
            [
                new() { Name = "Today's commits", Command = "  " + OldToday + "\r\n" },
            ];

            Assert.Equal(["Today's commits"], StarterCommands.Refresh(deck));
        }

        /// <summary>Running it twice is a no-op: the second pass has nothing left to match.</summary>
        [Fact]
        public void RefreshIsIdempotent()
        {
            List<CustomCommand> deck =
            [
                new() { Name = "Today's commits", Command = OldToday, Kind = CommandKind.Batch },
            ];

            Assert.Single(StarterCommands.Refresh(deck));
            Assert.Empty(StarterCommands.Refresh(deck));
        }

        /// <summary>A current deck is already right, so nothing is touched.</summary>
        [Fact]
        public void RefreshDoesNothingToAFreshDeck()
        {
            Assert.Empty(StarterCommands.Refresh(StarterCommands.For()));
        }
    }
}
