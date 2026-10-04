using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TableDocumentTests
    {
        [Test]
        public void Parse_ReadsKeysAndValues()
        {
            TableDocument document = TableDocument.Parse("Purchase = Buy\nTitle = Shop\n");

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            Assert.That(document.Entries[0].Key, Is.EqualTo("Purchase"));
            Assert.That(document.Entries[0].Value, Is.EqualTo("Buy"));
            Assert.That(document.Entries[1].Line, Is.EqualTo(2));
            Assert.That(document.Issues, Is.Empty);
        }

        [Test]
        public void Parse_TrimsBlanksAroundTheEqualsSignAndAtLineEnds()
        {
            TableDocument document = TableDocument.Parse("  Purchase\t=   Buy now  \t\nTitle=Shop");

            Assert.That(document.Entries[0].Value, Is.EqualTo("Buy now"));
            Assert.That(document.Entries[1].Value, Is.EqualTo("Shop"));
        }

        [Test]
        public void Parse_SplitsOnlyOnTheFirstEqualsSign()
        {
            TableDocument document = TableDocument.Parse("Equation = 2 + 2 = 4");

            Assert.That(document.Entries[0].Value, Is.EqualTo("2 + 2 = 4"));
        }

        [TestCase("Rank = #1 Seller", "#1 Seller")]
        [TestCase("Mail = name@example.com", "name@example.com")]
        [TestCase("Hint = [E] Open", "[E] Open")]
        [TestCase("Quote = \"Hello,\" she said", "\"Hello,\" she said")]
        [TestCase("Answer = No", "No")]
        [TestCase("Coins = {coins, plural, one {# coin} other {# coins}}", "{coins, plural, one {# coin} other {# coins}}")]
        public void Parse_KeepsSyntaxCharactersInsideValuesAsText(string line, string expectedValue)
        {
            TableDocument document = TableDocument.Parse(line);

            Assert.That(document.Entries[0].Value, Is.EqualTo(expectedValue));
            Assert.That(document.Issues, Is.Empty);
        }

        [Test]
        public void Parse_ReadsAnEmptyValueAsIntentionallyEmpty()
        {
            TableDocument document = TableDocument.Parse("Suffix =");

            Assert.That(document.Entries[0].Value, Is.Empty);
            Assert.That(document.HasErrors, Is.False);
        }

        [Test]
        public void Parse_ResolvesEscapes()
        {
            TableDocument document = TableDocument.Parse(@"Text = A\nB\tC\\D\u00A0E\u0020");

            Assert.That(document.Entries[0].Value, Is.EqualTo("A\nB\tC\\D\u00A0E "));
            Assert.That(document.Issues, Is.Empty);
        }

        [Test]
        public void Parse_KeepsUnknownEscapesAsWrittenWithAWarning()
        {
            TableDocument document = TableDocument.Parse(@"Path = C:\Games\u12");

            Assert.That(document.Entries[0].Value, Is.EqualTo(@"C:\Games\u12"));
            Assert.That(document.Issues.Count, Is.EqualTo(2));
            Assert.That(document.Issues[0].Severity, Is.EqualTo(IssueSeverity.Warning));
            Assert.That(document.Issues[0].Column, Is.EqualTo(10));
            Assert.That(document.HasErrors, Is.False);
        }

        [Test]
        public void Parse_AttachesCommentsAndAttributesToTheNextEntry()
        {
            TableDocument document = TableDocument.Parse(
                "# Button that confirms buying the selected item.\n" +
                "@maximumLength 16\n" +
                "@formerly BuyButton\n" +
                "Purchase = Buy\n" +
                "Title = Shop\n");

            TableDocumentEntry purchase = document.Entries[0];
            Assert.That(purchase.Comments, Is.EqualTo(new[] { "Button that confirms buying the selected item." }));
            Assert.That(purchase.TryGetAttribute(DocumentNames.MaximumLength, out string maximumLength), Is.True);
            Assert.That(maximumLength, Is.EqualTo("16"));
            Assert.That(purchase.TryGetAttribute("FORMERLY", out string formerly), Is.True);
            Assert.That(formerly, Is.EqualTo("BuyButton"));
            Assert.That(document.Entries[1].Comments, Is.Empty);
            Assert.That(document.Entries[1].Attributes, Is.Empty);
        }

        [Test]
        public void Parse_KeepsCommentsAcrossBlankLinesInsideTheFile()
        {
            TableDocument document = TableDocument.Parse("First = 1\n\n# About the second.\n\nSecond = 2");

            Assert.That(document.Entries[1].Comments, Is.EqualTo(new[] { "About the second." }));
        }

        [Test]
        public void Parse_ReadsCommentsOpeningTheFileAsItsHeader()
        {
            TableDocument document = TableDocument.Parse("# Texts of the shop screen.\n\n# About the purchase.\nPurchase = Buy");

            Assert.That(document.HeaderComments, Is.EqualTo(new[] { "Texts of the shop screen." }));
            Assert.That(document.Entries[0].Comments, Is.EqualTo(new[] { "About the purchase." }));
        }

        [Test]
        public void Parse_ReadsTableSettingsAtTheTop()
        {
            TableDocument document = TableDocument.Parse("# Shop.\n@Loading OnDemand\n@delivery Streaming\n\nPurchase = Buy");

            Assert.That(document.Settings.Count, Is.EqualTo(2));
            Assert.That(document.Settings[0].Name, Is.EqualTo(DocumentNames.Loading));
            Assert.That(document.TryGetSetting(DocumentNames.Delivery, out string delivery), Is.True);
            Assert.That(delivery, Is.EqualTo("Streaming"));
            Assert.That(document.HeaderComments, Is.EqualTo(new[] { "Shop." }));
            Assert.That(document.Entries[0].Attributes, Is.Empty);
        }

        [Test]
        public void Parse_RejectsTableSettingsAfterTheFirstEntry()
        {
            TableDocument document = TableDocument.Parse("Purchase = Buy\n@loading OnDemand");

            Assert.That(document.Settings, Is.Empty);
            Assert.That(document.HasErrors, Is.True);
            Assert.That(document.Issues[0].Line, Is.EqualTo(2));
        }

        [Test]
        public void Parse_KeepsUnknownAttributesWithAWarning()
        {
            TableDocument document = TableDocument.Parse("@reviewedBy Anna\nPurchase = Buy");

            Assert.That(document.Entries[0].TryGetAttribute("reviewedBy", out string reviewer), Is.True);
            Assert.That(reviewer, Is.EqualTo("Anna"));
            Assert.That(document.Issues[0].Severity, Is.EqualTo(IssueSeverity.Warning));
        }

        [Test]
        public void Parse_WarnsAboutAttributesWithNoEntryAfterThem()
        {
            TableDocument document = TableDocument.Parse("Purchase = Buy\n@formerly Old");

            Assert.That(document.Issues.Count, Is.EqualTo(1));
            Assert.That(document.Issues[0].Severity, Is.EqualTo(IssueSeverity.Warning));
            Assert.That(document.Issues[0].Line, Is.EqualTo(2));
        }

        [Test]
        public void Parse_KeepsTheFirstOfTwoKeysThatDifferOnlyInCase()
        {
            TableDocument document = TableDocument.Parse("Purchase = Buy\n# Dropped with it.\npurchase = Get");

            Assert.That(document.Entries.Count, Is.EqualTo(1));
            Assert.That(document.Entries[0].Value, Is.EqualTo("Buy"));
            Assert.That(document.Issues[0].Severity, Is.EqualTo(IssueSeverity.Error));
            Assert.That(document.Issues[0].Message, Does.Contain("line 1"));
            Assert.That(document.TrailingComments, Is.Empty);
        }

        [TestCase("Buy Now = Buy", 5)]
        [TestCase("1st = First", 1)]
        [TestCase("\u041a\u0443\u043f\u0438\u0442\u044c = Buy", 1)]
        [TestCase("Shop.Purchase = Buy", 5)]
        [TestCase("Purchase Buy", 10)]
        [TestCase("Purchase [8b0e47 = Buy", 10)]
        public void Parse_RejectsMalformedEntryLinesAtTheirPosition(string line, int expectedColumn)
        {
            TableDocument document = TableDocument.Parse("Title = Shop\n" + line + "\nFooter = End");

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            Assert.That(document.Issues.Count, Is.EqualTo(1));
            Assert.That(document.Issues[0].Severity, Is.EqualTo(IssueSeverity.Error));
            Assert.That(document.Issues[0].Line, Is.EqualTo(2));
            Assert.That(document.Issues[0].Column, Is.EqualTo(expectedColumn));
        }

        [Test]
        public void Parse_DropsTheCommentsOfARejectedEntry()
        {
            TableDocument document = TableDocument.Parse("# About the broken one.\nBroken Line\nGood = Yes");

            Assert.That(document.Entries[0].Comments, Is.Empty);
        }

        [Test]
        public void Parse_ReadsFingerprints()
        {
            TableDocument document = TableDocument.Parse("Purchase [3FA2c1] = \u041a\u0443\u043f\u0438\u0442\u044c\nTitle = Shop");

            Assert.That(document.Entries[0].HasFingerprint, Is.True);
            Assert.That(document.Entries[0].Fingerprint, Is.EqualTo(0x3FA2C1u));
            Assert.That(document.Entries[0].Value, Is.EqualTo("\u041a\u0443\u043f\u0438\u0442\u044c"));
            Assert.That(document.Entries[1].HasFingerprint, Is.False);
        }

        [Test]
        public void Parse_IgnoresAMalformedFingerprintWithAWarning()
        {
            TableDocument document = TableDocument.Parse("Purchase [3fa2] = Buy");

            Assert.That(document.Entries[0].HasFingerprint, Is.False);
            Assert.That(document.Entries[0].Value, Is.EqualTo("Buy"));
            Assert.That(document.Issues[0].Severity, Is.EqualTo(IssueSeverity.Warning));
        }

        [Test]
        public void Parse_ReportsMergeConflictMarkers()
        {
            TableDocument document = TableDocument.Parse("<<<<<<< HEAD\nPurchase = Buy\n=======\nPurchase = Get\n>>>>>>> branch");

            Assert.That(document.HasErrors, Is.True);
            Assert.That(document.Issues.Count, Is.EqualTo(4));
            Assert.That(document.Issues[0].Line, Is.EqualTo(1));
            Assert.That(document.Issues[1].Line, Is.EqualTo(3));
        }

        [TestCase("Title = Shop\r\nPurchase = Buy\r\n")]
        [TestCase("Title = Shop\rPurchase = Buy\r")]
        [TestCase("\uFEFFTitle = Shop\nPurchase = Buy")]
        public void Parse_AcceptsEveryLineEndingAndAByteOrderMark(string text)
        {
            TableDocument document = TableDocument.Parse(text);

            Assert.That(document.Entries.Count, Is.EqualTo(2));
            Assert.That(document.Entries[0].Key, Is.EqualTo("Title"));
            Assert.That(document.Entries[1].Line, Is.EqualTo(2));
            Assert.That(document.Issues, Is.Empty);
        }

        [Test]
        public void Parse_KeepsCommentsAfterTheLastEntry()
        {
            TableDocument document = TableDocument.Parse("Purchase = Buy\n\n# More texts come later.");

            Assert.That(document.TrailingComments, Is.EqualTo(new[] { "More texts come later." }));
        }

        [Test]
        public void Parse_ReadsNothingFromNullOrEmptyText()
        {
            Assert.That(TableDocument.Parse(null).Entries, Is.Empty);
            Assert.That(TableDocument.Parse(string.Empty).Issues, Is.Empty);
        }

        [Test]
        public void TryGetEntry_IgnoresCase()
        {
            TableDocument document = TableDocument.Parse("Purchase = Buy");

            Assert.That(document.TryGetEntry("PURCHASE", out TableDocumentEntry entry), Is.True);
            Assert.That(entry.Value, Is.EqualTo("Buy"));
            Assert.That(document.TryGetEntry("Missing", out _), Is.False);
        }
    }
}
