using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>The background effect setting, and folding many processes into one row.</summary>
    public class ThemeEffectAndGroupingTests
    {
        [Fact]
        public void MatchingTheThemeWearsTheThemesOwnEffect()
        {
            ThemePalette matrix = AppTheme.Parse("Matrix");

            Assert.Equal(ThemeBackdrop.Rain, ThemeEffect.Resolve(null, matrix));
            Assert.Equal(ThemeBackdrop.Rain, ThemeEffect.Resolve(ThemeEffect.MatchTheme, matrix));
        }

        [Fact]
        public void AChosenEffectBeatsTheThemes()
        {
            Assert.Equal(ThemeBackdrop.None, ThemeEffect.Resolve("None", AppTheme.Parse("Aurora")));
            Assert.Equal(ThemeBackdrop.Snow, ThemeEffect.Resolve("snow", AppTheme.Dark));
        }

        /// <summary>A file written before effects existed keeps the plain look it always had.</summary>
        [Fact]
        public void TheClassicThemesHaveNoEffect()
        {
            Assert.Equal(ThemeBackdrop.None, ThemeEffect.Resolve(null, AppTheme.Dark));
            Assert.Equal(ThemeBackdrop.None, ThemeEffect.Resolve(null, AppTheme.Light));
            Assert.Equal(ThemeBackdrop.None, ThemeEffect.Resolve("garbage", AppTheme.Dark));
        }

        [Fact]
        public void EveryEffectIsOfferedOnceAfterMatchTheTheme()
        {
            Assert.Equal(ThemeEffect.MatchTheme, ThemeEffect.Choices[0]);
            Assert.Equal(Enum.GetValues<ThemeBackdrop>().Length + 1, ThemeEffect.Choices.Distinct().Count());
        }

        [Fact]
        public void EveryEffectHasAThemeThatWearsIt()
        {
            foreach (ThemeBackdrop effect in Enum.GetValues<ThemeBackdrop>().Where(e => e != ThemeBackdrop.None))
            {
                Assert.Contains(AppTheme.All, palette => palette.Backdrop == effect);
            }
        }

        [Fact]
        public void CopiesOfOneProgramBecomeOneRow()
        {
            ProcessUsage[] found =
            [
                new(10, "node", 300, 1.5),
                new(11, "NODE", 500, 2.0),
                new(12, "node", 100, 0.5),
                new(20, "chrome", 700, 4),
            ];

            List<ProcessUsage> grouped = [.. ProcessSampler.Group(found).OrderByDescending(u => u.WorkingSet)];

            Assert.Equal(2, grouped.Count);

            ProcessUsage node = grouped.Single(u => u.Name.Equals("node", StringComparison.OrdinalIgnoreCase));

            Assert.Equal(900, node.WorkingSet);
            Assert.Equal(4.0, node.CpuPercent, 3);
            Assert.True(node.IsGroup);
            Assert.Equal(3, node.Count);

            // The row is named and keyed by its heaviest member.
            Assert.Equal(11, node.Id);
            Assert.Equal([11, 10, 12], node.Ids);
        }

        [Fact]
        public void ALoneProcessIsExactlyTheRowItWas()
        {
            ProcessUsage single = ProcessSampler.Group([new ProcessUsage(20, "chrome", 700, 4)]).Single();

            Assert.False(single.IsGroup);
            Assert.Equal(20, single.Id);
            Assert.Equal([20], single.Ids);
        }
    }
}
