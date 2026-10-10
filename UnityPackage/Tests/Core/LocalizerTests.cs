using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class LocalizerTests
    {
        private const string CatalogText =
            "@source English\n" +
            "[English]\n" +
            "[Russian]\nCulture = ru\n" +
            "[Pirate]\nFallback = Parrot\n" +
            "[Parrot]\n";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Title = new("Shop", "Title");
        private static readonly EntryKey Greeting = new("Common", "Greeting");
        private static readonly LanguageKey Russian = new("Russian");
        private static readonly LanguageKey Pirate = new("Pirate");

        private static MemoryTableSource CreateSource()
        {
            return new MemoryTableSource("Localization", CatalogText,
                ("Shop", "English", "@formerly BuyButton\nPurchase = Buy\nTitle = Shop"),
                ("Shop", "Russian", "Purchase = Kupit"),
                ("Shop", "Parrot", "Purchase = Squawk\nTitle = Perch"),
                ("Shop", "Pirate", "Purchase = Plunder"),
                ("Common", "English", "Greeting = Hello"));
        }

        private static Localizer CreateLocalizer(MemoryTableSource source, TestHost host)
        {
            return new Localizer(source.CatalogKey, host);
        }

        [Test]
        public void InitializeAsync_WithSynchronousTables_IsCompleteWhenReturned()
        {
            TestHost host = new(CreateSource());
            using Localizer localizer = new(new CatalogKey("Localization"), host);

            Task initialization = localizer.InitializeAsync();

            Assert.That(initialization.IsCompleted, Is.True);
            Assert.That(localizer.IsInitialized, Is.True);
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("English"));
            Assert.That(localizer.Languages.Count, Is.EqualTo(4));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Buy"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void InitializeAsync_CalledAgain_ReturnsTheSameTask()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));

            Assert.That(localizer.InitializeAsync(), Is.SameAs(localizer.InitializeAsync()));
        }

        [Test]
        public void Get_FallsBackThroughTheChainToTheSourceLanguage()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));
            localizer.InitializeAsync();

            localizer.SetLanguageAsync(Russian);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Shop"));

            localizer.SetLanguageAsync(Pirate);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Plunder"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Perch"));
            Assert.That(localizer.Get(Greeting), Is.EqualTo("Hello"));
        }

        [Test]
        public void Get_ReturnsTheSameStringInstanceEveryTime()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));
            localizer.InitializeAsync();

            Assert.That(localizer.Get(Purchase), Is.SameAs(localizer.Get(Purchase)));
        }

        [Test]
        public void Get_ResolvesAliasesAndNamesFromData()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));
            localizer.InitializeAsync();

            Assert.That(localizer.Get(new EntryKey("Shop", "BuyButton")), Is.EqualTo("Buy"));
            Assert.That(localizer.Get("shop".AsSpan(), "PURCHASE".AsSpan()), Is.EqualTo("Buy"));
            Assert.That(localizer.GetMemory(Title).ToString(), Is.EqualTo("Shop"));
        }

        [Test]
        public void Get_ShowsAMarkerForMissingKeysAndReportsEachOnce()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(new EntryKey("Shop", "Refund")), Is.EqualTo("[Shop.Refund]"));
            Assert.That(localizer.Get(new EntryKey("Shop", "Refund")), Is.EqualTo("[Shop.Refund]"));
            Assert.That(localizer.Get("Inventory".AsSpan(), "Sword".AsSpan()), Is.EqualTo("[Inventory.Sword]"));

            Assert.That(localizer.MissingKeyCount, Is.EqualTo(2));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(2));
            Assert.That(host.Reports[0].Message, Does.Contain("has no entry 'Refund'"));
            Assert.That(host.Reports[1].Message, Does.Contain("has no table 'Inventory'"));
        }

        [Test]
        public void TryGet_DoesNotReportMissingKeys()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();

            Assert.That(localizer.TryGet(new EntryKey("Shop", "Refund"), out string missing), Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(localizer.TryGet(Purchase, out string found), Is.True);
            Assert.That(found, Is.EqualTo("Buy"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void Get_BeforeInitialization_ReturnsEmptyTextAndWarnsOnce()
        {
            TestHost host = new(CreateSource());
            using Localizer localizer = new(new CatalogKey("Localization"), host);

            Assert.That(localizer.Get(Purchase), Is.Empty);
            Assert.That(localizer.Get(Title), Is.Empty);
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
            Assert.That(localizer.MissingKeyCount, Is.Zero);
        }

        [Test]
        public void Get_WithAnEmptyKey_ReturnsEmptyTextAndWarnsOnce()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(default(EntryKey)), Is.Empty);
            Assert.That(localizer.Get(default(EntryKey)), Is.Empty);
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void SetLanguageAsync_BeforeInitialization_ChoosesTheStartingLanguage()
        {
            MemoryTableSource source = CreateSource();
            source.IsDeferred = true;
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));

            Task switching = localizer.SetLanguageAsync(Russian);
            Task initialization = localizer.InitializeAsync();
            source.DeliverAll();

            Assert.That(initialization.IsCompleted, Is.True);
            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Russian"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }

        [Test]
        public void SetLanguageAsync_ToAnUnknownLanguage_ChangesNothingAndWarns()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();

            Task<bool> switching = (Task<bool>)localizer.SetLanguageAsync(new LanguageKey("Klingon"));

            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(switching.Result, Is.False);
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("English"));
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void SetLanguageAsync_WithAsynchronousTables_SwitchesOnlyOnceEverythingArrived()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));
            localizer.InitializeAsync();
            source.IsDeferred = true;

            Task switching = localizer.SetLanguageAsync(Russian);

            Assert.That(switching.IsCompleted, Is.False);
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("English"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Buy"));
            source.DeliverPending();
            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }

        [Test]
        public void SetLanguageAsync_InQuickSuccession_AppliesOnlyTheLatest()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));
            localizer.InitializeAsync();
            source.IsDeferred = true;
            List<string> changes = new();
            localizer.LanguageChanged += language => changes.Add(language.Name);

            Task toRussian = localizer.SetLanguageAsync(Russian);
            Task toPirate = localizer.SetLanguageAsync(Pirate);
            source.DeliverAll();

            Assert.That(toRussian.IsCompleted, Is.True);
            Assert.That(toPirate.IsCompleted, Is.True);
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Pirate"));
            Assert.That(changes, Is.EqualTo(new[] { "Pirate" }));
        }

        [Test]
        public void SetLanguageAsync_ReusesTablesAlreadyLoaded()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));
            localizer.InitializeAsync();
            int afterInitialization = source.RequestCount;

            localizer.SetLanguageAsync(Russian);

            // Only Shop in Russian is new: Russian has no Common table, and English is already loaded.
            Assert.That(source.RequestCount - afterInitialization, Is.EqualTo(1));
        }

        [Test]
        public void LanguageEvents_AreRaisedAroundASwitch_EvenWhenAHandlerThrows()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();
            List<string> events = new();
            localizer.LanguageChanging += language => throw new InvalidOperationException("Handler failure.");
            localizer.LanguageChanging += language => events.Add($"Changing {language.Name} from {localizer.Get(Purchase)}");
            localizer.LanguageChanged += language => events.Add($"Changed {language.Name} to {localizer.Get(Purchase)}");

            localizer.SetLanguageAsync(Russian);

            Assert.That(events, Is.EqualTo(new[] { "Changing Russian from Buy", "Changed Russian to Kupit" }));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
        }

        [Test]
        public void InitializeAsync_WithoutTheCatalog_ReportsAndCanBeRetried()
        {
            MemoryTableSource source = CreateSource();
            source.RemoveCatalog();
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);

            Task failed = localizer.InitializeAsync();

            Assert.That(failed.IsCompleted, Is.True);
            Assert.That(localizer.IsInitialized, Is.False);
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
            host.Sources.Add(CreateSource());
            Assert.That(localizer.InitializeAsync().IsCompleted, Is.True);
            Assert.That(localizer.IsInitialized, Is.True);
        }

        [Test]
        public void DamagedTranslation_IsReportedAndFallsBack()
        {
            MemoryTableSource source = CreateSource();
            source.SetRawTable("Shop", "Russian", new byte[] { 1, 2, 3 });
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();

            localizer.SetLanguageAsync(Russian);

            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Russian"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Buy"));
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void RequestsFromOtherThreads_AreQueuedForTheHostThread()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();
            host.IsTestThreadTheHost = false;

            Task switching = localizer.SetLanguageAsync(Russian);

            Assert.That(switching.IsCompleted, Is.False);
            Assert.That(host.ScheduledUpdateCount, Is.EqualTo(1));
            host.RunUpdates();
            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }

        [Test]
        public void Dispose_ReleasesEverythingAndLeavesReadsHarmless()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            Localizer localizer = CreateLocalizer(source, host);
            localizer.InitializeAsync();
            source.IsDeferred = true;
            Task switching = localizer.SetLanguageAsync(Russian);

            localizer.Dispose();
            localizer.Dispose();

            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.IsInitialized, Is.False);
            Assert.That(localizer.Get(Purchase), Is.Empty);
            Assert.That(localizer.Bind(Purchase, new List<string>(), static (list, text) => list.Add(text)).IsActive, Is.False);
            Assert.That(localizer.InitializeAsync().IsCompleted, Is.True);
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void Reads_FromOtherThreads_AlwaysSeeOneWholeLanguage()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateLocalizer(source, new TestHost(source));
            localizer.InitializeAsync();
            int invalidReads = 0;
            bool isRunning = true;
            Thread reader = new(() =>
            {
                while (Volatile.Read(ref isRunning))
                {
                    string text = localizer.Get(Purchase);
                    if (text != "Buy" && text != "Kupit")
                    {
                        Interlocked.Increment(ref invalidReads);
                    }
                }
            });
            reader.Start();

            for (int i = 0; i < 2000; i++)
            {
                localizer.SetLanguageAsync(i % 2 == 0 ? Russian : new LanguageKey("English"));
            }
            Volatile.Write(ref isRunning, false);
            reader.Join();

            Assert.That(invalidReads, Is.Zero);
        }
    }
}
