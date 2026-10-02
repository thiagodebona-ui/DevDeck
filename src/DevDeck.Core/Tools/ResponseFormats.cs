using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>How a response can be shown other than as its own text.</summary>
    internal enum ResponsePreview
    {
        /// <summary>Nothing to draw: the source is all there is.</summary>
        None,

        /// <summary>A raster image - PNG, JPEG, GIF, WebP, BMP, ICO.</summary>
        Image,

        /// <summary>An SVG, drawn; its source is the XML.</summary>
        Svg,

        /// <summary>A PDF, drawn page by page.</summary>
        Pdf,

        /// <summary>A page, as its readable text, with the browser one click away.</summary>
        Html,

        Markdown,

        /// <summary>CSV or TSV, laid out as a table.</summary>
        Table,

        /// <summary>JSON, parsed into a tree to walk with the arrow keys.</summary>
        Tree,
    }

    /// <summary>
    ///  Works out what a response really is, and the second way of showing it.
    /// </summary>
    /// <remarks>
    ///  The content type is the server's claim and the bytes are the evidence. A storage bucket
    ///  serving every object as application/octet-stream, a download endpoint that says
    ///  application/force-download, a framework that labels everything text/plain - each of those
    ///  sends a PNG that the header alone would have described as "binary". The first few bytes of
    ///  every common format are fixed, so they are read whenever the header says nothing useful.
    /// </remarks>
    internal static class ResponseFormats
    {
        /// <summary>The labels that say "some bytes" and nothing about which.</summary>
        private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
        {
            string.Empty,
            "application/octet-stream",
            "binary/octet-stream",
            "application/binary",
            "application/x-binary",
            "application/download",
            "application/force-download",
            "application/x-download",
            "application/unknown",
            "text/plain",
        };

        /// <summary>
        ///  The media type to go by: the header's, unless it is generic and the bytes say better.
        /// </summary>
        /// <remarks>
        ///  Never overrides a specific header. A server that says image/png and sends a JPEG is
        ///  rare, and the image decoder copes with that on its own; second-guessing every label
        ///  would mean an XML document that happens to begin with an svg element stops being
        ///  shown as what its server said it was.
        /// </remarks>
        public static string Effective(string media, byte[] bytes)
        {
            string type = media.Trim();

            return Generic.Contains(type) && Sniff(bytes) is { Length: > 0 } sniffed ? sniffed : type;
        }

        /// <summary>The media type the first bytes give away, or empty when they give nothing away.</summary>
        public static string Sniff(byte[] bytes)
        {
            ReadOnlySpan<byte> head = bytes.AsSpan(0, Math.Min(bytes.Length, 512));

            if (Starts(head, [0x89, (byte)'P', (byte)'N', (byte)'G']))
            {
                return "image/png";
            }

            if (Starts(head, [0xFF, 0xD8, 0xFF]))
            {
                return "image/jpeg";
            }

            if (head.StartsWith("GIF8"u8))
            {
                return "image/gif";
            }

            if (head.Length >= 12 && head.StartsWith("RIFF"u8) && head[8..12].SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            if (head.StartsWith("BM"u8) && head.Length > 14)
            {
                return "image/bmp";
            }

            if (Starts(head, [0x00, 0x00, 0x01, 0x00]))
            {
                return "image/x-icon";
            }

            if (head.StartsWith("%PDF-"u8))
            {
                return "application/pdf";
            }

            if (Starts(head, [(byte)'P', (byte)'K', 0x03, 0x04]))
            {
                return "application/zip";
            }

            if (Starts(head, [0x1F, 0x8B]))
            {
                return "application/gzip";
            }

            if (head.Length >= 12 && head[4..8].SequenceEqual("ftyp"u8))
            {
                return "video/mp4";
            }

            if (head.StartsWith("ID3"u8))
            {
                return "audio/mpeg";
            }

            if (head.StartsWith("OggS"u8))
            {
                return "audio/ogg";
            }

            // Text formats, by their opening: only the ones whose opening is unambiguous.
            string text = Encoding.UTF8.GetString(head).TrimStart('﻿', ' ', '\t', '\r', '\n');

            if (IsSvg(text))
            {
                return "image/svg+xml";
            }

            return string.Empty;
        }

        private static bool Starts(ReadOnlySpan<byte> head, ReadOnlySpan<byte> magic) => head.StartsWith(magic);

        /// <summary>Whether a text body is an SVG document, prolog and comments allowed before it.</summary>
        public static bool IsSvg(string text)
        {
            string head = text.Length > 2048 ? text[..2048] : text;

            int svg = head.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);

            if (svg < 0)
            {
                return false;
            }

            // Anything before the root has to be prolog: a declaration, a doctype, comments.
            string before = Regex.Replace(head[..svg], @"<\?.*?\?>|<!--.*?-->|<!DOCTYPE[^>]*>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            return before.Trim().Length == 0;
        }

        /// <summary>Which preview fits a response, from its media type, kind and body.</summary>
        public static ResponsePreview PreviewFor(string media, BodyKind kind, string body, byte[] bytes)
        {
            if (kind == BodyKind.Image)
            {
                return ResponsePreview.Image;
            }

            if (media.Contains("svg", StringComparison.OrdinalIgnoreCase)
                || (kind is BodyKind.Xml or BodyKind.Plain && IsSvg(body)))
            {
                return ResponsePreview.Svg;
            }

            if (media.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
                || bytes.AsSpan().StartsWith("%PDF-"u8))
            {
                return ResponsePreview.Pdf;
            }

            if (kind == BodyKind.Html)
            {
                return ResponsePreview.Html;
            }

            if (media.EndsWith("/markdown", StringComparison.OrdinalIgnoreCase)
                || media.EndsWith("/x-markdown", StringComparison.OrdinalIgnoreCase))
            {
                return ResponsePreview.Markdown;
            }

            if (kind == BodyKind.Json)
            {
                return ResponsePreview.Tree;
            }

            if (media.EndsWith("/csv", StringComparison.OrdinalIgnoreCase)
                || media.EndsWith("/tab-separated-values", StringComparison.OrdinalIgnoreCase))
            {
                return ResponsePreview.Table;
            }

            return ResponsePreview.None;
        }

        /// <summary>
        ///  The file name the server suggested, from Content-Disposition, made safe; or empty.
        /// </summary>
        /// <remarks>
        ///  The extended filename* wins over the plain one when both are there, as RFC 6266 says:
        ///  it is the one that can carry a name that is not ASCII, and a server that bothers to
        ///  send it means it.
        /// </remarks>
        public static string DispositionName(string? fileNameStar, string? fileName)
        {
            string chosen = (fileNameStar is { Length: > 0 } ? fileNameStar : fileName ?? string.Empty).Trim().Trim('"');

            // Only the name: a path in there is either a server bug or an attempt to write
            // somewhere other than where the user is about to choose.
            chosen = chosen.Replace('\\', '/');
            chosen = chosen[(chosen.LastIndexOf('/') + 1)..];

            return Names.Safe(chosen);
        }

        /// <summary>
        ///  The bytes as a hex dump: offset, sixteen bytes, and the printable ones as text.
        /// </summary>
        /// <remarks>
        ///  Bounded, because a dump is three times the size of what it dumps and a download of a
        ///  hundred megabytes is not read this way. The head is what tells you what a file is.
        /// </remarks>
        public static string Hex(byte[] bytes, int most = 64 * 1024)
        {
            int shown = Math.Min(bytes.Length, most);
            StringBuilder dump = new(shown * 4 + 128);

            for (int row = 0; row < shown; row += 16)
            {
                dump.Append(row.ToString("X8", CultureInfo.InvariantCulture)).Append("  ");

                for (int at = 0; at < 16; at++)
                {
                    if (row + at < shown)
                    {
                        dump.Append(bytes[row + at].ToString("X2", CultureInfo.InvariantCulture)).Append(' ');
                    }
                    else
                    {
                        dump.Append("   ");
                    }

                    if (at == 7)
                    {
                        dump.Append(' ');
                    }
                }

                dump.Append(' ');

                for (int at = 0; at < 16 && row + at < shown; at++)
                {
                    byte b = bytes[row + at];
                    dump.Append(b is >= 0x20 and < 0x7F ? (char)b : '.');
                }

                dump.Append('\n');
            }

            if (bytes.Length > shown)
            {
                dump.Append(CultureInfo.InvariantCulture, $"\n… {bytes.Length - shown:N0} more bytes not shown.");
            }

            return dump.ToString();
        }

        /// <summary>
        ///  A page as the text a person would read on it.
        /// </summary>
        /// <remarks>
        ///  Not a renderer, and not trying to be one: there is no browser engine in the app. What a
        ///  developer wants from an HTML reply is nearly always the words in it - the error message
        ///  on a 500 page, the text of a status page - and those survive having the markup taken
        ///  away. Scripts and styles go entirely, block elements become line breaks, and entities
        ///  are decoded. The browser is one button away for the rest.
        /// </remarks>
        public static string ReadableHtml(string html)
        {
            string text = Regex.Replace(html, @"<(script|style|noscript|template|svg|head)\b.*?</\1\s*>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<!--.*?-->", " ", RegexOptions.Singleline);

            // The title is in the head, which went above; it is worth keeping as the first line.
            string title = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase).Groups[1].Value;

            text = Regex.Replace(text, @"<li\b[^>]*>", "\n• ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<(br|hr)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</?(p|div|section|article|header|footer|main|nav|aside|h[1-6]|tr|table|ul|ol|pre|blockquote|form|fieldset|dl|dt|dd)\b[^>]*>", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"</t[dh]\s*>", "\t", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", string.Empty);
            text = WebUtility.HtmlDecode(text);

            StringBuilder readable = new();

            if (title.Trim().Length > 0)
            {
                readable.Append(WebUtility.HtmlDecode(Regex.Replace(title, @"\s+", " ").Trim())).Append("\n\n");
            }

            bool blank = true;

            foreach (string raw in text.Split('\n'))
            {
                string line = Regex.Replace(raw, @"[ \t ]+", " ").Trim();

                if (line.Length == 0)
                {
                    if (!blank)
                    {
                        readable.Append('\n');
                    }

                    blank = true;

                    continue;
                }

                readable.Append(line).Append('\n');
                blank = false;
            }

            return readable.ToString().Trim();
        }

        /// <summary>
        ///  CSV or TSV laid out in aligned columns, the header row underlined.
        /// </summary>
        /// <remarks>
        ///  Quoted fields are read properly - commas and doubled quotes inside them - because that
        ///  is where splitting on commas goes wrong, and a misaligned table is worse than raw text.
        ///  Cells are clipped so one long description does not push every other column off screen.
        /// </remarks>
        public static string Table(string text, char separator, int mostRows = 2000, int widest = 48)
        {
            List<List<string>> rows = [];

            foreach (List<string> row in Rows(text, separator))
            {
                if (rows.Count == mostRows)
                {
                    break;
                }

                rows.Add(row);
            }

            if (rows.Count == 0)
            {
                return string.Empty;
            }

            int columns = rows.Max(row => row.Count);
            int[] widths = new int[columns];

            foreach (List<string> row in rows)
            {
                for (int at = 0; at < row.Count; at++)
                {
                    widths[at] = Math.Max(widths[at], Math.Min(widest, row[at].Length));
                }
            }

            StringBuilder table = new();

            for (int index = 0; index < rows.Count; index++)
            {
                List<string> row = rows[index];

                for (int at = 0; at < columns; at++)
                {
                    string cell = at < row.Count ? row[at].Replace('\n', ' ').Replace('\r', ' ') : string.Empty;

                    if (cell.Length > widest)
                    {
                        cell = cell[..(widest - 1)] + "…";
                    }

                    table.Append(cell.PadRight(widths[at]));

                    if (at < columns - 1)
                    {
                        table.Append(" │ ");
                    }
                }

                table.Append('\n');

                if (index == 0 && rows.Count > 1)
                {
                    table.Append(string.Join("─┼─", widths.Select(width => new string('─', width)))).Append('\n');
                }
            }

            return table.ToString().TrimEnd();
        }

        private static IEnumerable<List<string>> Rows(string text, char separator)
        {
            List<string> row = [];
            StringBuilder cell = new();
            bool quoted = false;

            for (int at = 0; at < text.Length; at++)
            {
                char c = text[at];

                if (quoted)
                {
                    if (c == '"' && at + 1 < text.Length && text[at + 1] == '"')
                    {
                        cell.Append('"');
                        at++;
                    }
                    else if (c == '"')
                    {
                        quoted = false;
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                if (c == '"' && cell.Length == 0)
                {
                    quoted = true;
                }
                else if (c == separator)
                {
                    row.Add(cell.ToString());
                    cell.Clear();
                }
                else if (c == '\n')
                {
                    row.Add(cell.ToString().TrimEnd('\r'));
                    cell.Clear();

                    if (row.Count > 1 || row[0].Length > 0)
                    {
                        yield return row;
                    }

                    row = [];
                }
                else
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString().TrimEnd('\r'));
                yield return row;
            }
        }
    }
}
