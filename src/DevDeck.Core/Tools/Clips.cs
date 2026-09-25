using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevDeck.Core
{
    /// <summary>One thing that was on the clipboard.</summary>
    internal sealed class Clip
    {
        public string Text { get; set; } = string.Empty;

        public DateTime At { get; set; }

        /// <summary>Kept out of the sweep and out of the cap, until the user unpins it.</summary>
        public bool Pinned { get; set; }

        [JsonIgnore]
        public string When
        {
            get
            {
                TimeSpan ago = DateTime.Now - At;

                return ago < TimeSpan.FromMinutes(1) ? "just now"
                    : ago < TimeSpan.FromHours(1) ? $"{(int)ago.TotalMinutes}m ago"
                    : ago < TimeSpan.FromDays(1) ? $"{(int)ago.TotalHours}h ago"
                    : At.ToString("ddd HH:mm");
            }
        }

        /// <summary>The first meaningful line, which is what identifies a clip in a list.</summary>
        [JsonIgnore]
        public string Preview
        {
            get
            {
                string line = Text
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault(part => part.Trim().Length > 0)?.Trim() ?? string.Empty;

                return line.Length > 120 ? line[..120] + "…" : line;
            }
        }

        [JsonIgnore]
        public string Shape
        {
            get
            {
                int lines = Text.Count(c => c == '\n') + 1;

                return lines > 1
                    ? $"{lines} lines · {Text.Length} characters"
                    : $"{Text.Length} characters";
            }
        }

        [JsonIgnore]
        public bool IsMultiline => Text.Contains('\n');

        /// <summary>
        ///  The clip as a web address, when the whole of it is one.
        /// </summary>
        /// <remarks>
        ///  A URL is one of the most-copied things there is and the least useful to have to paste
        ///  somewhere before it can be followed, so a clip that is one is offered as a link.
        ///
        ///  The rule is deliberately the strict one: the entire clip, trimmed, has to be a single
        ///  absolute http or https address. Not "contains a URL" - a stack trace, a log line or a
        ///  curl command all contain one, and picking a link out of the middle of a body of text
        ///  turns a history into a guessing game about what a click will do.
        ///
        ///  <b>Only http and https.</b> This is a security boundary rather than a tidiness rule.
        ///  The text is arbitrary content that arrived on the clipboard, and handing an arbitrary
        ///  scheme to the shell is how a copied <c>file://</c> path opens a document, a
        ///  <c>javascript:</c> URI runs, or a registered custom scheme launches its application
        ///  with an argument nobody read. Two schemes, both of which do the one thing a person
        ///  expects a link to do, is the whole of what is allowed through.
        /// </remarks>
        [JsonIgnore]
        public string? Link
        {
            get
            {
                string trimmed = Text.Trim();

                if (trimmed.Length == 0 || trimmed.Length > 2048 || trimmed.Any(char.IsWhiteSpace))
                {
                    return null;
                }

                return Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? parsed)
                    && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps)
                    ? trimmed
                    : null;
            }
        }

        /// <summary>Whether this clip is a web address, and so worth drawing as one.</summary>
        [JsonIgnore]
        public bool IsLink => Link is not null;

        /// <summary>
        ///  Enough of the clip to recognise it by, for the tooltip.
        /// </summary>
        /// <remarks>
        ///  A one-line preview is no help at all with the things this history is most worth having
        ///  - a curl command from devtools, a stack trace, a block of JSON - because all of them
        ///  look alike for their first hundred characters. This shows the head of it and says how
        ///  much was left out, which is enough to tell two of them apart without opening anything.
        ///
        ///  Capped well below the clip limit on purpose: a tooltip is a glance, and one that fills
        ///  the screen is a worse way to read a large clip than pasting it somewhere.
        /// </remarks>
        [JsonIgnore]
        public string Peek
        {
            get
            {
                const int Head = 2000;

                return Text.Length <= Head
                    ? Text
                    : Text[..Head] + $"\n\n… {Text.Length - Head:N0} more characters.";
            }
        }
    }

    /// <summary>
    ///  The last hundred things that were copied, searchable.
    /// </summary>
    /// <remarks>
    ///  A one-slot clipboard loses work constantly: copy an id to look something up, copy the thing
    ///  you found, and the id is gone. Every platform has a third-party tool for this and none of
    ///  them are installed on a fresh machine.
    ///
    ///  What it deliberately does not keep: anything that looks like a password or a key. The rule
    ///  in <see cref="Sensitive"/> is a heuristic and is stated as one - it catches what password
    ///  managers actually put on the clipboard and it will not catch everything. It is paired with
    ///  the capture being off until the user turns it on, and a history that can be wiped in one
    ///  press, because a clipboard log is a genuinely risky thing to keep and pretending otherwise
    ///  would be the wrong trade to make quietly.
    ///
    ///  Stored in plain JSON rather than wrapped: the entries are things that were on the clipboard
    ///  of a logged-in session, the sensitive ones are dropped rather than encrypted, and encrypting
    ///  the rest would suggest a protection the design has not earned.
    /// </remarks>
    internal sealed class Clips
    {
        /// <summary>Beyond this the list is an archive nobody searches.</summary>
        private const int Cap = 100;

        /// <summary>
        ///  The largest clip that will be recorded.
        /// </summary>
        /// <remarks>
        ///  Sized for the real worst case a developer copies deliberately - a curl command from
        ///  devtools, a stack trace, a certificate, a block of JSON - rather than for a comfortable
        ///  preview. Anything above this is a file being moved through the clipboard, and a history
        ///  of a hundred of those is a settings file that fails to load.
        /// </remarks>
        private const int MaxClip = 1024 * 1024;

        private const string FileName = "clips.json";

        private readonly List<Clip> clips = [];

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        private Clips()
        {
        }

        public static Clips Instance { get; } = Load();

        public static string Path => System.IO.Path.Combine(SettingsPath.Directory, FileName);

        /// <summary>Newest first, pinned entries above everything.</summary>
        public IReadOnlyList<Clip> All =>
        [
            .. clips.OrderByDescending(clip => clip.Pinned).ThenByDescending(clip => clip.At)
        ];

        /// <summary>Entries whose text contains every word of the query, in any order.</summary>
        public IReadOnlyList<Clip> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return All;
            }

            string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return
            [
                .. All.Where(clip => words.All(word =>
                    clip.Text.Contains(word, StringComparison.OrdinalIgnoreCase)))
            ];
        }

        /// <summary>
        ///  Records a clipboard value, if it is worth recording.
        /// </summary>
        /// <returns>True when the list changed, so a poll that saw nothing new does no work.</returns>
        public bool Add(string? text)
        {
            if (string.IsNullOrWhiteSpace(text) || Sensitive(text))
            {
                return false;
            }

            // A guard on the settings file's size rather than a judgement about what is worth
            // keeping. A curl command copied out of a browser's devtools is routinely twenty or
            // thirty thousand characters - cookies and a bearer token see to that - and it is
            // precisely the thing this history exists to hang on to, so the ceiling has to sit
            // well above it. Past a megabyte the clipboard is being used to move a file, and a
            // hundred of those would be a settings file nothing can load.
            if (text.Length > MaxClip)
            {
                return false;
            }

            if (clips.FirstOrDefault(clip => string.Equals(clip.Text, text, StringComparison.Ordinal))
                is { } existing)
            {
                // Copying the same thing again moves it back to the top rather than duplicating it,
                // which is what the list being ordered by recency is for.
                if (existing.At >= DateTime.Now.AddSeconds(-2))
                {
                    return false;
                }

                existing.At = DateTime.Now;
                Save();

                return true;
            }

            clips.Add(new Clip { Text = text, At = DateTime.Now });

            // Pinned entries are exempt: the cap exists to bound a scrolling history, and something
            // deliberately kept is not part of that.
            foreach (Clip old in clips.Where(clip => !clip.Pinned)
                         .OrderByDescending(clip => clip.At)
                         .Skip(Cap)
                         .ToList())
            {
                clips.Remove(old);
            }

            Save();

            return true;
        }

        public void Remove(Clip clip)
        {
            clips.Remove(clip);
            Save();
        }

        public void Pin(Clip clip, bool pinned)
        {
            clip.Pinned = pinned;
            Save();
        }

        /// <summary>Drops everything, pinned included. The one-press wipe.</summary>
        public void Clear()
        {
            clips.Clear();
            Save();
        }

        /// <summary>
        ///  Whether a value looks like a credential and should not be kept.
        /// </summary>
        /// <remarks>
        ///  Three rules, each aimed at something real. A recognised token shape - a JWT, a GitHub
        ///  or Slack or AWS key, an OpenAI-style key - is never a false positive and is exactly what
        ///  must not be logged. A short single-line value of mixed classes with no spaces is what a
        ///  password manager puts on the clipboard, and while an id or a hash can look the same, the
        ///  cost of dropping one of those is that the user copies it again.
        ///
        ///  It is a heuristic. A password made of two dictionary words will pass it. The capture
        ///  being opt-in is what makes that acceptable; this narrows the window rather than closing
        ///  it, and the class remarks say so.
        /// </remarks>
        public static bool Sensitive(string text)
        {
            string trimmed = text.Trim();

            if (trimmed.Length == 0 || trimmed.Contains('\n'))
            {
                // A multi-line paste is a document, a log or code. Credentials are pasted alone.
                return false;
            }

            // A command is a document too, and the one case where that matters is the curl copied
            // out of a browser's devtools: it arrives on a single line, it is stuffed with a bearer
            // token and a cookie jar, and it is the single most useful thing this history can hold
            // - the HTTP panel reads one straight back into a request. Dropping it for containing a
            // credential would be refusing the payload because of the envelope. What follows still
            // applies to a token copied on its own, which is the thing the rule is actually for.
            if (Command(trimmed))
            {
                return false;
            }

            string[] prefixes =
            [
                "ghp_", "gho_", "ghu_", "ghs_", "ghr_", "github_pat_",
                "sk-", "sk_live_", "sk_test_", "pk_live_", "rk_live_",
                "xoxb-", "xoxp-", "xoxa-", "xapp-",
                "AKIA", "ASIA",
                "AIza",
                "glpat-", "dop_v1_", "shpat_", "npm_",
            ];

            if (prefixes.Any(prefix => trimmed.StartsWith(prefix, StringComparison.Ordinal)))
            {
                return true;
            }

            // A JWT: three dot-separated base64url segments, the first of which decodes to a header.
            if (trimmed.Count(c => c == '.') == 2
                && trimmed.StartsWith("eyJ", StringComparison.Ordinal))
            {
                return true;
            }

            if (trimmed.Contains(' ') || trimmed.Length is < 12 or > 128)
            {
                return false;
            }

            if (Structural(trimmed))
            {
                return false;
            }

            bool upper = trimmed.Any(char.IsUpper);
            bool lower = trimmed.Any(char.IsLower);
            bool digit = trimmed.Any(char.IsDigit);
            bool symbol = trimmed.Any(c => !char.IsLetterOrDigit(c));

            // Three or four character classes, no spaces, password length: the shape of a generated
            // password. A hex hash or a UUID has at most two classes and passes through.
            int classes = (upper ? 1 : 0) + (lower ? 1 : 0) + (digit ? 1 : 0) + (symbol ? 1 : 0);

            return classes >= 3;
        }

        /// <summary>
        ///  Whether a value is a command line rather than a bare secret.
        /// </summary>
        /// <remarks>
        ///  Deliberately narrow: it wants a known command at the very start and some evidence of
        ///  arguments, so that a token which merely happens to begin with the letters "curl" is not
        ///  waved through. The list is the handful of tools whose invocations carry credentials and
        ///  are worth keeping anyway.
        /// </remarks>
        private static bool Command(string trimmed)
        {
            string[] commands = ["curl ", "wget ", "http ", "https ", "git ", "docker ", "ssh ", "scp "];

            return commands.Any(command => trimmed.StartsWith(command, StringComparison.OrdinalIgnoreCase))
                && (trimmed.Contains(" -", StringComparison.Ordinal)
                    || trimmed.Contains("://", StringComparison.Ordinal));
        }

        /// <summary>
        ///  Whether a value is one of the shapes a developer copies all day.
        /// </summary>
        /// <remarks>
        ///  The character-class rule above exists to catch a generated password, and on its own it
        ///  also catches the three things most often on a developer's clipboard - a URL, a file path
        ///  and a UUID - each of which reaches three classes through punctuation that is structure
        ///  rather than entropy.
        ///
        ///  Matched as named shapes rather than by asking whether the symbols look separator-ish:
        ///  the second reading is shorter and wrong, because an ampersand is both a query separator
        ///  and a perfectly ordinary password character. These three are recognised, everything else
        ///  goes on being judged by its classes.
        /// </remarks>
        private static bool Structural(string text)
        {
            if (text.Contains("://", StringComparison.Ordinal)
                || text.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // A path: a separator, and nothing punctuating it that a filename would not contain.
            if ((text.Contains('/') || text.Contains('\\'))
                && text.All(c => char.IsLetterOrDigit(c) || c is '/' or '\\' or '.' or '-' or '_' or ' ' or ':' or '~'))
            {
                return true;
            }

            // A UUID, with or without its hyphens.
            return text.All(c => Uri.IsHexDigit(c) || c == '-')
                && text.Count(char.IsLetterOrDigit) is 32;
        }

        /// <summary>
        ///  A digest of the current clipboard, for a poller to compare cheaply.
        /// </summary>
        /// <remarks>
        ///  The clipboard has to be polled - no platform offers a portable change notification - and
        ///  comparing a hash rather than holding the last string means a megabyte of copied text is
        ///  not kept alive in memory between ticks just to answer "has this changed".
        /// </remarks>
        public static string Digest(string? text) => text is null or ""
            ? string.Empty
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

        private static Clips Load()
        {
            Clips loaded = new();

            try
            {
                if (File.Exists(Path)
                    && JsonSerializer.Deserialize<List<Clip>>(File.ReadAllText(Path)) is { } saved)
                {
                    loaded.clips.AddRange(saved.Where(clip => !string.IsNullOrEmpty(clip.Text)));
                }
            }
            catch (Exception)
            {
                // A corrupt or unreadable history is not worth a message. It starts empty.
            }

            return loaded;
        }

        private void Save()
        {
            try
            {
                System.IO.Directory.CreateDirectory(SettingsPath.Directory);
                File.WriteAllText(Path, JsonSerializer.Serialize(clips, Options));
            }
            catch (Exception)
            {
                // Read-only folder, or no disk. The history stays in memory for this session.
            }
        }
    }
}
