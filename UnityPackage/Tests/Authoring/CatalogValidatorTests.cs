using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class CatalogValidatorTests
    {
        private const string CatalogText =
            "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\nRequired = true\n[Pirate]\nFallback = English\n";

        private static readonly string BuyFingerprint = Hashing.ComputeFingerprint("Buy").ToString("x6");

        private static ValidationReport Validate(IReadOnlyList<EntryUse> uses, params (string Table, string Language, string Text)[] files)
        {
            return Validate(uses, null, files);
        }

        private static ValidationReport Validate(IReadOnlyList<EntryUse> uses, ValidationOptions options, params (string Table, string Language, string Text)[] files)
        {
            Dictionary<string, List<ValidatedFile>> byTable = new();
            List<(string TableName, TableDocument Source)> sources = new();
            foreach ((string Table, string Language, string Text) file in files)
            {
                if (!byTable.TryGetValue(file.Table, out List<ValidatedFile> tableFiles))
                {
                    tableFiles = new List<ValidatedFile>();
                    byTable.Add(file.Table, tableFiles);
                }
                TableDocument document = TableDocument.Parse(file.Text);
                tableFiles.Add(new ValidatedFile($"{file.Table}.{file.Language}.lang", file.Language, document));
                if (file.Language == "English")
                {
                    sources.Add((file.Table, document));
                }
            }
            List<ValidatedTable> tables = new();
            foreach (KeyValuePair<string, List<ValidatedFile>> table in byTable)
            {
                tables.Add(new ValidatedTable(table.Key, table.Value));
            }
            CatalogInfo withoutTables = CatalogInfo.FromDocument(new CatalogKey("Localization"), CatalogDocument.Parse(CatalogText), null, null);
            CatalogInfo catalog = new(withoutTables.Key, withoutTables.SourceLanguage.Key, withoutTables.Languages, null, MovedEntries.Collect(sources));
            return CatalogValidator.Validate(catalog, tables, uses, options);
        }

        private static List<string> Describe(ValidationReport report)
        {
            List<string> lines = new();
            foreach (ValidationIssue issue in report.Issues)
            {
                lines.Add(issue.ToString());
            }
            return lines;
        }

        [Test]
        public void ACompleteCatalog_Passes()
        {
            ValidationReport report = Validate(new List<EntryUse> { new("Shop", "Purchase", "Main.unity") },
                ("Shop", "English", "Purchase = Buy\n"),
                ("Shop", "Russian", $"Purchase [{BuyFingerprint}] = Kupit\n"),
                ("Shop", "Pirate", $"Purchase [{BuyFingerprint}] = Plunder\n"));

            Assert.That(report.IsPassing, Is.True);
            Assert.That(report.Issues, Is.Empty, string.Join("\n", Describe(report)));
        }

        [Test]
        public void MissingEntries_FailARequiredLanguageAndWarnForOthers()
        {
            ValidationReport report = Validate(null,
                ("Shop", "English", "Purchase = Buy\nTitle = Shop\n"),
                ("Shop", "Russian", $"Purchase [{BuyFingerprint}] = Kupit\n"));
            List<string> lines = Describe(report);

            Assert.That(report.ErrorCount, Is.EqualTo(1));
            Assert.That(lines[0], Is.EqualTo("Shop.Russian.lang: error: Russian is required to have every entry, but 'Shop' lacks 1 entry in it: Title."));
            Assert.That(lines[1], Is.EqualTo("Shop.English.lang: warning: Pirate lacks 2 entries of 'Shop', shown in its fallback language: Purchase, Title."));
        }

        [Test]
        public void FileProblems_AreReportedAtTheirLineAsTheImportReportsThem()
        {
            ValidationReport report = Validate(null,
                ("Shop", "English", "Purchase = Buy\nCoins = {coins, plural, one {# coin}}\n<<<<<<< HEAD\n"),
                ("Shop", "Russian", $"Purchase [{BuyFingerprint}] = Kupit\nPurhcase = Kupit\n"));
            List<string> lines = Describe(report);

            Assert.That(lines, Has.Some.StartsWith("Shop.English.lang(3,1): error: This is a merge conflict marker"));
            Assert.That(lines, Has.Some.Contains("Shop.English.lang(2,").And.Contains("other"));
            Assert.That(lines, Has.Some.StartsWith("Shop.Russian.lang(2,1): error: 'Purhcase' isn't a key"));
            Assert.That(report.IsPassing, Is.False);
        }

        [Test]
        public void OutdatedUnverifiedAndLongTranslations_AreWarnings()
        {
            ValidationReport report = Validate(null,
                ("Shop", "English", "@maximumLength 6\nPurchase = Buy now\nTitle = Shop\n"),
                ("Shop", "Russian", $"Purchase [{BuyFingerprint}] = Kupit\nTitle = Magazin\n"),
                ("Shop", "Pirate", $"Purchase [{Hashing.ComputeFingerprint("Buy now"):x6}] = Plunder it\nTitle [{Hashing.ComputeFingerprint("Shop"):x6}] = Store\n"));
            List<string> lines = Describe(report);

            Assert.That(report.IsPassing, Is.True, string.Join("\n", lines));
            Assert.That(lines, Has.Some.EqualTo("Shop.English.lang(2,12): warning: 'Purchase' is 7 characters long in English, over its maximum of 6."));
            Assert.That(lines, Has.Some.EqualTo("Shop.Russian.lang(1,1): warning: 1 translation is outdated, as their source text changed since: Purchase."));
            Assert.That(lines, Has.Some.EqualTo("Shop.Russian.lang(2,1): warning: 1 translation has no fingerprint, so nothing tells whether they follow the current source text: Title."));
            Assert.That(lines, Has.Some.EqualTo("Shop.Pirate.lang(1,21): warning: 'Purchase' is 10 characters long in Pirate, over its maximum of 6."));
        }

        [Test]
        public void References_ToMissingKeysFailAndFormerNamesWarn()
        {
            List<EntryUse> uses = new()
            {
                new EntryUse("Shop", "Purchse", "Main.unity: _buy"),
                new EntryUse("Shop", "BuyButton", "Main.unity: _old"),
                new EntryUse("Hud", "Greeting", "Hud.prefab: _greeting"),
                new EntryUse("Shp", "Title", "Menu.prefab: _title")
            };

            ValidationReport report = Validate(uses,
                ("Shop", "English", "@formerly BuyButton\n@formerly Checkout\nPurchase = Buy\nTitle = Shop\n"),
                ("Dialogue", "English", "@formerly Hud.Greeting\nWelcome = Hello\n"),
                ("Shop", "Russian", $"Purchase [{BuyFingerprint}] = Kupit\nTitle [{Hashing.ComputeFingerprint("Shop"):x6}] = Magazin\n"),
                ("Dialogue", "Russian", $"Welcome [{Hashing.ComputeFingerprint("Hello"):x6}] = Privet\n"));
            List<string> lines = Describe(report);

            Assert.That(lines, Has.Some.EqualTo("Main.unity: _buy: error: It refers to 'Shop.Purchse', which doesn't exist. Did you mean 'Shop.Purchase'?"));
            Assert.That(lines, Has.Some.EqualTo("Menu.prefab: _title: error: It refers to 'Shp.Title', which doesn't exist. There is no table 'Shp'; did you mean 'Shop'?"));
            Assert.That(lines, Has.Some.StartsWith("Main.unity: _old: warning: It refers to 'Shop.BuyButton' by the entry's former name; it was renamed to 'Shop.Purchase'."));
            Assert.That(lines, Has.Some.StartsWith("Hud.prefab: _greeting: warning: It refers to 'Hud.Greeting' by the entry's former key; it moved to 'Dialogue.Welcome'."));
            Assert.That(lines, Has.Some.StartsWith("Shop.English.lang(2,1): warning: No scene, prefab or asset refers to 'Checkout'"));
            Assert.That(lines, Has.None.Contains("refers to 'BuyButton', the former name"));
            Assert.That(lines, Has.None.Contains("refers to 'Hud.Greeting', the former name"));
            Assert.That(report.ErrorCount, Is.EqualTo(2));
        }

        [Test]
        public void ATableWithoutItsSourceFile_IsAnError()
        {
            ValidationReport report = Validate(null, ("Shop", "Russian", "Purchase = Kupit\n"));

            Assert.That(Describe(report)[0], Is.EqualTo("Shop.Russian.lang: error: 'Shop' has no English file, the language that defines its keys, so none of its translations can be shown."));
        }

        [Test]
        public void PascalCase_IsCheckedOnlyWhenAsked()
        {
            (string, string, string) source = ("main_menu", "English", "playButton = Play\n");

            ValidationReport plain = Validate(null, source);
            ValidationReport checkedNames = Validate(null, new ValidationOptions { IsCheckingPascalCase = true }, source);

            Assert.That(Describe(plain), Has.None.Contains("PascalCase"));
            Assert.That(Describe(checkedNames), Has.Some.Contains("The table name 'main_menu' doesn't follow the PascalCase convention"));
            Assert.That(Describe(checkedNames), Has.Some.StartsWith("main_menu.English.lang(1,1): warning: 'playButton' doesn't follow"));
        }
    }
}
