using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Tables;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TableLoadingTests
    {
        private const string CatalogText =
            "@source English\n" +
            "[English]\nCulture = en\n" +
            "[Russian]\nCulture = ru\n" +
            "[Pirate]\nFallback = Parrot\n" +
            "[Parrot]\n";

        private const string EnglishShop = "@formerly BuyButton\nPurchase = Buy\nTitle = Shop";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Title = new("Shop", "Title");
        private static readonly LanguageKey English = new("English");
        private static readonly LanguageKey Russian = new("Russian");
        private static readonly LanguageKey Pirate = new("Pirate");

        private static MemoryTableSource CreateSource(string russianShop)
        {
            return MemoryTableSource.Imported("Localization", CatalogText, null,
                ("Shop", "English", EnglishShop),
                ("Shop", "Russian", russianShop),
                ("Shop", "Parrot", "Purchase = Squawk"),
                ("Shop", "Pirate", "Purchase = Plunder"));
        }

        [Test]
        public void CompleteTranslation_LoadsNoFallbackLanguage()
        {
            MemoryTableSource source = CreateSource("Purchase = Kupit\nTitle = Magazin");
            TestHost host = new(source);
            using Localizer localizer = new(source.CatalogKey, host);

            localizer.SetLanguageAsync(Russian);
            localizer.InitializeAsync();

            Assert.That(source.Requests, Is.EqualTo(new[] { "Catalog", "Shop.Russian" }));
            Assert.That(localizer.Get(Title), Is.EqualTo("Magazin"));
            Assert.That(localizer.Get(new EntryKey("Shop", "BuyButton")), Is.EqualTo("Kupit"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void TranslationWithGaps_LoadsItsFallbacksUntilOneIsComplete()
        {
            MemoryTableSource source = CreateSource("Purchase = Kupit");
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();
            source.Requests.Clear();

            localizer.SetLanguageAsync(Pirate);

            // Pirate and Parrot both have gaps, so the source language fills them; it is still loaded from English.
            Assert.That(source.Requests, Is.EqualTo(new[] { "Shop.Pirate", "Shop.Parrot" }));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Plunder"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Shop"));
        }

        [Test]
        public void SwitchingAway_ReleasesTablesTheNewLanguageDoesNotUse()
        {
            MemoryTableSource source = CreateSource("Purchase = Kupit\nTitle = Magazin");
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();

            localizer.SetLanguageAsync(Russian);
            localizer.SetLanguageAsync(English);

            Assert.That(source.Requests, Is.EqualTo(new[] { "Catalog", "Shop.English", "Shop.Russian", "Shop.English" }));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Buy"));
        }

        [Test]
        public void TranslationCompiledForOtherSourceKeys_StillLoadsItsFallback()
        {
            MemoryTableSource source = CreateSource("Purchase = Kupit\nTitle = Magazin");
            // A translation from an older release, complete for a source text that had no Title yet.
            CatalogInfo older = CatalogInfo.FromDocument(source.CatalogKey, CatalogDocument.Parse(CatalogText), null, null);
            source.SetRawTable("Shop", "Russian", TableCompiler.Compile(older, new TableKey("Shop"), Russian,
                TableDocument.Parse("Purchase = Kupit"), TableDocument.Parse("Purchase = Buy"), null));
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));

            localizer.SetLanguageAsync(Russian);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Shop"));
        }

        [Test]
        public void SourcesAskedFirst_PatchTheLanguageBehindThem()
        {
            MemoryTableSource game = CreateSource("Purchase = Kupit\nTitle = Magazin");
            MemoryTableSource mod = new("Localization", CatalogText, ("Shop", "Russian", "Purchase = Kupit seychas"));
            mod.RemoveCatalog();
            TestHost host = new(mod, game);
            using Localizer localizer = new(game.CatalogKey, host);

            localizer.SetLanguageAsync(Russian);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit seychas"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Magazin"));
            Assert.That(game.Requests, Is.EqualTo(new[] { "Catalog", "Shop.Russian" }));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void SourceDeliveringDamagedData_IsReportedAndTheNextSourceAsked()
        {
            MemoryTableSource game = CreateSource("Purchase = Kupit\nTitle = Magazin");
            MemoryTableSource damaged = new("Localization", CatalogText, ("Shop", "Russian", "Purchase = Kupit"));
            damaged.RemoveCatalog();
            damaged.SetRawTable("Shop", "Russian", new byte[] { 1, 2, 3 });
            TestHost host = new(damaged, game);
            using Localizer localizer = new(game.CatalogKey, host);

            localizer.SetLanguageAsync(Russian);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(Title), Is.EqualTo("Magazin"));
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void RapidSwitches_LoadEachTableOnce()
        {
            MemoryTableSource source = CreateSource("Purchase = Kupit\nTitle = Magazin");
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();
            source.IsDeferred = true;
            source.Requests.Clear();

            Task first = localizer.SetLanguageAsync(Russian);
            Task second = localizer.SetLanguageAsync(English);
            Task third = localizer.SetLanguageAsync(Russian);
            source.DeliverAll();

            Assert.That(source.Requests, Is.EqualTo(new[] { "Shop.Russian" }));
            Assert.That(first.IsCompleted && second.IsCompleted && third.IsCompleted, Is.True);
            Assert.That(localizer.Get(Title), Is.EqualTo("Magazin"));
        }

        [Test]
        public void SupersededSwitch_StopsLoadingItsFallbacks()
        {
            MemoryTableSource source = CreateSource("Purchase = Kupit\nTitle = Magazin");
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();
            source.IsDeferred = true;
            source.Requests.Clear();

            localizer.SetLanguageAsync(Pirate);
            Task toRussian = localizer.SetLanguageAsync(Russian);
            source.DeliverAll();

            Assert.That(source.Requests, Is.EqualTo(new[] { "Shop.Pirate", "Shop.Russian" }));
            Assert.That(toRussian.IsCompleted, Is.True);
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Russian"));
        }

        [Test]
        public void MissingSourceLanguageTable_IsReportedOnce()
        {
            MemoryTableSource source = MemoryTableSource.Imported("Localization", CatalogText, null,
                ("Shop", "English", EnglishShop),
                ("Common", "Russian", "Greeting = Privet"));
            TestHost host = new(source);
            using Localizer localizer = new(source.CatalogKey, host);

            localizer.SetLanguageAsync(Russian);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(new EntryKey("Common", "Greeting")), Is.EqualTo("Privet"));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
            Assert.That(host.Reports[0].Message, Does.Contain("the table 'Common' in English"));
        }
    }
}
