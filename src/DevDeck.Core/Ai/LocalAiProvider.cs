using System.Diagnostics;

namespace DevDeck.Core
{
    /// <summary>
    ///  A local AI runtime the app knows how to install and start for the user.
    /// </summary>
    /// <remarks>
    ///  Only local runtimes appear here. A hosted provider needs an account and a key, which is not
    ///  something to automate on someone's behalf.
    /// </remarks>
    internal abstract class LocalAiProvider
    {
        public abstract string Name { get; }

        /// <summary>winget package, used when the runtime is not installed yet.</summary>
        public abstract string PackageId { get; }

        /// <summary>Where to get it by hand if winget is unavailable or blocked.</summary>
        public abstract string DownloadUrl { get; }

        /// <summary>
        ///  False when the user has to finish something in the runtime's own window. Ollama is a
        ///  server and can be driven end to end; LM Studio is a desktop app that owns its models.
        /// </summary>
        public abstract bool FullyAutomatic { get; }

        /// <summary>Path to the runtime's command line tool, or null when it is not installed.</summary>
        public abstract string? FindExecutable();

        /// <summary>
        ///  Gets the runtime installed, running and - where it can - holding the wanted model.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///  Setup cannot go further on its own, carrying the reason and what to do about it.
        /// </exception>
        public abstract Task PrepareAsync(
            AiClient client,
            string model,
            Action<string> milestone,
            Action<string> status,
            CancellationToken token);

        /// <summary>Every runtime the app can set up.</summary>
        public static readonly LocalAiProvider[] All = [new OllamaProvider(), new LmStudioProvider()];

        /// <summary>
        ///  The runtime that owns <paramref name="baseUrl"/>, or null when the endpoint is not one
        ///  this app can install - a hosted provider, or a local server of the user's own.
        /// </summary>
        public static LocalAiProvider? For(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) || !uri.IsLoopback)
            {
                return null;
            }

            return All.FirstOrDefault(provider => provider.OwnsPort(uri.Port));
        }

        protected abstract bool OwnsPort(int port);

        /// <summary>Installs the package with winget when the runtime is missing.</summary>
        protected async Task InstallAsync(Action<string> milestone, Action<string> status, CancellationToken token)
        {
            string winget = AiSetup.FindWinget()
                ?? throw new InvalidOperationException(
                    $"{Name} is not installed, and winget is not available here to install it."
                    + $"{Environment.NewLine}Install it by hand from {DownloadUrl}, then choose Check again.");

            milestone($"Installing {Name}. This can take a few minutes…");

            int code = await AiSetup.RunProcessAsync(
                winget,
                $"install --id {PackageId} --exact --silent --accept-package-agreements "
                    + "--accept-source-agreements --disable-interactivity",
                status,
                token);

            // winget returns non-zero for things that are not failures, "already installed" among
            // them, so the file on disk is the real answer.
            if (FindExecutable() is null)
            {
                throw new InvalidOperationException(
                    $"The {Name} install did not complete (winget exit code {code})."
                    + $"{Environment.NewLine}Install it by hand from {DownloadUrl}, then choose Check again.");
            }

            milestone($"{Name} installed.");
        }
    }

    /// <summary>
    ///  Ollama: a background server with a command line, so this can be done start to finish
    ///  without the user touching anything.
    /// </summary>
    internal sealed class OllamaProvider : LocalAiProvider
    {
        public override string Name => "Ollama";

        public override string PackageId => "Ollama.Ollama";

        public override string DownloadUrl => "https://ollama.com/download";

        public override bool FullyAutomatic => true;

        protected override bool OwnsPort(int port) => port == 11434;

        public override string? FindExecutable() => AiSetup.FindProgram(
            "ollama.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Ollama", "ollama.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Ollama", "ollama.exe"));

        public override async Task PrepareAsync(
            AiClient client,
            string model,
            Action<string> milestone,
            Action<string> status,
            CancellationToken token)
        {
            string? ollama = FindExecutable();

            if (ollama is null)
            {
                await InstallAsync(milestone, status, token);
                ollama = FindExecutable()!;
            }
            else
            {
                milestone("Ollama is already installed.");
            }

            token.ThrowIfCancellationRequested();

            if (!await AiSetup.IsReachableAsync(client, token))
            {
                milestone("Starting the Ollama server…");
                AiSetup.StartDetached(ollama, "serve");

                if (!await AiSetup.WaitForServerAsync(client, status, token))
                {
                    throw new InvalidOperationException(
                        $"Ollama is installed but nothing is answering on {client.BaseUrl}."
                        + $"{Environment.NewLine}Start Ollama from the Start menu, then choose Check again.");
                }
            }

            milestone("The Ollama server is running.");
            token.ThrowIfCancellationRequested();

            if (model.Length == 0)
            {
                throw new InvalidOperationException("No model is set on the Settings tab.");
            }

            List<string> present = await AiSetup.SafeListAsync(client, token);

            if (present.Any(name => string.Equals(name, model, StringComparison.OrdinalIgnoreCase)))
            {
                milestone($"The model {model} is already downloaded.");
                return;
            }

            AiSetup.EnsureDiskSpace();

            milestone($"Downloading {model}. This is several gigabytes and only happens once…");

            int code = await AiSetup.RunProcessAsync(ollama, $"pull {model}", status, token);

            if (code != 0)
            {
                throw new InvalidOperationException(
                    $"Downloading {model} failed (exit code {code})."
                    + $"{Environment.NewLine}Check the model name on the Settings tab - it must be one "
                    + "Ollama publishes, such as qwen2.5-coder:7b.");
            }

            milestone($"{model} downloaded.");
        }
    }

    /// <summary>
    ///  LM Studio: a desktop app that happens to expose a server.
    /// </summary>
    /// <remarks>
    ///  Its command line tool, lms, only appears once the app itself has run at least once, and
    ///  models are managed inside its own window. So this installs it, starts it, and turns the
    ///  server on when it can - and says plainly when the last step has to be done by hand, rather
    ///  than pretending and leaving the user staring at a dead tab.
    /// </remarks>
    internal sealed class LmStudioProvider : LocalAiProvider
    {
        public override string Name => "LM Studio";

        public override string PackageId => "ElementLabs.LMStudio";

        public override string DownloadUrl => "https://lmstudio.ai/download";

        public override bool FullyAutomatic => false;

        protected override bool OwnsPort(int port) => port == 1234;

        /// <summary>The lms command line tool, which the app installs on first run.</summary>
        public override string? FindExecutable()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return AiSetup.FindProgram(
                "lms.exe",
                Path.Combine(home, ".lmstudio", "bin", "lms.exe"),
                Path.Combine(home, ".cache", "lm-studio", "bin", "lms.exe"));
        }

        /// <summary>The desktop app, which exists before lms does.</summary>
        private static string? FindApp() => AiSetup.FindProgram(
            "LM Studio.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "LM Studio", "LM Studio.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "LM Studio", "LM Studio.exe"));

        public override async Task PrepareAsync(
            AiClient client,
            string model,
            Action<string> milestone,
            Action<string> status,
            CancellationToken token)
        {
            if (FindApp() is null && FindExecutable() is null)
            {
                await InstallAsync(milestone, status, token);
            }
            else
            {
                milestone("LM Studio is already installed.");
            }

            token.ThrowIfCancellationRequested();

            if (await AiSetup.IsReachableAsync(client, token))
            {
                milestone("The LM Studio server is already running.");
            }
            else
            {
                string? lms = FindExecutable();

                if (lms is not null)
                {
                    milestone("Starting the LM Studio server…");
                    await AiSetup.RunProcessAsync(lms, "server start", status, token);
                }
                else
                {
                    // lms is written on the app's first run, so open the app and let it appear.
                    milestone("Opening LM Studio so it can finish setting itself up…");
                    OpenApp();
                }

                if (!await AiSetup.WaitForServerAsync(client, status, token))
                {
                    throw new InvalidOperationException(
                        $"LM Studio is installed but its server is not answering on {client.BaseUrl}."
                        + Environment.NewLine + Environment.NewLine
                        + "In the LM Studio window, open the Developer tab and turn the local server on, "
                        + "then choose Check again here.");
                }
            }

            token.ThrowIfCancellationRequested();

            List<string> present = await AiSetup.SafeListAsync(client, token);

            if (present.Count == 0)
            {
                OpenApp();

                throw new InvalidOperationException(
                    "The LM Studio server is running but has no model loaded."
                    + Environment.NewLine + Environment.NewLine
                    + "In the LM Studio window, search for a model on the Discover tab, download it, "
                    + "then choose Check again here. A coding model such as Qwen2.5 Coder 7B suits this app.");
            }

            milestone(present.Any(name => string.Equals(name, model, StringComparison.OrdinalIgnoreCase))
                ? $"The model {model} is loaded."
                : $"LM Studio has {present.Count} model(s) available.");
        }

        private static void OpenApp()
        {
            if (FindApp() is string app)
            {
                AiSetup.StartDetached(app, string.Empty, shell: true);
            }
        }
    }
}
