using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevDeck.Core
{
    /// <summary>One saved conversation.</summary>
    internal sealed class SavedChat
    {
        /// <summary>The file's own name, which is what makes it findable again.</summary>
        [JsonIgnore]
        public string Id { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public DateTime At { get; set; } = DateTime.Now;

        public string Model { get; set; } = string.Empty;

        public List<SavedTurn> Turns { get; set; } = [];

        /// <summary>"Yesterday 14:02" - close enough, and shorter than a date nobody reads.</summary>
        [JsonIgnore]
        public string When
        {
            get
            {
                TimeSpan ago = DateTime.Now - At;

                return ago.TotalDays switch
                {
                    < 1 when At.Date == DateTime.Today => $"Today {At:HH:mm}",
                    < 2 => $"Yesterday {At:HH:mm}",
                    < 7 => $"{At:dddd HH:mm}",
                    _ => At.ToString("d MMM yyyy"),
                };
            }
        }

        [JsonIgnore]
        public string Summary => $"{Turns.Count} turns · {(Model.Length > 0 ? Model : "unknown model")}";

        public override string ToString() => Title;
    }

    /// <summary>One turn inside a saved conversation.</summary>
    internal sealed record SavedTurn(string Role, string Text, DateTime At);

    /// <summary>
    ///  Keeps conversations on disk, so closing the app is not the same as forgetting.
    /// </summary>
    /// <remarks>
    ///  The thing that made the assistant feel disposable: every restart lost the explanation of
    ///  that build failure, along with the script you had been refining for ten minutes. Saving
    ///  them turns it from a toy into somewhere work accumulates.
    ///
    ///  One file per conversation rather than one file holding all of them, for the ordinary
    ///  reason: a single file has to be rewritten in full on every save, and a half-written one
    ///  loses every conversation rather than the newest. Separate files also mean a conversation
    ///  can be deleted, copied out or read by hand without the rest being involved.
    ///
    ///  Beside the settings file rather than in it. Transcripts are large, they grow without bound,
    ///  and settings.json is read on every start before the window exists - it has no business
    ///  carrying a megabyte of chat.
    /// </remarks>
    internal static class ChatArchive
    {
        /// <summary>
        ///  How many are kept.
        /// </summary>
        /// <remarks>
        ///  A bound rather than a guess at what is enough: this is a folder the user never looks at,
        ///  so nothing else will ever prune it. Fifty is roughly a month of ordinary use and a few
        ///  megabytes at worst.
        /// </remarks>
        public const int Keep = 50;

        private static readonly JsonSerializerOptions Format = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string Folder => Path.Combine(SettingsPath.Directory, "conversations");

        /// <summary>Every saved conversation, newest first.</summary>
        public static IReadOnlyList<SavedChat> All()
        {
            if (!Directory.Exists(Folder))
            {
                return [];
            }

            List<SavedChat> chats = [];

            foreach (string file in Directory.EnumerateFiles(Folder, "*.json"))
            {
                if (Read(file) is { } chat)
                {
                    chats.Add(chat);
                }
            }

            return [.. chats.OrderByDescending(chat => chat.At)];
        }

        public static SavedChat? Read(string file)
        {
            try
            {
                SavedChat? chat = JsonSerializer.Deserialize<SavedChat>(File.ReadAllText(file));

                if (chat is null)
                {
                    return null;
                }

                chat.Id = Path.GetFileNameWithoutExtension(file);

                return chat;
            }
            catch (Exception exception)
            {
                // One unreadable file is not a reason to lose the list. It is named, because a
                // transcript the user expected to be there and is not is worth explaining.
                AppLog.Instance.Failure("chat", $"Could not read {Path.GetFileName(file)}", exception);

                return null;
            }
        }

        /// <summary>
        ///  Writes a conversation, and returns the id it was written under.
        /// </summary>
        /// <remarks>
        ///  Given an id, it overwrites - so a conversation that is still being added to updates in
        ///  place rather than leaving a trail of one file per exchange.
        /// </remarks>
        public static string? Save(SavedChat chat)
        {
            if (chat.Turns.Count == 0)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(Folder);

                if (chat.Id.Length == 0)
                {
                    // Sortable and unique without a counter: the timestamp orders the folder the way
                    // a person would, and the suffix settles two started in the same second.
                    chat.Id = $"{chat.At:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString()[..4]}";
                }

                File.WriteAllText(
                    Path.Combine(Folder, chat.Id + ".json"),
                    JsonSerializer.Serialize(chat, Format));

                Prune();

                return chat.Id;
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("chat", "Could not save the conversation", exception);

                return null;
            }
        }

        public static void Delete(string id)
        {
            try
            {
                string file = Path.Combine(Folder, id + ".json");

                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception exception)
            {
                AppLog.Instance.Failure("chat", "Could not delete the conversation", exception);
            }
        }

        /// <summary>
        ///  A title from the first thing that was asked.
        /// </summary>
        /// <remarks>
        ///  Rather than asking the model to name it, which costs a call and a wait to produce
        ///  something the first line already says. The question is what the user will recognise.
        /// </remarks>
        public static string TitleFor(string firstQuestion)
        {
            string line = firstQuestion
                .Split('\n')
                .Select(text => text.Trim())
                .FirstOrDefault(text => text.Length > 0) ?? "Conversation";

            return line.Length <= 50 ? line : line[..50].TrimEnd() + "…";
        }

        private static void Prune()
        {
            try
            {
                string[] files = [.. Directory
                    .EnumerateFiles(Folder, "*.json")
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Skip(Keep)];

                foreach (string file in files)
                {
                    File.Delete(file);
                }
            }
            catch (Exception)
            {
                // Pruning is housekeeping. A conversation that was saved is more important than the
                // folder being exactly the right size, so this never fails a save.
            }
        }
    }
}
