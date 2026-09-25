using System.Text.Json.Serialization;

namespace DevDeck.Core
{
    /// <summary>
    ///  Everything one provider needs to be talked to: its endpoint, the model picked for it, and
    ///  its key.
    /// </summary>
    /// <remarks>
    ///  One of these per provider rather than one endpoint for the whole app, because the settings
    ///  are not interchangeable: switching to Groq to try it used to overwrite the Ollama model
    ///  name, and switching back left an endpoint pointing at a model that is not there. Each
    ///  provider keeping its own means switching is a drop-down again, not a retype.
    /// </remarks>
    internal sealed class AiProfile
    {
        /// <summary>The preset this belongs to, by name. The key the profiles are found by.</summary>
        public string Provider { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public string Model { get; set; } = string.Empty;

        /// <summary>
        ///  What this endpoint last said it had, so the Assistant tab can offer the list before
        ///  anything has answered - and still offer it when the provider is offline.
        /// </summary>
        public List<string> Models { get; set; } = [];

        /// <summary>Key for this endpoint, in the clear and in memory only.</summary>
        [JsonIgnore]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        ///  The only form of the key that reaches the disk: wrapped by DPAPI, exactly as the single
        ///  key on <see cref="AppSettings"/> was.
        /// </summary>
        [JsonPropertyName("ApiKeyProtected")]
        public string ProtectedApiKey
        {
            get => DataProtection.Protect(ApiKey);
            set => ApiKey = DataProtection.Unprotect(value);
        }

        /// <summary>A profile holding the preset's own defaults, for a provider never used yet.</summary>
        public static AiProfile FromPreset(AiPreset preset) => new()
        {
            Provider = preset.Name,
            BaseUrl = preset.BaseUrl,
            Model = preset.Model
        };
    }
}
