using System.Text;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>The toolbox's JSON tree, and the token parts it is built from for a JWT.</summary>
    [Collection("Strings")]
    public class JsonTreeTests
    {
        [Fact]
        public void ADocumentBecomesNodes()
        {
            JsonNode root = JsonTree.Build("{\"name\":\"Ana\",\"age\":3,\"tags\":[\"a\",\"b\"],\"ok\":true,\"none\":null}")!;

            Assert.Equal("{ 5 }", root.Value);
            Assert.Equal(["name", "age", "tags", "ok", "none"], root.Children.Select(child => child.Key));
            Assert.Equal("\"Ana\"", root.Children[0].Value);
            Assert.True(root.Children[1].IsNumber);
            Assert.Equal("[ 2 ]", root.Children[2].Value);
            Assert.Equal("[1]", root.Children[2].Children[1].Key);
            Assert.True(root.Children[3].IsLiteral);
            Assert.Equal(JsonNodeKind.Null, root.Children[4].Kind);
        }

        [Fact]
        public void TextThatIsNotJsonHasNoTree()
        {
            Assert.Null(JsonTree.Build("{\"half\":"));
            Assert.Null(JsonTree.Build("   "));
        }

        [Fact]
        public void AHugeArrayIsCutWithAMarker()
        {
            string big = "[" + string.Join(",", Enumerable.Range(0, JsonTree.MostNodes + 50)) + "]";

            JsonNode root = JsonTree.Build(big)!;

            Assert.True(root.Children.Count <= JsonTree.MostNodes + 1);
            Assert.Equal(JsonNodeKind.More, root.Children[^1].Kind);
        }

        [Fact]
        public void ATokensPartsBecomeOneDocument()
        {
            static string Part(string json) =>
                Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

            string token = $"{Part("{\"alg\":\"HS256\"}")}.{Part("{\"sub\":\"42\"}")}.sig";

            JsonNode root = JsonTree.Build(TextTools.JwtAsJson(token))!;

            Assert.Equal(["header", "payload"], root.Children.Select(child => child.Key));
            Assert.Equal("\"42\"", root.Children[1].Children[0].Value);
        }

        [Fact]
        public void NotATokenGivesNothing()
        {
            Assert.Equal(string.Empty, TextTools.JwtAsJson("not a token"));
        }
    }
}
