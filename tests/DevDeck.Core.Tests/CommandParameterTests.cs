using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Placeholder discovery and substitution.
    /// </summary>
    /// <remarks>
    ///  These run before a command does, on a body the user wrote, and a mistake here is not a
    ///  wrong colour - it is the wrong command executing against a real repository. The cases
    ///  worth pinning are therefore the ones where the answer must be "leave it alone": a brace
    ///  that is not a placeholder, a name that appears twice, and a value that would end the
    ///  command and start another.
    /// </remarks>
    public class CommandParameterTests
    {
        [Fact]
        public void FindsNothingInAPlainCommand()
        {
            Assert.False(CommandParameters.Any("git status -sb"));
            Assert.Empty(CommandParameters.Find("git status -sb"));
        }

        [Fact]
        public void FindsANameAndItsDefault()
        {
            CommandParameter parameter = Assert.Single(CommandParameters.Find("git checkout {{branch:main}}"));

            Assert.Equal("branch", parameter.Name);
            Assert.Equal("main", parameter.Default);
            Assert.False(parameter.IsSecret);
        }

        /// <summary>
        ///  The same name twice is one question. Asking twice for "branch" in a command that uses
        ///  it in three places would be unusable, and answering differently each time would produce
        ///  a command that makes no sense.
        /// </summary>
        [Fact]
        public void RepeatedNamesAreAskedForOnceAndFilledEverywhere()
        {
            const string body = "git fetch origin {{branch}} && git checkout {{branch}} && git pull origin {{branch}}";

            Assert.Single(CommandParameters.Find(body));

            string filled = CommandParameters.Fill(body, new Dictionary<string, string> { ["branch"] = "release" });

            Assert.Equal(
                "git fetch origin release && git checkout release && git pull origin release",
                filled);
        }

        /// <summary>The first occurrence documents the parameter; a later bare one must not blank it.</summary>
        [Fact]
        public void TheFirstOccurrenceKeepsTheDefault()
        {
            CommandParameter parameter = Assert.Single(CommandParameters.Find("echo {{name:world}} {{name}}"));

            Assert.Equal("world", parameter.Default);
        }

        [Fact]
        public void AnUnansweredParameterFallsBackToItsDefault()
        {
            string filled = CommandParameters.Fill(
                "npm run {{script:build}}",
                new Dictionary<string, string>());

            Assert.Equal("npm run build", filled);
        }

        /// <summary>An answer of "" is the same as no answer: the default is the sensible value.</summary>
        [Fact]
        public void AnEmptyAnswerFallsBackToTheDefaultToo()
        {
            string filled = CommandParameters.Fill(
                "npm run {{script:build}}",
                new Dictionary<string, string> { ["script"] = string.Empty });

            Assert.Equal("npm run build", filled);
        }

        [Theory]
        [InlineData("echo ${HOME}")]
        [InlineData("awk '{ print $1 }'")]
        [InlineData("echo {not a placeholder}")]
        [InlineData("jq '.items[] | {name}'")]
        public void LeavesBracesThatAreNotPlaceholdersAlone(string body)
        {
            Assert.False(CommandParameters.Any(body));
            Assert.Equal(body, CommandParameters.Fill(body, new Dictionary<string, string>()));
        }

        [Fact]
        public void SecretsAreFoundSeparatelyAndResolvedFromTheVault()
        {
            CommandParameter parameter = Assert.Single(CommandParameters.Find("npm publish --token {{secret:NPM_TOKEN}}"));

            Assert.True(parameter.IsSecret);
            Assert.Equal("NPM_TOKEN", parameter.Name);

            string filled = CommandParameters.Fill(
                "npm publish --token {{secret:NPM_TOKEN}}",
                new Dictionary<string, string>(),
                name => name == "NPM_TOKEN" ? "abc123" : null);

            Assert.Equal("npm publish --token abc123", filled);
        }

        /// <summary>
        ///  A secret the vault does not hold becomes empty rather than being left as the literal
        ///  placeholder - which would otherwise be sent to the server as if it were the token.
        /// </summary>
        [Fact]
        public void AnUnknownSecretFillsAsEmpty()
        {
            Assert.Equal(
                "npm publish --token ",
                CommandParameters.Fill(
                    "npm publish --token {{secret:MISSING}}",
                    new Dictionary<string, string>(),
                    _ => null));
        }

        [Fact]
        public void SecretIsNotItselfAskedForAsAParameter()
        {
            Assert.DoesNotContain(
                CommandParameters.Find("deploy {{secret:KEY}} {{region:us}}"),
                p => p.Name == "secret");
        }

        [Theory]
        [InlineData("feature/login", false)]
        [InlineData("3000", false)]
        [InlineData("my branch name", false)]
        [InlineData("main; rm -rf /", true)]
        [InlineData("main && curl evil.sh", true)]
        [InlineData("main\nrm -rf /", true)]
        [InlineData("`whoami`", true)]
        [InlineData("a | tee /etc/passwd", true)]
        public void FlagsValuesThatWouldRunASecondCommand(string value, bool expected)
        {
            Assert.Equal(expected, CommandParameters.Unsafe(value));
        }

        [Fact]
        public void NamesTheCharacterThatWasFlagged()
        {
            Assert.Contains("a semicolon", CommandParameters.Offending("main; ls"));
            Assert.Contains("a new line", CommandParameters.Offending("main\nls"));
        }

        /// <summary>
        ///  Substitution stays textual. A placeholder inside a quoted string or a path is a normal
        ///  way to write one of these, and quoting the value here would break both.
        /// </summary>
        [Fact]
        public void SubstitutionDoesNotQuoteOrEscapeTheValue()
        {
            Assert.Equal(
                "curl \"https://api/v1/users/42\"",
                CommandParameters.Fill(
                    "curl \"https://api/v1/users/{{id}}\"",
                    new Dictionary<string, string> { ["id"] = "42" }));
        }

        [Fact]
        public void LabelIsReadableFromAnUnderscoredName()
        {
            Assert.Equal("Base branch", new CommandParameter("base_branch", string.Empty, false).Label);
        }
    }
}
