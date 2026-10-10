using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Hosting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class OnDemandTableTests
    {
        private const string CatalogText =
            "@source English\n" +
            "[English]\nCulture = en\n" +
            "[Russian]\nCulture = ru\n";

        private static readonly TableKey Dialogue = new("Dialogue");
        private static readonly EntryKey Hello = new("Dialogue", "Hello");
        private static readonly EntryKey Farewell = new("Dialogue", "Farewell");
        private static readonly EntryKey Note = new("Journal", "Note");
        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly LanguageKey Russian = new("Russian");

        private static MemoryTableSource CreateSource()
        {
            return MemoryTableSource.Imported("Localization", CatalogText,
                new[] { TableLoading.Preload, TableLoading.OnDemand, TableLoading.OnDemand },
                ("Shop", "English", "Purchase = Buy"),
                ("Dialogue", "English", "Hello = Hi there\nFarewell = Bye"),
                ("Journal", "English", "Note = Day one"),
                ("Shop", "Russian", "Purchase = Kupit"),
                ("Dialogue", "Russian", "Hello = Privet\nFarewell = Poka"),
                ("Journal", "Russian", "Note = Den odin"));
        }

        private static Localizer CreateInitialized(MemoryTableSource source, out TestHost host)
        {
            host = new TestHost(source);
            Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();
            return localizer;
        }

        [Test]
        public void Initialization_LoadsOnlyPreloadedTables()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out _);

            Assert.That(source.Requests, Is.EqualTo(new[] { "Catalog", "Shop.English" }));
        }

        [Test]
        public void Bind_LoadsTheTableAndShowsItsText()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);
            List<string> texts = new();

            using TextBinding binding = localizer.Bind(Hello, texts, static (list, text) => list.Add(text));

            Assert.That(texts, Is.EqualTo(new[] { "Hi there" }));
            Assert.That(source.Requests, Does.Contain("Dialogue.English"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void Bind_WhileTheTableLoads_ShowsEmptyTextThenTheText()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out _);
            source.IsDeferred = true;
            List<string> first = new();
            List<string> second = new();

            using TextBinding hello = localizer.Bind(Hello, first, static (list, text) => list.Add(text));
            using TextBinding farewell = localizer.Bind(Farewell, second, static (list, text) => list.Add(text));
            source.DeliverAll();

            Assert.That(first, Is.EqualTo(new[] { string.Empty, "Hi there" }));
            Assert.That(second, Is.EqualTo(new[] { string.Empty, "Bye" }));
            Assert.That(source.Requests.FindAll(request => request == "Dialogue.English").Count, Is.EqualTo(1));
        }

        [Test]
        public void DisposingTheLastBinding_UnloadsTheTableTwoUpdatesLater()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);
            TextBinding binding = localizer.Bind(Hello, new List<string>(), static (list, text) => list.Add(text));

            binding.Dispose();
            host.RunUpdates();
            Assert.That(localizer.TryGet(Hello, out _), Is.True);
            host.RunUpdates();

            Assert.That(localizer.TryGet(Hello, out _), Is.False);
            Assert.That(host.ScheduledUpdateCount, Is.Zero);
        }

        [Test]
        public void BindingAgainBeforeTheUnload_KeepsTheTable()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);
            List<string> texts = new();
            localizer.Bind(Hello, texts, static (list, text) => list.Add(text)).Dispose();
            host.RunUpdates();

            using TextBinding again = localizer.Bind(Hello, texts, static (list, text) => list.Add(text));
            host.RunUpdates();
            host.RunUpdates();

            Assert.That(source.Requests.FindAll(request => request == "Dialogue.English").Count, Is.EqualTo(1));
            Assert.That(localizer.Get(Hello), Is.EqualTo("Hi there"));
        }

        [Test]
        public void HoldTable_LoadsTheTableForDirectReads()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);

            TableHandle handle = localizer.HoldTable(Dialogue);

            Assert.That(handle.IsActive, Is.True);
            Assert.That(handle.IsLoaded, Is.True);
            Assert.That(handle.WhenLoaded.IsCompleted && handle.WhenLoaded.Result, Is.True);
            Assert.That(localizer.Get(Hello), Is.EqualTo("Hi there"));
            handle.Dispose();
            handle.Dispose();
            Assert.That(handle.IsActive, Is.False);
            Assert.That(handle.IsLoaded, Is.False);
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void HoldTable_WithAsynchronousTables_CompletesWhenLoaded()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out _);
            source.IsDeferred = true;

            using TableHandle handle = localizer.HoldTable(Dialogue);
            Task<bool> loaded = handle.WhenLoaded;

            Assert.That(loaded.IsCompleted, Is.False);
            Assert.That(handle.IsLoaded, Is.False);
            source.DeliverAll();
            Assert.That(loaded.Wait(1000) && loaded.Result, Is.True);
            Assert.That(handle.IsLoaded, Is.True);
        }

        [Test]
        public void HoldTable_BeforeInitialization_IsLoadedByIt()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = new(source.CatalogKey, host);

            using TableHandle handle = localizer.HoldTable(Dialogue);
            Task initialization = localizer.InitializeAsync();

            Assert.That(initialization.IsCompleted, Is.True);
            Assert.That(handle.IsLoaded, Is.True);
            Assert.That(localizer.Get(Hello), Is.EqualTo("Hi there"));
        }

        [Test]
        public void HoldTable_OfAnUnknownTable_IsReportedOnceAndNeverLoads()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);

            using TableHandle first = localizer.HoldTable(new TableKey("Dialog"));
            using TableHandle second = localizer.HoldTable(new TableKey("Dialog"));

            Assert.That(first.WhenLoaded.IsCompleted && !first.WhenLoaded.Result, Is.True);
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
            Assert.That(host.Reports[0].Message, Does.Contain("no table 'Dialog'"));
        }

        [Test]
        public void HoldTable_OfAPreloadedTable_IsLoadedRightAway()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out _);
            int requests = source.RequestCount;

            using TableHandle handle = localizer.HoldTable(new TableKey("Shop"));

            Assert.That(handle.IsLoaded, Is.True);
            Assert.That(source.RequestCount, Is.EqualTo(requests));
        }

        [Test]
        public void Get_OfATableNothingHolds_ReturnsEmptyTextAndWarnsOncePerTable()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);

            Assert.That(localizer.Get(Hello), Is.Empty);
            Assert.That(localizer.Get(Farewell), Is.Empty);
            Assert.That(localizer.TryGet(Hello, out _), Is.False);
            Assert.That(localizer.MissingKeyCount, Is.Zero);
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
            Assert.That(host.Reports[0].Message, Does.Contain("loads on demand"));
        }

        [Test]
        public void LanguageSwitch_LoadsHeldTablesInTheNewLanguageBeforeApplying()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out _);
            List<string> texts = new();
            using TextBinding binding = localizer.Bind(Hello, texts, static (list, text) => list.Add(text));
            source.IsDeferred = true;

            Task switching = localizer.SetLanguageAsync(Russian);
            source.DeliverPending();
            Assert.That(switching.IsCompleted, Is.True);

            Assert.That(texts, Is.EqualTo(new[] { "Hi there", "Privet" }));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }

        [Test]
        public void HoldTable_DuringASwitch_JoinsTheSwitch()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out _);
            source.IsDeferred = true;

            Task switching = localizer.SetLanguageAsync(Russian);
            using TableHandle handle = localizer.HoldTable(Dialogue);
            source.DeliverPending();

            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(handle.IsLoaded, Is.True);
            Assert.That(localizer.Get(Hello), Is.EqualTo("Privet"));
            Assert.That(source.Requests, Does.Not.Contain("Dialogue.English"));
        }

        [Test]
        public void ReleasingATableDuringASwitch_StopsTheSwitchWaitingForIt()
        {
            MemoryTableSource source = CreateSource();
            // Asked first for dialogue, and never answering once deferred.
            MemoryTableSource dialogue = MemoryTableSource.Imported("Localization", CatalogText, null,
                ("Dialogue", "English", "Hello = Hi there"), ("Dialogue", "Russian", "Hello = Privet"));
            dialogue.RemoveCatalog();
            TestHost host = new(dialogue, source);
            using Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();
            TableHandle handle = localizer.HoldTable(Dialogue);
            source.IsDeferred = true;
            dialogue.IsDeferred = true;

            Task switching = localizer.SetLanguageAsync(Russian);
            handle.Dispose();
            host.RunUpdates();
            host.RunUpdates();
            source.DeliverAll();

            Assert.That(dialogue.PendingCount, Is.EqualTo(1));
            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
            Assert.That(localizer.TryGet(Hello, out _), Is.False);
        }

        [Test]
        public void SetMessage_ToAnotherTable_KeepsTheTextUntilThatTableArrives()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);
            List<string> texts = new();
            TextBinding binding = localizer.Bind(Hello, texts, static (list, text) => list.Add(text));
            source.IsDeferred = true;

            binding.SetMessage(new EntryMessage(Note));
            Assert.That(texts, Is.EqualTo(new[] { "Hi there" }));
            source.DeliverAll();
            host.RunUpdates();
            host.RunUpdates();

            Assert.That(texts, Is.EqualTo(new[] { "Hi there", "Day one" }));
            Assert.That(localizer.TryGet(Hello, out _), Is.False);
            binding.Dispose();
        }

        [Test]
        public void ReleasingFromAnotherThread_UnloadsOnTheHostThread()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out TestHost host);
            TableHandle handle = localizer.HoldTable(Dialogue);

            Thread releaser = new(() => handle.Dispose());
            releaser.Start();
            releaser.Join();
            host.RunUpdates();
            host.RunUpdates();

            Assert.That(localizer.TryGet(Hello, out _), Is.False);
        }

        [Test]
        public void Dispose_CompletesWaitingTasksAndDeactivatesHandles()
        {
            MemoryTableSource source = CreateSource();
            Localizer localizer = CreateInitialized(source, out _);
            source.IsDeferred = true;
            TableHandle handle = localizer.HoldTable(Dialogue);
            Task<bool> loaded = handle.WhenLoaded;

            localizer.Dispose();

            Assert.That(loaded.Wait(1000) && !loaded.Result, Is.True);
            Assert.That(handle.IsActive, Is.False);
            Assert.That(localizer.HoldTable(Dialogue).IsActive, Is.False);
        }

        [Test]
        public void BindAndDispose_OfALoadedTable_AllocateNothing()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = CreateInitialized(source, out _);
            using TableHandle handle = localizer.HoldTable(Dialogue);
            List<string> texts = new() { string.Empty };
            localizer.Bind(Hello, texts, static (list, text) => list[0] = text).Dispose();
            localizer.HoldTable(Dialogue).Dispose();

            Allocations.AssertNone(() =>
            {
                localizer.Bind(Hello, texts, static (list, text) => list[0] = text).Dispose();
                localizer.HoldTable(Dialogue).Dispose();
            });
        }
    }
}
