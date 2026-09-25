using System.Text.Json;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Reading and writing the deck a project commits alongside its code.
    /// </summary>
    /// <remarks>
    ///  The file is the contract: people edit it by hand, commit it, and review it in a diff. So
    ///  these tests pin the things a hand-edited file relies on - that comments and trailing commas
    ///  survive, that a half-written file does not take the panel down with it, and that a deck
    ///  written by the app can be read back by the app.
    /// </remarks>
    public class ProjectDeckTests : IDisposable
    {
        private readonly string folder;

        public ProjectDeckTests()
        {
            folder = Path.Combine(Path.GetTempPath(), "devdeck-tests-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(folder);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // A temporary folder left behind is not a failed test.
            }
        }

        private string Put(string json)
        {
            string path = ProjectDeck.PathIn(folder);

            File.WriteAllText(path, json);

            return path;
        }

        [Fact]
        public void NoFileIsNotAnError()
        {
            Assert.False(ProjectDeck.Exists(folder));
            Assert.Null(ProjectDeck.Read(folder));
        }

        [Fact]
        public void ReadsTheCommandsAProjectCommitted()
        {
            Put("""
                {
                  "name": "checkout-api",
                  "commands": [
                    { "name": "build", "command": "dotnet build" },
                    { "name": "test", "command": "dotnet test", "description": "the fast ones" }
                  ]
                }
                """);

            DeckFile deck = ProjectDeck.Read(folder)!;

            Assert.Equal("checkout-api", deck.Name);
            Assert.Equal(2, deck.Commands.Count);
            Assert.Equal("dotnet build", deck.Commands[0].Command);
            Assert.Equal("the fast ones", deck.Commands[1].Description);
        }

        [Fact]
        public void ReadsAFileEditedByHand()
        {
            // Comments and a trailing comma: both illegal in strict JSON, both what a human writes
            // in a file they maintain. Rejecting them would make the format hostile to its users.
            Put("""
                {
                  // the one everyone runs first
                  "commands": [
                    { "name": "build", "command": "make" },
                  ],
                }
                """);

            DeckFile deck = ProjectDeck.Read(folder)!;

            Assert.Single(deck.Commands);
        }

        [Fact]
        public void TheCasingOfTheKeysDoesNotMatter()
        {
            Put("""{ "Commands": [ { "Name": "build", "COMMAND": "make" } ] }""");

            Assert.Single(ProjectDeck.Read(folder)!.Commands);
        }

        [Fact]
        public void AHalfWrittenFileReadsAsNothing()
        {
            // Which is what a file being saved looks like for a few milliseconds, and the panel
            // has no better answer than carrying on without it.
            Put("{ \"commands\": [ { \"name\": \"bui");

            Assert.Null(ProjectDeck.Read(folder));
        }

        [Fact]
        public void EntriesWithNothingToRunAreLeftOut()
        {
            Put("""
                {
                  "commands": [
                    { "name": "build", "command": "make" },
                    { "name": "placeholder", "command": "" },
                    { "name": "", "command": "make test" }
                  ]
                }
                """);

            DeckFile deck = ProjectDeck.Read(folder)!;

            Assert.Single(deck.Commands);
            Assert.Equal("build", deck.Commands[0].Name);
        }

        /// <remarks>
        ///  The expected kind is named rather than passed: a test method is public, CommandKind is
        ///  not, and a public method cannot take an internal type. Comparing the names keeps the
        ///  table readable without widening the enum's accessibility for the sake of a test.
        /// </remarks>
        [Theory]
        [InlineData("powershell", "PowerShell")]
        [InlineData("pwsh", "PowerShell")]
        [InlineData("PS1", "PowerShell")]
        [InlineData("batch", "Batch")]
        [InlineData("cmd", "Batch")]
        [InlineData("bash", "Bash")]
        [InlineData("sh", "Bash")]
        [InlineData("shell", "Shell")]
        [InlineData(null, "Shell")]
        [InlineData("something else", "Shell")]
        public void TheKindIsReadLeniently(string? written, string expected)
        {
            DeckEntry entry = new() { Name = "x", Command = "y", Kind = written };

            Assert.Equal(expected, entry.Parsed.ToString());
        }

        [Fact]
        public void AnEntryBecomesSomethingRunnable()
        {
            CustomCommand command = ProjectDeck.ToCommand(new DeckEntry
            {
                Name = "  serve  ",
                Command = "npm run dev",
                Kind = "bash",
                Detached = true,
            });

            Assert.Equal("serve", command.Name);
            Assert.Equal(CommandKind.Bash, command.Kind);
            Assert.True(command.Detached);
        }

        [Fact]
        public void WhatItWritesItCanReadBack()
        {
            ProjectDeck.Write(folder, "checkout-api",
            [
                new CustomCommand { Name = "build", Command = "dotnet build" },
                new CustomCommand { Name = "serve", Command = "npm run dev", Kind = CommandKind.Bash, Detached = true },
            ]);

            DeckFile deck = ProjectDeck.Read(folder)!;

            Assert.Equal("checkout-api", deck.Name);
            Assert.Equal(2, deck.Commands.Count);
            Assert.Equal(CommandKind.Bash, deck.Commands[1].Parsed);
            Assert.True(deck.Commands[1].Detached);
        }

        [Fact]
        public void TheOrdinaryKindIsLeftOutOfTheFile()
        {
            // Every entry carrying "kind": "shell" is noise in a file people read in a diff.
            ProjectDeck.Write(folder, null, [new CustomCommand { Name = "build", Command = "make" }]);

            string json = File.ReadAllText(ProjectDeck.PathIn(folder));

            Assert.DoesNotContain("\"kind\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"name\": null", json);
        }

        [Fact]
        public void WhatItWritesIsValidJson()
        {
            ProjectDeck.Write(folder, "x", [new CustomCommand { Name = "build", Command = "make" }]);

            // Read strictly this time: the file goes into a repository, where the next thing to
            // open it may not be this app.
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ProjectDeck.PathIn(folder)));

            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }

        [Fact]
        public void AStarterFileIsNamedAfterTheFolder()
        {
            ProjectDeck.Sample(folder, []);

            DeckFile deck = ProjectDeck.Read(folder)!;

            Assert.Equal(new DirectoryInfo(folder).Name, deck.Name);
            Assert.Single(deck.Commands);
        }

        [Fact]
        public void AStarterFileUsesTheCommandsThereAlreadyAre()
        {
            ProjectDeck.Sample(folder,
            [
                .. Enumerable.Range(0, 9).Select(at => new CustomCommand
                {
                    Name = $"command {at}",
                    Command = "make",
                }),
            ]);

            // Six, not nine: a starter file is meant to be read, and the whole deck pasted into a
            // repository is a wall of text nobody edits.
            Assert.Equal(6, ProjectDeck.Read(folder)!.Commands.Count);
        }
    }
}
