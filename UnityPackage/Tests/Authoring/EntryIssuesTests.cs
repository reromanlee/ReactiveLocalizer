using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class EntryIssuesTests
    {
        private static readonly CatalogInfo Catalog = CatalogInfo.FromDocument(new CatalogKey("Game"),
            CatalogDocument.Parse("@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n"), null, null);

        [Test]
        public void Collect_PutsEachIssueWithTheEntryItIsAbout()
        {
            TableDocument source = TableDocument.Parse("Purchase = Buy\nWelcome = Hello, {name}!\n");
            TableDocument russian = TableDocument.Parse("Purchase = Buy {count}\nWelcome = Hello, {name}!\nGhost = Boo\nBroken line\n");
            Dictionary<string, List<DocumentIssue>> byKey = new(StringComparer.OrdinalIgnoreCase);
            List<DocumentIssue> fileIssues = new();

            EntryIssues.Collect(Catalog, "Shop", "Russian", russian, source, byKey, fileIssues);

            Assert.That(byKey.ContainsKey("Purchase"), Is.True);
            Assert.That(byKey.ContainsKey("Welcome"), Is.False);
            Assert.That(byKey["ghost"][0].Severity, Is.EqualTo(IssueSeverity.Error));
            Assert.That(fileIssues.Count, Is.EqualTo(1));
            Assert.That(fileIssues[0].Line, Is.EqualTo(4));
        }

        [Test]
        public void Collect_PutsAttributeIssuesWithTheirEntry()
        {
            TableDocument source = TableDocument.Parse("@formerly Bad Name\nPurchase = Buy\n");
            Dictionary<string, List<DocumentIssue>> byKey = new(StringComparer.OrdinalIgnoreCase);
            List<DocumentIssue> fileIssues = new();

            EntryIssues.Collect(Catalog, "Shop", "English", source, null, byKey, fileIssues);

            Assert.That(byKey.ContainsKey("Purchase"), Is.True);
            Assert.That(fileIssues, Is.Empty);
        }

        [TestCase("16", 16)]
        [TestCase("0", -1)]
        [TestCase("-3", -1)]
        [TestCase("wide", -1)]
        public void MaximumLength_ReadsOnlyAPositiveNumber(string value, int expected)
        {
            bool isRead = MaximumLength.TryParse(value, out int limit);

            Assert.That(isRead ? limit : -1, Is.EqualTo(expected));
        }

        [Test]
        public void MaximumLength_MeasuresPlainTextOnly()
        {
            Assert.That(MaximumLength.IsExceededBy("Twelve chars", 11), Is.True);
            Assert.That(MaximumLength.IsExceededBy("Twelve chars", 12), Is.False);
            Assert.That(MaximumLength.IsExceededBy("{count} very long message", 3), Is.False);
        }
    }
}
