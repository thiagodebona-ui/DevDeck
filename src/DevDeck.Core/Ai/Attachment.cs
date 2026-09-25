using System.Text;

namespace DevDeck.Core
{
    /// <summary>
    ///  A file put in front of the model as context.
    /// </summary>
    /// <remarks>
    ///  The gap between "explain this error" and "explain this error in this file" is most of what
    ///  makes an assistant useful in a repository, and pasting a file into a chat box by hand is
    ///  both tedious and lossy - the paste loses the name, which is often the most informative
    ///  thing about it.
    ///
    ///  Read once, at the moment of attaching, rather than on every send. A file that changes
    ///  between two questions would otherwise make the earlier answer unreproducible, and rereading
    ///  silently is how a conversation ends up discussing a version of the file the user never saw.
    /// </remarks>
    internal sealed class Attachment
    {
        /// <summary>
        ///  The largest file that will be read.
        /// </summary>
        /// <remarks>
        ///  Not a guess at what is useful but a guard on what is affordable: at roughly four
        ///  characters per token this is some sixteen thousand tokens, which already fills a small
        ///  context window on its own. Anything larger is truncated with a line saying so, rather
        ///  than being sent in full and silently dropping the conversation it was meant to inform.
        /// </remarks>
        public const int MaxCharacters = 64 * 1024;

        private Attachment(string path, string text, bool truncated)
        {
            Path = path;
            Text = text;
            Truncated = truncated;
        }

        public string Path { get; }

        public string Text { get; }

        /// <summary>Whether only the beginning of the file was taken.</summary>
        public bool Truncated { get; }

        public string Name => System.IO.Path.GetFileName(Path);

        public int Tokens => TokenCount.Of(Text);

        /// <summary>"Program.cs · ~1,200 tokens" - the two things worth knowing before sending.</summary>
        public string Summary => Truncated
            ? $"{Name} · ~{Tokens:N0} tokens · shortened"
            : $"{Name} · ~{Tokens:N0} tokens";

        public override string ToString() => Name;

        /// <summary>
        ///  Reads a file, or says why it could not.
        /// </summary>
        /// <remarks>
        ///  Refuses anything that is not text. A binary sent to a model is thousands of tokens of
        ///  mojibake that crowds out the actual question, and the user cannot tell from the answer
        ///  that that is what happened.
        /// </remarks>
        public static Attachment? Read(string path, out string problem)
        {
            problem = string.Empty;

            try
            {
                FileInfo file = new(path);

                if (!file.Exists)
                {
                    problem = "There is no file there.";
                    return null;
                }

                if (file.Length == 0)
                {
                    problem = $"{file.Name} is empty.";
                    return null;
                }

                byte[] bytes = new byte[Math.Min(file.Length, MaxCharacters)];
                int read;

                using (FileStream stream = file.OpenRead())
                {
                    read = stream.Read(bytes, 0, bytes.Length);
                }

                if (LooksBinary(bytes, read))
                {
                    problem = $"{file.Name} does not look like text.";
                    return null;
                }

                string text = new UTF8Encoding(false).GetString(bytes, 0, read);
                bool truncated = file.Length > read;

                if (truncated)
                {
                    // Cut at a line ending rather than mid-token, and say so in the text itself so
                    // the model knows the file continues and does not reason about its ending.
                    int lastBreak = text.LastIndexOf('\n');

                    if (lastBreak > 0)
                    {
                        text = text[..lastBreak];
                    }

                    text += $"\n\n… (only the first {read:N0} bytes of {file.Length:N0} are shown)";
                }

                return new Attachment(file.FullName, text, truncated);
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("chat", $"Could not attach {path}", exception);

                problem = $"Could not read it: {exception.Message}";

                return null;
            }
        }

        /// <summary>
        ///  The block that goes in front of the question.
        /// </summary>
        /// <remarks>
        ///  Named and fenced with the file's own extension, because both matter to the answer: the
        ///  name is often the context ("this is the test, not the code") and the language tag is
        ///  what stops a model guessing wrongly at a file whose syntax is ambiguous.
        /// </remarks>
        public string AsContext() =>
            $"File: {Name}\n```{Fence()}\n{Text}\n```";

        private string Fence() => System.IO.Path.GetExtension(Path).TrimStart('.').ToLowerInvariant() switch
        {
            "cs" => "csharp",
            "ts" => "typescript",
            "js" => "javascript",
            "py" => "python",
            "rs" => "rust",
            "go" => "go",
            "sh" or "bash" => "bash",
            "ps1" => "powershell",
            "yml" or "yaml" => "yaml",
            "md" => "markdown",
            var other => other,
        };

        /// <summary>
        ///  Whether these bytes are a binary rather than text.
        /// </summary>
        /// <remarks>
        ///  A null byte is the test every tool from git to grep uses, and for good reason: it
        ///  practically never occurs in text and practically always occurs early in a binary. Only
        ///  the beginning is checked, because a file whose first kilobyte is clean text is text.
        /// </remarks>
        private static bool LooksBinary(byte[] bytes, int length)
        {
            int checkable = Math.Min(length, 1024);

            for (int index = 0; index < checkable; index++)
            {
                if (bytes[index] == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
