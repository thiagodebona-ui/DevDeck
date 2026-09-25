using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The rule that decides what the clipboard history refuses to keep.
    /// </summary>
    /// <remarks>
    ///  Tested harder than its size suggests, because it is the only thing standing between a
    ///  convenience feature and a file full of the user's passwords. The false-negative cases are
    ///  asserted too: the heuristic's limits are part of its contract, and a test that pretended
    ///  otherwise would be the wrong kind of reassuring.
    /// </remarks>
    public class ClipTests
    {
        [Theory]
        [InlineData("ghp_16CharactersOfTokenHere0123456789")]
        [InlineData("github_pat_11ABCDEFG0abcdefghijklmn")]
        [InlineData("sk-proj-aBcDeFgHiJkLmNoPqRsTuVwXyZ")]
        [InlineData("xoxb-123456789012-1234567890123-abcdefghijklmnop")]
        [InlineData("AKIAIOSFODNN7EXAMPLE")]
        [InlineData("AIzaSyD-0123456789abcdefghijklmnopqrstu")]
        [InlineData("glpat-abcdefgh12345678ijkl")]
        [InlineData("npm_aBcDeFgHiJkLmNoPqRsTuVwXyZ0123456789")]
        public void RefusesARecognisedToken(string token) => Assert.True(Clips.Sensitive(token));

        [Fact]
        public void RefusesAJwt()
        {
            Assert.True(Clips.Sensitive(
                "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxIn0.dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));
        }

        [Fact]
        public void RefusesWhatAPasswordManagerPastes()
        {
            // Mixed classes, no spaces, password length. The shape, not the contents.
            Assert.True(Clips.Sensitive("xK9#mQ2$vL7pR4wZ"));
            Assert.True(Clips.Sensitive("Tr0ub4dor&3xyzab"));
        }

        [Theory]
        [InlineData("git status")]
        [InlineData("https://github.com/owner/repo/pull/1234")]
        [InlineData("C:\\Users\\someone\\Documents\\notes.txt")]
        [InlineData("550e8400-e29b-41d4-a716-446655440000")]
        [InlineData("d41d8cd98f00b204e9800998ecf8427e")]
        [InlineData("short")]
        public void KeepsThingsThatAreNotCredentials(string text) =>
            Assert.False(Clips.Sensitive(text));

        [Fact]
        public void AUuidAndAHashPassBecauseTheyHaveTooFewCharacterClasses()
        {
            // Stated as a design point rather than an accident: an id is the single most copied
            // thing in this job, and dropping every id to catch a few more passwords would make the
            // feature useless in exchange for very little.
            Assert.False(Clips.Sensitive("a3f5c9e18b2d4067af13c8e9b2d40671"));
            Assert.False(Clips.Sensitive("550E8400E29B41D4A716446655440000"));
        }

        [Fact]
        public void AMultiLinePasteIsNeverTreatedAsACredential()
        {
            // Code, logs and stack traces are the bulk of what this keeps, and a heuristic that ate
            // them would leave nothing behind.
            Assert.False(Clips.Sensitive("password=xK9#mQ2$vL7p\nhost=localhost"));
        }

        [Fact]
        public void AnOrdinaryPassphraseGetsThroughAndThatIsKnown()
        {
            // The documented limit of the heuristic. If this ever starts failing the rule has been
            // tightened, which is a decision to make deliberately rather than discover here.
            Assert.False(Clips.Sensitive("correct horse battery staple"));
        }

        [Fact]
        public void EmptyAndBlankAreNotSensitiveButAreNotKeptEither()
        {
            Assert.False(Clips.Sensitive(string.Empty));
            Assert.False(Clips.Sensitive("   "));
        }

        [Fact]
        public void TheDigestChangesWithTheTextAndIsStableWithoutIt()
        {
            Assert.Equal(Clips.Digest("abc"), Clips.Digest("abc"));
            Assert.NotEqual(Clips.Digest("abc"), Clips.Digest("abd"));
            Assert.Equal(string.Empty, Clips.Digest(null));
        }

        [Fact]
        public void PreviewTakesTheFirstMeaningfulLine()
        {
            Clip clip = new() { Text = "\n\n  git push --force-with-lease  \nand more", At = DateTime.Now };

            Assert.Equal("git push --force-with-lease", clip.Preview);
            Assert.True(clip.IsMultiline);
        }

        [Fact]
        public void PreviewIsCutRatherThanWrapped()
        {
            Clip clip = new() { Text = new string('x', 300), At = DateTime.Now };

            Assert.Equal(121, clip.Preview.Length);
            Assert.EndsWith("…", clip.Preview);
        }

        #region Commands, which carry credentials and are worth keeping anyway

        /// <summary>
        ///  The case the whole exemption exists for: a curl copied out of a browser's devtools. It
        ///  arrives on one line, it is stuffed with a bearer token and a cookie jar, and the HTTP
        ///  panel reads one straight back into a request - so dropping it for containing a
        ///  credential would be refusing the payload because of the envelope.
        /// </summary>
        [Fact]
        public void ACurlFromDevtoolsIsKept()
        {
            string curl =
                "curl --url 'https://example.com/api/thing?q=1' "
                + "-H 'accept: */*' "
                + "-H 'authorization: Bearer header.payload.signature' "
                + "-b 'first=one; second=two; third=three'";

            Assert.False(Clips.Sensitive(curl));
        }

        [Theory]
        [InlineData("wget https://example.com/file.tar.gz")]
        [InlineData("git clone https://token@example.com/repo.git")]
        [InlineData("docker run -e SECRET=abc123 image")]
        [InlineData("ssh -i key.pem user@example.com")]
        public void OtherCommandsThatCarryCredentialsAreKeptToo(string command) =>
            Assert.False(Clips.Sensitive(command));

        /// <summary>
        ///  The exemption is narrow on purpose. A token that merely happens to begin with the
        ///  letters of a command is still a token, and is not waved through.
        /// </summary>
        [Theory]
        [InlineData("curlyBrace#9xKmQ2vL")]
        [InlineData("sshKeyPass#2024xyz")]
        public void ATokenThatMerelyLooksLikeACommandIsStillRefused(string token) =>
            Assert.True(Clips.Sensitive(token));

        /// <summary>A bare token on its own is exactly what the rule is for, exemption or not.</summary>
        [Fact]
        public void ABareTokenIsStillRefused()
        {
            Assert.True(Clips.Sensitive("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.c2lnbmF0dXJl"));
            Assert.True(Clips.Sensitive("ghp_16CharactersOfTokenHere0123456789"));
        }

        #endregion

        #region Big clips

        /// <summary>
        ///  A devtools curl is routinely tens of thousands of characters - cookies and a bearer
        ///  token see to that - and it is precisely what this history exists to hang on to.
        /// </summary>
        [Fact]
        public void ALargeCurlIsRecordedRatherThanDropped()
        {
            string curl = "curl --url 'https://example.com/api' -b '"
                + string.Join("; ", Enumerable.Range(0, 400).Select(i => $"cookie{i}={new string('x', 60)}"))
                + "'";

            Assert.True(curl.Length > 20_000);
            Assert.False(Clips.Sensitive(curl));
        }

        /// <summary>
        ///  The tooltip shows the head of a large clip and says how much was left out, which is
        ///  what tells two similar-looking curls apart without opening either.
        /// </summary>
        [Fact]
        public void PeekShowsTheHeadOfALargeClipAndSaysWhatWasLeftOut()
        {
            Clip clip = new() { Text = new string('x', 5000), At = DateTime.Now };

            Assert.StartsWith(new string('x', 2000), clip.Peek);
            Assert.Contains("3,000 more characters", clip.Peek);
        }

        /// <summary>A clip that fits is shown whole - no ellipsis on something already complete.</summary>
        [Fact]
        public void PeekLeavesASmallClipAlone()
        {
            Clip clip = new() { Text = "git push --force-with-lease", At = DateTime.Now };

            Assert.Equal("git push --force-with-lease", clip.Peek);
        }

        #endregion

        #region A clip that is a link

        /// <summary>
        ///  The case the feature exists for: a copied address is offered as one.
        /// </summary>
        [Theory]
        [InlineData("https://example.com")]
        [InlineData("http://example.com")]
        [InlineData("https://example.com/a/b?c=d&e=f#g")]
        [InlineData("http://localhost:5173/health")]
        public void AnAddressOnItsOwnIsALink(string copied)
        {
            Clip clip = new() { Text = copied, At = DateTime.Now };

            Assert.True(clip.IsLink);
            Assert.Equal(copied, clip.Link);
        }

        /// <summary>Whitespace around it is still one address, and is trimmed off the link.</summary>
        [Fact]
        public void SurroundingWhitespaceDoesNotStopItBeingALink()
        {
            Clip clip = new() { Text = "  https://example.com/x \r\n", At = DateTime.Now };

            Assert.True(clip.IsLink);
            Assert.Equal("https://example.com/x", clip.Link);
        }

        /// <summary>
        ///  Text that merely contains an address is not a link.
        /// </summary>
        /// <remarks>
        ///  The strict rule stated as a test, because the loose one is the tempting mistake: a
        ///  stack trace, a log line and a curl command all have a URL in them, and a history where
        ///  a click might follow something buried in the middle of a body of text is one nobody
        ///  can predict.
        /// </remarks>
        [Theory]
        [InlineData("see https://example.com for details")]
        [InlineData("curl https://example.com -H 'a: b'")]
        [InlineData("at Thing.Do() in https://example.com/x:line 4")]
        [InlineData("https://example.com https://example.org")]
        public void TextContainingAnAddressIsNotALink(string copied)
        {
            Clip clip = new() { Text = copied, At = DateTime.Now };

            Assert.False(clip.IsLink);
            Assert.Null(clip.Link);
        }

        /// <summary>
        ///  Only http and https, which is a security boundary rather than a tidiness rule.
        /// </summary>
        /// <remarks>
        ///  The clip is arbitrary text that arrived on the clipboard and the link ends in a shell
        ///  execute. Every scheme below does something other than open a page - a local file, a
        ///  script, a mail client, or whatever application claimed a custom scheme - and none of
        ///  them are what a person means when they click a link.
        /// </remarks>
        [Theory]
        [InlineData("file:///C:/Windows/System32/calc.exe")]
        [InlineData("javascript:alert(1)")]
        [InlineData("data:text/html,<script>alert(1)</script>")]
        [InlineData("vbscript:msgbox(1)")]
        [InlineData("ftp://example.com/x")]
        [InlineData("mailto:someone@example.com")]
        [InlineData("devdeck://run/thing")]
        [InlineData("ms-msdt:/id")]
        public void OnlyWebSchemesAreFollowed(string copied)
        {
            Clip clip = new() { Text = copied, At = DateTime.Now };

            Assert.False(clip.IsLink);
            Assert.Null(clip.Link);
        }

        /// <summary>A relative path is not an address, however much it looks like one.</summary>
        [Theory]
        [InlineData("example.com")]
        [InlineData("/usr/local/bin")]
        [InlineData("C:\\Users\\someone")]
        [InlineData("")]
        [InlineData("   ")]
        public void SomethingThatIsNotAnAbsoluteAddressIsNotALink(string copied)
        {
            Clip clip = new() { Text = copied, At = DateTime.Now };

            Assert.False(clip.IsLink);
        }

        /// <summary>
        ///  An absurdly long one is refused rather than handed to the shell.
        /// </summary>
        [Fact]
        public void AnOverlongAddressIsNotALink()
        {
            Clip clip = new() { Text = "https://example.com/" + new string('x', 4000), At = DateTime.Now };

            Assert.False(clip.IsLink);
        }

        #endregion

    }
}
