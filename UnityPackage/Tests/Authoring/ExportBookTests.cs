using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class ExportBookTests
    {
        [Test]
        public void Collect_LaysOutEveryEntryInNaturalOrder()
        {
            ExportBook book = ExchangeFixture.Export(null);

            Assert.That(book.SourceLanguage.Name, Is.EqualTo("English"));
            Assert.That(Names(book.Languages), Is.EqualTo(new[] { "Russian", "Pirate" }));
            Assert.That(book.Tables.Count, Is.EqualTo(2));
            Assert.That(book.Tables[0].Name, Is.EqualTo("Common"));
            Assert.That(Keys(book.Tables[1]), Is.EqualTo(new[] { "Balance", "Purchase", "Title" }));
            Assert.That(book.RowCount, Is.EqualTo(4));
        }

        [Test]
        public void Collect_GivesEachCellItsTextAndState()
        {
            ExportTable shop = ExchangeFixture.ExportRussian().Tables[1];
            ExportCell balance = shop.Rows[0].Cells[0];
            ExportCell purchase = shop.Rows[1].Cells[0];
            ExportCell title = shop.Rows[2].Cells[0];
            ExportCell confirm = ExchangeFixture.ExportRussian().Tables[0].Rows[0].Cells[0];

            Assert.That(balance.Text, Is.Null);
            Assert.That(balance.State, Is.EqualTo(TranslationState.Missing));
            Assert.That(purchase.Text, Is.EqualTo("Kupit"));
            Assert.That(purchase.State, Is.EqualTo(TranslationState.Current));
            Assert.That(title.State, Is.EqualTo(TranslationState.Outdated));
            Assert.That(confirm.State, Is.EqualTo(TranslationState.Unverified));
        }

        [Test]
        public void Collect_KeepsTheContextAndMaximumLength()
        {
            ExportRow purchase = ExchangeFixture.ExportRussian().Tables[1].Rows[1];

            Assert.That(purchase.Context, Is.EqualTo("Button that confirms buying the selected item."));
            Assert.That(purchase.MaximumLength, Is.EqualTo(16));
            Assert.That(purchase.SourceText, Is.EqualTo("Buy"));
        }

        [TestCase(ExportedEntries.Missing, new[] { "Shop.Balance" })]
        [TestCase(ExportedEntries.Outdated, new[] { "Common.Confirm", "Shop.Title" })]
        [TestCase(ExportedEntries.MissingOrOutdated, new[] { "Common.Confirm", "Shop.Balance", "Shop.Title" })]
        public void Collect_KeepsOnlyTheEntriesAsked(ExportedEntries entries, string[] expected)
        {
            ExportBook book = ExchangeFixture.ExportRussian(entries);

            Assert.That(QualifiedKeys(book), Is.EqualTo(expected));
        }

        [Test]
        public void Collect_KeepsOnlyTheTablesAsked()
        {
            ExportBook book = ExchangeFixture.Export(new ExportOptions { Tables = new[] { "shop" } });

            Assert.That(book.Tables.Count, Is.EqualTo(1));
            Assert.That(book.Tables[0].Name, Is.EqualTo("Shop"));
        }

        [Test]
        public void Collect_LeavesOutATableWithoutSourceFile()
        {
            ExportBook book = ExportBook.Collect(ExchangeFixture.Catalog, ExchangeFixture.Tables(("Lost", "Russian", "Line = Stroka\n")), null);

            Assert.That(book.Tables.Count, Is.EqualTo(0));
        }

        private static List<string> Names(IReadOnlyList<LanguageInfo> languages)
        {
            List<string> names = new();
            foreach (LanguageInfo language in languages)
            {
                names.Add(language.Name);
            }
            return names;
        }

        private static List<string> Keys(ExportTable table)
        {
            List<string> keys = new();
            foreach (ExportRow row in table.Rows)
            {
                keys.Add(row.Key);
            }
            return keys;
        }

        private static List<string> QualifiedKeys(ExportBook book)
        {
            List<string> keys = new();
            foreach (ExportTable table in book.Tables)
            {
                foreach (ExportRow row in table.Rows)
                {
                    keys.Add($"{table.Name}.{row.Key}");
                }
            }
            return keys;
        }
    }
}
