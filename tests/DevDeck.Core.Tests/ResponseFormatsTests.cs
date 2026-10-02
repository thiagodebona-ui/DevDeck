using System.Text;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  What a response really is when its header says nothing useful, how it is previewed, and
    ///  what it is called when it is saved.
    /// </summary>
    public class ResponseFormatsTests
    {
        private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

        [Theory]
        [InlineData("application/octet-stream")]
        [InlineData("")]
        [InlineData("binary/octet-stream")]
        [InlineData("application/force-download")]
        public void AGenericLabelGivesWayToTheBytes(string media)
        {
            Assert.Equal("image/png", ResponseFormats.Effective(media, Png));
        }

        [Fact]
        public void ASpecificLabelIsKept()
        {
            Assert.Equal("image/jpeg", ResponseFormats.Effective("image/jpeg", Png));
        }

        [Fact]
        public void ThePdfMagicIsRead()
        {
            Assert.Equal("application/pdf", ResponseFormats.Sniff(Encoding.ASCII.GetBytes("%PDF-1.7\n...")));
        }

        [Theory]
        [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
        [InlineData("<?xml version=\"1.0\"?>\n<!-- logo -->\n<svg viewBox=\"0 0 1 1\"></svg>")]
        public void AnSvgIsRecognisedByItsRoot(string text)
        {
            Assert.True(ResponseFormats.IsSvg(text));
            Assert.Equal("image/svg+xml", ResponseFormats.Sniff(Encoding.UTF8.GetBytes(text)));
        }

        [Fact]
        public void XmlWithAnSvgInsideIsNotAnSvg()
        {
            Assert.False(ResponseFormats.IsSvg("<feed><entry><svg/></entry></feed>"));
        }

        [Fact]
        public void AnSvgLabelledAsSvgIsPreviewedAsOne()
        {
            Assert.Equal(
                ResponsePreview.Svg,
                ResponseFormats.PreviewFor("image/svg+xml", BodyKind.Xml, "<svg/>", Encoding.UTF8.GetBytes("<svg/>")));
        }

        [Theory]
        [InlineData("application/pdf", (int)BodyKind.Binary, (int)ResponsePreview.Pdf)]
        [InlineData("text/html", (int)BodyKind.Html, (int)ResponsePreview.Html)]
        [InlineData("text/markdown", (int)BodyKind.Plain, (int)ResponsePreview.Markdown)]
        [InlineData("text/csv", (int)BodyKind.Plain, (int)ResponsePreview.Table)]
        [InlineData("application/json", (int)BodyKind.Json, (int)ResponsePreview.Tree)]
        [InlineData("application/xml", (int)BodyKind.Xml, (int)ResponsePreview.None)]
        public void EachKindGetsItsPreview(string media, int kind, int expected)
        {
            Assert.Equal((ResponsePreview)expected, ResponseFormats.PreviewFor(media, (BodyKind)kind, "a", [(byte)'a']));
        }

        [Fact]
        public void TheServersFileNameWins()
        {
            Assert.Equal("report-2026.pdf", Names.For("https://x.test/download?id=7", "application/pdf", BodyKind.Binary, "report-2026.pdf"));
        }

        [Fact]
        public void AServerNameWithoutAnExtensionGetsOne()
        {
            Assert.Equal("logo.png", Names.For("https://x.test/a", "image/png", BodyKind.Image, "logo"));
        }

        [Fact]
        public void AStaleExtensionInTheUrlIsReplaced()
        {
            Assert.Equal("avatar.png", Names.For("https://x.test/u/avatar.jpg", "image/png", BodyKind.Image));
        }

        [Fact]
        public void APathIsNamedWithTheMediaTypesExtension()
        {
            Assert.Equal("chart.png", Names.For("https://x.test/chart", "image/png", BodyKind.Image));
            Assert.Equal("users.json", Names.For("https://x.test/users?page=2", "application/json", BodyKind.Json));
            Assert.Equal(
                "sheet.xlsx",
                Names.For("https://x.test/sheet", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", BodyKind.Binary));
        }

        [Fact]
        public void ADispositionNameLosesAnyPath()
        {
            Assert.Equal("evil.txt", ResponseFormats.DispositionName(null, "\"..\\..\\evil.txt\""));
            Assert.Equal("café.pdf", ResponseFormats.DispositionName("café.pdf", "cafe.pdf"));
        }

        [Fact]
        public void TheHexDumpShowsOffsetBytesAndText()
        {
            string dump = ResponseFormats.Hex(Encoding.ASCII.GetBytes("Hello"));

            Assert.StartsWith("00000000  48 65 6C 6C 6F", dump);
            Assert.Contains("Hello", dump);
        }

        [Fact]
        public void APageIsReadAsItsWords()
        {
            string text = ResponseFormats.ReadableHtml(
                "<html><head><title>Oops</title><style>p{}</style></head><body><h1>Error 500</h1><p>Something &amp; else</p><script>x()</script></body></html>");

            Assert.StartsWith("Oops", text);
            Assert.Contains("Error 500", text);
            Assert.Contains("Something & else", text);
            Assert.DoesNotContain("x()", text);
            Assert.DoesNotContain("p{}", text);
        }

        [Fact]
        public void ACsvIsLaidOutInColumns()
        {
            string table = ResponseFormats.Table("name,city\nAna,\"Porto, Alegre\"\nBo,Rio\n", ',');
            string[] lines = table.Split('\n');

            Assert.Equal(4, lines.Length);
            Assert.Contains("Porto, Alegre", lines[2]);
            Assert.Equal(lines[0].IndexOf('│'), lines[2].IndexOf('│'));
        }
    }
}
