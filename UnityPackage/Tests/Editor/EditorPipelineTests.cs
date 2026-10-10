using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Editor;
using reromanlee.ReactiveLocalizer.Tables;
using reromanlee.ReactiveLocalizer.Unity;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
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

        private const string EnglishBalance = "Balance = You have {coins, plural, one {# coin} other {# coins}}.\n";
        private const string RussianBalance = "Balance = \u0423 \u0432\u0430\u0441 {coins, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430} few {# \u043C\u043E\u043D\u0435\u0442\u044B} many {# \u043C\u043E\u043D\u0435\u0442} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}.\n";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");

        private static EntryMessage BalanceOf(int coins) => new(new EntryKey("Shop", "Balance"), new MessageArgument("coins", coins));

        [SetUp]
        public void CreateFiles()
        {
            KeysGenerator.IsSuspended = true;
            Directory.CreateDirectory($"{Folder}/Feature");
            File.WriteAllText($"{Folder}/Game.catalog", "@source English\n\n[English]\nCulture = en\n\n[Russian]\nCulture = ru\n");
            File.WriteAllText($"{Folder}/Shop.English.lang", "# Buys the selected item.\n@formerly BuyButton\nPurchase = Buy\nTitle = Shop\n" + EnglishBalance);
            File.WriteAllText($"{Folder}/Shop.Russian.lang", "Purchase = Kupit\n" + RussianBalance);
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
            KeysScript script = CatalogIndex.Find(new CatalogKey("Game")).CreateKeysScript(out string brokenMessage);
            List<string> problems = new();

            string source = KeysScriptWriter.Write(script, problems);

            Assert.That(problems, Is.Empty);
            Assert.That(brokenMessage, Is.Null);
            Assert.That(source, Does.Contain("EntryMessage Balance(global::reromanlee.ReactiveLocalizer.MessageNumber coins)"));
            Assert.That(source, Does.Contain("public static class GameKeys"));
            Assert.That(source, Does.Contain("EntryKey Purchase = new(TableKey, \"Purchase\");"));
            Assert.That(source, Does.Contain("EntryKey BuyButton = Purchase;"));
            Assert.That(source, Does.Contain("EntryKey Sword = new(TableKey, \"Sword\");"));
            Assert.That(source, Does.Contain("LanguageKey Russian = new(\"Russian\");"));
        }

        [Test]
        public void Localizer_InTheEditor_FormatsMessagesOfTheImportedFiles()
        {
            using Localizer localizer = new(new CatalogKey("Game"), new UnityHost());
            localizer.InitializeAsync();

            Assert.That(localizer.Get(BalanceOf(21)), Is.EqualTo("You have 21 coins."));
            localizer.SetLanguageAsync(new LanguageKey("Russian"));
            Assert.That(localizer.Get(BalanceOf(21)), Is.EqualTo("\u0423 \u0432\u0430\u0441 21 \u043C\u043E\u043D\u0435\u0442\u0430."));
        }

        [Test]
        public void Translation_WithOtherArguments_IsLeftOutUntilItMatches()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"Shop\.Russian\.lang\(2,12\): error: \{money\} isn't an argument of the source text"));
            LogAssert.Expect(LogType.Error, new Regex(@"Shop\.Russian\.lang\(2,11\): error: The translation leaves out \{coins\}"));
            File.WriteAllText($"{Folder}/Shop.Russian.lang", "Purchase = Kupit\nBalance = {money} \u043C\u043E\u043D\u0435\u0442\n");
            AssetDatabase.ImportAsset($"{Folder}/Shop.Russian.lang", ImportAssetOptions.ForceSynchronousImport);

            using Localizer localizer = new(new CatalogKey("Game"), new UnityHost());
            localizer.SetLanguageAsync(new LanguageKey("Russian"));
            localizer.InitializeAsync();
            Assert.That(localizer.Get(BalanceOf(2)), Is.EqualTo("You have 2 coins."));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }

        [Test]
        public void Translation_IsCheckedAgainWhenItsSourceChanges()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"Shop\.Russian\.lang\(2,.*\{coins\} isn't an argument of the source text"));
            LogAssert.Expect(LogType.Error, new Regex(@"Shop\.Russian\.lang\(2,.*leaves out \{money\}"));
            File.WriteAllText($"{Folder}/Shop.English.lang", "Purchase = Buy\nBalance = You have {money, plural, one {# coin} other {# coins}}.\n");
            AssetDatabase.ImportAsset($"{Folder}/Shop.English.lang", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            using Localizer localizer = new(new CatalogKey("Game"), new UnityHost());
            localizer.SetLanguageAsync(new LanguageKey("Russian"));
            localizer.InitializeAsync();
            Assert.That(localizer.Get(new EntryMessage(new EntryKey("Shop", "Balance"), new MessageArgument("money", 5))), Is.EqualTo("You have 5 coins."));
        }

        [Test]
        public void CompleteTranslation_IsMarkedCompleteForTheCatalogsKeys()
        {
            File.WriteAllText($"{Folder}/Shop.Russian.lang", "Purchase = Kupit\nTitle = Magazin\n" + RussianBalance);
            AssetDatabase.ImportAsset($"{Folder}/Shop.Russian.lang", ImportAssetOptions.ForceSynchronousImport);
            CatalogIndex.Invalidate();
            IndexedCatalog catalog = CatalogIndex.Find(new CatalogKey("Game"));
            catalog.Info.TryGetTable(new TableKey("Shop"), out TableInfo shop);

            Assert.That(Read($"{Folder}/Shop.Russian.lang").IsComplete(shop.KeysHash), Is.True);
            Assert.That(Read($"{Folder}/Shop.English.lang").IsComplete(shop.KeysHash), Is.True);
            File.WriteAllText($"{Folder}/Shop.Russian.lang", "Purchase = Kupit\n");
            AssetDatabase.ImportAsset($"{Folder}/Shop.Russian.lang", ImportAssetOptions.ForceSynchronousImport);
            Assert.That(Read($"{Folder}/Shop.Russian.lang").IsComplete(shop.KeysHash), Is.False);
        }

        [Test]
        public void Pack_PutsStreamingTablesInTheirOwnFolderWithAList()
        {
            File.WriteAllText($"{Folder}/Feature/Dialogue.English.lang", "@delivery Streaming\n\nLine1 = Hello\n");
            File.WriteAllText($"{Folder}/Feature/Dialogue.Russian.lang", "Line1 = Privet\n");
            CatalogLayout.Refresh();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CatalogIndex.Invalidate();
            string output = Path.Combine(Path.GetTempPath(), "ReactiveLocalizerPack", System.Guid.NewGuid().ToString("N"));
            try
            {
                TableBuildStep.Pack($"{output}/Resources", $"{output}/Streaming");

                Assert.That(File.Exists($"{output}/Resources/Game/catalog.bytes"), Is.True);
                Assert.That(File.Exists($"{output}/Resources/Game/Russian/Shop.bytes"), Is.True);
                Assert.That(File.Exists($"{output}/Resources/Game/English/Dialogue.bytes"), Is.False);
                Assert.That(File.Exists($"{output}/Streaming/Game/English/Dialogue.bytes"), Is.True);
                Assert.That(File.Exists($"{output}/Streaming/Game/Russian/Dialogue.bytes"), Is.True);
                Assert.That(File.ReadAllText($"{output}/Resources/Game/streaming.bytes"), Is.EqualTo("English/Dialogue\nRussian/Dialogue\n"));
            }
            finally
            {
                Directory.Delete(output, true);
            }
        }

        [Test]
        public void Validate_FindsBrokenAndRenamedReferencesInAssets()
        {
            EntryHolder holder = ScriptableObject.CreateInstance<EntryHolder>();
            holder.Entry = new EntryReference("Game", "Shop", "Purchse");
            holder.Entries.Add(new EntryReference("Game", "Shop", "BuyButton"));
            holder.Entries.Add(new EntryReference("Game", "Shop", "Title"));
            AssetDatabase.CreateAsset(holder, $"{Folder}/Holder.asset");

            List<EntryReferenceScanner.FoundReference> found = EntryReferenceScanner.Scan(new[] { $"{Folder}/Holder.asset" });
            ValidationReport report = LocalizationValidation.Validate(CatalogIndex.Find(new CatalogKey("Game")), found, true);
            List<string> lines = new();
            foreach (ValidationIssue issue in report.Issues)
            {
                lines.Add(issue.ToString());
            }

            Assert.That(found.Count, Is.EqualTo(3));
            Assert.That(found[0].Location, Is.EqualTo($"{Folder}/Holder.asset (EntryHolder.Entry)"));
            Assert.That(lines, Has.Some.EqualTo($"{Folder}/Holder.asset (EntryHolder.Entry): error: It refers to 'Shop.Purchse', which doesn't exist. Did you mean 'Shop.Purchase'?"));
            Assert.That(lines, Has.Some.StartsWith($"{Folder}/Holder.asset (EntryHolder.Entries.Array.data[0]): warning: It refers to 'Shop.BuyButton' by the entry's former name"));
            Assert.That(report.ErrorCount, Is.EqualTo(1));
        }

        [Test]
        public void BuildGate_FailsOnAReferenceToAMissingKeyInResources()
        {
            Directory.CreateDirectory($"{Folder}/Resources");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EntryHolder holder = ScriptableObject.CreateInstance<EntryHolder>();
            holder.Entry = new EntryReference("Game", "Shop", "Refund");
            AssetDatabase.CreateAsset(holder, $"{Folder}/Resources/Holder.asset");
            LogAssert.Expect(LogType.Error, new Regex(@"Holder\.asset \(EntryHolder\.Entry\): error: It refers to 'Shop\.Refund', which doesn't exist"));

            Assert.That(() => new ValidationBuildStep().OnPreprocessBuild(null), Throws.TypeOf<UnityEditor.Build.BuildFailedException>());
        }

        [UnityTest]
        public IEnumerator EditingAFile_UpdatesBoundTextLive()
        {
            Localizer localizer = new(new CatalogKey("Game"), new UnityHost());
            localizer.InitializeAsync();
            List<string> texts = new();
            TextBinding binding = localizer.Bind(Purchase, texts, static (list, text) => list.Add(text));

            File.WriteAllText($"{Folder}/Shop.English.lang", "# Buys the selected item.\n@formerly BuyButton\nPurchase = Buy now\nTitle = Shop\n" + EnglishBalance);
            AssetDatabase.ImportAsset($"{Folder}/Shop.English.lang", ImportAssetOptions.ForceSynchronousImport);
            for (int frame = 0; frame < 100 && texts.Count < 2; frame++)
            {
                yield return null;
            }

            Assert.That(texts, Is.EqualTo(new[] { "Buy", "Buy now" }));
            binding.Dispose();
            localizer.Dispose();
        }

        private static CompiledTable Read(string path)
        {
            TableAsset asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
            Assert.That(CompiledTable.TryRead(asset.Data.Span, out CompiledTable table, out string error), Is.True, error);
            return table;
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
        public void CreatingEntries_CanBeUndoneStepByStep()
        {
            string english = File.ReadAllText($"{Folder}/Shop.English.lang");

            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Shop", "Refund", "Return it", out _, out string problem), Is.True, problem);
            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Hud", "Health", "Health", out _, out problem), Is.True, problem);
            Assert.That(File.ReadAllText($"{Folder}/Shop.English.lang"), Does.Contain("Refund = Return it"));

            Undo.PerformUndo();
            Assert.That(File.Exists($"{Folder}/Hud.English.lang"), Is.False);
            Undo.PerformUndo();
            Assert.That(File.ReadAllText($"{Folder}/Shop.English.lang"), Is.EqualTo(english));
            Undo.PerformRedo();
            Assert.That(File.ReadAllText($"{Folder}/Shop.English.lang"), Does.Contain("Refund = Return it"));
        }

        [Test]
        public void EntryPreview_ShowsTheTextInThePreviewLanguageOrWhatIsWrong()
        {
            LocalizationUserSettings settings = LocalizationUserSettings.instance;
            string previous = settings.PreviewLanguage;
            try
            {
                settings.PreviewLanguage = string.Empty;
                Assert.That(EntryPreview.Describe(new EntryReference("Game", "Shop", "Purchase")).Text, Is.EqualTo("Buy"));

                settings.PreviewLanguage = "Russian";
                EntryDescription purchase = EntryPreview.Describe(new EntryReference("Game", "Shop", "Purchase"));
                EntryDescription title = EntryPreview.Describe(new EntryReference("Game", "Shop", "Title"));
                EntryDescription renamed = EntryPreview.Describe(new EntryReference("Game", "Shop", "BuyButton"));
                EntryDescription missing = EntryPreview.Describe(new EntryReference("Game", "Shop", "Purchse"));
                EntryDescription elsewhere = EntryPreview.Describe(new EntryReference("Game", "Shop", "Purchase"), "Tools");

                Assert.That(purchase.Kind, Is.EqualTo(EntryPreviewKind.Found));
                Assert.That(purchase.Text, Is.EqualTo("Kupit"));
                Assert.That(title.Text, Is.EqualTo("Shop"));
                Assert.That(title.Tooltip, Does.Contain("Russian"));
                Assert.That(renamed.Kind, Is.EqualTo(EntryPreviewKind.Renamed));
                Assert.That(renamed.Fix, Is.EqualTo(new EntryReference("Game", "Shop", "Purchase")));
                Assert.That(missing.Kind, Is.EqualTo(EntryPreviewKind.Broken));
                Assert.That(missing.Text, Does.Contain("Did you mean 'Shop.Purchase'?"));
                Assert.That(elsewhere.Kind, Is.EqualTo(EntryPreviewKind.Broken));
                Assert.That(EntryPreview.Describe(default).Kind, Is.EqualTo(EntryPreviewKind.Unpicked));
            }
            finally
            {
                settings.PreviewLanguage = previous;
            }
        }

        [Test]
        public void NewCatalogText_DefinesTheSourceLanguageAndItsCulture()
        {
            List<DocumentIssue> issues = new();

            CatalogInfo catalog = CatalogInfo.FromDocument(new CatalogKey("Fresh"),
                CatalogDocument.Parse(CatalogCreationWindow.CreateText("Japanese", "ja", "Nihongo")), null, issues);
            CatalogInfo withoutCulture = CatalogInfo.FromDocument(new CatalogKey("Fresh"),
                CatalogDocument.Parse(CatalogCreationWindow.CreateText("Elvish", string.Empty, "Elvish")), null, issues);

            Assert.That(issues, Is.Empty);
            Assert.That(catalog.SourceLanguage.Name, Is.EqualTo("Japanese"));
            Assert.That(catalog.SourceLanguage.Culture, Is.EqualTo("ja"));
            Assert.That(catalog.SourceLanguage.DisplayName, Is.EqualTo("Nihongo"));
            Assert.That(withoutCulture.SourceLanguage.Name, Is.EqualTo("Elvish"));
        }

        [Test]
        public void EditModePreview_ShowsTextInThePreviewLanguage()
        {
            LocalizationUserSettings settings = LocalizationUserSettings.instance;
            string previous = settings.PreviewLanguage;
            try
            {
                settings.PreviewLanguage = string.Empty;
                ILocalizer preview = EditModePreview.For(new CatalogKey("Game"));

                Assert.That(EditModePreview.IsActive, Is.True);
                Assert.That(preview.Get(Purchase), Is.EqualTo("Buy"));
                settings.PreviewLanguage = "Russian";
                Assert.That(preview.Get(Purchase), Is.EqualTo("Kupit"));
                settings.PreviewLanguage = "Klingon";
                Assert.That(preview.Get(Purchase), Is.EqualTo("Buy"));
                Assert.That(EditModePreview.For(new CatalogKey("Nowhere")), Is.Null);
            }
            finally
            {
                settings.PreviewLanguage = previous;
            }
        }

        [Test]
        public void SavingAScene_SavesTheAuthoredTextInsteadOfThePreview()
        {
            GameObject previewed = new("Previewed");
            int restored = 0;
            int shown = 0;
            EditModePreview.Track(previewed.transform, () => restored++, () => shown++);
            try
            {
                Assert.That(EditorSceneManager.SaveScene(previewed.scene, $"{Folder}/Previewed.unity", true), Is.True);

                Assert.That(restored, Is.EqualTo(1));
                Assert.That(shown, Is.EqualTo(1));
            }
            finally
            {
                EditModePreview.Untrack(previewed.transform);
                Object.DestroyImmediate(previewed);
            }
        }

        [Test]
        public void CreatingEntries_LeavesGeneratedCodeUntilTheEditorLosesFocus()
        {
            KeysGenerator.Run();
            Assert.That(KeysGenerator.IsPending, Is.False);

            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Shop", "Refund", "Return it", out _, out string problem), Is.True, problem);

            Assert.That(KeysGenerator.IsPending, Is.True);
            KeysGenerator.Run();
            Assert.That(KeysGenerator.IsPending, Is.False);
        }

        [Test]
        public void UndoingSomethingElse_LeavesFilesChangedElsewhereAlone()
        {
            Assert.That(EntryAuthoring.TryCreateEntry("Game", "Shop", "Refund", "Return it", out _, out string problem), Is.True, problem);
            File.WriteAllText($"{Folder}/Shop.English.lang", "Purchase = Changed in a text editor\n");
            EntryHolder holder = ScriptableObject.CreateInstance<EntryHolder>();
            try
            {
                Undo.IncrementCurrentGroup();
                Undo.RecordObject(holder, "Rename the holder");
                holder.name = "Renamed";

                Undo.PerformUndo();

                Assert.That(File.ReadAllText($"{Folder}/Shop.English.lang"), Is.EqualTo("Purchase = Changed in a text editor\n"));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
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
            List<string> errors = new();
            void Collect(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Error)
                {
                    errors.Add(message);
                }
            }
            File.WriteAllText($"{Folder}/Shop.English.lang", "Purchase = Buy\nBroken Line\n");
            // The two files report in whichever order their imports finish, so the errors are checked as a set.
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += Collect;
            try
            {
                AssetDatabase.ImportAsset($"{Folder}/Shop.English.lang", ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                Application.logMessageReceived -= Collect;
                LogAssert.ignoreFailingMessages = false;
            }

            Assert.That(errors, Has.Some.Match(@"Shop\.English\.lang\(2,(1|8)\): error: ('Broken Line' can't start a line|Expected '=')"));
            // Without Balance in the source, the Russian Balance is an orphan, reported when Russian is checked again.
            Assert.That(errors, Has.Some.Match(@"Shop\.Russian\.lang\(2,1\): error: 'Balance' isn't a key of the source language"));
        }
    }
}
