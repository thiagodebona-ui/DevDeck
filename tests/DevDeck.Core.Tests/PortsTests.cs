using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  What the listening panel reads, and the one thing that emptied it.
    /// </summary>
    /// <remarks>
    ///  The listing itself is the machine's, so what is pinned here is the shape of the answer
    ///  rather than its contents: a developer machine has listeners, and the panel must show them.
    ///
    ///  The bug these were written for looked exactly like a machine with nothing listening. Every
    ///  port whose owner netstat could not name left a default tuple with a null name, reading
    ///  Length off it threw, and the blanket catch around the loop turned the throw into an empty
    ///  list - so the failure presented as the honest answer to a different question.
    /// </remarks>
    public class PortsTests
    {
        #region What is listening

        /// <summary>
        ///  Any machine running this has something listening on it. A zero here is the reported
        ///  bug, not a quiet machine.
        /// </summary>
        [Fact]
        public void AMachineRunningThisHasListeners()
        {
            Assert.NotEmpty(Ports.Listening());
        }

        /// <summary>
        ///  The failure was in the ports netstat could not account for, so the test that catches it
        ///  is the one that walks every row rather than the first.
        /// </summary>
        [Fact]
        public void EveryRowSurvivesBeingRead()
        {
            foreach (ListeningPort port in Ports.Listening())
            {
                Assert.InRange(port.Port, 1, 65_535);
                Assert.False(string.IsNullOrEmpty(port.Process));
                Assert.False(string.IsNullOrEmpty(port.Address));
            }
        }

        /// <summary>
        ///  An unidentified owner is named, not left blank. "unknown" still answers "is something
        ///  holding this port", which is the question being asked.
        /// </summary>
        [Fact]
        public void APortWithNoNameableOwnerStillSaysSomething()
        {
            IReadOnlyList<ListeningPort> ports = Ports.Listening();

            Assert.All(ports, port => Assert.NotEqual(string.Empty, port.Process));
        }

        /// <summary>One row per port: a service on both stacks is one thing, not two.</summary>
        [Fact]
        public void EachPortAppearsOnce()
        {
            IReadOnlyList<ListeningPort> ports = Ports.Listening();

            Assert.Equal(ports.Count, ports.Select(port => port.Port).Distinct().Count());
        }

        [Fact]
        public void TheListIsInPortOrder()
        {
            IReadOnlyList<ListeningPort> ports = Ports.Listening();

            Assert.Equal([.. ports.Select(port => port.Port).Order()], [.. ports.Select(port => port.Port)]);
        }

        #endregion

        #region What a port is called

        [Fact]
        public void TheConventionalOwnerOfAPortIsNamed()
        {
            Assert.Equal("postgres", new ListeningPort(5432, 0, "x", "::").Note);
            Assert.Equal("dev server", new ListeningPort(5173, 0, "x", "::").Note);
            Assert.Equal("https", new ListeningPort(443, 0, "x", "::").Note);
        }

        /// <summary>A port with no convention says nothing rather than guessing.</summary>
        [Fact]
        public void AnUnconventionalPortIsNotGuessedAt()
        {
            Assert.Empty(new ListeningPort(49_665, 0, "x", "::").Note);
            Assert.False(new ListeningPort(49_665, 0, "x", "::").HasNote);
        }

        /// <summary>Only the ports a browser could do something with offer to open one.</summary>
        [Fact]
        public void OnlyWebPortsOfferToOpen()
        {
            Assert.True(new ListeningPort(8080, 0, "x", "::").IsWeb);
            Assert.False(new ListeningPort(5432, 0, "x", "::").IsWeb);
        }

        #endregion

        #region Stopping something

        /// <summary>
        ///  A row whose owner was never identified has nothing to kill, and says so rather than
        ///  killing process zero.
        /// </summary>
        [Fact]
        public void AnUnidentifiedOwnerIsNotKilled()
        {
            string said = Ports.Kill(new ListeningPort(5432, 0, "unknown", "::"));

            Assert.Contains("could not be identified", said, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
