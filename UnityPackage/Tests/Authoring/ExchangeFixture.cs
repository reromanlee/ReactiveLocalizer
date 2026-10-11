using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using reromanlee.ReactiveLocalizer.Documents;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// The catalog exchange tests share: English as the source, Russian with a culture, and Pirate falling back to
    /// English without one. Its tables hold a current, an outdated, an unverified and a missing translation.
    /// </summary>
    internal static class ExchangeFixture
    {
        public const string CatalogText =
            "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\nDisplayName = Russkij\n[Pirate]\nFallback = English\n";

        public const string ShopEnglish =
            "# Button that confirms buying the selected item.\n@maximumLength 16\nPurchase = Buy\n\n" +
            "Balance = You have {coins, plural, one {# coin} other {# coins}}.\nTitle = Shop\n";

        public const string CommonEnglish = "Confirm = OK\n";

        public const string CommonRussian = "Confirm = Da\n";

        public static readonly string ShopRussian = $"Purchase [{Tag("Buy")}] = Kupit\nTitle [{Tag("Old shop")}] = Magazin\n";

        private static CatalogInfo _catalog;

        public static CatalogInfo Catalog => _catalog ??= CatalogInfo.FromDocument(new CatalogKey("Localization"), CatalogDocument.Parse(CatalogText), null, null);

        /// <summary>Returns the fingerprint tag a translation of <paramref name="sourceText"/> carries.</summary>
        public static string Tag(string sourceText) => Hashing.ComputeFingerprint(sourceText).ToString("x6");

        /// <summary>The fixture's tables: Shop in English and Russian, Common in English and Russian.</summary>
        public static List<ValidatedTable> Tables() => Tables(
            ("Shop", "English", ShopEnglish), ("Shop", "Russian", ShopRussian),
            ("Common", "English", CommonEnglish), ("Common", "Russian", CommonRussian));

        public static List<ValidatedTable> Tables(params (string Table, string Language, string Text)[] files)
        {
            Dictionary<string, List<ValidatedFile>> byTable = new();
            List<string> order = new();
            foreach ((string Table, string Language, string Text) file in files)
            {
                if (!byTable.TryGetValue(file.Table, out List<ValidatedFile> tableFiles))
                {
                    tableFiles = new List<ValidatedFile>();
                    byTable.Add(file.Table, tableFiles);
                    order.Add(file.Table);
                }
                tableFiles.Add(new ValidatedFile($"{file.Table}.{file.Language}.lang", file.Language, TableDocument.Parse(file.Text)));
            }
            List<ValidatedTable> tables = new();
            foreach (string name in order)
            {
                tables.Add(new ValidatedTable(name, byTable[name]));
            }
            return tables;
        }

        /// <summary>Exports the fixture's tables.</summary>
        public static ExportBook Export(ExportOptions options) => ExportBook.Collect(Catalog, Tables(), options);

        /// <summary>Exports the fixture's tables in Russian only.</summary>
        public static ExportBook ExportRussian(ExportedEntries entries = ExportedEntries.All) =>
            Export(new ExportOptions { Languages = new[] { "Russian" }, Entries = entries });

        /// <summary>Opens a table's files for editing, as imports change them.</summary>
        public static TableFileSet Open(string table, params (string Language, string Text)[] files)
        {
            TableFileSet set = new(table, "English");
            foreach ((string Language, string Text) file in files)
            {
                set.Set(file.Language, TableFile.Read(file.Text));
            }
            return set;
        }
    }
}
