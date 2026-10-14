using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>The memory widget's look, as stored and as named in its menu.</summary>
    [Collection("Strings")]
    public class WidgetSkinTests
    {
        /// <summary>A file written before there was a choice keeps the dial.</summary>
        [Fact]
        public void AMissingOrUnknownSkinIsTheDial()
        {
            Assert.Equal(WidgetSkin.Gauge, WidgetSkins.Parse(null));
            Assert.Equal(WidgetSkin.Gauge, WidgetSkins.Parse(""));
            Assert.Equal(WidgetSkin.Gauge, WidgetSkins.Parse("Spaceship"));
            Assert.Equal(WidgetSkin.Gauge, WidgetSkins.Parse(new AppSettings().MemoryWidgetSkin));
        }

        /// <summary>A number is not a name: "3" must not quietly pick whichever skin is third.</summary>
        [Fact]
        public void ANumberIsNotASkin()
        {
            Assert.Equal(WidgetSkin.Gauge, WidgetSkins.Parse("3"));
            Assert.Equal(WidgetSkin.Gauge, WidgetSkins.Parse("99"));
        }

        [Fact]
        public void EverySkinReadsBackByItsStoredName()
        {
            foreach (WidgetSkin skin in WidgetSkins.All)
            {
                Assert.Equal(skin, WidgetSkins.Parse(skin.ToString()));
                Assert.Equal(skin, WidgetSkins.Parse(skin.ToString().ToLowerInvariant()));
            }
        }

        [Fact]
        public void EverySkinHasAMenuNameInEveryLanguage()
        {
            try
            {
                foreach (LanguageChoice language in AppLanguage.All)
                {
                    Strings.Use(language);

                    foreach (WidgetSkin skin in WidgetSkins.All)
                    {
                        string key = WidgetSkins.NameKey(skin);

                        Assert.NotEqual(key, Strings.Text(key));
                        Assert.False(string.IsNullOrWhiteSpace(Strings.Text(key)));
                    }

                    Assert.NotEqual("MemoryWidgetSkinMenu", Strings.Text("MemoryWidgetSkinMenu"));
                }
            }
            finally
            {
                Strings.Use(AppLanguage.English);
            }
        }
    }
}
