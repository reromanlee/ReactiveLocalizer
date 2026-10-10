using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TableFileTests
    {
        private const string Source =
            "# Texts of the shop screen.\n" +
            "\n" +
            "@loading OnDemand\n" +
            "@delivery Streaming\n" +
            "\n" +
            "@formerly CoinsLabel\n" +
            "CoinBalance = You have {coins, plural, one {# coin} other {# coins}}.\n" +
            "\n" +
            "Disclaimer = Prices include VAT.\\nRefunds within 14 days.\n" +
            "Line9 = Nine\n" +
            "Line10 = Ten\n" +
            "\n" +
            "# Button that confirms buying the selected item.\n" +
            "@maximumLength 16\n" +
            "Purchase = Buy\n" +
            "\n" +
            "Suffix =\n" +
            "\n" +
            "# Kept at the end.\n";

        private const string Translation =
            "CoinBalance [8b0e47] = \u0423 \u0432\u0430\u0441 {coins, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}.\n" +
            "Purchase [3fa2c1] = \u041A\u0443\u043F\u0438\u0442\u044C\n" +
            "Title = \\u0020Spaced\\u00A0text\\\\\n";

        [TestCase(Source)]
        [TestCase(Translation)]
        [TestCase("")]
        [TestCase("Purchase = Buy\n")]
        public void Write_GivesACanonicalFileBackUnchanged(string text)
        {
            Assert.That(TableFile.Read(text).Write(), Is.EqualTo(text));
        }

        [Test]
        public void Write_KeepsCarriageReturnLineEndings()
        {
            string text = Source.Replace("\n", "\r\n");

            Assert.That(TableFile.Read(text).Write(), Is.EqualTo(text));
        }

        [Test]
        public void Write_SortsEntriesInNaturalOrderWithTheirCommentsAndAttributes()
        {
            TableFile file = TableFile.Read("Line10 = Ten\n# About nine.\n@maximumLength 4\nline9 = Nine\nAlpha = A\n");

            Assert.That(file.Write(), Is.EqualTo("Alpha = A\n\n# About nine.\n@maximumLength 4\nline9 = Nine\n\nLine10 = Ten\n"));
        }

        [Test]
        public void Write_PutsSettingsAtTheTopInTheirFixedOrder()
        {
            TableFile file = TableFile.Read("@generateCode false\n@loading OnDemand\n\nTitle = Shop\n");
            file.SetSetting(DocumentNames.Delivery, "Streaming");

            Assert.That(file.Write(), Is.EqualTo("@loading OnDemand\n@delivery Streaming\n@generateCode false\n\nTitle = Shop\n"));
        }

        [Test]
        public void Write_EscapesWhatTrimmingOrReviewsWouldLose()
        {
            TableFile file = new();
            file.TryAddEntry(new TableFileEntry("Spaced", " padded \t\n"));
            file.TryAddEntry(new TableFileEntry("Invisible", "a\u00A0b\u200Bc\\d"));

            string written = file.Write();

            Assert.That(written, Is.EqualTo("Invisible = a\\u00A0b\\u200Bc\\\\d\nSpaced = \\u0020padded \\t\\n\n"));
            Assert.That(TableDocument.Parse(written).Entries[1].Value, Is.EqualTo(" padded \t\n"));
        }

        [Test]
        public void StampFingerprint_MarksTheTranslationCurrentForItsSource()
        {
            TableFile file = TableFile.Read("Purchase = Kupit\n");
            file.Entries[0].StampFingerprint("Buy");

            string written = file.Write();

            Assert.That(written, Is.EqualTo("Purchase [9a1248] = Kupit\n"));
            Assert.That(TableDocument.Parse(written).Entries[0].Fingerprint, Is.EqualTo(Hashing.ComputeFingerprint("Buy")));
        }

        [Test]
        public void Read_FlagsFilesWithErrorsSoTheyAreNeverRewritten()
        {
            Assert.That(TableFile.Read("Purchase = Buy\nBroken line\n").HasErrors, Is.True);
            Assert.That(TableFile.Read("<<<<<<< HEAD\nPurchase = Buy\n").HasErrors, Is.True);
            Assert.That(TableFile.Read(Source).HasErrors, Is.False);
        }

        [Test]
        public void Entries_RenameAndRemoveIgnoringCaseAndKeepKeysUnique()
        {
            TableFile file = TableFile.Read("Purchase = Buy\nTitle = Shop\n");

            Assert.That(file.TryAddEntry(new TableFileEntry("TITLE", "Again")), Is.False);
            Assert.That(file.RenameEntry("purchase", "Title"), Is.False);
            Assert.That(file.RenameEntry("purchase", "Buy"), Is.True);
            Assert.That(file.RemoveEntry("title"), Is.True);
            Assert.That(file.Write(), Is.EqualTo("Buy = Buy\n"));
        }

        [Test]
        public void Write_ReadsBackTheSameEntriesForRandomText()
        {
            Random random = new(1234);
            const string Alphabet = "ab {}#@=\\\n\t'\u00A0\u200B\u041A";
            for (int round = 0; round < 200; round++)
            {
                TableFile file = new();
                int count = random.Next(1, 6);
                for (int i = 0; i < count; i++)
                {
                    StringBuilder value = new();
                    int length = random.Next(0, 12);
                    for (int c = 0; c < length; c++)
                    {
                        value.Append(Alphabet[random.Next(Alphabet.Length)]);
                    }
                    TableFileEntry entry = new($"Key{i}", value.ToString());
                    if (random.Next(3) == 0)
                    {
                        entry.Comments.Add("Context " + i);
                    }
                    file.TryAddEntry(entry);
                }

                string written = file.Write();
                TableDocument read = TableDocument.Parse(written);

                Assert.That(read.Issues, Is.Empty, written);
                Assert.That(read.Entries.Count, Is.EqualTo(count));
                for (int i = 0; i < count; i++)
                {
                    Assert.That(read.TryGetEntry($"Key{i}", out TableDocumentEntry entry), Is.True);
                    Assert.That(entry.Value, Is.EqualTo(file.Entries[i].Value), written);
                }
                Assert.That(TableFile.Read(written).Write(), Is.EqualTo(written));
            }
        }

        [TestCase("Line9", "Line10", -1)]
        [TestCase("line10", "Line9", 1)]
        [TestCase("Alpha", "beta", -1)]
        [TestCase("Item007", "Item7", -1)]
        [TestCase("Item2Part10", "Item2Part9", 1)]
        [TestCase("Same", "Same", 0)]
        public void NaturalOrder_CountsNumbersByValue(string left, string right, int expected)
        {
            Assert.That(Math.Sign(NaturalOrder.Instance.Compare(left, right)), Is.EqualTo(expected));
        }

        [Test]
        public void NaturalOrder_SortsAListLikePeopleCount()
        {
            List<string> names = new() { "Line10", "Line2", "line1", "Intro", "Line9" };

            names.Sort(NaturalOrder.Instance);

            Assert.That(names, Is.EqualTo(new[] { "Intro", "line1", "Line2", "Line9", "Line10" }));
        }
    }
}
