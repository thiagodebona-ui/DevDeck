using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The parts of the ports and containers panels that do not need the machine to cooperate.
    /// </summary>
    /// <remarks>
    ///  Neither panel can be tested end to end here - one depends on what happens to be listening
    ///  and the other on an engine that may not be installed. What is testable is the reading: the
    ///  port conventions, the port parsing out of an engine's format string, and that a listing
    ///  with no engine behind it returns nothing rather than failing.
    /// </remarks>
    public class PortAndContainerTests
    {
        [Theory]
        [InlineData(5432, "postgres")]
        [InlineData(6379, "redis")]
        [InlineData(3000, "dev server")]
        [InlineData(5173, "dev server")]
        [InlineData(443, "https")]
        [InlineData(27017, "mongo")]
        public void NamesThePortsEveryoneKnows(int port, string note)
        {
            ListeningPort listening = new(port, 1, "node", "0.0.0.0");

            Assert.Equal(note, listening.Note);
            Assert.True(listening.HasNote);
        }

        [Fact]
        public void SaysNothingAboutAPortWithNoConvention()
        {
            ListeningPort listening = new(49_737, 1, "svchost", "0.0.0.0");

            Assert.Equal(string.Empty, listening.Note);
            Assert.False(listening.HasNote);
        }

        [Fact]
        public void OffersToOpenOnlyThePortsABrowserWouldUnderstand()
        {
            Assert.True(new ListeningPort(3000, 1, "node", "::").IsWeb);
            Assert.False(new ListeningPort(5432, 1, "postgres", "::").IsWeb);
            Assert.Equal("http://localhost:8080", new ListeningPort(8080, 1, "java", "::").Url);
        }

        [Fact]
        public void RefusesToKillWhatItCouldNotIdentify()
        {
            string said = Ports.Kill(new ListeningPort(3000, 0, "unknown", "::"));

            Assert.Contains("could not be identified", said);
        }

        [Fact]
        public void RunningSomethingThatIsNotInstalledIsQuiet()
        {
            // Every caller of this treats an empty string as "no information", so a missing tool
            // must not become an exception on a panel refresh.
            Assert.Equal(string.Empty, Ports.Run("devdeck-no-such-tool-exists", "--version", 2000));
        }

        [Fact]
        public void ReadsTheFirstPublishedPortOutOfTheEnginesFormat()
        {
            ContainerInfo container = new(
                "abc123", "web", "nginx", "running", "Up 2 hours",
                "0.0.0.0:8080->80/tcp, :::8080->80/tcp");

            Assert.Equal(8080, container.Published);
            Assert.True(container.HasPublished);
            Assert.Equal("http://localhost:8080", container.Url);
        }

        [Fact]
        public void AContainerPublishingNothingSaysSo()
        {
            ContainerInfo container = new("abc", "worker", "redis", "running", "Up", "6379/tcp");

            Assert.Null(container.Published);
            Assert.False(container.HasPublished);
        }

        [Fact]
        public void DropsTheRegistryFromAnImageName()
        {
            Assert.Equal("service:1.4.2",
                new ContainerInfo("a", "b", "myregistry.azurecr.io/team/service:1.4.2", "running", "", "").ImageName);

            Assert.Equal("nginx:alpine",
                new ContainerInfo("a", "b", "nginx:alpine", "running", "", "").ImageName);
        }

        [Fact]
        public void ShortensAFullLengthIdToWhatPeopleType()
        {
            ContainerInfo container = new(new string('a', 64), "web", "nginx", "exited", "Exited (0)", "");

            Assert.Equal(12, container.Short.Length);
            Assert.False(container.IsRunning);
        }

        [Fact]
        public void TheFollowCommandIsSomethingTheDeckCouldRun()
        {
            string command = Containers.FollowCommand(
                new ContainerInfo("abc", "api", "nginx", "running", "Up", ""));

            Assert.Contains("logs -f", command);
            Assert.EndsWith("api", command);
        }

        [Fact]
        public void WithoutAnEngineNothingBlowsUp()
        {
            // Skipped where an engine genuinely is installed: the assertion is about the no-engine
            // path, and on a machine with docker this would be asserting the opposite thing.
            if (Containers.Available)
            {
                Assert.NotNull(Containers.Engine);

                return;
            }

            Assert.Empty(Containers.List());
            Assert.Contains("No container engine",
                Containers.Do("start", new ContainerInfo("a", "b", "c", "running", "", "")));
        }
    }
}
