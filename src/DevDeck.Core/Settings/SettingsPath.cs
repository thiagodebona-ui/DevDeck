namespace DevDeck.Core
{
    /// <summary>
    ///  Where this build keeps its settings file, and how it got there.
    /// </summary>
    /// <remarks>
    ///  V2 kept settings next to the executable, which is right for a portable Windows app and
    ///  wrong everywhere else: on macOS that is inside a signed .app bundle, where a write either
    ///  fails or breaks the signature, and on Linux it is usually under a system prefix that the
    ///  user cannot write to at all.
    ///
    ///  The rule is portable-first, per-user as the fallback. A settings.json sitting beside the
    ///  executable still wins if it is there and the folder is writable, so an existing Windows
    ///  install and a USB-stick copy both keep working exactly as they did. Otherwise the file
    ///  lives in the platform's own config directory, and a portable file found later is migrated
    ///  into it rather than being read from two places at once.
    /// </remarks>
    internal static class SettingsPath
    {
        private const string FileName = "settings.json";

        private static string? resolved;

        /// <summary>The settings file this run reads and writes.</summary>
        public static string Current => resolved ??= Resolve();

        /// <summary>
        ///  The folder the user sees the app in: the launcher's, not the one the code runs from.
        /// </summary>
        /// <remarks>
        ///  A published build is a launcher beside a lib folder that holds the app and its runtime,
        ///  so <see cref="AppContext.BaseDirectory"/> is lib. Portable files belong one level up,
        ///  next to the thing that was double-clicked - and next to where every existing install
        ///  already has them. Only a lib folder directly under the running launcher counts, so
        ///  <c>dotnet run</c>, a test host and an old flat publish all keep the base directory.
        /// </remarks>
        public static string Home { get; } = FindHome();

        /// <summary>The folder holding it, for anything else that needs to sit alongside.</summary>
        public static string Directory => System.IO.Path.GetDirectoryName(Current) ?? Home;

        private static string FindHome()
        {
            string code = System.IO.Path.GetFullPath(AppContext.BaseDirectory)
                .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

            string? launcher = System.IO.Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty);

            if (!string.IsNullOrEmpty(launcher)
                && string.Equals(System.IO.Path.GetFileName(code), "lib", StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    System.IO.Path.GetDirectoryName(code),
                    System.IO.Path.GetFullPath(launcher).TrimEnd(System.IO.Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
            {
                return launcher;
            }

            return AppContext.BaseDirectory;
        }

        /// <summary>Whether the file lives beside the executable rather than in the user's profile.</summary>
        public static bool IsPortable =>
            string.Equals(
                System.IO.Path.GetFullPath(Directory).TrimEnd(System.IO.Path.DirectorySeparatorChar),
                System.IO.Path.GetFullPath(Home).TrimEnd(System.IO.Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

        /// <summary>The per-user config folder for this platform, whether or not it is in use.</summary>
        public static string UserDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevDeck");
            }

            if (OperatingSystem.IsMacOS())
            {
                return System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "DevDeck");
            }

            // XDG, with the spec's own default when the variable is unset.
            string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? string.Empty;

            if (config.Length == 0)
            {
                config = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            }

            return System.IO.Path.Combine(config, "devdeck");
        }

        /// <summary>
        ///  Picks the file, and moves a portable one into the user folder when it can no longer be
        ///  written where it sits.
        /// </summary>
        private static string Resolve()
        {
            string portable = System.IO.Path.Combine(Home, FileName);
            string user = System.IO.Path.Combine(UserDirectory(), FileName);

            // An existing portable file keeps working, as long as the folder still takes a write -
            // which is the case that matters on Windows and the one that fails inside an .app.
            if (File.Exists(portable) && Writable(Home))
            {
                return portable;
            }

            if (File.Exists(user))
            {
                return user;
            }

            // A portable file we cannot write to: carry it across rather than starting empty and
            // silently losing every saved command.
            if (File.Exists(portable))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(UserDirectory());
                    File.Copy(portable, user, overwrite: false);

                    AppLog.Instance.Warn(
                        "settings",
                        $"Could not write beside the executable, so the settings were copied to {user}.");

                    return user;
                }
                catch (Exception)
                {
                    // Fall through: a fresh file in the user folder beats refusing to start.
                }
            }

            // Fresh install. Windows stays portable, so a copied folder carries its commands with
            // it the way V2's did; the other two platforms have nowhere safe beside the binary.
            if (OperatingSystem.IsWindows() && Writable(Home))
            {
                return portable;
            }

            try
            {
                System.IO.Directory.CreateDirectory(UserDirectory());
            }
            catch (Exception)
            {
                return portable;
            }

            return user;
        }

        /// <summary>
        ///  Whether a folder actually takes a write, asked by writing rather than by reading ACLs.
        /// </summary>
        /// <remarks>
        ///  Permissions, read-only media, a signed bundle and a virus scanner holding the folder all
        ///  produce different exceptions and none of them are visible in the attributes. The only
        ///  honest test is the write itself.
        /// </remarks>
        private static bool Writable(string directory)
        {
            try
            {
                string probe = System.IO.Path.Combine(directory, $".devdeck-write-{Guid.NewGuid():N}");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
