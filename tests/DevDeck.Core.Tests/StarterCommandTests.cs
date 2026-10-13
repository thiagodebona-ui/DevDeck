using System.Diagnostics;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The deck a new install starts with. These ship to every user without being reviewed again,
    ///  so the safety properties are worth pinning down - and they are there to be read and copied,
    ///  so each one is run against a real repository rather than only inspected.
    /// </summary>
    public class StarterCommandTests
    {
        private static readonly List<CustomCommand> Starters = StarterCommands.For();

        private static readonly List<CommandChain> Chains = StarterCommands.Chains();

        private static readonly List<WatchRule> Watches = StarterCommands.Watches();

        /// <summary>A small repository with a release tag, work since it, and a build folder.</summary>
        private static readonly Lazy<string> Repository = new(MakeRepository);

        /// <summary>Few on purpose: a deck the user has to prune before it is theirs is not a start.</summary>
        [Fact]
        public void ThereAreNine()
        {
            Assert.Equal(9, Starters.Count);
        }

        /// <summary>One body for every platform, run by Node rather than by a per-platform shell.</summary>
        [Fact]
        public void EveryOneIsJavaScript()
        {
            Assert.All(Starters, command => Assert.Equal(CommandKind.JavaScript, command.Kind));
        }

        /// <summary>
        ///  The chain that demonstrates passing values must be a genuine pipeline.
        /// </summary>
        /// <remarks>
        ///  Its whole purpose is to show a value travelling, so a later step reading the previous
        ///  step's output is the one property that cannot be allowed to rot - and it is invisible
        ///  in the chain itself, which is just names. It lives in the command bodies, so that is
        ///  where it is checked.
        /// </remarks>
        [Fact]
        public void TheWorkedChainActuallyReadsWhatItIsHanded()
        {
            CommandChain chain = Assert.Single(
                Chains, one => one.Steps.Count > 1 && one.StopOnFailure);

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
        ///  next person adding a starter command, not a sandbox - the point is that adding a
        ///  delete, a write or a push to this list fails the build rather than shipping.
        /// </remarks>
        [Theory]
        [InlineData("'rm'")]
        [InlineData("rm -")]
        [InlineData("execSync")]
        [InlineData("rmSync")]
        [InlineData("rmdir")]
        [InlineData("unlink")]
        [InlineData("writeFile")]
        [InlineData("appendFile")]
        [InlineData("rename")]
        [InlineData("process.kill")]
        [InlineData("taskkill")]
        [InlineData("'push'")]
        [InlineData("'reset'")]
        [InlineData("'clean'")]
        [InlineData("'checkout'")]
        [InlineData("'install'")]
        [InlineData("'publish'")]
        public void NothingDestructiveShips(string forbidden)
        {
            CustomCommand? offender = Starters.FirstOrDefault(
                command => command.Command.Contains(forbidden, StringComparison.OrdinalIgnoreCase));

            Assert.True(
                offender is null,
                $"starter command '{offender?.Name}' contains '{forbidden}'");
        }

        /// <summary>Each one finishes and reports, so none is left to run unwatched.</summary>
        [Fact]
        public void NoneIsDetached()
        {
            Assert.DoesNotContain(Starters, command => command.Detached);
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

        /// <summary>Between them they show every feature a command can use.</summary>
        [Fact]
        public void TheyShowEachFeatureOnce()
        {
            Assert.Contains(Starters, command => command.Arguments.Count > 0);
            Assert.Contains(Starters, command => CommandParameters.Find(command.Command).Any(p => !p.IsSecret));
            Assert.Contains(Starters, command => CommandParameters.Find(command.Command).Any(p => p.IsSecret));
            Assert.Contains(Starters, command => command.Command.Contains(RunContext.ChangedFiles, StringComparison.Ordinal));
        }

        /// <summary>Each one run by hand, as the deck would, with its own parameter rows.</summary>
        [Fact]
        public void EachRunsOnItsOwn()
        {
            foreach (CustomCommand command in Starters)
            {
                (int exit, string output, string errors) = Run(command, []);

                Assert.True(errors.Trim().Length == 0, $"{command.Name} wrote to stderr: {errors}");
                Assert.True(exit == 0, $"{command.Name} exited {exit}:\n{output}");
            }
        }

        [Fact]
        public void TheOverviewReadsThePackageAndGit()
        {
            string output = Run(Named("Project overview"), []).Output;

            Assert.Contains("demo 1.2.3 (Node, npm)", output);
            Assert.Contains("npm run build", output);
            Assert.Contains("Branch   feature", output);
            Assert.Contains("1 uncommitted file(s)", output);
        }

        [Fact]
        public void TheBranchIsComparedWithMain()
        {
            string output = Run(Named("What changed vs main"), []).Output;

            Assert.Contains("against main", output);
            Assert.Contains("add search", output);
            Assert.Contains("search.js", output);
        }

        [Fact]
        public void TodosAreFoundAndTheBuildFolderSkipped()
        {
            string output = Run(Named("Find TODOs"), []).Output;

            Assert.Contains("search.js:2", output);
            Assert.DoesNotContain("node_modules", output);
            Assert.Contains("1 found.", output);
        }

        [Fact]
        public void TheBuildFolderIsReported()
        {
            string output = Run(Named("Heavy build folders"), []).Output;

            Assert.Contains("node_modules", output);
            Assert.Contains("in 1 folders", output);
        }

        [Fact]
        public void APortIsReportedFreeOrTaken()
        {
            string output = Run(Named("Who is using a port"), []).Output;

            Assert.Matches(@"Port 3000 is (free|used by)", output);
        }

        /// <summary>
        ///  The chain, step by step, handing on what the real runner hands on.
        /// </summary>
        [Fact]
        public void TheReleaseNotesChainPassesTheTagAlong()
        {
            CommandChain chain = Chains.Single(one => one.Name == "Release notes");
            IReadOnlyList<CustomCommand> steps = chain.Resolve(Starters);

            (int exit, string first, _) = Run(steps[0], []);

            Assert.Equal(0, exit);

            string tag = first.Split('\n').Select(line => line.Trim()).Last(line => line.Length > 0);

            Assert.Equal("v1.0.0", tag);

            string notes = Run(steps[1], new()
            {
                [RunContext.PreviousName] = steps[0].Name,
                [RunContext.Previous] = first.TrimEnd(),
                [RunContext.PreviousLine] = tag,
                [RunContext.PreviousExit] = "0",
            }).Output;

            Assert.Contains("Since v1.0.0, handed on by \"Last release tag\"", notes);
            Assert.Contains("## Features\n- add search", notes.Replace("\r\n", "\n"));
            Assert.Contains("## Fixes\n- empty query", notes.Replace("\r\n", "\n"));
            Assert.DoesNotContain("first release", notes);
        }

        [Fact]
        public void AWatchHandsOnWhatChanged()
        {
            string output = Run(Named("What changed just now"), new()
            {
                [RunContext.Trigger] = "watch",
                [RunContext.ChangedFile] = "/repo/a b.js",
                [RunContext.ChangedFiles] = "/repo/a b.js\n/repo/c.js",
            }).Output;

            Assert.Contains("  /repo/a b.js", output);
            Assert.Contains("  /repo/c.js", output);
        }

        private static CustomCommand Named(string name) => Starters.Single(command => command.Name == name);

        /// <summary>Runs a starter as the deck would: placeholders filled with their defaults.</summary>
        private static (int Exit, string Output, string Errors) Run(
            CustomCommand command, Dictionary<string, string> environment)
        {
            CustomCommand filled = new()
            {
                Name = command.Name,
                Command = CommandParameters.Fill(command.Command, new Dictionary<string, string>(), _ => "hidden"),
                Kind = command.Kind,
                Arguments = command.Arguments,
            };

            using ScriptFile file = ScriptFile.Create(filled);

            ProcessStartInfo start = new(file.FileName, file.Arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Repository.Value,
            };

            foreach (KeyValuePair<string, string> variable in ScriptFile.EnvironmentFor(filled).Concat(environment))
            {
                start.Environment[variable.Key] = variable.Value;
            }

            using Process process = Process.Start(start)!;

            Task<string> errors = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output, errors.Result);
        }

        private static string MakeRepository()
        {
            string folder = Path.Combine(Path.GetTempPath(), "DevDeck-starters-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);

            void Git(params string[] args)
            {
                ProcessStartInfo start = new("git")
                {
                    WorkingDirectory = folder,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                foreach (string arg in (string[])["-c", "user.name=Test", "-c", "user.email=test@example.com", .. args])
                {
                    start.ArgumentList.Add(arg);
                }

                using Process process = Process.Start(start)!;
                process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();
            }

            void Write(string name, string text)
            {
                string path = Path.Combine(folder, name);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, text);
            }

            Git("init", "-q", "-b", "main");
            Write(".gitignore", "node_modules/\n");
            Write("package.json", """{ "name": "demo", "version": "1.2.3", "scripts": { "build": "tsc" } }""");
            Git("add", ".");
            Git("commit", "-q", "-m", "first release");
            Git("tag", "v1.0.0");

            Git("checkout", "-q", "-b", "feature");
            Write("search.js", "// search\n// TODO: rank the results\n");
            Git("add", ".");
            Git("commit", "-q", "-m", "feat: add search");
            Write("search.js", "// search\n// TODO: rank the results\nconst empty = '';\n");
            Git("commit", "-q", "-am", "fix(search): empty query");

            Write("node_modules/left-pad/index.js", "// TODO: inside a build folder\n");
            Write("notes.md", "uncommitted\n");

            return folder;
        }
    }
}
