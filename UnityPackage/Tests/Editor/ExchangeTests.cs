using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using reromanlee.ReactiveLocalizer.Editor;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// Runs exports and imports on real files: a catalog's tables exported to each format, a translator's changes
    /// imported back as one Undo step, and an untouched export changing nothing.
    /// </summary>
    public class ExchangeTests
    {
        private const string Folder = "Assets/ReactiveLocalizerExchangeTests";

        private string _exportFolder;

        private static IndexedCatalog Catalog => CatalogIndex.Find(new CatalogKey("Trade"));

        [SetUp]
        public void CreateFiles()
        {
            KeysGenerator.IsSuspended = true;
            _exportFolder = Path.Combine(Path.GetTempPath(), "ReactiveLocalizerExchange" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Folder);
            File.WriteAllText($"{Folder}/Trade.catalog", "@source English\n\n[English]\nCulture = en\n\n[Russian]\nCulture = ru\n");
            File.WriteAllText($"{Folder}/Market.English.lang", "# Buys the selected item.\nPurchase = Buy\nTitle = Market\n");
            File.WriteAllText($"{Folder}/Market.Russian.lang", $"Purchase [{Hashing.ComputeFingerprint("Buy"):x6}] = Kupit\n");
            CatalogLayout.Refresh();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CatalogIndex.Invalidate();
        }

        [TearDown]
        public void DeleteFiles()
        {
            AssetDatabase.DeleteAsset(Folder);
            CatalogLayout.Refresh();
            CatalogIndex.Invalidate();
            KeysGenerator.IsSuspended = false;
            if (Directory.Exists(_exportFolder))
            {
                Directory.Delete(_exportFolder, true);
            }
        }

        [Test]
        public void Csv_TranslatedAndImported_ChangesOnlyTheTranslationAsOneUndoStep()
        {
            string russian = File.ReadAllText($"{Folder}/Market.Russian.lang");
            List<string> written = LocalizationExchange.Export(Catalog, ExchangeFormat.Csv, new ExportOptions { Entries = ExportedEntries.MissingOrOutdated }, _exportFolder, new List<string>());
            Assert.That(written, Is.EqualTo(new[] { Path.Combine(_exportFolder, "Market.csv") }));
            File.WriteAllText(written[0], File.ReadAllText(written[0]).Replace("Market.Title,,,Market,", "Market.Title,,,Market,Rynok"));

            ImportSession session = new(LocalizationExchange.ReadFolder(_exportFolder));
            ImportPlan plan = session.Apply();

            Assert.That(session.Catalog.Name, Is.EqualTo("Trade"), "The catalog is found by the tables the files name.");
            Assert.That(plan.Count(ImportChangeKind.Added), Is.EqualTo(1));
            Assert.That(plan.Problems, Is.Empty);
            string imported = File.ReadAllText($"{Folder}/Market.Russian.lang");
            Assert.That(imported, Does.Contain($"Title [{Hashing.ComputeFingerprint("Market"):x6}] = Rynok"));
            Undo.PerformUndo();
            Assert.That(File.ReadAllText($"{Folder}/Market.Russian.lang"), Is.EqualTo(russian));
        }

        [Test]
        public void Xlsx_ImportedUntouched_ChangesNothing()
        {
            string path = Path.Combine(_exportFolder, "Trade.xlsx");
            List<string> written = LocalizationExchange.Export(Catalog, ExchangeFormat.Xlsx, null, path, new List<string>());

            ImportPlan plan = new ImportSession(new[] { LocalizationExchange.Read(written[0]) }).Plan();

            Assert.That(plan.HasChanges, Is.False);
            Assert.That(plan.Changes, Is.Empty);
            Assert.That(plan.UnchangedCount, Is.EqualTo(1));
        }

        [Test]
        public void Xliff_WritesAFilePerLanguageThatNamesItsCatalog()
        {
            List<string> written = LocalizationExchange.Export(Catalog, ExchangeFormat.Xliff20, null, _exportFolder, new List<string>());

            Assert.That(written, Is.EqualTo(new[] { Path.Combine(_exportFolder, "Trade.Russian.xlf") }));
            ImportedFile file = LocalizationExchange.Read(written[0]);
            Assert.That(file.CatalogName, Is.EqualTo("Trade"));
            Assert.That(file.Rows.Count, Is.EqualTo(2));
        }

        [Test]
        public void LlmReply_ImportsWithTheTagsOfTheTextsTheModelSaw()
        {
            string handoff = LocalizationExchange.CreateHandoff(Catalog, new ExportOptions { Entries = ExportedEntries.Missing }, "Russian");
            string reply = handoff.Substring(handoff.IndexOf("\n@catalog ", StringComparison.Ordinal) + 1).Replace("] = Market", "] = Rynok");

            ImportPlan plan = new ImportSession(new[] { LlmHandoff.Read("The reply", reply) }).Apply();

            Assert.That(plan.Count(ImportChangeKind.Added), Is.EqualTo(1));
            Assert.That(File.ReadAllText($"{Folder}/Market.Russian.lang"), Does.Contain($"Title [{Hashing.ComputeFingerprint("Market"):x6}] = Rynok"));
        }
    }
}
