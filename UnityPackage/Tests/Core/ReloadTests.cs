using NUnit.Framework;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class ReloadTests
    {
        private const string CatalogText = "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Hello = new("Dialogue", "Hello");
        private static readonly LanguageKey Russian = new("Russian");

        private static MemoryTableSource CreateSource()
        {
            return MemoryTableSource.Imported("Localization", CatalogText, new[] { TableLoading.Preload, TableLoading.OnDemand },
                ("Shop", "English", "Purchase = Buy"),
                ("Dialogue", "English", "Hello = Hi"),
                ("Shop", "Russian", "Purchase = Kupit"),
                ("Dialogue", "Russian", "Hello = Privet"));
        }

        [Test]
        public void Reload_ShowsEditedTextInBindingsWithoutLanguageEvents()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();
            localizer.SetLanguageAsync(Russian);
            List<string> texts = new();
            List<string> events = new();
            localizer.LanguageChanging += language => events.Add("Changing " + language.Name);
            localizer.LanguageChanged += language => events.Add("Changed " + language.Name);
            using TextBinding binding = localizer.Bind(Purchase, texts, static (list, text) => list.Add(text));

            source.SetTable("Shop", "Russian", "Purchase = Kupit seychas");
            localizer.Reload();

            Assert.That(texts, Is.EqualTo(new[] { "Kupit", "Kupit seychas" }));
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Russian"));
            Assert.That(events, Is.Empty);
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void Reload_ReadsHeldOnDemandTablesAgain()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();
            List<string> texts = new();
            using TextBinding binding = localizer.Bind(Hello, texts, static (list, text) => list.Add(text));

            source.SetTable("Dialogue", "English", "Hello = Hello there");
            Localizer.ReloadAll();

            Assert.That(texts, Is.EqualTo(new[] { "Hi", "Hello there" }));
        }

        [Test]
        public void Reload_KeepsLanguagesRegisteredAtRuntime()
        {
            MemoryTableSource source = CreateSource();
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();
            localizer.RegisterLanguage(new LanguageInfo(new LanguageKey("Pirate"), "Pirate", string.Empty, new LanguageKey("English"), TextDirection.LeftToRight, false));
            localizer.SetLanguageAsync(new LanguageKey("Pirate"));

            localizer.Reload();

            Assert.That(localizer.Languages.Count, Is.EqualTo(3));
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("Pirate"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Buy"));
        }

        [Test]
        public void Reload_BeforeInitializationOrAfterDispose_DoesNothing()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            Localizer localizer = new(source.CatalogKey, host);

            localizer.Reload();
            Assert.That(localizer.IsInitialized, Is.False);
            localizer.Dispose();
            localizer.Reload();

            Assert.That(host.Reports, Is.Empty);
        }
    }
}
