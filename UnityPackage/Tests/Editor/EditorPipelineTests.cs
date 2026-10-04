using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Editor;
using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// Runs the editor's whole path on real files: importing tables and a catalog, finding which catalog owns each
    /// table, and a localizer reading them through the editor's table source.
    /// </summary>
    public class EditorPipelineTests
    {
        private const string Folder = "Assets/ReactiveLocalizerTests";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");

        [SetUp]
        public void CreateFiles()
        {
            KeysGenerator.IsSuspended = true;
            Directory.CreateDirectory($"{Folder}/Feature");
            File.WriteAllText($"{Folder}/Game.catalog", "@source English\n\n[English]\nCulture = en\n\n[Russian]\nCulture = ru\n");
            File.WriteAllText($"{Folder}/Shop.English.lang", "# Buys the selected item.\n@formerly BuyButton\nPurchase = Buy\nTitle = Shop\n");
            File.WriteAllText($"{Folder}/Shop.Russian.lang", "Purchase = Kupit\n");
            File.WriteAllText($"{Folder}/Feature/Inventory.English.lang", "@loading OnDemand\n\nSword = Sword\n");
            // The layout is scanned before importing, so the tables find their catalog in this very import.
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
        }

        [Test]
        public void TableFiles_ImportCompiledForTheCatalogThatOwnsThem()
        {
            TableAsset shop = AssetDatabase.LoadAssetAtPath<TableAsset>($"{Folder}/Shop.English.lang");
            TableAsset inventory = AssetDatabase.LoadAssetAtPath<TableAsset>($"{Folder}/Feature/Inventory.English.lang");

            Assert.That(shop.CatalogName, Is.EqualTo("Game"));
            Assert.That(shop.TableName, Is.EqualTo("Shop"));
            Assert.That(shop.LanguageName, Is.EqualTo("English"));
            Assert.That(shop.Data.IsEmpty, Is.False);
            Assert.That(inventory.CatalogName, Is.EqualTo("Game"));
        }

        [Test]
        public void CatalogIndex_CollectsTablesLanguagesAndSettings()
        {
            IndexedCatalog catalog = CatalogIndex.Find(new CatalogKey("Game"));

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Info.Languages.Count, Is.EqualTo(2));
            Assert.That(catalog.Tables.Count, Is.EqualTo(2));
            Assert.That(catalog.TryGetTable(new TableKey("Inventory"), out IndexedTable inventory), Is.True);
            Assert.That(inventory.Settings.Loading, Is.EqualTo(TableLoading.OnDemand));
            Assert.That(catalog.GeneratedCodePath, Is.EqualTo($"{Folder}/GameKeys.cs"));
        }

        [Test]
        public void Localizer_InTheEditor_ReadsTheImportedFiles()
        {
            using Localizer localizer = new(new CatalogKey("Game"), new UnityHost());

            Assert.That(localizer.InitializeAsync().IsCompleted, Is.True);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Buy"));
            Assert.That(localizer.Get(new EntryKey("Shop", "BuyButton")), Is.EqualTo("Buy"));
            localizer.SetLanguageAsync(new LanguageKey("Russian"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
            Assert.That(localizer.Get(new EntryKey("Shop", "Title")), Is.EqualTo("Shop"));
        }

        [Test]
        public void KeysScript_HoldsTheSourceKeysAndAliases()
        {
            KeysScript script = CatalogIndex.Find(new CatalogKey("Game")).CreateKeysScript();
            List<string> problems = new();

            string source = KeysScriptWriter.Write(script, problems);

            Assert.That(problems, Is.Empty);
            Assert.That(source, Does.Contain("public static class GameKeys"));
            Assert.That(source, Does.Contain("EntryKey Purchase = new(TableKey, \"Purchase\");"));
            Assert.That(source, Does.Contain("EntryKey BuyButton = Purchase;"));
            Assert.That(source, Does.Contain("EntryKey Sword = new(TableKey, \"Sword\");"));
            Assert.That(source, Does.Contain("LanguageKey Russian = new(\"Russian\");"));
        }

        [TestCase("Title Label (1)", "Title")]
        [TestCase("TitleText", "Title")]
        [TestCase("ConfirmButton", "ConfirmButton")]
        [TestCase("buy-now caption", "BuyNow")]
        [TestCase("Label", "Label")]
        [TestCase("(2)", "Entry")]
        [TestCase("\u041A\u043D\u043E\u043F\u043A\u0430", "Entry")]
        public void SuggestKey_NamesTheRoleRatherThanTheElement(string gameObjectName, string expectedKey)
        {
            Assert.That(EntryAuthoring.SuggestKey(gameObjectName), Is.EqualTo(expectedKey));
        }

        [Test]
        public void TryCreateEntry_AddsToTheSourceFileAndNumbersATakenKey()
        {
            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Shop", "Refund", "Return it", out EntryReference refund, out string problem), Is.True, problem);
            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Shop", "Title", " Spaced \\ text ", out EntryReference title, out problem), Is.True, problem);
            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Hud", "Health", "Health", out EntryReference health, out problem), Is.True, problem);

            Assert.That(refund, Is.EqualTo(new EntryReference("Game", "Shop", "Refund")));
            Assert.That(title.EntryName, Is.EqualTo("Title2"));
            Assert.That(health.TableName, Is.EqualTo("Hud"));
            using Localizer localizer = new(new CatalogKey("Game"), new UnityHost());
            localizer.InitializeAsync();
            Assert.That(localizer.Get(new EntryKey("Shop", "Refund")), Is.EqualTo("Return it"));
            Assert.That(localizer.Get(new EntryKey("Shop", "Title2")), Is.EqualTo(" Spaced \\ text "));
            Assert.That(localizer.Get(new EntryKey("Hud", "Health")), Is.EqualTo("Health"));
            Assert.That(File.Exists($"{Folder}/Hud.English.lang"), Is.True);
        }

        [Test]
        public void TryCreateEntry_RejectsMissingCatalogsAndInvalidNames()
        {
            Assert.That(EntryAuthoring.TryCreateEntry("Nowhere", "Shop", "Refund", "Text", out _, out string missing), Is.False);
            Assert.That(missing, Does.Contain("Nowhere"));
            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Shop", "Buy Now", "Text", out _, out string invalid), Is.False);
            Assert.That(invalid, Does.Contain("Buy Now"));
        }

        [Test]
        public void ImportProblems_AreReportedAtTheirLine()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"Shop\.English\.lang\(2,1\): error: 'Broken Line' can't start a line|Shop\.English\.lang\(2,8\): error: Expected '='"));
            File.WriteAllText($"{Folder}/Shop.English.lang", "Purchase = Buy\nBroken Line\n");

            AssetDatabase.ImportAsset($"{Folder}/Shop.English.lang", ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
