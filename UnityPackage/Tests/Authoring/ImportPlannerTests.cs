using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class ImportPlannerTests
    {
        private const string RussianBalance = "U vas {coins, plural, one {# moneta} few {# monety} many {# monet} other {# monety}}.";

        private Dictionary<string, TableFileSet> _tables;

        [SetUp]
        public void OpenTables()
        {
            _tables = new Dictionary<string, TableFileSet>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["Shop"] = ExchangeFixture.Open("Shop", ("English", ExchangeFixture.ShopEnglish), ("Russian", ExchangeFixture.ShopRussian)),
                ["Common"] = ExchangeFixture.Open("Common", ("English", ExchangeFixture.CommonEnglish), ("Russian", ExchangeFixture.CommonRussian))
            };
        }

        [Test]
        public void Plan_AddsAndUpdatesTranslations_StampedWithTheFilesSourceText()
        {
            ImportPlan plan = Plan(Grid(
                ("Shop.Balance", "You have {coins, plural, one {# coin} other {# coins}}.", RussianBalance),
                ("Shop.Purchase", "Buy", "Kupit"),
                ("Shop.Title", "Shop", "Magazin!"),
                ("Common.Confirm", "OK", "Da")));

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Balance (Russian): Added", "Shop.Title (Russian): Updated" }));
            Assert.That(plan.UnchangedCount, Is.EqualTo(2));
            Assert.That(plan.Problems, Is.Empty);
            Assert.That(_tables["Shop"].GetState("Russian", "Balance"), Is.EqualTo(TranslationState.Current));
            Assert.That(_tables["Shop"].GetState("Russian", "Title"), Is.EqualTo(TranslationState.Current));
            Assert.That(_tables["Common"].GetState("Russian", "Confirm"), Is.EqualTo(TranslationState.Unverified), "An unchanged text stays as it was.");
        }

        [Test]
        public void Plan_NeverAddsEntries()
        {
            ImportPlan plan = Plan(Grid(("Shop.Purchse", "Buy", "Kupit!")));

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Purchse: Rejected" }));
            Assert.That(plan.Changes[0].Message, Does.Contain("Did you mean 'Purchase'?"));
            Assert.That(_tables["Shop"].Get("Russian").IndexOf("Purchse"), Is.EqualTo(-1));
        }

        [Test]
        public void Plan_NeverErasesWithAnEmptyCell()
        {
            ImportPlan plan = Plan(Grid(("Shop.Purchase", "Buy", null), ("Shop.Title", "Shop", string.Empty)));

            Assert.That(plan.Changes, Is.Empty);
            Assert.That(_tables["Shop"].Get("Russian").Entries.Count, Is.EqualTo(2));
        }

        [Test]
        public void Plan_TranslationOfAnOlderSourceText_ArrivesOutdated()
        {
            ImportPlan plan = Plan(Grid(("Shop.Title", "Old shop", "Lavka")));

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Title (Russian): Outdated" }));
            Assert.That(_tables["Shop"].GetState("Russian", "Title"), Is.EqualTo(TranslationState.Outdated));
            Assert.That(plan.Problems.Count, Is.EqualTo(1), "The file's differing source text is pointed out.");
        }

        [Test]
        public void Plan_TakesSourceChangesOnlyWhenAsked()
        {
            ImportedFile file = Grid(("Shop.Title", "Store", "Magazin"));

            ImportPlan ignored = Plan(file);
            Assert.That(ignored.Changes, Is.Empty);
            Assert.That(_tables["Shop"].Source.Entries.Find(entry => entry.Key == "Title").Value, Is.EqualTo("Shop"));

            ImportPlan taken = ImportPlanner.Plan(ExchangeFixture.Catalog, new[] { file }, Open, new ImportOptions { IsImportingSourceChanges = true });
            Assert.That(Describe(taken), Is.EqualTo(new[] { "Shop.Title (English): SourceChanged" }));
            Assert.That(_tables["Shop"].Source.Entries.Find(entry => entry.Key == "Title").Value, Is.EqualTo("Store"));
        }

        [Test]
        public void Plan_RejectsATranslationWhoseArgumentsDontMatch()
        {
            ImportPlan plan = Plan(Grid(("Shop.Balance", null, "U vas {money}")));

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Balance (Russian): Rejected" }));
            Assert.That(plan.Changes[0].Message, Does.Contain("{money}"));
            Assert.That(_tables["Shop"].Get("Russian").IndexOf("Balance"), Is.EqualTo(-1), "The rejected text is taken back.");
        }

        [Test]
        public void Plan_RejectsABrokenSourceChange()
        {
            ImportPlan plan = ImportPlanner.Plan(ExchangeFixture.Catalog, new[] { Grid(("Shop.Balance", "You have {coins, plural, one {# coin}", null)) }, Open,
                new ImportOptions { IsImportingSourceChanges = true });

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Balance (English): Rejected" }));
            Assert.That(_tables["Shop"].Source.Entries.Find(entry => entry.Key == "Balance").Value, Does.StartWith("You have {coins, plural, one {# coin} other"));
        }

        [Test]
        public void Plan_UsesTheFingerprintTagARowCarries()
        {
            ImportedFile file = new("Reply");
            file.Languages.Add(new ImportedLanguage("Russian", false, "Russian"));
            file.Rows.Add(new ImportedRow("line 1", "Shop", "Title", new[] { "Magazin!" }) { HasFingerprint = true, Fingerprint = Hashing.ComputeFingerprint("Shop") });
            file.Rows.Add(new ImportedRow("line 2", "Shop", "Purchase", new[] { "Kupit!" }) { HasFingerprint = true, Fingerprint = Hashing.ComputeFingerprint("Purchase") });
            ImportPlanner.ResolveLanguages(ExchangeFixture.Catalog, file);

            ImportPlan plan = Plan(file);

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Title (Russian): Updated", "Shop.Purchase (Russian): Outdated" }));
        }

        [Test]
        public void Plan_ConfirmsAnUnchangedTranslationTheFileMarksAsChecked()
        {
            ImportedFile file = Grid(("Shop.Title", "Shop", "Magazin"));
            file.Rows[0].IsConfirmed = true;

            ImportPlan plan = Plan(file);

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Title (Russian): Confirmed" }));
            Assert.That(_tables["Shop"].GetState("Russian", "Title"), Is.EqualTo(TranslationState.Current));
        }

        [Test]
        public void Plan_WithoutSourceText_LeavesTranslationsUnverified()
        {
            ImportedFile file = new("Shop.csv");
            file.Languages.Add(new ImportedLanguage("Russian", false, "Russian"));
            file.Rows.Add(new ImportedRow("row 2", "Shop", "Title", new[] { "Magazin!" }));
            ImportPlanner.ResolveLanguages(ExchangeFixture.Catalog, file);

            ImportPlan plan = Plan(file);

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Title (Russian): Updated" }));
            Assert.That(plan.Changes[0].Message, Does.Contain("unverified"));
            Assert.That(_tables["Shop"].GetState("Russian", "Title"), Is.EqualTo(TranslationState.Unverified));
        }

        [Test]
        public void Plan_ReportsUnknownLanguagesAndTables()
        {
            ImportedFile file = new("Shop.csv");
            file.Languages.Add(new ImportedLanguage("Klingon", false, "Klingon"));
            file.Languages.Add(new ImportedLanguage("Russian", false, "Russian"));
            file.Rows.Add(new ImportedRow("row 2", "Shopp", "Title", new[] { "Qagh", "Magazin!" }));
            file.Rows.Add(new ImportedRow("row 3", "Shopp", "Purchase", new[] { "Qagh", "Kupit!" }));
            ImportPlanner.ResolveLanguages(ExchangeFixture.Catalog, file);

            ImportPlan plan = Plan(file);

            Assert.That(plan.Changes, Is.Empty);
            Assert.That(plan.Problems.Count, Is.EqualTo(2));
            Assert.That(plan.Problems[0], Does.Contain("'Klingon' isn't a language of the catalog"));
            Assert.That(plan.Problems[1], Does.Contain("no table 'Shopp', so 2 of the rows"));
        }

        [Test]
        public void Plan_KeepsTheFirstOfTwoTextsForOneEntry()
        {
            ImportPlan plan = Plan(Grid(("Shop.Title", "Shop", "Magazin 1"), ("Shop.Title", "Shop", "Magazin 2")));

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Title (Russian): Updated", "Shop.Title (Russian): Rejected" }));
            Assert.That(_tables["Shop"].Get("Russian").Entries.Find(entry => entry.Key == "Title").Value, Is.EqualTo("Magazin 1"));
        }

        [Test]
        public void Plan_LeavesAFileWithErrorsAlone()
        {
            _tables["Shop"] = ExchangeFixture.Open("Shop", ("English", ExchangeFixture.ShopEnglish), ("Russian", "this line is no entry\n"));

            ImportPlan plan = Plan(Grid(("Shop.Title", "Shop", "Magazin")));

            Assert.That(Describe(plan), Is.EqualTo(new[] { "Shop.Title (Russian): Rejected" }));
            Assert.That(plan.Changes[0].Message, Does.Contain("has errors"));
        }

        [Test]
        public void ResolveLanguages_MatchesNamesThenCultures()
        {
            ImportedFile file = new("Localization.xlf");
            file.Languages.Add(new ImportedLanguage("pirate", false, "pirate"));
            file.Languages.Add(new ImportedLanguage("ru-RU", false, "Russian_final", "ru_RU"));
            file.Languages.Add(new ImportedLanguage("en", true, "en"));
            file.Languages.Add(new ImportedLanguage("de", false, "de"));

            ImportPlanner.ResolveLanguages(ExchangeFixture.Catalog, file);

            Assert.That(file.Languages[0].LanguageName, Is.EqualTo("Pirate"));
            Assert.That(file.Languages[1].LanguageName, Is.EqualTo("Russian"), "A region tag finds the language of its base culture.");
            Assert.That(file.Languages[2].LanguageName, Is.EqualTo("English"), "Pirate has no culture of its own, so en is English.");
            Assert.That(file.Languages[3].LanguageName, Is.Null);
        }

        private ImportPlan Plan(ImportedFile file) => ImportPlanner.Plan(ExchangeFixture.Catalog, new[] { file }, Open, null);

        private TableFileSet Open(string tableName) => _tables.TryGetValue(tableName, out TableFileSet set) ? set : null;

        private static ImportedFile Grid(params (string Key, string English, string Russian)[] rows)
        {
            ImportedFile file = new("Shop.csv");
            file.Languages.Add(new ImportedLanguage("English", false, "English"));
            file.Languages.Add(new ImportedLanguage("Russian", false, "Russian"));
            for (int i = 0; i < rows.Length; i++)
            {
                string[] parts = rows[i].Key.Split('.');
                file.Rows.Add(new ImportedRow($"Shop.csv, row {i + 2}", parts[0], parts[1], new[] { rows[i].English, rows[i].Russian }));
            }
            ImportPlanner.ResolveLanguages(ExchangeFixture.Catalog, file);
            return file;
        }

        private static List<string> Describe(ImportPlan plan)
        {
            List<string> changes = new();
            foreach (ImportChange change in plan.Changes)
            {
                changes.Add(change.ToString());
            }
            return changes;
        }
    }
}
