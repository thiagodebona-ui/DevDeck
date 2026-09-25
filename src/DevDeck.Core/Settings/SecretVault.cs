using System.Text.Json;

namespace DevDeck.Core
{
    /// <summary>
    ///  Named secrets a command can reference, each wrapped before it reaches the disk.
    /// </summary>
    /// <remarks>
    ///  A parameterised command needs tokens as often as it needs branch names - a publish needs an
    ///  NPM_TOKEN, a deploy needs an API key - and the obvious way to supply one is to type it into
    ///  the command body, where it lands in settings.json in the clear and then into every backup
    ///  and sync of that folder. <c>{{secret:NAME}}</c> exists so that never has to happen.
    ///
    ///  The wrapping is the same <see cref="DataProtection"/> the AI key already uses: DPAPI on
    ///  Windows, AES-GCM under a 0600 key file elsewhere. The guarantee is therefore the same one,
    ///  and worth stating plainly - it protects a file copied off the machine or read by another
    ///  user on it, and it does not protect against code already running as this user. A command
    ///  that uses a secret can also print it; that is the user's call and the app does not pretend
    ///  otherwise.
    ///
    ///  Separate file from settings.json, so that sharing a deck - exporting it, or committing a
    ///  .devdeck.json - cannot carry the secrets with it by accident. That separation is the point:
    ///  a secret is per-machine by construction, and a deck that references one is portable.
    /// </remarks>
    internal sealed class SecretVault
    {
        private const string FileName = "secrets.json";

        private readonly Dictionary<string, string> secrets = new(StringComparer.Ordinal);

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        private SecretVault()
        {
        }

        /// <summary>The vault for this machine, read once.</summary>
        public static SecretVault Instance { get; } = Load();

        /// <summary>Where the wrapped secrets live, beside the settings file.</summary>
        public static string Path => System.IO.Path.Combine(SettingsPath.Directory, FileName);

        /// <summary>The names held, for the settings page to list. Never the values.</summary>
        public IReadOnlyList<string> Names => [.. secrets.Keys.OrderBy(name => name, StringComparer.Ordinal)];

        public bool Has(string name) => secrets.ContainsKey(name);

        /// <summary>The secret's value, or null if the vault has no such name.</summary>
        public string? Value(string name) => secrets.TryGetValue(name, out string? value) ? value : null;

        /// <summary>Stores a secret under a name, replacing any previous value.</summary>
        public void Set(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            secrets[name.Trim()] = value;
            Save();
        }

        public void Remove(string name)
        {
            if (secrets.Remove(name))
            {
                Save();
            }
        }

        private static SecretVault Load()
        {
            SecretVault vault = new();

            try
            {
                if (!File.Exists(Path))
                {
                    return vault;
                }

                Dictionary<string, string>? wrapped =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path), Options);

                foreach ((string name, string blob) in wrapped ?? [])
                {
                    string plain = DataProtection.Unprotect(blob);

                    // An empty result means the blob was written by a different user or on a
                    // different machine. Dropping it is right: a secret that cannot be unwrapped is
                    // not a secret, and keeping the name around would offer a command a value it
                    // can never have.
                    if (plain.Length > 0)
                    {
                        vault.secrets[name] = plain;
                    }
                    else
                    {
                        // Worth saying out loud. The command that uses it will run with the
                        // placeholder left as typed, and that failure is otherwise baffling.
                        AppLog.Instance.Warn(
                            "secrets",
                            $"\"{name}\" could not be unwrapped - it was stored by a different user or machine.");
                    }
                }
            }
            catch (Exception exception)
            {
                // A corrupt vault costs the secrets in it, not the app's ability to start. They are
                // re-enterable; a failure to launch is not recoverable by the user at all.
                AppLog.Instance.Failure("secrets", "Could not read the vault", exception);
            }

            return vault;
        }

        private void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsPath.Directory);

                Dictionary<string, string> wrapped = secrets.ToDictionary(
                    pair => pair.Key,
                    pair => DataProtection.Protect(pair.Value),
                    StringComparer.Ordinal);

                string temp = Path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(wrapped, Options));
                File.Move(temp, Path, overwrite: true);

                if (!OperatingSystem.IsWindows())
                {
                    // The AES fallback's key file is already 0600; the blobs it produces deserve
                    // the same treatment rather than defaulting to world-readable.
                    File.SetUnixFileMode(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            catch (Exception exception)
            {
                // Same trade as AppSettings.Save: losing a stored secret is not worth interrupting
                // the user mid-task over, and it can be set again.
                AppLog.Instance.Failure("secrets", "Could not save the vault", exception);
            }
        }
    }
}
