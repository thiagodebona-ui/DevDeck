using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>
    ///  One-time import of V1's commonData.json into V2's settings.
    /// </summary>
    /// <remarks>
    ///  Runs only when V2 has no settings of its own yet and a V1 file is sitting next to the
    ///  executable, so it can never overwrite choices made in V2.
    /// </remarks>
    internal static class SettingsMigration
    {
        /// <summary>The file V1 wrote its settings to.</summary>
        public const string LegacyFileName = "commonData.json";

        /// <summary>Placeholder row V1 kept at the top of its folder list.</summary>
        private const string LegacyAddEntry = "1. Add new...";

        /// <summary>
        ///  Returns settings built from a V1 file in <paramref name="directory"/>, or null when
        ///  there is nothing to import.
        /// </summary>
        public static AppSettings? TryImport(string directory)
        {
            string legacy = Path.Combine(directory, LegacyFileName);

            if (!File.Exists(legacy))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(legacy));
                JsonElement root = document.RootElement;

                AppSettings settings = new();

                ImportTheme(root, settings);
                ImportWorkspaces(root, settings);
                ImportCommands(root, settings);
                ImportWindowSize(root, settings);

                return settings;
            }
            catch (Exception)
            {
                // A V1 file we cannot read is not worth blocking start up over - V2 will just use
                // its own defaults.
                return null;
            }
        }

        private static void ImportTheme(JsonElement root, AppSettings settings)
        {
            // V1 stored the palette name, and before that only a dark on/off flag.
            if (root.TryGetProperty("theme", out JsonElement theme) && theme.ValueKind == JsonValueKind.String)
            {
                settings.Theme = AppTheme.Parse(theme.GetString()).Id;
                return;
            }

            bool dark = !root.TryGetProperty("darkTheme", out JsonElement darkTheme)
                || darkTheme.ValueKind != JsonValueKind.False;

            settings.Theme = (dark ? AppTheme.Dark : AppTheme.Light).Id;
        }

        private static void ImportWorkspaces(JsonElement root, AppSettings settings)
        {
            if (!root.TryGetProperty("pathList", out JsonElement pathList)
                || pathList.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            // V1's combo box held the "1. Add new..." placeholder as a real row, and its
            // selectedIndex counted it - so the index has to be resolved against the raw list.
            List<string> raw = [];

            foreach (JsonElement entry in pathList.EnumerateArray())
            {
                raw.Add(entry.GetString() ?? string.Empty);
            }

            settings.Workspaces = raw
                .Where(path => path.Length > 0 && path != LegacyAddEntry)
                .Distinct()
                .ToList();

            if (root.TryGetProperty("selectedIndex", out JsonElement selected)
                && selected.ValueKind == JsonValueKind.Number
                && selected.TryGetInt32(out int index)
                && index >= 0
                && index < raw.Count
                && raw[index] != LegacyAddEntry)
            {
                settings.SelectedWorkspace = raw[index];
            }
            else
            {
                settings.SelectedWorkspace = settings.Workspaces.FirstOrDefault() ?? string.Empty;
            }
        }

        private static void ImportCommands(JsonElement root, AppSettings settings)
        {
            if (!root.TryGetProperty("customCommands", out JsonElement commands)
                || commands.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (JsonElement entry in commands.EnumerateArray())
            {
                string name = ReadString(entry, "Name");
                string command = ReadString(entry, "Command");

                if (name.Length > 0 && command.Length > 0)
                {
                    // Several V1 entries ended in a stray newline from the input dialog.
                    settings.CustomCommands.Add(new CustomCommand
                    {
                        Name = name.Trim(),
                        Command = command.Trim()
                    });
                }
            }

            if (root.TryGetProperty("customCommandSelectedIndex", out JsonElement selected)
                && selected.ValueKind == JsonValueKind.Number
                && selected.TryGetInt32(out int index)
                && index >= 0
                && index < settings.CustomCommands.Count)
            {
                settings.SelectedCommand = settings.CustomCommands[index].Name;
            }
            else
            {
                settings.SelectedCommand = settings.CustomCommands.FirstOrDefault()?.Name ?? string.Empty;
            }
        }

        private static void ImportWindowSize(JsonElement root, AppSettings settings)
        {
            if (root.TryGetProperty("formSizeWidth", out JsonElement width)
                && width.ValueKind == JsonValueKind.Number
                && width.TryGetInt32(out int w)
                && w > 0)
            {
                settings.WindowWidth = w;
            }

            if (root.TryGetProperty("formSizeHeight", out JsonElement height)
                && height.ValueKind == JsonValueKind.Number
                && height.TryGetInt32(out int h)
                && h > 0)
            {
                settings.WindowHeight = h;
            }
        }

        private static string ReadString(JsonElement element, string property)
        {
            return element.TryGetProperty(property, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
    }
}
