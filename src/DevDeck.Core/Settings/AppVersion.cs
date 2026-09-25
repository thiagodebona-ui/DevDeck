using System.Reflection;

namespace DevDeck.Core
{
    /// <summary>
    ///  The running build's version, read from the assembly rather than written out twice.
    /// </summary>
    /// <remarks>
    ///  The number itself lives in one place - the Version property in the .csproj - so bumping a
    ///  release means editing a single line. Everything on screen comes from here, which is what
    ///  stops the title bar and the Settings tab ever disagreeing about which build this is.
    /// </remarks>
    internal static class AppVersion
    {
        /// <summary>The version as written in the project file, for example "2.1.0".</summary>
        public static string Number { get; } = Read();

        /// <summary>When this build was produced, for telling two builds of one version apart.</summary>
        public static DateTime Built { get; } = ReadBuildDate();

        /// <summary>
        ///  The same number as a <see cref="System.Version"/>, for comparing with a release.
        /// </summary>
        /// <remarks>
        ///  Any prerelease suffix is dropped before parsing, because <see cref="System.Version"/>
        ///  refuses "3.0.0-alpha.1" outright and a failed parse here would fall back to 0.0.0 -
        ///  which would quietly make every published release look newer than the running build and
        ///  leave the update check permanently shouting. Dropping the suffix treats an alpha as its
        ///  own release number, which is imprecise in exactly one case (the final build of a
        ///  version the user already runs a prerelease of) and right in all the others.
        /// </remarks>
        public static Version Current { get; } = Parse(Number);

        private static Version Parse(string number)
        {
            int dash = number.IndexOf('-');
            string numeric = dash > 0 ? number[..dash] : number;

            return Version.TryParse(numeric, out Version? parsed) ? parsed : new Version(0, 0, 0);
        }

        /// <summary>"2.1.0, built 3 Sep 2026" - what the Settings tab shows.</summary>
        public static string Display => $"Version {Number}, built {Built:d MMM yyyy}";

        private static string Read()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            // The informational version is the one that keeps any suffix; the file version drops it.
            string? informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // The SDK appends "+<commit sha>" when the build knows one; that is noise on screen.
                int plus = informational.IndexOf('+');

                return plus > 0 ? informational[..plus] : informational;
            }

            Version? version = assembly.GetName().Version;

            return version is null ? "unknown" : $"{version.Major}.{version.Minor}.{version.Build}";
        }

        /// <summary>
        ///  Build date taken from the assembly file itself.
        /// </summary>
        /// <remarks>
        ///  .NET builds are deterministic by default, so the old trick of reading the PE header's
        ///  timestamp returns a fixed fake date. The file's own write time is the honest answer.
        /// </remarks>
        private static DateTime ReadBuildDate()
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;

                if (path.Length == 0)
                {
                    // Single-file publish leaves Location empty; fall back to the launcher.
                    path = Environment.ProcessPath ?? string.Empty;
                }

                return path.Length > 0 ? File.GetLastWriteTime(path) : DateTime.Now;
            }
            catch (Exception)
            {
                return DateTime.Now;
            }
        }
    }
}
