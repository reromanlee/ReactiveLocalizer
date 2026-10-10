using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Editor;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.TestTools;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>Tests the table window's model of a table, its filter and its sidebar counts, on real files.</summary>
    public class TableSheetTests
    {
        private const string Folder = "Assets/ReactiveLocalizerSheetTests";

        private IndexedCatalog _catalog;

        [SetUp]
        public void CreateFiles()
        {
            // The files have errors on purpose, which their imports report.
            LogAssert.ignoreFailingMessages = true;
            KeysGenerator.IsSuspended = true;
            Directory.CreateDirectory(Folder);
            File.WriteAllText($"{Folder}/Sheet.catalog", "@source English\n\n[English]\nCulture = en\n\n[Russian]\nCulture = ru\n");
            File.WriteAllText($"{Folder}/Shop.English.lang",
                "Farewell = Bye\n\n# The button that buys the item.\n@maximumLength 6\nPurchase = Buy now\n\nTitle = Shop\nWelcome = Hello, {name}!\n");
            File.WriteAllText($"{Folder}/Shop.Russian.lang",
                "Ghost = Boo\nPurchase [000000] = Kupit\nTitle = Magazin\nWelcome = Privet, {nme}!\n");
            CatalogLayout.Refresh();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CatalogIndex.Invalidate();
            _catalog = CatalogIndex.Find(new CatalogKey("Sheet"));
        }

        [TearDown]
        public void DeleteFiles()
        {
            AssetDatabase.DeleteAsset(Folder);
            CatalogLayout.Refresh();
            CatalogIndex.Invalidate();
            KeysGenerator.IsSuspended = false;
        }

        [Test]
        public void Sheet_ListsEveryEntryWithItsStateAndProblemsPerLanguage()
        {
            TableSheet sheet = new(_catalog, "Shop");

            Assert.That(sheet.Rows.ConvertAll(row => row.Key), Is.EqualTo(new[] { "Farewell", "Purchase", "Title", "Welcome", "Ghost" }));
            Assert.That(sheet.Languages[0].Name, Is.EqualTo("English"));
            TableSheetRow purchase = sheet.Find("purchase");
            Assert.That(purchase.Context, Is.EqualTo("The button that buys the item."));
            Assert.That(purchase.Cells[0].Problems, Is.EqualTo(TableSheetProblem.OverLimit));
            Assert.That(purchase.Cells[1].State, Is.EqualTo(TranslationState.Outdated));
            Assert.That(sheet.Find("Farewell").Cells[1].State, Is.EqualTo(TranslationState.Missing));
            Assert.That(sheet.Find("Title").Cells[1].State, Is.EqualTo(TranslationState.Unverified));
            Assert.That(sheet.Find("Welcome").Cells[1].Problems & TableSheetProblem.Error, Is.EqualTo(TableSheetProblem.Error));
            Assert.That(sheet.Find("Ghost").Cells[1].State, Is.EqualTo(TranslationState.Orphan));
            Assert.That(sheet.Find("Ghost").Cells[1].Problems & TableSheetProblem.Error, Is.EqualTo(TableSheetProblem.Error));
            Assert.That(sheet.IsCurrent(), Is.True);
        }

        [Test]
        public void Sheet_AppliesEditsAndStaysCurrent()
        {
            // Saving imports the files again, which reports their errors again.
            LogAssert.ignoreFailingMessages = true;
            TableSheet sheet = new(_catalog, "Shop");

            Assert.That(sheet.TryApply("Mark current", (TableFileSet files, out string problem) => files.TryMarkCurrent("Russian", "Purchase", out problem), out string failure), Is.True, failure);
            Assert.That(sheet.TryApply("Translate", (TableFileSet files, out string problem) => files.TrySetText("Russian", "Farewell", "Poka", out problem), out failure), Is.True, failure);

            Assert.That(sheet.Find("Purchase").Cells[1].State, Is.EqualTo(TranslationState.Current));
            Assert.That(sheet.Find("Farewell").Cells[1].Text, Is.EqualTo("Poka"));
            Assert.That(sheet.Find("Farewell").Cells[1].State, Is.EqualTo(TranslationState.Current));
            Assert.That(File.ReadAllText($"{Folder}/Shop.Russian.lang"), Does.Contain("Poka"));
            Assert.That(sheet.IsCurrent(), Is.True);
            File.WriteAllText($"{Folder}/Shop.Russian.lang", "Purchase = Changed elsewhere\n");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Assert.That(sheet.IsCurrent(), Is.False);
        }

        [Test]
        public void Filter_PicksRowsBySearchAndProblem()
        {
            TableSheet sheet = new(_catalog, "Shop");
            List<TableSheetRow> results = new();

            TableFilter.Apply(sheet.Rows, "magazin", TableFilterKind.All, -1, results);
            Assert.That(results.ConvertAll(row => row.Key), Is.EqualTo(new[] { "Title" }));
            TableFilter.Apply(sheet.Rows, string.Empty, TableFilterKind.Missing, 1, results);
            Assert.That(results.ConvertAll(row => row.Key), Is.EqualTo(new[] { "Farewell" }));
            // Translations without a fingerprint count too: nothing says they follow the current source text.
            TableFilter.Apply(sheet.Rows, string.Empty, TableFilterKind.Outdated, -1, results);
            Assert.That(results.ConvertAll(row => row.Key), Is.EqualTo(new[] { "Purchase", "Title", "Welcome" }));
            TableFilter.Apply(sheet.Rows, "buys", TableFilterKind.Problems, -1, results);
            Assert.That(results.ConvertAll(row => row.Key), Is.EqualTo(new[] { "Purchase" }));
        }

        [Test]
        public void Status_CountsWhatNeedsAttention()
        {
            Assert.That(_catalog.TryGetTable(new TableKey("Shop"), out IndexedTable table), Is.True);

            TableStatus status = TableStatus.Of(_catalog, table);

            Assert.That(status.Missing, Is.EqualTo(1));
            Assert.That(status.Outdated, Is.EqualTo(1));
            Assert.That(status.Errors, Is.GreaterThanOrEqualTo(2));
        }
    }
}
