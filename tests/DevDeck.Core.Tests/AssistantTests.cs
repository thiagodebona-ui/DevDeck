using System.Text;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The pieces the assistant gained: attachments, saved conversations, the prompt library, the
    ///  token and cost readout, the global hotkey parser and the update check's version comparison.
    /// </summary>
    /// <remarks>
    ///  Everything that touches the disk writes into a temporary directory it then removes, because
    ///  a test that writes into the real settings folder would delete a developer's own saved
    ///  conversations the first time the pruning logic ran.
    /// </remarks>
    public class AssistantTests
    {
        #region Attachments

        [Fact]
        public void AttachmentReadsATextFile()
        {
            using Scratch scratch = new();

            string path = scratch.Write("Program.cs", "int x = 1;\n");

            Attachment? attached = Attachment.Read(path, out string problem);

            Assert.NotNull(attached);
            Assert.Equal(string.Empty, problem);
            Assert.Equal("Program.cs", attached!.Name);
            Assert.Contains("int x = 1;", attached.Text);
            Assert.False(attached.Truncated);
        }

        [Fact]
        public void AttachmentRefusesAFileThatIsNotThere()
        {
            using Scratch scratch = new();

            Attachment? attached = Attachment.Read(Path.Combine(scratch.Path, "absent.txt"), out string problem);

            Assert.Null(attached);
            Assert.NotEqual(string.Empty, problem);
        }

        [Fact]
        public void AttachmentRefusesAnEmptyFile()
        {
            using Scratch scratch = new();

            Attachment? attached = Attachment.Read(scratch.Write("empty.txt", string.Empty), out string problem);

            Assert.Null(attached);
            Assert.Contains("empty", problem, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///  A null byte early on is the whole test - it is what every tool from git to grep uses,
        ///  and the reason is that a binary reaching a model is thousands of tokens of nonsense.
        /// </summary>
        [Fact]
        public void AttachmentRefusesABinary()
        {
            using Scratch scratch = new();

            string path = Path.Combine(scratch.Path, "image.png");

            File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47, 0x00, 0x0D, 0x0A]);

            Attachment? attached = Attachment.Read(path, out string problem);

            Assert.Null(attached);
            Assert.Contains("text", problem, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///  The cut has to announce itself. A model given the first half of a file with no note
        ///  will reason confidently about an ending that is not there.
        /// </summary>
        [Fact]
        public void AttachmentTruncatesAndSaysSo()
        {
            using Scratch scratch = new();

            StringBuilder big = new();

            while (big.Length < Attachment.MaxCharacters + 4096)
            {
                big.Append("a line of perfectly ordinary text\n");
            }

            Attachment? attached = Attachment.Read(scratch.Write("big.txt", big.ToString()), out _);

            Assert.NotNull(attached);
            Assert.True(attached!.Truncated);
            Assert.Contains("only the first", attached.Text);
            Assert.Contains("shortened", attached.Summary);
        }

        [Theory]
        [InlineData("Thing.cs", "csharp")]
        [InlineData("app.ts", "typescript")]
        [InlineData("run.py", "python")]
        [InlineData("deploy.sh", "bash")]
        [InlineData("notes.md", "markdown")]
        public void AttachmentFencesWithTheLanguage(string name, string fence)
        {
            using Scratch scratch = new();

            Attachment? attached = Attachment.Read(scratch.Write(name, "content\n"), out _);

            Assert.NotNull(attached);
            Assert.Contains($"```{fence}", attached!.AsContext());
        }

        /// <summary>The name is carried across, because it is often the context.</summary>
        [Fact]
        public void AttachmentNamesTheFileInItsContext()
        {
            using Scratch scratch = new();

            Attachment? attached = Attachment.Read(scratch.Write("Widget.cs", "class Widget { }\n"), out _);

            Assert.StartsWith("File: Widget.cs", attached!.AsContext());
        }

        #endregion

        #region Saved conversations

        [Fact]
        public void TitleTakesTheFirstNonEmptyLine()
        {
            Assert.Equal("Why is this failing", ChatArchive.TitleFor("\n\n  Why is this failing  \nand also"));
        }

        [Fact]
        public void TitleShortensALongQuestion()
        {
            string title = ChatArchive.TitleFor(new string('x', 200));

            Assert.True(title.Length <= 51);
            Assert.EndsWith("…", title);
        }

        [Fact]
        public void WhenReadsAsTodayForARecentChat()
        {
            SavedChat chat = new() { At = DateTime.Now.AddMinutes(-5) };

            Assert.StartsWith("Today", chat.When);
        }

        [Fact]
        public void SummaryCountsTurnsAndNamesTheModel()
        {
            SavedChat chat = new()
            {
                Model = "gpt-4o-mini",
                Turns =
                [
                    new SavedTurn("user", "hello", DateTime.Now),
                    new SavedTurn("assistant", "hello yourself", DateTime.Now),
                ],
            };

            Assert.Contains("2 turns", chat.Summary);
            Assert.Contains("gpt-4o-mini", chat.Summary);
        }

        [Fact]
        public void SummarySaysSoWhenTheModelIsUnknown()
        {
            Assert.Contains("unknown model", new SavedChat().Summary);
        }

        #endregion

        #region The prompt library

        /// <summary>
        ///  Seeding twice must add nothing the second time, or every start would stack another set
        ///  of starters on the list.
        /// </summary>
        [Fact]
        public void SeedingIsIdempotent()
        {
            List<SavedPrompt> prompts = [];

            int first = PromptLibrary.Seed(prompts);

            Assert.True(first > 0);
            Assert.Equal(first, prompts.Count);
            Assert.Equal(0, PromptLibrary.Seed(prompts));
            Assert.Equal(first, prompts.Count);
        }

        /// <summary>
        ///  An edited starter is the user's, not the app's. Matching by name is what keeps a
        ///  reseed from quietly replacing an improved prompt with the shipped one.
        /// </summary>
        [Fact]
        public void SeedingLeavesAnEditedStarterAlone()
        {
            SavedPrompt starter = PromptLibrary.Starters[0];

            List<SavedPrompt> prompts =
            [
                new SavedPrompt { Name = starter.Name, Body = "my own wording" },
            ];

            PromptLibrary.Seed(prompts);

            Assert.Equal("my own wording", prompts.Single(p => p.Name == starter.Name).Body);
        }

        [Fact]
        public void StartersAreNamedAndHaveBodies()
        {
            Assert.All(PromptLibrary.Starters, prompt =>
            {
                Assert.NotEqual(string.Empty, prompt.Name);
                Assert.NotEqual(string.Empty, prompt.Body);
                Assert.True(prompt.IsStarter);
            });
        }

        #endregion

        #region Tokens, windows and money

        [Fact]
        public void EmptyTextIsNoTokens()
        {
            Assert.Equal(0, TokenCount.Of((string?)null));
            Assert.Equal(0, TokenCount.Of(string.Empty));
        }

        [Fact]
        public void TokensGrowWithLength()
        {
            Assert.True(TokenCount.Of(new string('a', 400)) > TokenCount.Of(new string('a', 40)));
        }

        /// <summary>"128k" must not be read as the "8k" inside it - hence longest-first.</summary>
        [Theory]
        [InlineData("some-model-128k", 128 * 1024)]
        [InlineData("some-model-8k", 8 * 1024)]
        [InlineData("gpt-4o", 128 * 1024)]
        [InlineData("claude-sonnet", 200 * 1024)]
        public void WindowIsReadFromTheName(string model, int window)
        {
            Assert.Equal(window, TokenCount.WindowFor(model));
        }

        /// <summary>
        ///  A guess here would be wrong by a factor of ten either way, so nothing is printed.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("some-model-nobody-has-heard-of")]
        public void WindowIsNothingWhenTheNameDoesNotSay(string model)
        {
            Assert.Null(TokenCount.WindowFor(model));
        }

        /// <summary>A model on the user's own processor costs nothing, and should say nothing.</summary>
        [Fact]
        public void LocalEndpointsAreFree()
        {
            Assert.Equal((0m, 0m), TokenCount.PriceFor("http://localhost:11434/v1", "llama3"));
            Assert.Equal((0m, 0m), TokenCount.PriceFor("http://127.0.0.1:1234/v1", "anything"));
        }

        [Fact]
        public void UnknownModelsReportNoMoney()
        {
            Assert.Null(TokenCount.PriceFor("https://example.com/v1", "a-model-with-no-published-price"));

            Spend spend = TokenCount.Reckon("https://example.com/v1", "a-model-with-no-published-price", 1000, 1000, true);

            Assert.Equal(0m, spend.Money);
            Assert.DoesNotContain("USD", spend.Describe());
        }

        [Fact]
        public void PricedModelsReckonAmountsInDollars()
        {
            Spend spend = TokenCount.Reckon("https://api.openai.com/v1", "gpt-4o-mini", 1_000_000, 1_000_000, true);

            Assert.Equal(0.75m, spend.Money);
            Assert.Contains("USD", spend.Describe());
        }

        /// <summary>
        ///  The tilde is load-bearing. An estimate shown as an exact figure is worse than none.
        /// </summary>
        [Fact]
        public void EstimatesAreMarkedAndMeasurementsAreNot()
        {
            Assert.Contains("~", TokenCount.Reckon("https://example.com", "gpt-4o", 10, 10, measured: false).Describe());
            Assert.DoesNotContain("~", TokenCount.Reckon("https://example.com", "gpt-4o", 10, 10, measured: true).Describe());
        }

        [Fact]
        public void SpendTotalsBothDirections()
        {
            Assert.Equal(30, new Spend(10, 20, 0m, true).Total);
        }

        #endregion

        #region The global hotkey

        [Theory]
        [InlineData("Ctrl+Shift+D")]
        [InlineData("ctrl+alt+k")]
        [InlineData("Alt+F4")]
        [InlineData("Win+Space")]
        [InlineData("Ctrl + Shift + 9")]
        public void GoodCombinationsParse(string combination)
        {
            Assert.True(GlobalHotkey.Parse(combination, out uint modifiers, out uint key));
            Assert.NotEqual(0u, modifiers);
            Assert.NotEqual(0u, key);
        }

        /// <summary>
        ///  A bare letter would swallow that letter in every application on the machine, which the
        ///  user would experience as a broken keyboard rather than as a shortcut.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("D")]
        [InlineData("Ctrl")]
        [InlineData("Ctrl+Shift")]
        [InlineData("Ctrl+D+K")]
        [InlineData("Ctrl+NotAKey")]
        public void BadCombinationsAreRefused(string combination)
        {
            Assert.False(GlobalHotkey.Parse(combination, out _, out _));
        }

        [Fact]
        public void ModifierNamesHaveAliases()
        {
            Assert.True(GlobalHotkey.Parse("Control+D", out uint spelled, out _));
            Assert.True(GlobalHotkey.Parse("Ctrl+D", out uint shortened, out _));
            Assert.Equal(spelled, shortened);
        }

        [Fact]
        public void FunctionKeysAreNumberedFromF1()
        {
            Assert.True(GlobalHotkey.Parse("Ctrl+F1", out _, out uint first));
            Assert.True(GlobalHotkey.Parse("Ctrl+F12", out _, out uint twelfth));
            Assert.Equal(first + 11, twelfth);
        }

        /// <summary>F25 does not exist, and a code past the range would land on some other key.</summary>
        [Fact]
        public void FunctionKeysStopAtF24()
        {
            Assert.False(GlobalHotkey.Parse("Ctrl+F25", out _, out _));
        }

        [Fact]
        public void AnUnavailablePlatformStillExplainsItself()
        {
            if (GlobalHotkey.IsAvailable)
            {
                return;
            }

            Assert.NotEqual(string.Empty, GlobalHotkey.Unavailable);
            Assert.Contains("devdeck show", GlobalHotkey.Unavailable);
        }

        #endregion

        #region The update check

        /// <summary>
        ///  Compared as versions, not as text: "1.10.0" sorts before "1.9.0" as a string, and an
        ///  update check that goes backwards is worse than no update check.
        /// </summary>
        [Fact]
        public void ANewerVersionIsNewer()
        {
            Assert.True(new Release("1.10.0", "", "").IsNewerThan(new Version(1, 9, 0)));
            Assert.True(new Release("v2.0.0", "", "").IsNewerThan(new Version(1, 9, 9)));
        }

        [Fact]
        public void TheSameOrOlderVersionIsNot()
        {
            Assert.False(new Release("1.0.0", "", "").IsNewerThan(new Version(1, 0, 0)));
            Assert.False(new Release("0.9.0", "", "").IsNewerThan(new Version(1, 0, 0)));
        }

        /// <summary>A tag someone typed by hand is not a reason to tell the user to upgrade.</summary>
        [Theory]
        [InlineData("")]
        [InlineData("nightly")]
        [InlineData("release-candidate")]
        public void AnUnreadableTagIsNotNewer(string tag)
        {
            Assert.False(new Release(tag, "", "").IsNewerThan(new Version(1, 0, 0)));
        }

        /// <summary>
        ///  A prerelease build must still produce a real version number. Version.TryParse refuses
        ///  "3.0.0-alpha.1", and the 0.0.0 fallback that follows would make every published
        ///  release look newer - an update check permanently shouting at an alpha tester.
        /// </summary>
        [Fact]
        public void TheRunningVersionIsReadable()
        {
            Assert.True(AppVersion.Current > new Version(0, 0, 0));
        }

        [Fact]
        public void APrereleaseBuildIsNotOlderThanItsOwnRelease()
        {
            Assert.False(new Release(AppVersion.Number, "", "").IsNewerThan(AppVersion.Current));
        }

        #endregion

        /// <summary>A directory that cleans up after itself.</summary>
        private sealed class Scratch : IDisposable
        {
            public Scratch()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "devdeck-tests-" + Guid.NewGuid().ToString("n")[..8]);

                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public string Write(string name, string content)
            {
                string file = System.IO.Path.Combine(Path, name);

                File.WriteAllText(file, content);

                return file;
            }

            public void Dispose()
            {
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch (Exception)
                {
                    // A temporary directory left behind is not worth failing a test over.
                }
            }
        }
    }
}
