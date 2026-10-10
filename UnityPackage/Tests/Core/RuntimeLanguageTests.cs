using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Hosting;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class RuntimeLanguageTests
    {
        private const string CatalogText = "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Title = new("Shop", "Title");
        private static readonly EntryKey Coins = new("Shop", "Coins");
        private static readonly LanguageKey Klingon = new("Klingon");

        private static readonly LanguageInfo KlingonLanguage =
            new(Klingon, "tlhIngan Hol", string.Empty, new LanguageKey("English"), TextDirection.LeftToRight, false);

        private static MemoryTableSource CreateGame()
        {
            return MemoryTableSource.Imported("Localization", CatalogText, null,
                ("Shop", "English", "Purchase = Buy\nTitle = Shop\nCoins = {coins, plural, one {# coin} other {# coins}}"),
                ("Shop", "Russian", "Purchase = Kupit"));
        }

        private static MemoryTableSource CreateMod()
        {
            MemoryTableSource mod = new("Localization", CatalogText + "[Klingon]\n", ("Shop", "Klingon", "Purchase = je'"));
            mod.RemoveCatalog();
            return mod;
        }

        [Test]
        public void RegisterLanguage_AddsALanguageToSwitchTo()
        {
            MemoryTableSource game = CreateGame();
            TestHost host = new(CreateMod(), game);
            using Localizer localizer = new(game.CatalogKey, host);
            localizer.InitializeAsync();

            localizer.RegisterLanguage(KlingonLanguage);
            Task switching = localizer.SetLanguageAsync(Klingon);

            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.Languages.Count, Is.EqualTo(3));
            Assert.That(localizer.CurrentLanguage.DisplayName, Is.EqualTo("tlhIngan Hol"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("je'"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Shop"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void RegisterLanguage_BeforeInitialization_CanBeTheStartingLanguage()
        {
            MemoryTableSource game = CreateGame();
            using Localizer localizer = new(game.CatalogKey, new TestHost(CreateMod(), game));

            localizer.RegisterLanguage(KlingonLanguage);
            localizer.SetLanguageAsync(Klingon);
            localizer.InitializeAsync();

            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Klingon"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("je'"));
        }

        [Test]
        public void RegisterLanguage_FormatsWithItsFallbacksRulesWithoutACulture()
        {
            MemoryTableSource game = CreateGame();
            using Localizer localizer = new(game.CatalogKey, new TestHost(CreateMod(), game));
            localizer.InitializeAsync();

            localizer.RegisterLanguage(KlingonLanguage);
            localizer.SetLanguageAsync(Klingon);

            Assert.That(localizer.Get(new EntryMessage(Coins, new MessageArgument("coins", 1))), Is.EqualTo("1 coin"));
        }

        [Test]
        public void RegisterLanguage_ReportsATakenNameOrAnUnknownFallback()
        {
            MemoryTableSource game = CreateGame();
            TestHost host = new(game);
            using Localizer localizer = new(game.CatalogKey, host);
            localizer.InitializeAsync();

            localizer.RegisterLanguage(new LanguageInfo(new LanguageKey("Russian"), "Again", "ru", default, TextDirection.LeftToRight, false));
            localizer.RegisterLanguage(new LanguageInfo(Klingon, string.Empty, string.Empty, new LanguageKey("Vulcan"), TextDirection.LeftToRight, false));

            Assert.That(localizer.Languages.Count, Is.EqualTo(2));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(2));
            Assert.That(host.Reports[0].Message, Does.Contain("already a language"));
            Assert.That(host.Reports[1].Message, Does.Contain("'Vulcan'"));
        }

        [Test]
        public void RegisterLanguage_FromAnotherThread_AppliesInOrderWithTheSwitch()
        {
            MemoryTableSource game = CreateGame();
            TestHost host = new(CreateMod(), game);
            using Localizer localizer = new(game.CatalogKey, host);
            localizer.InitializeAsync();
            host.IsTestThreadTheHost = false;

            localizer.RegisterLanguage(KlingonLanguage);
            Task switching = localizer.SetLanguageAsync(Klingon);
            host.RunUpdates();

            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Klingon"));
        }

        [Test]
        public void ReadLanguages_ReadsSectionsWhoseFallbacksAreElsewhere()
        {
            List<DocumentIssue> issues = new();

            IReadOnlyList<LanguageInfo> languages = CatalogInfo.ReadLanguages(CatalogDocument.Parse(
                "[Klingon]\nDisplayName = tlhIngan Hol\nFallback = English\n[Elvish]\nFallback = not a name\n"), issues);

            Assert.That(languages.Count, Is.EqualTo(2));
            Assert.That(languages[0].Fallback.Name, Is.EqualTo("English"));
            Assert.That(languages[0].DisplayName, Is.EqualTo("tlhIngan Hol"));
            Assert.That(languages[1].HasFallback, Is.False);
            Assert.That(issues.Count, Is.EqualTo(1));
        }
    }
}
