using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class MovedEntryTests
    {
        private const string CatalogText = "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n";

        private static readonly EntryKey FormerGreeting = new("Hud", "Greeting");
        private static readonly EntryKey FormerFarewell = new("Hud", "Bye");
        private static readonly LanguageKey Russian = new("Russian");

        private static MemoryTableSource CreateSource(TableLoading dialogueLoading = TableLoading.Preload)
        {
            return MemoryTableSource.Imported("Localization", CatalogText, new[] { TableLoading.Preload, dialogueLoading },
                ("Hud", "English", "Health = Health"),
                ("Dialogue", "English", "@formerly Hud.Greeting\nWelcome = Hello\n\n@formerly Hud.Bye\n@formerly Dialogue.Goodbye\nFarewell = Bye"),
                ("Dialogue", "Russian", "Welcome = Privet\nFarewell = Poka"));
        }

        [Test]
        public void FormerKeys_FindTheEntriesInTheTableTheyMovedTo()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(FormerGreeting), Is.EqualTo("Hello"));
            localizer.SetLanguageAsync(Russian);
            Assert.That(localizer.Get(FormerGreeting), Is.EqualTo("Privet"));
            Assert.That(localizer.Get(FormerFarewell), Is.EqualTo("Poka"));
            Assert.That(localizer.Get(new EntryKey("Dialogue", "Goodbye")), Is.EqualTo("Poka"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void AFormerKey_IsReportedMissingOnlyWhenNothingMovedFromIt()
        {
            MemoryTableSource source = CreateSource();
            TestHost host = new(source);
            using Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();

            Assert.That(localizer.Get(new EntryKey("Hud", "Mana")), Is.EqualTo("[Hud.Mana]"));
            Assert.That(localizer.MissingKeyCount, Is.EqualTo(1));
        }

        [Test]
        public void ABindingOfAFormerKey_HoldsTheTableItMovedTo()
        {
            MemoryTableSource source = CreateSource(TableLoading.OnDemand);
            using Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();
            List<string> texts = new();

            using TextBinding binding = localizer.Bind(FormerGreeting, texts, static (list, text) => list.Add(text));
            localizer.SetLanguageAsync(Russian);

            Assert.That(texts, Is.EqualTo(new[] { "Hello", "Privet" }));
        }

        [Test]
        public void Collect_ReadsQualifiedAliasesOfOtherTablesOnly()
        {
            Dictionary<(ulong Table, ulong Entry), EntryKey> moved = MovedEntries.Collect(new[]
            {
                ("Dialogue", TableDocument.Parse("@formerly Hud.Greeting\n@formerly Dialogue.Hi\n@formerly Old\nWelcome = Hello"))
            });

            Assert.That(moved.Count, Is.EqualTo(1));
            Assert.That(moved[(new TableKey("Hud").Hash, Hashing.ComputeNameHash("Greeting"))], Is.EqualTo(new EntryKey("Dialogue", "Welcome")));
        }

        [Test]
        public void Compile_TreatsAQualifiedAliasOfItsOwnTableAsAnAlias()
        {
            List<DocumentIssue> issues = new();
            byte[] data = TableCompiler.Compile(new CatalogKey("Localization"), new TableKey("Dialogue"), new LanguageKey("English"),
                TableDocument.Parse("@formerly Hud.Greeting\n@formerly Dialogue.Hi\nWelcome = Hello"), issues);

            Assert.That(issues, Is.Empty);
            Assert.That(CompiledTable.TryRead(data, out CompiledTable table, out _), Is.True);
            Assert.That(table.TryFind(Hashing.ComputeNameHash("Hi"), out _), Is.True);
            Assert.That(table.TryFind(Hashing.ComputeNameHash("Greeting"), out _), Is.False);
        }

        [Test]
        public void CompiledCatalog_KeepsTheMovedEntries()
        {
            Dictionary<(ulong Table, ulong Entry), EntryKey> moved = new() { [(new TableKey("Hud").Hash, Hashing.ComputeNameHash("Greeting"))] = new EntryKey("Dialogue", "Welcome") };
            CatalogInfo withoutTables = CatalogInfo.FromDocument(new CatalogKey("Localization"), CatalogDocument.Parse(CatalogText), null, null);
            CatalogInfo catalog = new(withoutTables.Key, withoutTables.SourceLanguage.Key, withoutTables.Languages,
                new[] { new TableInfo(new TableKey("Dialogue"), TableLoading.Preload, TableDelivery.Embedded) }, moved);

            byte[] data = CompiledCatalog.Write(catalog);

            Assert.That(CompiledCatalog.TryRead(data, out CatalogInfo read, out string error), Is.True, error);
            Assert.That(read.MovedEntries.Count, Is.EqualTo(1));
            Assert.That(read.MovedEntries[(new TableKey("Hud").Hash, Hashing.ComputeNameHash("Greeting"))].Name, Is.EqualTo("Welcome"));
            for (int length = 0; length < data.Length; length++)
            {
                Assert.That(CompiledCatalog.TryRead(data.AsSpan(0, length), out _, out _), Is.False, $"Read {length} of {data.Length} bytes.");
            }
        }
    }
}
