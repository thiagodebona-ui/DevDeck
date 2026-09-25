using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Variables in a request, and the environment that supplies them.
    /// </summary>
    /// <remarks>
    ///  Two things are being protected here, and only one of them is convenience.
    ///
    ///  The first is that an unresolved name stays visible. A <c>{{typo}}</c> that becomes an empty
    ///  string turns into a URL with a path segment missing, which reads as a server bug and is
    ///  diagnosed by nobody; one that stays as <c>{{typo}}</c> is a question the user can answer at
    ///  a glance.
    ///
    ///  The second is that resolving never writes back. What is saved, shown and exported keeps its
    ///  braces, so the panel is not a place a token is displayed and a request shared with a
    ///  colleague carries the template rather than this machine's credentials.
    /// </remarks>
    public class HttpEnvironmentTests
    {
        #region Finding what is referenced

        [Fact]
        public void APlainStringHasNothingToResolve()
        {
            Assert.False(HttpVariables.Any("https://example.com/health"));
            Assert.Empty(HttpVariables.Used("https://example.com/health"));
        }

        [Theory]
        [InlineData("{{baseUrl}}/health", "baseUrl")]
        [InlineData("{{ baseUrl }}/health", "baseUrl")]
        [InlineData("Bearer {{token}}", "token")]
        [InlineData("{{api.host}}", "api.host")]
        [InlineData("{{my-var}}", "my-var")]
        public void AReferenceIsFoundHoweverItIsSpaced(string text, string expected)
        {
            Assert.True(HttpVariables.Any(text));
            Assert.Equal([expected], HttpVariables.Used(text));
        }

        /// <summary>The same name twice is one name: this drives a "what is missing" list.</summary>
        [Fact]
        public void ANameUsedTwiceIsListedOnce()
        {
            Assert.Equal(["a"], HttpVariables.Used("{{a}}/x/{{a}}"));
        }

        [Fact]
        public void SeveralNamesComeBackInTheOrderTheyAppear()
        {
            Assert.Equal(["host", "version", "id"], HttpVariables.Used("{{host}}/{{version}}/thing/{{id}}"));
        }

        /// <summary>
        ///  The command parameters' own syntax is left alone.
        /// </summary>
        /// <remarks>
        ///  Both live in one settings file. A pattern loose enough to match both would make each
        ///  one's behaviour depend on which ran first, which is not a thing anyone could predict.
        /// </remarks>
        [Fact]
        public void TheSecretTokenSyntaxIsNotAVariable()
        {
            Assert.Empty(HttpVariables.Used("{{secret:API_KEY}}"));
            Assert.False(HttpVariables.Any("{{secret:API_KEY}}"));
        }

        #endregion

        #region Resolving

        [Fact]
        public void AKnownNameIsReplaced()
        {
            HttpEnvironment local = Environment("Local", ("baseUrl", "http://localhost:5000"));

            Assert.Equal(
                "http://localhost:5000/health",
                HttpVariables.Resolve("{{baseUrl}}/health", local));
        }

        /// <summary>
        ///  The rule that keeps a mistake diagnosable.
        /// </summary>
        [Fact]
        public void AnUnknownNameIsLeftExactlyAsWritten()
        {
            HttpEnvironment local = Environment("Local", ("baseUrl", "http://localhost:5000"));

            Assert.Equal("{{typo}}/health", HttpVariables.Resolve("{{typo}}/health", local));
        }

        [Fact]
        public void AValueThatIsSwitchedOffDoesNotResolve()
        {
            HttpEnvironment local = new()
            {
                Name = "Local",
                Values = [new EnvironmentValue { Name = "baseUrl", Value = "http://x", Enabled = false }],
            };

            Assert.Equal("{{baseUrl}}/health", HttpVariables.Resolve("{{baseUrl}}/health", local));
        }

        /// <summary>No environment set resolves nothing, rather than erroring about it.</summary>
        [Fact]
        public void WithNoEnvironmentTheTextIsUnchanged()
        {
            Assert.Equal("{{baseUrl}}/health", HttpVariables.Resolve("{{baseUrl}}/health", null));
        }

        /// <summary>
        ///  One pass, not many. A value containing braces is used as written.
        /// </summary>
        /// <remarks>
        ///  Resolving the result again would invite both a cycle and a way to smuggle one
        ///  variable's contents into another's position.
        /// </remarks>
        [Fact]
        public void ResolvingDoesNotRecurse()
        {
            HttpEnvironment env = Environment("E", ("a", "{{b}}"), ("b", "final"));

            Assert.Equal("{{b}}", HttpVariables.Resolve("{{a}}", env));
        }

        [Fact]
        public void SeveralReferencesInOneStringAreAllReplaced()
        {
            HttpEnvironment env = Environment("E", ("host", "example.com"), ("v", "v2"));

            Assert.Equal("https://example.com/v2/thing", HttpVariables.Resolve("https://{{host}}/{{v}}/thing", env));
        }

        #endregion

        #region A resolved request is a copy

        /// <summary>
        ///  The guarantee that stops a token reaching settings.json.
        /// </summary>
        /// <remarks>
        ///  Resolving in place would write the resolved values back into the saved request the
        ///  moment anything persisted it. The original has to come out of this untouched.
        /// </remarks>
        [Fact]
        public void TheOriginalRequestKeepsItsBraces()
        {
            HttpEnvironment env = Environment("Prod", ("baseUrl", "https://api.example.com"), ("token", "abc123"));

            HttpRequest saved = new()
            {
                Name = "Health",
                Method = "GET",
                Url = "{{baseUrl}}/health",
                Body = "{\"at\":\"{{baseUrl}}\"}",
                Headers = [new HeaderLine { Name = "Authorization", Value = "Bearer {{token}}" }],
            };

            HttpRequest sent = HttpVariables.Resolved(saved, env);

            Assert.Equal("{{baseUrl}}/health", saved.Url);
            Assert.Equal("{\"at\":\"{{baseUrl}}\"}", saved.Body);
            Assert.Equal("Bearer {{token}}", saved.Headers[0].Value);

            Assert.Equal("https://api.example.com/health", sent.Url);
            Assert.Equal("{\"at\":\"https://api.example.com\"}", sent.Body);
            Assert.Equal("Bearer abc123", sent.Headers[0].Value);
        }

        /// <summary>The flag and the group travel with the copy, so nothing is lost by sending.</summary>
        [Fact]
        public void TheCopyKeepsWhatTheRequestWasFiledAs()
        {
            HttpEnvironment env = Environment("E", ("a", "1"));

            HttpRequest saved = new()
            {
                Name = "Thing",
                Url = "{{a}}",
                Colour = RequestColour.Amber,
                Group = "Billing",
            };

            HttpRequest sent = HttpVariables.Resolved(saved, env);

            Assert.Equal(RequestColour.Amber, sent.Colour);
            Assert.Equal("Billing", sent.Group);
            Assert.Equal("Thing", sent.Name);
        }

        [Fact]
        public void WithNoEnvironmentTheRequestIsHandedBackAsItIs()
        {
            HttpRequest saved = new() { Url = "{{a}}/x" };

            Assert.Equal("{{a}}/x", HttpVariables.Resolved(saved, null).Url);
        }

        #endregion

        #region What is missing

        /// <summary>
        ///  The warning before a send. The usual failure is a misspelling, or the right variable
        ///  defined in the other environment - both cheap to say and expensive to read off a 404.
        /// </summary>
        [Fact]
        public void AReferenceTheEnvironmentCannotSupplyIsReported()
        {
            HttpEnvironment env = Environment("Local", ("baseUrl", "http://localhost"));

            HttpRequest request = new()
            {
                Url = "{{baseUrl}}/{{version}}/health",
                Headers = [new HeaderLine { Name = "X-Key", Value = "{{apiKey}}" }],
            };

            Assert.Equal(["version", "apiKey"], HttpVariables.Missing(request, env));
        }

        [Fact]
        public void ARequestWithEverythingSuppliedIsMissingNothing()
        {
            HttpEnvironment env = Environment("Local", ("baseUrl", "http://localhost"));

            Assert.Empty(HttpVariables.Missing(new HttpRequest { Url = "{{baseUrl}}/health" }, env));
        }

        [Fact]
        public void ARequestWithNoVariablesIsMissingNothingEvenWithNoEnvironment()
        {
            Assert.Empty(HttpVariables.Missing(new HttpRequest { Url = "https://example.com" }, null));
        }

        /// <summary>A header that is switched off is not sent, so what it refers to is not wanted.</summary>
        [Fact]
        public void ASwitchedOffHeaderDoesNotMakeAVariableMissing()
        {
            HttpRequest request = new()
            {
                Url = "https://example.com",
                Headers = [new HeaderLine { Name = "X-Key", Value = "{{apiKey}}", Enabled = false }],
            };

            Assert.Empty(HttpVariables.Missing(request, null));
        }

        #endregion

        #region Secrets

        /// <summary>
        ///  A secret's name is qualified by its environment.
        /// </summary>
        /// <remarks>
        ///  Without this, a Staging token and a Production token both called API_KEY are one entry
        ///  in the vault, and whichever was saved last is what both environments send. That failure
        ///  is silent and points the wrong credentials at the wrong server.
        /// </remarks>
        [Fact]
        public void TwoEnvironmentsCanHoldASecretOfTheSameName()
        {
            Assert.NotEqual(
                EnvironmentValue.VaultName("Staging", "API_KEY"),
                EnvironmentValue.VaultName("Production", "API_KEY"));
        }

        [Fact]
        public void AVaultNameIsStableAcrossSpacing()
        {
            Assert.Equal(
                EnvironmentValue.VaultName("Staging", "API_KEY"),
                EnvironmentValue.VaultName("  Staging  ", "  API_KEY  "));
        }

        /// <summary>
        ///  A secret with nothing behind it in the vault stays visibly unresolved.
        /// </summary>
        /// <remarks>
        ///  The same rule as an unknown name, and for the same reason: sending an empty
        ///  Authorization header produces a 401 that looks like an expired token, where a literal
        ///  <c>{{token}}</c> in the header says exactly what went wrong.
        /// </remarks>
        [Fact]
        public void ASecretMissingFromTheVaultIsLeftAsWritten()
        {
            HttpEnvironment env = new()
            {
                Name = "NoSuchEnvironmentForTests",
                Values = [new EnvironmentValue { Name = "token", IsSecret = true }],
            };

            Assert.Equal("Bearer {{token}}", HttpVariables.Resolve("Bearer {{token}}", env));
        }

        /// <summary>
        ///  A secret variable holds no value in the settings file itself.
        /// </summary>
        [Fact]
        public void ASecretCarriesNoValueOfItsOwn()
        {
            EnvironmentValue secret = new() { Name = "token", IsSecret = true };

            Assert.Empty(secret.Value);
        }

        #endregion

        #region Colours

        [Fact]
        public void TheKnownColoursAreTheOnesOffered()
        {
            Assert.True(RequestColour.Known(RequestColour.Red));
            Assert.True(RequestColour.Known(RequestColour.None));
            Assert.False(RequestColour.Known("chartreuse"));
        }

        /// <summary>None comes first, so "no flag" is the top of the menu rather than buried.</summary>
        [Fact]
        public void NoColourIsTheFirstChoice()
        {
            Assert.Equal(RequestColour.None, RequestColour.All[0]);
        }

        #endregion

        /// <summary>An environment of plain, enabled values.</summary>
        private static HttpEnvironment Environment(string name, params (string Name, string Value)[] values) =>
            new()
            {
                Name = name,
                Values = [.. values.Select(pair => new EnvironmentValue { Name = pair.Name, Value = pair.Value })],
            };
    }
}
