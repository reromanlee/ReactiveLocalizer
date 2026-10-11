using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class ZipContainerTests
    {
        [Test]
        public void Write_ThenRead_GivesTheSameEntries()
        {
            string large = new('x', 5000);
            byte[] archive = ZipContainer.Write(new[]
            {
                Entry("tiny.txt", "a"),
                Entry("folder/large.xml", large),
                Entry("empty.txt", string.Empty)
            });

            bool isRead = ZipContainer.TryRead(archive, out Dictionary<string, byte[]> entries, out string problem);

            Assert.That(isRead, Is.True, problem);
            Assert.That(entries.Count, Is.EqualTo(3));
            Assert.That(Encoding.UTF8.GetString(entries["tiny.txt"]), Is.EqualTo("a"));
            Assert.That(Encoding.UTF8.GetString(entries["FOLDER/Large.xml"]), Is.EqualTo(large));
            Assert.That(entries["empty.txt"].Length, Is.EqualTo(0));
            Assert.That(archive.Length, Is.LessThan(large.Length), "Large entries are compressed.");
        }

        [Test]
        public void Write_IsTheSameEveryTime()
        {
            KeyValuePair<string, byte[]>[] entries = { Entry("a.xml", "<a/>"), Entry("b.xml", new string('b', 300)) };

            Assert.That(ZipContainer.Write(entries), Is.EqualTo(ZipContainer.Write(entries)));
        }

        [Test]
        public void TryRead_ReportsWhatIsNotAnArchive()
        {
            bool isRead = ZipContainer.TryRead(Encoding.UTF8.GetBytes("Key,English\nShop.Title,Shop\n"), out _, out string problem);

            Assert.That(isRead, Is.False);
            Assert.That(problem, Does.Contain("isn't a zip archive"));
        }

        [Test]
        public void TryRead_ReportsADamagedEntry()
        {
            byte[] archive = ZipContainer.Write(new[] { Entry("a.txt", "abc") });
            // The stored text starts right after the 30-byte local header and the 5-byte name.
            archive[35] ^= 0xFF;

            bool isRead = ZipContainer.TryRead(archive, out _, out string problem);

            Assert.That(isRead, Is.False);
            Assert.That(problem, Does.Contain("damaged"));
        }

        [Test]
        public void TryRead_ReportsAPasswordProtectedEntry()
        {
            byte[] archive = ZipContainer.Write(new[] { Entry("a.txt", "abc") });
            int central = FindCentralHeader(archive);
            archive[central + 8] |= 1;

            bool isRead = ZipContainer.TryRead(archive, out _, out string problem);

            Assert.That(isRead, Is.False);
            Assert.That(problem, Does.Contain("password"));
        }

        private static KeyValuePair<string, byte[]> Entry(string name, string text) => new(name, Encoding.UTF8.GetBytes(text));

        private static int FindCentralHeader(byte[] archive)
        {
            for (int i = 0; i + 4 <= archive.Length; i++)
            {
                if (archive[i] == 0x50 && archive[i + 1] == 0x4B && archive[i + 2] == 0x01 && archive[i + 3] == 0x02)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
