using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class CatalogInfoTests
    {
        private static readonly CatalogKey Catalog = new("Localization");

        private static readonly TableInfo[] Tables =
        {
            new(new TableKey("Shop"), TableLoading.Preload, TableDelivery.Embedded),
            new(new TableKey("Dialogue"), TableLoading.OnDemand, TableDelivery.Streaming)
        };

        private const string Document =
            "@source English\n" +
            "[English]\nCulture = en\n" +
            "[Russian]\nDisplayName = Russkiy\nCulture = ru\nRequired = true\n" +
            "[Pirate]\nDisplayName = Pirate Speak\nFallback = English\n" +
            "[Arabic]\nCulture = ar\nDirection = RightToLeft\nDigits = 0123456789\nGroupSeparator =\n";

        [Test]
        public void FromDocument_ReadsEveryLanguageField()
        {
            List<DocumentIssue> issues = new();

            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(Document), Tables, issues);

            Assert.That(issues, Is.Empty);
            Assert.That(catalog.SourceLanguage.Name, Is.EqualTo("English"));
            Assert.That(catalog.Languages.Count, Is.EqualTo(4));
            Assert.That(catalog.TryGetLanguage(new LanguageKey("russian"), out LanguageInfo russian), Is.True);
            Assert.That(russian.DisplayName, Is.EqualTo("Russkiy"));
            Assert.That(russian.Culture, Is.EqualTo("ru"));
            Assert.That(russian.IsRequired, Is.True);
            Assert.That(catalog.Languages[0].DisplayName, Is.EqualTo("English"));
            Assert.That(catalog.Languages[2].Fallback, Is.EqualTo(new LanguageKey("English")));
            Assert.That(catalog.Languages[3].Direction, Is.EqualTo(TextDirection.RightToLeft));
            Assert.That(catalog.TryGetTable(new TableKey("Dialogue"), out TableInfo dialogue), Is.True);
            Assert.That(dialogue.Loading, Is.EqualTo(TableLoading.OnDemand));
        }

        [Test]
        public void GetFallbackChain_EndsWithTheSourceLanguageOnce()
        {
            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(
                "@source English\n[English]\n[Portuguese]\n[PortugueseBrazil]\nFallback = Portuguese\n"), Tables, null);
            catalog.TryGetLanguage(new LanguageKey("PortugueseBrazil"), out LanguageInfo brazil);

            IReadOnlyList<LanguageInfo> chain = catalog.GetFallbackChain(brazil);

            Assert.That(chain.Count, Is.EqualTo(3));
            Assert.That(chain[0].Name, Is.EqualTo("PortugueseBrazil"));
            Assert.That(chain[1].Name, Is.EqualTo("Portuguese"));
            Assert.That(chain[2].Name, Is.EqualTo("English"));
            Assert.That(catalog.GetFallbackChain(catalog.SourceLanguage).Count, Is.EqualTo(1));
        }

        [TestCase("[English]", "names no source language")]
        [TestCase("@source French\n[English]", "not one of the catalog's language sections")]
        [TestCase("@source English", "defines no language")]
        public void FromDocument_RejectsACatalogWithoutAUsableSource(string text, string expectedMessage)
        {
            List<DocumentIssue> issues = new();

            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(text), Tables, issues);

            Assert.That(catalog, Is.Null);
            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Message, Does.Contain(expectedMessage));
        }

        [Test]
        public void FromDocument_DropsFallbacksThatFormALoop()
        {
            List<DocumentIssue> issues = new();

            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(
                "@source English\n[English]\n[Pirate]\nFallback = Parrot\n[Parrot]\nFallback = Pirate\n"), Tables, issues);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Line, Is.EqualTo(3));
            Assert.That(catalog.Languages[1].HasFallback, Is.False);
            Assert.That(catalog.Languages[2].HasFallback, Is.True);
        }

        [Test]
        public void FromDocument_ReportsBadFieldsAndKeepsTheRest()
        {
            List<DocumentIssue> issues = new();

            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(
                "@source English\n[English]\nDirection = Upwards\nRequired = yes\nFallback = Klingon\nCulture = english language\n"), Tables, issues);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(issues.FindAll(issue => issue.Severity == IssueSeverity.Error).Count, Is.EqualTo(3));
            Assert.That(issues.FindAll(issue => issue.Severity == IssueSeverity.Warning).Count, Is.EqualTo(1));
            Assert.That(catalog.SourceLanguage.Direction, Is.EqualTo(TextDirection.LeftToRight));
            Assert.That(catalog.SourceLanguage.IsRequired, Is.False);
            Assert.That(catalog.SourceLanguage.HasFallback, Is.False);
        }

        [Test]
        public void Constructor_RejectsInconsistentDefinitions()
        {
            LanguageInfo english = new(new LanguageKey("English"), null, null, default, TextDirection.LeftToRight, false);
            LanguageInfo pirate = new(new LanguageKey("Pirate"), null, null, new LanguageKey("Parrot"), TextDirection.LeftToRight, false);

            Assert.That(() => { _ = new CatalogInfo(Catalog, new LanguageKey("French"), new[] { english }, Tables); }, Throws.ArgumentException);
            Assert.That(() => { _ = new CatalogInfo(Catalog, english.Key, new[] { english, pirate }, Tables); }, Throws.ArgumentException);
            Assert.That(() => { _ = new CatalogInfo(Catalog, english.Key, new[] { english, english }, Tables); }, Throws.ArgumentException);
            Assert.That(() => { _ = new CatalogInfo(Catalog, english.Key, Array.Empty<LanguageInfo>(), Tables); }, Throws.ArgumentException);
            Assert.That(() => { _ = new LanguageInfo(english.Key, null, null, english.Key, TextDirection.LeftToRight, false); }, Throws.ArgumentException);
        }

        [Test]
        public void CompiledCatalog_RoundTripsEveryField()
        {
            CatalogInfo original = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(Document), Tables, null);

            Assert.That(CompiledCatalog.TryRead(CompiledCatalog.Write(original), out CatalogInfo read, out string error), Is.True, error);
            Assert.That(read.Key, Is.EqualTo(Catalog));
            Assert.That(read.SourceLanguage.Name, Is.EqualTo("English"));
            Assert.That(read.Languages.Count, Is.EqualTo(4));
            for (int i = 0; i < original.Languages.Count; i++)
            {
                LanguageInfo expected = original.Languages[i];
                LanguageInfo actual = read.Languages[i];
                Assert.That(actual.Name, Is.EqualTo(expected.Name));
                Assert.That(actual.DisplayName, Is.EqualTo(expected.DisplayName));
                Assert.That(actual.Culture, Is.EqualTo(expected.Culture));
                Assert.That(actual.Fallback, Is.EqualTo(expected.Fallback));
                Assert.That(actual.Direction, Is.EqualTo(expected.Direction));
                Assert.That(actual.IsRequired, Is.EqualTo(expected.IsRequired));
                Assert.That(actual.Digits, Is.EqualTo(expected.Digits));
                Assert.That(actual.DecimalSeparator, Is.EqualTo(expected.DecimalSeparator));
                Assert.That(actual.GroupSeparator, Is.EqualTo(expected.GroupSeparator));
            }
            Assert.That(read.Languages[3].Digits, Is.EqualTo("0123456789"));
            Assert.That(read.Languages[3].GroupSeparator, Is.Empty);
            Assert.That(read.Languages[0].GroupSeparator, Is.Null);
            Assert.That(read.Tables.Count, Is.EqualTo(2));
            Assert.That(read.Tables[1].Delivery, Is.EqualTo(TableDelivery.Streaming));
        }

        [Test]
        public void GetFormat_ComesFromTheCultureThenTheFallbackThenTheRoot()
        {
            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(Document + "[Klingon]\n"), Tables, null);
            LanguageFormat english = FormatOf(catalog, "English");
            LanguageFormat russian = FormatOf(catalog, "Russian");

            Assert.That(PluralRules.TryFindCardinal("ru", out int russianRules), Is.True);
            Assert.That(russian.CardinalRules, Is.EqualTo(russianRules));
            Assert.That(russian.Numbers.DecimalSeparator, Is.EqualTo(","));
            Assert.That(FormatOf(catalog, "Pirate"), Is.SameAs(english));
            Assert.That(FormatOf(catalog, "Klingon"), Is.SameAs(LanguageFormat.Root));
        }

        [Test]
        public void GetFormat_AppliesTheLanguagesOwnNumberSymbols()
        {
            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(Document), Tables, null);
            LanguageFormat arabic = FormatOf(catalog, "Arabic");

            Assert.That(PluralRules.TryFindCardinal("ar", out int arabicRules), Is.True);
            Assert.That(arabic.CardinalRules, Is.EqualTo(arabicRules));
            Assert.That(arabic.Numbers.Digits, Is.EqualTo("0123456789"));
            Assert.That(arabic.Numbers.GroupSeparator, Is.Empty);
        }

        [Test]
        public void FromDocument_InheritsTheFallbackForCulturesCldrDoesntKnow()
        {
            List<DocumentIssue> issues = new();
            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(
                "@source English\n[English]\nCulture = en\n[Elvish]\nCulture = qya\nFallback = English\n"), Tables, issues);

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Severity, Is.EqualTo(IssueSeverity.Warning));
            Assert.That(issues[0].Message, Does.Contain("qya"));
            Assert.That(FormatOf(catalog, "Elvish"), Is.SameAs(FormatOf(catalog, "English")));
        }

        [Test]
        public void FromDocument_ReportsDigitsThatArentTen()
        {
            List<DocumentIssue> issues = new();
            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(
                "@source English\n[English]\nDigits = 0123\nDecimalSeparator =\n"), Tables, issues);

            Assert.That(issues.Count, Is.EqualTo(2));
            Assert.That(catalog.SourceLanguage.Digits, Is.Null);
            Assert.That(catalog.SourceLanguage.DecimalSeparator, Is.Null);
        }

        [Test]
        public void CompiledCatalog_RejectsDataCutShortAtAnyPoint()
        {
            byte[] data = CompiledCatalog.Write(CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(Document), Tables, null));
            for (int length = 0; length < data.Length; length++)
            {
                Assert.That(CompiledCatalog.TryRead(data.AsSpan(0, length), out _, out _), Is.False, $"Read {length} of {data.Length} bytes.");
            }
        }

        private static LanguageFormat FormatOf(CatalogInfo catalog, string language)
        {
            Assert.That(catalog.TryGetLanguage(new LanguageKey(language), out LanguageInfo info), Is.True, language);
            return catalog.GetFormat(info);
        }
    }
}
