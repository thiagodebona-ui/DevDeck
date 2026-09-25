using System.Collections.Generic;
using System.Text.Json;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The icon a request or a group wears, and the fact that it survives being saved.
    /// </summary>
    /// <remarks>
    ///  Nothing here draws anything. The shapes live in the app project, which has no test rig,
    ///  so what is worth pinning down is the half that Core owns: an icon is a name, a name is a
    ///  string, and a string has to come back out of settings.json looking the way it went in.
    /// </remarks>
    public class IconTests
    {
        [Fact]
        public void ARequestWithNoIconSaysSoWithAnEmptyName()
        {
            Assert.Equal(string.Empty, new HttpRequest().Icon);
        }

        [Fact]
        public void TheIconOnARequestSurvivesARoundTrip()
        {
            HttpRequest sent = new() { Name = "Billing", Icon = "database" };

            string written = JsonSerializer.Serialize(sent);
            HttpRequest? read = JsonSerializer.Deserialize<HttpRequest>(written);

            Assert.NotNull(read);
            Assert.Equal("database", read!.Icon);
        }

        [Fact]
        public void AGroupIconIsKeptBesideTheRequestsRatherThanOnThem()
        {
            // The point of the dictionary: one fact stored once. Moving a request between groups
            // must not carry a group's icon with it, and it cannot, because the request never
            // held it.
            AppSettings settings = new();
            settings.HttpGroupIcons["Billing"] = "card";

            string written = JsonSerializer.Serialize(settings);
            AppSettings? read = JsonSerializer.Deserialize<AppSettings>(written);

            Assert.NotNull(read);
            Assert.Equal("card", read!.HttpGroupIcons["Billing"]);
        }

        [Fact]
        public void AFreshSettingsFileHasNoGroupIcons()
        {
            Assert.Empty(new AppSettings().HttpGroupIcons);
        }

        [Fact]
        public void SettingsWrittenBeforeGroupIconsExistedStillLoad()
        {
            // The property was added after people already had settings files. An absent key has
            // to mean "no icons", not a null dictionary that throws on the first lookup.
            AppSettings? read = JsonSerializer.Deserialize<AppSettings>("{}");

            Assert.NotNull(read);
            Assert.NotNull(read!.HttpGroupIcons);
            Assert.Empty(read.HttpGroupIcons);
        }

        [Fact]
        public void AnIconNameIsStoredVerbatimWhateverItSays()
        {
            // Core does not know which names are real ones - that is the picker's business, and
            // the drawing layer treats an unknown name as no icon. Storing it unchanged is what
            // makes a hand-edited settings file recoverable by fixing the typo.
            HttpRequest request = new() { Icon = "databse" };

            Dictionary<string, string> icons = new() { ["Billing"] = "  card  " };

            Assert.Equal("databse", request.Icon);
            Assert.Equal("  card  ", icons["Billing"]);
        }
    }
}
