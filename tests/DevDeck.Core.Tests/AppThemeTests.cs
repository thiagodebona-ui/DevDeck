using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  Reading a stored theme back. This is an upgrade path: the value in a settings file may have
    ///  been written by V1, V2, or either of V3's two attempts at naming themes.
    /// </summary>
    public class AppThemeTests
    {
        [Fact]
        public void EveryShippedThemeHasADistinctIdAndName()
        {
            Assert.Equal(AppTheme.All.Count, AppTheme.All.Select(t => t.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.Equal(AppTheme.All.Count, AppTheme.All.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        /// <summary>
        ///  A palette that is listed but has no dictionary behind it would leave the app with no
        ///  brushes at all, which is a blank window rather than a wrong colour.
        /// </summary>
        [Fact]
        public void EveryShippedThemeRoundTripsThroughParse()
        {
            foreach (ThemePalette palette in AppTheme.All)
            {
                Assert.Equal(palette.Id, AppTheme.Parse(palette.Id).Id);
                Assert.Equal(palette.Id, AppTheme.Parse(palette.Name).Id);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("something nobody has ever heard of")]
        public void UnknownValuesFallBackToDark(string? stored)
        {
            // Not merely "a dark theme": falling back somewhere predictable is the point, because
            // this is what a corrupted or hand-edited settings file lands on.
            Assert.Equal(AppTheme.Dark.Id, AppTheme.Parse(stored).Id);
        }

        /// <summary>V3's first cut stored the three Avalonia variant names.</summary>
        [Theory]
        [InlineData("Dark", "SteelDark")]
        [InlineData("dark", "SteelDark")]
        [InlineData("Light", "SteelLight")]
        [InlineData("System", AppTheme.SystemId)]
        public void EarlierV3NamesStillResolve(string stored, string expected)
        {
            Assert.Equal(expected, AppTheme.Parse(stored).Id);
        }

        /// <summary>
        ///  V2 stored a Krypton palette name. There were about forty, and the only thing they had
        ///  in common was saying so in the name.
        /// </summary>
        [Theory]
        [InlineData("Office2010Black", false)]
        [InlineData("Office365DarkGray", false)]
        [InlineData("SparkleBlueDarkMode", false)]
        [InlineData("Office2010Silver", true)]
        [InlineData("Office365White", true)]
        public void V2PaletteNamesLandOnTheRightSide(string stored, bool expectLight)
        {
            ThemePalette parsed = AppTheme.Parse(stored);

            Assert.Equal(expectLight, !parsed.IsDark);
        }

        [Fact]
        public void SystemResolvesToWhicheverTheDesktopIs()
        {
            ThemePalette system = AppTheme.All.Single(t => AppTheme.IsSystem(t));

            Assert.Equal(AppTheme.Dark.Id, AppTheme.Resolve(system, desktopIsDark: true).Id);
            Assert.Equal(AppTheme.Light.Id, AppTheme.Resolve(system, desktopIsDark: false).Id);
        }

        [Fact]
        public void AnExplicitThemeIgnoresTheDesktop()
        {
            Assert.Equal(AppTheme.Light.Id, AppTheme.Resolve(AppTheme.Light, desktopIsDark: true).Id);
            Assert.Equal(AppTheme.Dark.Id, AppTheme.Resolve(AppTheme.Dark, desktopIsDark: false).Id);
        }

        [Fact]
        public void SourceUriPointsAtTheThemesFolder()
        {
            Assert.All(AppTheme.All, palette =>
                Assert.Equal($"avares://DevDeck/Themes/{palette.Id}.axaml", palette.Source));
        }
    }
}
