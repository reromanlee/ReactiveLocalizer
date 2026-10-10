using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class FolderTableSourceTests
    {
        private const string CatalogText = "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Title = new("Shop", "Title");
        private static readonly LanguageKey Klingon = new("Klingon");
        private static readonly LanguageKey Russian = new("Russian");

        private string _folder;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ReactiveLocalizerTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        private void Write(string relativePath, string text)
        {
            string path = Path.Combine(_folder, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        private static MemoryTableSource CreateGame()
        {
            return MemoryTableSource.Imported("Localization", CatalogText, null,
                ("Shop", "English", "Purchase = Buy\nTitle = Shop"),
                ("Shop", "Russian", "Purchase = Kupit\nTitle = Magazin"));
        }

        [Test]
        public void ReadLanguages_ReadsTheSectionsOfEveryCatalogFile()
        {
            Write("Klingon/Klingon.catalog", "[Klingon]\nDisplayName = tlhIngan Hol\nFallback = English\n");
            Write("Elvish/Elvish.catalog", "[Elvish]\nFallback = English\n[Klingon]\n");
            List<string> problems = new();

            IReadOnlyList<LanguageInfo> languages = new FolderTableSource(new CatalogKey("Localization"), _folder).ReadLanguages(problems);

            // Files are read in path order, so the Elvish folder's definition of Klingon comes first.
            Assert.That(languages.Count, Is.EqualTo(2));
            Assert.That(languages[0].Name, Is.EqualTo("Elvish"));
            Assert.That(languages[1].Name, Is.EqualTo("Klingon"));
            Assert.That(languages[1].DisplayName, Is.Not.EqualTo("tlhIngan Hol"));
            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("Klingon.catalog").And.Contain("first definition"));
        }

        [Test]
        public void Tables_ComeFirstAndPatchTheLanguageTheyTranslate()
        {
            Write("Klingon/Shop.Klingon.lang", "Purchase = je'");
            Write("Fixes/Shop.Russian.lang", "Purchase = Kupit seychas");
            MemoryTableSource game = CreateGame();
            FolderTableSource mods = new(game.CatalogKey, _folder);
            TestHost host = new(mods, game);
            using Localizer localizer = new(game.CatalogKey, host);
            localizer.InitializeAsync();

            foreach (LanguageInfo language in mods.ReadLanguages(null))
            {
                localizer.RegisterLanguage(language);
            }
            localizer.RegisterLanguage(new LanguageInfo(Klingon, string.Empty, string.Empty, new LanguageKey("English"), TextDirection.LeftToRight, false));
            localizer.SetLanguageAsync(Klingon);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("je'"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Shop"));

            localizer.SetLanguageAsync(Russian);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit seychas"));
            Assert.That(localizer.Get(Title), Is.EqualTo("Magazin"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void ProblemsInAFile_AreReportedWithItsPathAndLine()
        {
            Write("Shop.Klingon.lang", "Purchase = je'\nTitle\nCoins = {coins, plural, one {# Huch}");
            MemoryTableSource game = CreateGame();
            TestHost host = new(new FolderTableSource(game.CatalogKey, _folder), game);
            using Localizer localizer = new(game.CatalogKey, host);
            localizer.RegisterLanguage(new LanguageInfo(Klingon, string.Empty, string.Empty, new LanguageKey("English"), TextDirection.LeftToRight, false));
            localizer.SetLanguageAsync(Klingon);

            localizer.InitializeAsync();

            Assert.That(localizer.Get(Purchase), Is.EqualTo("je'"));
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(2));
            Assert.That(host.Reports[0].Message, Does.Contain("Shop.Klingon.lang(2,"));
            Assert.That(host.Reports[1].Message, Does.Contain("Shop.Klingon.lang(3,"));
        }

        [Test]
        public void MisnamedAndRepeatedFiles_AreReported()
        {
            Write("Shop-Klingon.lang", "Purchase = je'");
            Write("A/Shop.Klingon.lang", "Purchase = je'");
            Write("B/Shop.Klingon.lang", "Purchase = ghobe'");
            MemoryTableSource game = CreateGame();
            FolderTableSource mods = new(game.CatalogKey, _folder);
            TestHost host = new(mods, game);
            using Localizer localizer = new(game.CatalogKey, host);
            List<string> problems = new();
            mods.ReadLanguages(problems);
            localizer.RegisterLanguage(new LanguageInfo(Klingon, string.Empty, string.Empty, new LanguageKey("English"), TextDirection.LeftToRight, false));
            localizer.SetLanguageAsync(Klingon);

            localizer.InitializeAsync();

            Assert.That(problems.Count, Is.EqualTo(1));
            Assert.That(problems[0], Does.Contain("Shop-Klingon.lang"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("je'"));
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
            Assert.That(host.Reports[0].Message, Does.Contain("is ignored"));
        }

        [Test]
        public void AMissingFolder_HasNoTablesAndNoProblems()
        {
            FolderTableSource source = new(new CatalogKey("Localization"), Path.Combine(_folder, "Missing"));
            List<string> problems = new();

            Assert.That(source.ReadLanguages(problems), Is.Empty);
            Assert.That(problems, Is.Empty);
            Assert.That(source.TryLoad(TableRequest.ForTable(source.Catalog, new TableInfo(new TableKey("Shop"), TableLoading.Preload, TableDelivery.Embedded), Klingon), null), Is.False);
        }

        [Test]
        public void Refresh_FindsFilesAddedSince()
        {
            MemoryTableSource game = CreateGame();
            FolderTableSource mods = new(game.CatalogKey, _folder);
            using Localizer localizer = new(game.CatalogKey, new TestHost(mods, game));
            localizer.InitializeAsync();
            localizer.SetLanguageAsync(Russian);

            Write("Shop.Russian.lang", "Title = Lavka");
            mods.Refresh();
            localizer.SetLanguageAsync(new LanguageKey("English"));
            localizer.SetLanguageAsync(Russian);

            Assert.That(localizer.Get(Title), Is.EqualTo("Lavka"));
        }

        [Test]
        public void ReadingInBackground_DeliversOnTheHostThread()
        {
            Write("Shop.Russian.lang", "Title = Lavka");
            MemoryTableSource game = CreateGame();
            TestHost host = new(new FolderTableSource(game.CatalogKey, _folder, true), game);
            using Localizer localizer = new(game.CatalogKey, host);
            localizer.InitializeAsync();

            Task switching = localizer.SetLanguageAsync(Russian);
            for (int attempt = 0; attempt < 200 && !switching.IsCompleted; attempt++)
            {
                Thread.Sleep(5);
                host.RunUpdates();
            }

            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.Get(Title), Is.EqualTo("Lavka"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }
    }
}
