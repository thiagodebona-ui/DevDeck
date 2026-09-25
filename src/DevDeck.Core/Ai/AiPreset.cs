namespace DevDeck.Core
{
    /// <summary>A ready-made endpoint, so getting started is a drop-down rather than a URL hunt.</summary>
    internal sealed record AiPreset(string Name, string BaseUrl, string Model, bool NeedsKey)
    {
        public override string ToString() => Name;
    }

    /// <summary>
    ///  The free ways to run this, local first.
    /// </summary>
    /// <remarks>
    ///  Ollama leads because it is the only one that is free with no sign-up and no data leaving the
    ///  machine, which matters when the thing you are pasting in is work code. The hosted options
    ///  are all genuinely free tiers rather than trials, and all speak the same OpenAI dialect, so
    ///  switching is a URL and a key.
    /// </remarks>
    internal static class AiPresets
    {
        public const string CustomName = "Custom…";

        public static readonly AiPreset[] All =
        [
            new("Ollama - local, free, no key", "http://localhost:11434/v1", "qwen2.5-coder:7b", false),
            new("LM Studio - local, free, no key", "http://localhost:1234/v1", "", false),
            new("Groq - free tier", "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile", true),
            new("Google Gemini - free tier", "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-2.0-flash", true),
            new("OpenRouter - free models", "https://openrouter.ai/api/v1", "meta-llama/llama-3.3-70b-instruct:free", true),
            new(CustomName, "", "", false)
        ];

        /// <summary>Finds a preset by the name its profile was saved under, or nothing.</summary>
        public static AiPreset? ByName(string name) =>
            All.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Finds the preset a saved base URL came from, or the custom entry.</summary>
        public static AiPreset Match(string baseUrl) =>
            All.FirstOrDefault(p => p.BaseUrl.Length > 0
                && string.Equals(p.BaseUrl, baseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            ?? All[^1];
    }
}
