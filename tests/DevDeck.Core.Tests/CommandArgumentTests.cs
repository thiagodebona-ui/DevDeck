using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  How a command's parameter list reaches the script: on the command line and in the
    ///  environment.
    /// </summary>
    /// <remarks>
    ///  The cases worth pinning are the ones where a value would otherwise stop being one argument:
    ///  a space, a quote, and a row the user switched off.
    /// </remarks>
    public class CommandArgumentTests
    {
        private static CustomCommand With(CommandKind kind, params CommandArgument[] arguments) => new()
        {
            Name = "test",
            Command = "echo",
            Kind = kind,
            Arguments = [.. arguments],
        };

        [Fact]
        public void NoArgumentsAddNothing()
        {
            Assert.Equal(string.Empty, ScriptFile.ArgumentsFor(With(CommandKind.PowerShell)));
            Assert.Empty(ScriptFile.EnvironmentFor(With(CommandKind.PowerShell)));
        }

        [Fact]
        public void PowerShellGetsThemByName()
        {
            string line = ScriptFile.ArgumentsFor(With(
                CommandKind.PowerShell,
                new CommandArgument { Name = "Environment", Value = "staging" },
                new CommandArgument { Name = "Path", Value = @"C:\My Files\x" }));

            Assert.Equal(@" -Environment ""staging"" -Path ""C:\My Files\x""", line);
        }

        [Fact]
        public void APowerShellNameThatCannotBindGoesByPosition()
        {
            string line = ScriptFile.ArgumentsFor(With(
                CommandKind.PowerShell,
                new CommandArgument { Name = "not a name", Value = "v" },
                new CommandArgument { Value = "loose" }));

            Assert.Equal(@" ""v"" ""loose""", line);
        }

        [Fact]
        public void BatchGetsThemInOrderWithQuotesDoubled()
        {
            string line = ScriptFile.ArgumentsFor(With(
                CommandKind.Batch,
                new CommandArgument { Name = "a", Value = "one two" },
                new CommandArgument { Name = "b", Value = @"say ""hi""" }));

            Assert.Equal(@" ""one two"" ""say """"hi""""""", line);
        }

        [Fact]
        public void ARowSwitchedOffIsNotPassed()
        {
            CustomCommand command = With(
                CommandKind.Bash,
                new CommandArgument { Name = "on", Value = "1" },
                new CommandArgument { Name = "off", Value = "2", Enabled = false });

            Assert.Equal(@" ""1""", ScriptFile.ArgumentsFor(command));
            Assert.False(ScriptFile.EnvironmentFor(command).ContainsKey("DEVDECK_ARG_OFF"));
        }

        [Fact]
        public void TheEnvironmentHasThemByNameAndByPosition()
        {
            IReadOnlyDictionary<string, string> variables = ScriptFile.EnvironmentFor(With(
                CommandKind.Shell,
                new CommandArgument { Name = "base-branch", Value = "main" },
                new CommandArgument { Value = "x" }));

            Assert.Equal("main", variables["DEVDECK_ARG_BASE_BRANCH"]);
            Assert.Equal("main", variables["DEVDECK_ARG_1"]);
            Assert.Equal("x", variables["DEVDECK_ARG_2"]);
            Assert.Equal("2", variables["DEVDECK_ARG_COUNT"]);
        }
    }
}
