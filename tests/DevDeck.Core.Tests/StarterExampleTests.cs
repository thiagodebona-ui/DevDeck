using System.Diagnostics;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The Example: commands and chains. They exist to be read and copied, so one that does not
    ///  work as its comments say is worse than none - hence running them, not just inspecting them.
    /// </summary>
    public class StarterExampleTests
    {
        private static readonly List<CustomCommand> Examples = StarterExamples.Commands();

        [Fact]
        public void EveryChainStepIsAnExample()
        {
            Assert.All(StarterExamples.Chains(), chain =>
                Assert.Empty(chain.Missing(Examples)));
        }

        [Fact]
        public void NamesAreUniqueAndGroupedUnderExample()
        {
            List<string> names =
            [
                .. StarterCommands.For().Select(command => command.Name),
                .. Examples.Select(command => command.Name),
            ];

            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.All(Examples, command => Assert.StartsWith("Example: ", command.Name));
            Assert.All(StarterExamples.Chains(), chain => Assert.StartsWith("Example: ", chain.Name));
        }

        [Fact]
        public void EveryParameterRowHasANameAndAValue()
        {
            Assert.All(Examples.SelectMany(command => command.Arguments), argument =>
            {
                Assert.Matches(@"^[A-Za-z]\w*$", argument.Name);
                Assert.NotEqual(string.Empty, argument.Value);
            });
        }

        /// <summary>Each one run by hand, as the deck would, with its own parameter rows.</summary>
        [Fact]
        public void EachRunsOnItsOwn()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            foreach (CustomCommand command in Examples)
            {
                (int exit, string output, string errors) = Run(command, new Dictionary<string, string>());

                Assert.True(errors.Trim().Length == 0, $"{command.Name} wrote to stderr: {errors}");
                Assert.True(
                    exit == (command.Name == "Example: fail on purpose" ? 1 : 0),
                    $"{command.Name} exited {exit}:\n{output}");
            }
        }

        /// <summary>
        ///  Each chain, step by step, handing on what the real runner hands on.
        /// </summary>
        [Fact]
        public void EachChainPassesItsValuesAlong()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Dictionary<string, string> last = RunChain("Example: count, double, log");
            // Show the log ends on the line write-to-the-log just added, naming the step before it.
            Assert.Contains("[INFO] Example: double it said ", last[RunContext.PreviousLine]);

            last = RunChain("Example: several values");
            Assert.Contains("User   : " + Environment.UserName, last["output"]);

            last = RunChain("Example: stop on failure");
            Assert.Equal("2", last["stopped"]);
        }

        private static Dictionary<string, string> RunChain(string name)
        {
            CommandChain chain = StarterExamples.Chains().Single(one => one.Name == name);
            IReadOnlyList<CustomCommand> steps = chain.Resolve(Examples);
            Dictionary<string, string> handed = [];
            string output = string.Empty;

            for (int step = 0; step < steps.Count; step++)
            {
                Dictionary<string, string> environment = new(handed)
                {
                    [RunContext.ChainName] = chain.Name,
                    [RunContext.StepNumber] = (step + 1).ToString(),
                    [RunContext.StepCount] = steps.Count.ToString(),
                };

                (int exit, output, string errors) = Run(steps[step], environment);

                Assert.True(errors.Trim().Length == 0, $"{steps[step].Name} wrote to stderr: {errors}");

                handed = new()
                {
                    [RunContext.PreviousName] = steps[step].Name,
                    [RunContext.Previous] = output.TrimEnd(),
                    [RunContext.PreviousLine] = output
                        .Split('\n')
                        .Select(line => line.Trim())
                        .LastOrDefault(line => line.Length > 0) ?? string.Empty,
                    [RunContext.PreviousExit] = exit.ToString(),
                };

                if (exit != 0)
                {
                    Assert.True(chain.StopOnFailure, $"{steps[step].Name} failed:\n{output}");
                    handed["stopped"] = (step + 1).ToString();
                    break;
                }
            }

            handed["output"] = output;

            return handed;
        }

        private static (int Exit, string Output, string Errors) Run(
            CustomCommand command, Dictionary<string, string> environment)
        {
            using ScriptFile file = ScriptFile.Create(command);

            ProcessStartInfo start = new(file.FileName, file.Arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            foreach (KeyValuePair<string, string> variable in ScriptFile.EnvironmentFor(command).Concat(environment))
            {
                start.Environment[variable.Key] = variable.Value;
            }

            using Process process = Process.Start(start)!;

            Task<string> errors = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output, errors.Result);
        }
    }
}
