using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevDeck.Core
{
    /// <summary>The outcome of a conversion: the text, or the reason there isn't any.</summary>
    /// <remarks>
    ///  A result rather than an exception because every one of these runs on each keystroke against
    ///  text that is usually half-typed. Half-typed JSON is not an error the user needs telling
    ///  about in a dialog; it is the normal state of the box while they are still typing, and the
    ///  only useful response is to say what is wrong, quietly, and wait.
    /// </remarks>
    internal readonly record struct ToolResult(string Text, string? Error)
    {
        public static ToolResult Ok(string text) => new(text, null);

        public static ToolResult Fail(string error) => new(string.Empty, error);

        public bool Failed => Error is not null;
    }

    /// <summary>
    ///  The conversions a developer keeps a browser tab open for.
    /// </summary>
    /// <remarks>
    ///  Every one of these is a thing people paste into a random website many times a week - and
    ///  the thing being pasted is, routinely, a token, a customer record or a production payload.
    ///  Doing it offline is the entire point: no request leaves the machine, so the question of who
    ///  logged what never arises.
    ///
    ///  Pure functions over strings, deliberately: no UI, no files, no clock beyond the one the
    ///  timestamp tools ask for, which is what lets every one of them be tested directly.
    /// </remarks>
    internal static partial class TextTools
    {
        /// <summary>Pretty-prints JSON, or says where it stopped making sense.</summary>
        public static ToolResult FormatJson(string input, bool indented = true)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ToolResult.Ok(string.Empty);
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(input, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                });

                return ToolResult.Ok(JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = indented,
                }));
            }
            catch (JsonException exception)
            {
                // The line and position are the useful part of the message and the part a plain
                // "invalid JSON" throws away.
                return ToolResult.Fail(exception.LineNumber is { } line
                    ? $"Line {line + 1}, position {exception.BytePositionInLine + 1}: {Trim(exception.Message)}"
                    : Trim(exception.Message));
            }
        }

        /// <summary>Collapses JSON onto one line.</summary>
        public static ToolResult MinifyJson(string input) => FormatJson(input, indented: false);

        /// <summary>Indents XML, which is the other half of the same daily job.</summary>
        public static ToolResult FormatXml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ToolResult.Ok(string.Empty);
            }

            try
            {
                System.Xml.Linq.XDocument document = System.Xml.Linq.XDocument.Parse(
                    input, System.Xml.Linq.LoadOptions.PreserveWhitespace);

                return ToolResult.Ok(document.ToString());
            }
            catch (System.Xml.XmlException exception)
            {
                return ToolResult.Fail($"Line {exception.LineNumber}, position {exception.LinePosition}: {Trim(exception.Message)}");
            }
        }

        /// <summary>
        ///  Re-indents JavaScript, which nearly always arrives minified.
        /// </summary>
        /// <remarks>
        ///  Not a parser, and not trying to be one. A real JavaScript formatter is a parser, a
        ///  printer, and a long tail of syntax that keeps growing; what is wanted here is only to
        ///  make a bundle readable enough to find the line you came for, and a brace-and-semicolon
        ///  re-indenter does that without a dependency.
        ///
        ///  What it does have to get right is where it is allowed to break a line at all, and that
        ///  is entirely about what a character is inside: a brace in a string, a template literal,
        ///  a comment or a regex literal has to be copied through untouched. Those four are tracked
        ///  and nothing else is. Operator spacing, line length, whether an else hugs its brace - all
        ///  left as they were found, because guessing there is how a formatter changes meaning.
        ///
        ///  The regex case is the awkward one. A slash is division or the start of a literal
        ///  depending on what came before it, which cannot be answered without a parser; the rule
        ///  used is the usual approximation - a slash after a value divides, a slash after an
        ///  operator or an opening bracket starts a literal. It is wrong on a few genuinely
        ///  ambiguous lines and right on the rest, and the cost of being wrong is an odd-looking
        ///  line rather than altered code, because nothing is ever deleted.
        /// </remarks>
        public static ToolResult FormatJavaScript(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ToolResult.Ok(string.Empty);
            }

            StringBuilder built = new(input.Length + (input.Length / 4));

            // The brackets still open, innermost last. A plain depth count is not enough: a comma
            // ends a line inside an object or an array and separates arguments inside a call, and
            // which one it is depends only on which bracket is the nearest open one.
            Stack<char> open = new();

            // The last non-whitespace character copied, which is all the regex-or-division guess
            // needs to make its decision.
            char previous = '\0';

            // Set after a closing brace and honoured before the next real character: what follows a
            // block belongs on its own line, but the ";" or ")" that closes the same statement has
            // to be written first, so the break cannot simply be taken on the spot.
            bool pending = false;

            for (int at = 0; at < input.Length; at++)
            {
                char c = input[at];

                if (char.IsWhiteSpace(c))
                {
                    // Runs of whitespace collapse to one space, and whitespace that would open a
                    // line is dropped - the indent belongs to this method.
                    if (!pending && built.Length > 0 && !AtLineStart(built) && built[^1] != ' ')
                    {
                        built.Append(' ');
                    }

                    continue;
                }

                if (pending)
                {
                    pending = false;

                    // Everything that can legally follow a block on the same line: the punctuation
                    // that closes the statement the block was part of.
                    if (c is not (')' or ']' or '}' or ',' or ';' or '.'))
                    {
                        Break(built, open.Count(bracket => bracket != '('));
                    }
                }

                if (c is '"' or '\'' or '`')
                {
                    int end = Literal(input, at, c);
                    built.Append(input, at, end - at + 1);
                    previous = c;
                    at = end;
                    continue;
                }

                if (c == '/' && at + 1 < input.Length && input[at + 1] is '/' or '*')
                {
                    int end = Comment(input, at);
                    built.Append(input, at, end - at + 1);
                    at = end;
                    Break(built, open.Count(bracket => bracket != '('));
                    previous = '\0';
                    continue;
                }

                if (c == '/' && StartsRegex(previous))
                {
                    int end = RegexLiteral(input, at);
                    built.Append(input, at, end - at + 1);
                    previous = '/';
                    at = end;
                    continue;
                }

                if (c == '(')
                {
                    open.Push(c);
                    built.Append(c);
                    previous = c;
                    continue;
                }

                if (c is '{' or '[')
                {
                    TrimEnd(built);
                    open.Push(c);
                    built.Append(c);

                    // An empty literal stays on its line. Breaking here and closing on the next
                    // character would turn every "{}" and "[]" in a bundle into two lines of
                    // nothing, which is most of what made the first attempt at this unreadable.
                    if (Next(input, at + 1) != Closer(c))
                    {
                        Break(built, open.Count(bracket => bracket != '('));
                    }

                    previous = c;
                    continue;
                }

                if (c is '}' or ']' or ')')
                {
                    if (open.Count > 0)
                    {
                        open.Pop();
                    }

                    TrimEnd(built);

                    if (c != ')' && !EndsWith(built, Opener(c)))
                    {
                        int indent = open.Count(bracket => bracket != '(');

                        if (AtLineStart(built))
                        {
                            Reindent(built, indent);
                        }
                        else
                        {
                            Break(built, indent);
                        }
                    }

                    built.Append(c);
                    previous = c;
                    pending = c == '}';
                    continue;
                }

                if (c == ';')
                {
                    TrimEnd(built);
                    built.Append(c);

                    // Not inside a for-header, where the semicolons separate rather than terminate
                    // and breaking on them is exactly what makes the output unreadable.
                    if (InFor(input, at))
                    {
                        built.Append(' ');
                    }
                    else
                    {
                        Break(built, open.Count(bracket => bracket != '('));
                    }

                    previous = c;
                    continue;
                }

                if (c == ',' && open.Count > 0 && open.Peek() != '(')
                {
                    TrimEnd(built);
                    built.Append(c);
                    Break(built, open.Count(bracket => bracket != '('));
                    previous = c;
                    continue;
                }

                built.Append(c);
                previous = c;
            }

            string[] lines = built.ToString().Split('\n');

            return ToolResult.Ok(string.Join(
                '\n',
                lines.Select(line => line.TrimEnd()).Where(line => line.Length > 0)).Trim());
        }

        /// <summary>The bracket that closes the given opener.</summary>
        private static char Closer(char opener) => opener == '{' ? '}' : ']';

        /// <summary>The bracket that opens the given closer.</summary>
        private static char Opener(char closer) => closer == '}' ? '{' : '[';

        /// <summary>The next non-whitespace character from <paramref name="at"/>, or nothing.</summary>
        private static char Next(string text, int at)
        {
            while (at < text.Length && char.IsWhiteSpace(text[at]))
            {
                at++;
            }

            return at < text.Length ? text[at] : '\0';
        }

        /// <summary>Whether the last character written is the given one.</summary>
        private static bool EndsWith(StringBuilder built, char c) =>
            built.Length > 0 && built[^1] == c;

        /// <summary>Starts a new line at the given depth.</summary>
        private static void Break(StringBuilder built, int depth) =>
            built.Append('\n').Append(' ', Math.Max(depth, 0) * 2);

        /// <summary>Whether nothing but indent has been written since the last newline.</summary>
        private static bool AtLineStart(StringBuilder built)
        {
            for (int at = built.Length - 1; at >= 0; at--)
            {
                if (built[at] == '\n')
                {
                    return true;
                }

                if (built[at] != ' ')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The index of the quote closing the one at <paramref name="start"/>.</summary>
        private static int Literal(string text, int start, char quote)
        {
            for (int at = start + 1; at < text.Length; at++)
            {
                if (text[at] == '\\')
                {
                    at++;
                    continue;
                }

                if (text[at] == quote)
                {
                    return at;
                }

                // An unterminated quote is a syntax error in everything but a template literal, and
                // stopping at the newline keeps one stray quote from swallowing the whole file.
                if (quote != '`' && text[at] == '\n')
                {
                    return at - 1;
                }
            }

            return text.Length - 1;
        }

        /// <summary>The last index of the comment beginning at <paramref name="start"/>.</summary>
        private static int Comment(string text, int start)
        {
            if (text[start + 1] == '/')
            {
                int line = text.IndexOf('\n', start);

                return line < 0 ? text.Length - 1 : line - 1;
            }

            int close = text.IndexOf("*/", start + 2, StringComparison.Ordinal);

            return close < 0 ? text.Length - 1 : close + 1;
        }

        /// <summary>The index of the slash closing a regex literal, its flags included.</summary>
        private static int RegexLiteral(string text, int start)
        {
            bool inClass = false;

            for (int at = start + 1; at < text.Length; at++)
            {
                char c = text[at];

                if (c == '\\')
                {
                    at++;
                    continue;
                }

                if (c == '[')
                {
                    inClass = true;
                }
                else if (c == ']')
                {
                    inClass = false;
                }
                else if (c == '/' && !inClass)
                {
                    // The flags belong to the literal: stopping at the slash would leave "gi" to be
                    // read as an identifier, and the next quote to be read as opening a string.
                    while (at + 1 < text.Length && char.IsLetter(text[at + 1]))
                    {
                        at++;
                    }

                    return at;
                }
                else if (c == '\n')
                {
                    return at - 1;
                }
            }

            return text.Length - 1;
        }

        /// <summary>Whether a slash here begins a regex literal rather than dividing.</summary>
        private static bool StartsRegex(char previous) =>
            previous is '\0' or '(' or ',' or '=' or ':' or '[' or '!' or '&' or '|' or '?'
                or '{' or '}' or ';' or '+' or '-' or '*' or '%' or '<' or '>' or '~' or '^';

        /// <summary>Whether the semicolon at <paramref name="at"/> is inside a for-header.</summary>
        /// <remarks>
        ///  Answered by walking back to the nearest unbalanced open bracket and reading the word in
        ///  front of it. Cheap, bounded, and right for every for loop anybody writes.
        /// </remarks>
        private static bool InFor(string text, int at)
        {
            int closed = 0;

            for (int back = at - 1; back >= 0 && at - back < 400; back--)
            {
                char c = text[back];

                if (c == ')')
                {
                    closed++;
                }
                else if (c == '(')
                {
                    if (closed > 0)
                    {
                        closed--;
                        continue;
                    }

                    int word = back;

                    while (word > 0 && char.IsWhiteSpace(text[word - 1]))
                    {
                        word--;
                    }

                    return word >= 3 && text.AsSpan(word - 3, 3).SequenceEqual("for");
                }
            }

            return false;
        }

        /// <summary>Drops trailing spaces, so a break never leaves one at the end of a line.</summary>
        private static void TrimEnd(StringBuilder built)
        {
            while (built.Length > 0 && (built[^1] == ' ' || built[^1] == '\t'))
            {
                built.Length--;
            }
        }

        /// <summary>Replaces the indent of the line in progress, for a bracket that closes at once.</summary>
        private static void Reindent(StringBuilder built, int depth)
        {
            while (built.Length > 0 && built[^1] == ' ')
            {
                built.Length--;
            }

            built.Append(' ', Math.Max(depth, 0) * 2);
        }

        public static ToolResult ToBase64(string input) =>
            ToolResult.Ok(Convert.ToBase64String(Encoding.UTF8.GetBytes(input)));

        /// <summary>
        ///  Decodes base64, accepting the URL-safe alphabet and missing padding.
        /// </summary>
        /// <remarks>
        ///  Both are normal in the wild: JWT parts are URL-safe and unpadded by specification, and
        ///  plenty of tools emit base64 with the padding stripped. Refusing those would mean the
        ///  tool failed on exactly the strings people most often need decoded.
        /// </remarks>
        public static ToolResult FromBase64(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ToolResult.Ok(string.Empty);
            }

            try
            {
                return ToolResult.Ok(Encoding.UTF8.GetString(DecodeBase64(input)));
            }
            catch (FormatException)
            {
                return ToolResult.Fail("Not valid base64.");
            }
        }

        private static byte[] DecodeBase64(string input)
        {
            string cleaned = input.Trim()
                .Replace('-', '+')
                .Replace('_', '/')
                .Replace("\r", string.Empty)
                .Replace("\n", string.Empty);

            return Convert.FromBase64String(cleaned.PadRight(
                cleaned.Length + ((4 - (cleaned.Length % 4)) % 4), '='));
        }

        public static ToolResult UrlEncode(string input) => ToolResult.Ok(Uri.EscapeDataString(input));

        public static ToolResult UrlDecode(string input)
        {
            try
            {
                return ToolResult.Ok(Uri.UnescapeDataString(input));
            }
            catch (UriFormatException)
            {
                return ToolResult.Fail("Not a valid percent-encoded string.");
            }
        }

        public static ToolResult HtmlEncode(string input) =>
            ToolResult.Ok(System.Net.WebUtility.HtmlEncode(input));

        public static ToolResult HtmlDecode(string input) =>
            ToolResult.Ok(System.Net.WebUtility.HtmlDecode(input));

        /// <summary>
        ///  Decodes a JWT's header and payload, and says nothing about whether it is valid.
        /// </summary>
        /// <remarks>
        ///  Decoding is not verifying, and the tool says so rather than letting the user infer
        ///  otherwise: checking the signature needs the key, which is exactly the thing that must
        ///  not be pasted into a tool. What this answers is the question people actually have -
        ///  which claims are in here, and has it expired.
        /// </remarks>
        public static ToolResult DecodeJwt(string input)
        {
            string token = input.Trim();

            if (token.Length == 0)
            {
                return ToolResult.Ok(string.Empty);
            }

            string[] parts = token.Split('.');

            if (parts.Length is not (2 or 3))
            {
                return ToolResult.Fail("A JWT has three dot-separated parts.");
            }

            try
            {
                StringBuilder output = new();

                output.AppendLine("HEADER");
                output.AppendLine(FormatJson(Encoding.UTF8.GetString(DecodeBase64(parts[0]))).Text);
                output.AppendLine();
                output.AppendLine("PAYLOAD");

                string payload = Encoding.UTF8.GetString(DecodeBase64(parts[1]));
                output.AppendLine(FormatJson(payload).Text);

                if (Expiry(payload) is { } note)
                {
                    output.AppendLine();
                    output.AppendLine(note);
                }

                output.AppendLine();
                output.Append("The signature is not checked. Decoding a token does not verify it.");

                return ToolResult.Ok(output.ToString());
            }
            catch (Exception)
            {
                return ToolResult.Fail("The parts of this token are not valid base64 JSON.");
            }
        }

        /// <summary>
        ///  A token's header and payload as one JSON object, for the tree; empty when it is not a token.
        /// </summary>
        /// <remarks>
        ///  The decoded text interleaves labels and notes with the JSON, which is right for reading
        ///  and useless for parsing - so the tree is built from the parts themselves.
        /// </remarks>
        public static string JwtAsJson(string input)
        {
            string[] parts = input.Trim().Split('.');

            if (parts.Length is not (2 or 3))
            {
                return string.Empty;
            }

            try
            {
                string header = Encoding.UTF8.GetString(DecodeBase64(parts[0]));
                string payload = Encoding.UTF8.GetString(DecodeBase64(parts[1]));

                // Parsed first, so a part that is not JSON gives no tree rather than a broken one.
                using System.Text.Json.JsonDocument first = System.Text.Json.JsonDocument.Parse(header);
                using System.Text.Json.JsonDocument second = System.Text.Json.JsonDocument.Parse(payload);

                return $"{{\"header\":{header},\"payload\":{payload}}}";
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>Turns the exp and iat claims into something a human can read.</summary>
        private static string? Expiry(string payload)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(payload);

                List<string> notes = [];

                if (document.RootElement.TryGetProperty("iat", out JsonElement issued)
                    && issued.TryGetInt64(out long issuedAt))
                {
                    notes.Add($"Issued  {DateTimeOffset.FromUnixTimeSeconds(issuedAt).LocalDateTime:yyyy-MM-dd HH:mm:ss}");
                }

                if (document.RootElement.TryGetProperty("exp", out JsonElement expires)
                    && expires.TryGetInt64(out long expiresAt))
                {
                    DateTimeOffset when = DateTimeOffset.FromUnixTimeSeconds(expiresAt);
                    bool past = when < DateTimeOffset.UtcNow;

                    notes.Add($"Expires {when.LocalDateTime:yyyy-MM-dd HH:mm:ss}  ({(past ? "expired" : "valid")})");
                }

                return notes.Count == 0 ? null : string.Join(Environment.NewLine, notes);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Hashes text with the named algorithm.</summary>
        /// <remarks>
        ///  MD5 and SHA-1 are here because the daily use of a hash tool is checking a file against a
        ///  checksum someone else published, and those are still what gets published. They are not
        ///  offered as a way to protect anything.
        /// </remarks>
        public static ToolResult Hash(string input, string algorithm)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);

            byte[] digest = algorithm.ToUpperInvariant() switch
            {
                "MD5" => MD5.HashData(bytes),
                "SHA1" => SHA1.HashData(bytes),
                "SHA256" => SHA256.HashData(bytes),
                "SHA384" => SHA384.HashData(bytes),
                "SHA512" => SHA512.HashData(bytes),
                _ => [],
            };

            return digest.Length == 0
                ? ToolResult.Fail($"No such algorithm: {algorithm}")
                : ToolResult.Ok(Convert.ToHexString(digest).ToLowerInvariant());
        }

        /// <summary>The algorithms <see cref="Hash"/> accepts, in the order they should be offered.</summary>
        public static IReadOnlyList<string> HashAlgorithms { get; } = ["SHA256", "SHA1", "MD5", "SHA384", "SHA512"];

        public static string NewGuid(bool uppercase = false, bool braces = false)
        {
            string guid = Guid.NewGuid().ToString(braces ? "B" : "D");

            return uppercase ? guid.ToUpperInvariant() : guid;
        }

        /// <summary>
        ///  Reads a timestamp in any of the forms one actually meets, and shows all the others.
        /// </summary>
        /// <remarks>
        ///  Seconds and milliseconds are told apart by magnitude rather than by asking: a
        ///  ten-digit number is seconds until the year 2286 and a thirteen-digit one is
        ///  milliseconds, so the guess is safe for any date anyone is debugging.
        /// </remarks>
        public static ToolResult Timestamp(string input, DateTimeOffset? now = null)
        {
            string text = input.Trim();

            if (text.Length == 0)
            {
                return Describe(now ?? DateTimeOffset.Now);
            }

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
            {
                try
                {
                    return Describe(Math.Abs(number) > 99_999_999_999L
                        ? DateTimeOffset.FromUnixTimeMilliseconds(number)
                        : DateTimeOffset.FromUnixTimeSeconds(number));
                }
                catch (ArgumentOutOfRangeException)
                {
                    return ToolResult.Fail("That number is outside the range of a date.");
                }
            }

            if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal, out DateTimeOffset parsed))
            {
                return Describe(parsed);
            }

            return ToolResult.Fail("Not a Unix timestamp or a date this can read.");
        }

        private static ToolResult Describe(DateTimeOffset moment)
        {
            StringBuilder output = new();

            output.AppendLine($"Unix seconds       {moment.ToUnixTimeSeconds()}");
            output.AppendLine($"Unix milliseconds  {moment.ToUnixTimeMilliseconds()}");
            output.AppendLine($"ISO 8601 (UTC)     {moment.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}");
            output.AppendLine($"Local              {moment.LocalDateTime:yyyy-MM-dd HH:mm:ss}");
            output.AppendLine($"UTC                {moment.UtcDateTime:yyyy-MM-dd HH:mm:ss}");
            output.Append($"Relative           {Relative(moment)}");

            return ToolResult.Ok(output.ToString());
        }

        /// <summary>"3 days ago", which is the form the question is usually asked in.</summary>
        private static string Relative(DateTimeOffset moment)
        {
            TimeSpan difference = DateTimeOffset.Now - moment;
            bool past = difference.Ticks >= 0;

            TimeSpan size = difference.Duration();

            string amount = size.TotalSeconds < 60 ? $"{size.TotalSeconds:F0} seconds"
                : size.TotalMinutes < 60 ? $"{size.TotalMinutes:F0} minutes"
                : size.TotalHours < 24 ? $"{size.TotalHours:F0} hours"
                : size.TotalDays < 365 ? $"{size.TotalDays:F0} days"
                : $"{size.TotalDays / 365.25:F1} years";

            return past ? $"{amount} ago" : $"in {amount}";
        }

        /// <summary>Which of the case conversions to apply.</summary>
        internal enum CaseStyle
        {
            Lower,
            Upper,
            Title,
            Camel,
            Pascal,
            Snake,
            Kebab,
            Constant,
        }

        /// <summary>
        ///  Rewrites an identifier in another convention.
        /// </summary>
        /// <remarks>
        ///  Splitting is the whole job, and it has to handle the case that trips up naive
        ///  implementations: a run of capitals followed by a word, as in "HTTPServer" or "parseXMLFile",
        ///  where the boundary falls before the last capital rather than after it.
        /// </remarks>
        public static ToolResult ChangeCase(string input, CaseStyle style)
        {
            if (input.Length == 0)
            {
                return ToolResult.Ok(string.Empty);
            }

            if (style == CaseStyle.Lower)
            {
                return ToolResult.Ok(input.ToLowerInvariant());
            }

            if (style == CaseStyle.Upper)
            {
                return ToolResult.Ok(input.ToUpperInvariant());
            }

            string[] words = Words(input);

            if (words.Length == 0)
            {
                return ToolResult.Ok(input);
            }

            return ToolResult.Ok(style switch
            {
                CaseStyle.Snake => string.Join('_', words.Select(w => w.ToLowerInvariant())),
                CaseStyle.Kebab => string.Join('-', words.Select(w => w.ToLowerInvariant())),
                CaseStyle.Constant => string.Join('_', words.Select(w => w.ToUpperInvariant())),
                CaseStyle.Camel => words[0].ToLowerInvariant() + string.Concat(words.Skip(1).Select(Capitalise)),
                CaseStyle.Pascal => string.Concat(words.Select(Capitalise)),
                CaseStyle.Title => string.Join(' ', words.Select(Capitalise)),
                _ => input,
            });
        }

        /// <summary>Splits an identifier in any convention into its words.</summary>
        [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|[a-z0-9]+")]
        private static partial Regex WordParts();

        private static string[] Words(string input) =>
            [.. WordParts().Matches(input).Select(match => match.Value).Where(word => word.Length > 0)];

        private static string Capitalise(string word) =>
            word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

        /// <summary>One match from the regex tester.</summary>
        internal sealed record RegexMatch(int Index, int Length, string Value, IReadOnlyList<string> Groups);

        /// <summary>
        ///  Runs a pattern against text and reports what matched.
        /// </summary>
        /// <remarks>
        ///  The timeout is not optional. A user-written pattern against user-supplied text is the
        ///  textbook setup for catastrophic backtracking, and without a limit a mistyped pattern
        ///  does not show an error - it hangs the window with no way back.
        /// </remarks>
        public static ToolResult TestRegex(
            string pattern,
            string input,
            bool ignoreCase,
            bool multiline,
            out IReadOnlyList<RegexMatch> matches)
        {
            matches = [];

            if (pattern.Length == 0)
            {
                return ToolResult.Ok(string.Empty);
            }

            RegexOptions options = RegexOptions.None;

            if (ignoreCase)
            {
                options |= RegexOptions.IgnoreCase;
            }

            if (multiline)
            {
                options |= RegexOptions.Multiline;
            }

            try
            {
                Regex regex = new(pattern, options, TimeSpan.FromMilliseconds(500));

                List<RegexMatch> found = [];

                foreach (Match match in regex.Matches(input))
                {
                    found.Add(new RegexMatch(
                        match.Index,
                        match.Length,
                        match.Value,
                        [.. match.Groups.Values.Skip(1).Select(group => group.Value)]));

                    // A pattern matching every empty position produces one match per character;
                    // past a few hundred the list has stopped being informative anyway.
                    if (found.Count >= 500)
                    {
                        break;
                    }
                }

                matches = found;

                return ToolResult.Ok(found.Count switch
                {
                    0 => "No matches.",
                    1 => "1 match.",
                    _ => $"{found.Count} matches.",
                });
            }
            catch (ArgumentException exception)
            {
                return ToolResult.Fail(Trim(exception.Message));
            }
            catch (RegexMatchTimeoutException)
            {
                return ToolResult.Fail("That pattern took too long - it is probably backtracking. Try anchoring it.");
            }
        }

        /// <summary>Sorts lines, optionally dropping the repeats.</summary>
        public static ToolResult SortLines(string input, bool descending = false, bool unique = false)
        {
            string[] lines = input.ReplaceLineEndings("\n").Split('\n');

            IEnumerable<string> sorted = descending
                ? lines.OrderDescending(StringComparer.Ordinal)
                : lines.Order(StringComparer.Ordinal);

            if (unique)
            {
                sorted = sorted.Distinct(StringComparer.Ordinal);
            }

            return ToolResult.Ok(string.Join(Environment.NewLine, sorted));
        }

        /// <summary>Counts what a text box can tell you about its own contents.</summary>
        public static string Statistics(string input)
        {
            int lines = input.Length == 0 ? 0 : input.ReplaceLineEndings("\n").Split('\n').Length;
            int words = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

            return $"{input.Length} characters · {words} words · {lines} lines";
        }

        /// <summary>The first line of an exception message, without its trailing advice.</summary>
        private static string Trim(string message) =>
            message.Split('\n')[0].Trim();
    }
}
