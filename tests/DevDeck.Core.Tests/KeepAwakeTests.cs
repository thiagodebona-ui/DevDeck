using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  When keep-awake decides to act, and what it says it is doing.
    /// </summary>
    /// <remarks>
    ///  The parts worth testing without a desktop are the two decisions: whether a nudge is due,
    ///  and what the settings page is told. The calls themselves are the operating system's and
    ///  asserting them here would only be asserting that P/Invoke exists.
    ///
    ///  This whole feature shipped once as a tick box wired to nothing, so the test that matters
    ///  most is the one that pins the quiet behaviour: a rule that silently answers "no" forever
    ///  looks exactly like the bug that was reported.
    /// </remarks>
    public class KeepAwakeTests
    {
        #region When a nudge is due

        /// <summary>
        ///  While the user is actually typing there is nothing to do - their own keystrokes reset
        ///  the clock. Injecting one here is what made V1's jiggler fight for the keyboard.
        /// </summary>
        [Fact]
        public void NothingIsSentWhileTheUserIsWorking()
        {
            Assert.False(KeepAwake.ShouldNudge(true, true, TimeSpan.Zero));
            Assert.False(KeepAwake.ShouldNudge(true, true, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void AQuietMachineIsNudged()
        {
            Assert.True(KeepAwake.ShouldNudge(true, true, KeepAwake.Quiet));
            Assert.True(KeepAwake.ShouldNudge(true, true, TimeSpan.FromMinutes(10)));
        }

        /// <summary>
        ///  The second switch is a separate consent. Holding the machine awake is a power request;
        ///  typing on the user's behalf is not, and one must not imply the other.
        /// </summary>
        [Fact]
        public void TheIdleClockIsOnlyHeldBackWhenAskedFor()
        {
            Assert.False(KeepAwake.ShouldNudge(true, false, TimeSpan.FromMinutes(10)));
        }

        /// <summary>
        ///  A machine that is allowed to sleep must not be poked awake. Off means off, whatever the
        ///  other switch says.
        /// </summary>
        [Fact]
        public void NothingIsSentWhenKeepAwakeIsOff()
        {
            Assert.False(KeepAwake.ShouldNudge(false, true, TimeSpan.FromMinutes(10)));
            Assert.False(KeepAwake.ShouldNudge(false, false, TimeSpan.FromMinutes(10)));
        }

        /// <summary>
        ///  Shorter than any lock policy worth defeating and longer than a pause for thought. Both
        ///  ends matter: a threshold over a minute would lose the race against a one-minute policy,
        ///  and one under about ten seconds would be typing into the user's own pauses.
        /// </summary>
        [Fact]
        public void TheQuietThresholdSitsBetweenAPauseAndALockPolicy()
        {
            Assert.InRange(KeepAwake.Quiet, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));
        }

        #endregion

        #region What it says it is doing

        /// <summary>
        ///  The readout never goes blank. Reporting nothing is what left the user unable to tell a
        ///  working switch from an inert one, which is the bug this feature was fixed for.
        /// </summary>
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void ThereIsAlwaysSomethingToRead(bool enabled, bool applied, bool presence)
        {
            Assert.NotEmpty(KeepAwake.Describe(enabled, applied, presence));
        }

        /// <summary>
        ///  A refusal is reported as one rather than as success. Saying "on" over a request the OS
        ///  turned down would be the original bug wearing a label.
        /// </summary>
        [Fact]
        public void ARefusedRequestDoesNotClaimToBeOn()
        {
            string refused = KeepAwake.Describe(enabled: true, applied: false, presenceOn: false);

            Assert.Contains("refused", refused, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("will not sleep", refused, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Locking is named only when the half that actually defeats it is running.</summary>
        [Fact]
        public void OnlyThePresenceHalfClaimsToStopALock()
        {
            Assert.Contains("lock", KeepAwake.Describe(true, true, true), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("lock", KeepAwake.Describe(true, true, false), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void OffSaysTheMachineIsBackOnItsOwnSettings()
        {
            Assert.Contains("own", KeepAwake.Describe(false, false, false), StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region What this platform can do

        /// <summary>
        ///  Every desktop this ships to has some way to ask. Unlike the hotkey, there is no
        ///  platform here where the honest answer is "not possible".
        /// </summary>
        [Fact]
        public void EveryPlatformCanHoldTheMachineAwake()
        {
            Assert.True(KeepAwake.IsAvailable);
        }

        /// <summary>
        ///  Injection is Windows-only on purpose: elsewhere it needs permission to type on the
        ///  user's behalf, which is too much to ask for a key nobody has.
        /// </summary>
        [Fact]
        public void HoldingBackTheIdleClockIsWindowsOnly()
        {
            Assert.Equal(OperatingSystem.IsWindows(), KeepAwake.IsPresenceAvailable);
        }

        /// <summary>
        ///  The idle clock fails open. Reporting zero on an error would read as "the user is busy"
        ///  and switch the nudge off for good - the same silent nothing being fixed.
        /// </summary>
        [Fact]
        public void TheIdleClockFailsOpenRatherThanReportingBusy()
        {
            Assert.True(KeepAwake.Idle > TimeSpan.Zero || OperatingSystem.IsWindows());
        }

        #endregion
    }
}
