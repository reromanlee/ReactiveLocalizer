using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class XliffExchangeTests
    {
        private const string Identity = "rl:catalog=\"Localization\" rl:sourceLanguage=\"English\" rl:targetLanguage=\"Russian\"";

        private const string Expected12 =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<xliff version=\"1.2\" xmlns=\"urn:oasis:names:tc:xliff:document:1.2\" xmlns:rl=\"https://github.com/reromanlee/ReactiveLocalizer\">\n" +
            "  <file original=\"Common\" datatype=\"plaintext\" source-language=\"en\" target-language=\"ru\" " + Identity + ">\n" +
            "    <body>\n" +
            "      <trans-unit id=\"Confirm\" resname=\"Confirm\" xml:space=\"preserve\">\n" +
            "        <source>OK</source>\n" +
            "        <target state=\"needs-review-translation\">Da</target>\n" +
            "      </trans-unit>\n" +
            "    </body>\n" +
            "  </file>\n" +
            "  <file original=\"Shop\" datatype=\"plaintext\" source-language=\"en\" target-language=\"ru\" " + Identity + ">\n" +
            "    <body>\n" +
            "      <trans-unit id=\"Balance\" resname=\"Balance\" xml:space=\"preserve\">\n" +
            "        <source>You have {coins, plural, one {# coin} other {# coins}}.</source>\n" +
            "      </trans-unit>\n" +
            "      <trans-unit id=\"Purchase\" resname=\"Purchase\" xml:space=\"preserve\" maxwidth=\"16\" size-unit=\"char\">\n" +
            "        <source>Buy</source>\n" +
            "        <target state=\"translated\">Kupit</target>\n" +
            "        <note from=\"developer\">Button that confirms buying the selected item.</note>\n" +
            "      </trans-unit>\n" +
            "      <trans-unit id=\"Title\" resname=\"Title\" xml:space=\"preserve\">\n" +
            "        <source>Shop</source>\n" +
            "        <target state=\"needs-review-translation\">Magazin</target>\n" +
            "      </trans-unit>\n" +
            "    </body>\n" +
            "  </file>\n" +
            "</xliff>\n";

        private const string Expected20 =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<xliff xmlns=\"urn:oasis:names:tc:xliff:document:2.0\" xmlns:rl=\"https://github.com/reromanlee/ReactiveLocalizer\" version=\"2.0\" srcLang=\"en\" trgLang=\"ru\" " + Identity + ">\n" +
            "  <file id=\"Common\" original=\"Common\">\n" +
            "    <unit id=\"Confirm\" name=\"Confirm\" xml:space=\"preserve\">\n" +
            "      <segment state=\"initial\">\n" +
            "        <source>OK</source>\n" +
            "        <target>Da</target>\n" +
            "      </segment>\n" +
            "    </unit>\n" +
            "  </file>\n" +
            "  <file id=\"Shop\" original=\"Shop\">\n" +
            "    <unit id=\"Balance\" name=\"Balance\" xml:space=\"preserve\">\n" +
            "      <segment state=\"initial\">\n" +
            "        <source>You have {coins, plural, one {# coin} other {# coins}}.</source>\n" +
            "      </segment>\n" +
            "    </unit>\n" +
            "    <unit id=\"Purchase\" name=\"Purchase\" xml:space=\"preserve\">\n" +
            "      <notes>\n" +
            "        <note category=\"context\">Button that confirms buying the selected item.</note>\n" +
            "        <note category=\"maximumLength\">16</note>\n" +
            "      </notes>\n" +
            "      <segment state=\"translated\">\n" +
            "        <source>Buy</source>\n" +
            "        <target>Kupit</target>\n" +
            "      </segment>\n" +
            "    </unit>\n" +
            "    <unit id=\"Title\" name=\"Title\" xml:space=\"preserve\">\n" +
            "      <segment state=\"initial\">\n" +
            "        <source>Shop</source>\n" +
            "        <target>Magazin</target>\n" +
            "      </segment>\n" +
            "    </unit>\n" +
            "  </file>\n" +
            "</xliff>\n";

        [TestCase(XliffVersion.Version12, Expected12)]
        [TestCase(XliffVersion.Version20, Expected20)]
        public void Write_GivesAFilePerTableWithStatesAndNotes(XliffVersion version, string expected)
        {
            byte[] bytes = XliffExchange.Write(ExchangeFixture.ExportRussian(), version, null);

            Assert.That(Encoding.UTF8.GetString(bytes), Is.EqualTo(expected));
        }

        [TestCase(XliffVersion.Version12)]
        [TestCase(XliffVersion.Version20)]
        public void Read_GivesBackWhatWriteWrote(XliffVersion version)
        {
            ImportedFile file = XliffExchange.Read("Localization.Russian.xlf", XliffExchange.Write(ExchangeFixture.ExportRussian(), version, null));

            Assert.That(file.Problems, Is.Empty);
            Assert.That(file.CatalogName, Is.EqualTo("Localization"));
            Assert.That(file.Languages.Count, Is.EqualTo(2));
            Assert.That(file.Languages[0].Label, Is.EqualTo("English"));
            Assert.That(file.Languages[0].IsSourceOnly, Is.True);
            Assert.That(file.Languages[1].Candidates, Is.EqualTo(new[] { "Russian", "ru" }));
            Assert.That(file.Rows.Count, Is.EqualTo(4));
            ImportedRow purchase = file.Rows[2];
            Assert.That(purchase.ToString(), Is.EqualTo("Shop.Purchase"));
            Assert.That(purchase.GetText(0), Is.EqualTo("Buy"));
            Assert.That(purchase.GetText(1), Is.EqualTo("Kupit"));
            Assert.That(purchase.IsConfirmed, Is.True);
            Assert.That(file.Rows[1].GetText(1), Is.Null, "A missing translation has no target.");
            Assert.That(file.Rows[3].IsConfirmed, Is.False, "An outdated translation needs review.");
        }

        [Test]
        public void Read_Version12_TakesTheMarkupToolsAdd()
        {
            string xliff =
                "<xliff version=\"1.2\" xmlns=\"urn:oasis:names:tc:xliff:document:1.2\">" +
                "<file original=\"Shop\" datatype=\"plaintext\" source-language=\"en-US\" target-language=\"ru-RU\"><body><group id=\"g1\">" +
                "<trans-unit id=\"Purchase\" approved=\"yes\"><source>Buy <g id=\"1\">now</g></source>" +
                "<seg-source><mrk mtype=\"seg\" mid=\"1\">Buy now</mrk></seg-source>" +
                "<target state=\"needs-review-translation\"><mrk mtype=\"seg\" mid=\"1\">Kupit <g id=\"1\">seichas</g><ph id=\"2\">&lt;br&gt;</ph></mrk></target></trans-unit>" +
                "<trans-unit id=\"Hidden\" translate=\"no\"><source>x</source><target>y</target></trans-unit>" +
                "</group></body></file></xliff>";

            ImportedFile file = XliffExchange.Read("Translated.Russian.xlf", Encoding.UTF8.GetBytes(xliff));

            Assert.That(file.Problems, Is.Empty);
            Assert.That(file.Rows.Count, Is.EqualTo(1));
            Assert.That(file.Rows[0].GetText(0), Is.EqualTo("Buy now"));
            Assert.That(file.Rows[0].GetText(1), Is.EqualTo("Kupit seichas<br>"));
            Assert.That(file.Rows[0].IsConfirmed, Is.True);
            Assert.That(file.Languages[1].Label, Is.EqualTo("ru-RU"));
            Assert.That(file.Languages[1].Candidates, Is.EqualTo(new[] { "Russian", "ru-RU" }), "The file's name hints at its language.");
        }

        [Test]
        public void Read_Version20_JoinsSegmentsAndRestoresCodes()
        {
            string xliff =
                "<xliff xmlns=\"urn:oasis:names:tc:xliff:document:2.0\" version=\"2.0\" srcLang=\"en\" trgLang=\"ru\"><file id=\"f1\" original=\"Shop\">" +
                "<unit id=\"u1\" name=\"Title\"><originalData><data id=\"d1\">&lt;b&gt;</data><data id=\"d2\">&lt;/b&gt;</data></originalData>" +
                "<segment state=\"final\"><source>Shop.</source><target><pc id=\"1\" dataRefStart=\"d1\" dataRefEnd=\"d2\">Magazin</pc>.</target></segment>" +
                "<ignorable><source> </source></ignorable>" +
                "<segment state=\"reviewed\"><source>Welcome</source><target>Dobro<cp hex=\"0001\"/></target></segment></unit>" +
                "<unit id=\"u2\" name=\"Partial\"><segment><source>A.</source><target>A!</target></segment><segment><source>B.</source></segment></unit>" +
                "</file></xliff>";

            ImportedFile file = XliffExchange.Read("Shop.xlf", Encoding.UTF8.GetBytes(xliff));

            Assert.That(file.Rows.Count, Is.EqualTo(2));
            Assert.That(file.Rows[0].GetText(0), Is.EqualTo("Shop. Welcome"));
            Assert.That(file.Rows[0].GetText(1), Is.EqualTo("<b>Magazin</b>. Dobro" + (char)1));
            Assert.That(file.Rows[0].IsConfirmed, Is.True);
            Assert.That(file.Rows[1].GetText(1), Is.Null);
            Assert.That(file.Problems.Count, Is.EqualTo(1));
            Assert.That(file.Problems[0], Does.Contain("'Shop.Partial' is translated in only some of its segments"));
        }

        [Test]
        public void Write_Version20_KeepsControlCharactersThatVersion12LeavesOut()
        {
            const string Backslash = "\\";
            List<ValidatedTable> tables = ExchangeFixture.Tables(("Odd", "English", "Line = Line\n"), ("Odd", "Russian", "Line = a" + Backslash + "u0001b\n"));
            ExportBook book = ExportBook.Collect(ExchangeFixture.Catalog, tables, new ExportOptions { Languages = new[] { "Russian" } });
            List<string> problems = new();

            byte[] version12 = XliffExchange.Write(book, XliffVersion.Version12, problems);
            ImportedFile version20 = XliffExchange.Read("Odd.xlf", XliffExchange.Write(book, XliffVersion.Version20, null));

            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(Encoding.UTF8.GetString(version12), Does.Not.Contain("trans-unit"));
            Assert.That(version20.Rows[0].GetText(1), Is.EqualTo("a" + (char)1 + "b"));
        }

        [Test]
        public void GetLanguageTag_FollowsTheFallbackForALanguageWithoutCulture()
        {
            Assert.That(XliffExchange.GetLanguageTag(ExchangeFixture.Catalog, ExchangeFixture.Catalog.Languages[2]), Is.EqualTo("en"));
        }

        [Test]
        public void Write_RefusesABookOfSeveralLanguages()
        {
            Assert.That(() => XliffExchange.Write(ExchangeFixture.Export(null), XliffVersion.Version20, null), Throws.ArgumentException);
        }

        [Test]
        public void Read_ReportsWhatIsNotXliff()
        {
            ImportedFile notXliff = XliffExchange.Read("a.xlf", Encoding.UTF8.GetBytes("<resources><string name=\"a\">b</string></resources>"));
            ImportedFile broken = XliffExchange.Read("b.xlf", Encoding.UTF8.GetBytes("<xliff><file>"));

            Assert.That(notXliff.Problems[0], Does.Contain("isn't an XLIFF file"));
            Assert.That(broken.Problems[0], Does.Contain("can't be read as XLIFF"));
        }
    }
}
