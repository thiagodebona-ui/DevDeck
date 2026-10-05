using System.Buffers.Binary;
using DevDeck.Core;
using Xunit;

namespace DevDeck.Core.Tests
{
    /// <summary>
    ///  The screenshot gallery's store: what it keeps, what it recognises as a capture it already
    ///  has, and what it is allowed to delete on its own.
    /// </summary>
    /// <remarks>
    ///  The last of those is the one that matters most. The system's folder is the user's own
    ///  Pictures, and the sweep must never reach into it - so that is asserted rather than assumed.
    /// </remarks>
    public sealed class ShotsTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "devdeck-shots-" + Guid.NewGuid().ToString("N"));

        private string Own => Path.Combine(root, "own");

        private string System => Path.Combine(root, "system");

        private string Index => Path.Combine(root, "shots.json");

        public ShotsTests()
        {
            Directory.CreateDirectory(System);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        /// <summary>The header of a PNG this size, which is all the store ever reads.</summary>
        private static byte[] Png(int width, int height, byte salt = 0)
        {
            byte[] bytes = new byte[33];

            new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(8), 13);
            "IHDR"u8.CopyTo(bytes.AsSpan(12));
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(16), width);
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(20), height);
            bytes[32] = salt;

            return bytes;
        }

        private string SystemShot(string name, int width, int height, DateTime at)
        {
            string path = Path.Combine(System, name);

            File.WriteAllBytes(path, Png(width, height));
            File.SetLastWriteTime(path, at);

            return path;
        }

        private Shots Store() => new(Own, System, Index);

        [Fact]
        public void ReadsTheSizeFromAPngHeader()
        {
            Assert.Equal((1920, 1080), Shots.ImageSize(new MemoryStream(Png(1920, 1080))));
        }

        [Fact]
        public void AnythingButAPngHasNoSize()
        {
            Assert.Null(Shots.ImageSize(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, .. new byte[30]])));
            Assert.Null(Shots.ImageSize(new MemoryStream([0x89, (byte)'P'])));
        }

        [Fact]
        public void KeepsAClipboardImageAsAFileOfItsOwn()
        {
            Shots shots = Store();
            DateTime now = DateTime.Now;

            string? kept = shots.Keep(Png(800, 600), 800, 600, now);

            Assert.NotNull(kept);
            Assert.StartsWith(Own, kept);
            Assert.True(Assert.Single(shots.All()).Own);
        }

        [Fact]
        public void TheSameImageTwiceIsKeptOnce()
        {
            Shots shots = Store();
            DateTime now = DateTime.Now;

            Assert.NotNull(shots.Keep(Png(800, 600), 800, 600, now));
            Assert.Null(shots.Keep(Png(800, 600), 800, 600, now.AddSeconds(5)));
        }

        [Fact]
        public void AClipboardImageMatchingAFreshFileIsThatFile()
        {
            DateTime now = DateTime.Now;
            SystemShot("Screenshot 1.png", 1280, 720, now.AddSeconds(-2));

            Shots shots = Store();

            Assert.Null(shots.Keep(Png(1280, 720), 1280, 720, now));
            Assert.False(Directory.Exists(Own) && Directory.EnumerateFiles(Own).Any());
        }

        [Fact]
        public void AnOldFileOfTheSameSizeIsADifferentCapture()
        {
            DateTime now = DateTime.Now;
            SystemShot("Screenshot 1.png", 1280, 720, now.AddHours(-1));

            Assert.NotNull(Store().Keep(Png(1280, 720), 1280, 720, now));
        }

        [Fact]
        public void AFileArrivingAfterTheClipboardReplacesTheClipboardCopy()
        {
            DateTime now = DateTime.Now;
            Shots shots = Store();

            string kept = shots.Keep(Png(1280, 720), 1280, 720, now)!;
            string file = SystemShot("Screenshot 2.png", 1280, 720, now.AddSeconds(1));

            Assert.True(shots.Arrived(file));
            Assert.False(File.Exists(kept));
            Assert.Equal(file, Assert.Single(shots.All()).Path);
        }

        [Fact]
        public void TheSweepDropsOldClipboardCapturesButNeverTheSystemFolder()
        {
            DateTime now = DateTime.Now;
            string mine = SystemShot("Screenshot old.png", 100, 100, now.AddDays(-400));

            Shots shots = Store();
            string old = shots.Keep(Png(10, 10, 1), 10, 10, now.AddDays(-40))!;
            string fresh = shots.Keep(Png(10, 10, 2), 10, 10, now)!;

            Assert.Equal(1, shots.Sweep(now));
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(fresh));
            Assert.True(File.Exists(mine));
        }

        [Fact]
        public void AFavouriteSurvivesTheSweepAndARestart()
        {
            DateTime now = DateTime.Now;
            Shots shots = Store();
            string old = shots.Keep(Png(10, 10), 10, 10, now.AddDays(-40))!;

            shots.MarkFavourite(shots.All().Single(), true);

            Assert.Equal(0, shots.Sweep(now));
            Assert.True(File.Exists(old));

            Shot again = Assert.Single(Store().All());
            Assert.True(again.Favourite);
        }

        [Fact]
        public void NewestFirstWithFavouritesInTheirPlace()
        {
            DateTime now = DateTime.Now;
            SystemShot("a.png", 10, 10, now.AddMinutes(-30));
            SystemShot("b.png", 10, 10, now.AddMinutes(-20));
            SystemShot("c.png", 10, 10, now.AddMinutes(-10));

            Shots shots = Store();
            shots.MarkFavourite(shots.All().Single(shot => shot.Name == "a"), true);

            // A favourite is marked, not promoted: the gallery stays in the order shots were taken.
            Assert.Equal(["c", "b", "a"], shots.All().Select(shot => shot.Name));
        }

        [Fact]
        public void SearchesByNameAndByDate()
        {
            SystemShot("Screenshot login error.png", 10, 10, new DateTime(2026, 3, 14, 9, 0, 0));
            SystemShot("Screenshot dashboard.png", 10, 10, new DateTime(2026, 4, 2, 9, 0, 0));

            Shots shots = Store();

            Assert.Equal("Screenshot login error", Assert.Single(shots.Search("login")).Name);
            Assert.Equal("Screenshot dashboard", Assert.Single(shots.Search("2026-04")).Name);
            Assert.Equal(2, shots.Search("screenshot").Count);
        }

        [Fact]
        public void OnlyImagesAreListed()
        {
            SystemShot("shot.png", 10, 10, DateTime.Now);
            File.WriteAllText(Path.Combine(System, "desktop.ini"), "[.ShellClassInfo]");

            Assert.Equal("shot", Assert.Single(Store().All()).Name);
        }

        [Fact]
        public void AMissingFolderIsAnEmptyGalleryNotAnError()
        {
            Assert.Empty(new Shots(Path.Combine(root, "nope"), Path.Combine(root, "nor-this"), Index).All());
        }
    }
}
