using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// A catalog laid out for export, the same for every format: per table, a row per source entry with its context,
    /// maximum length, source text, and its text and state in each exported language.
    /// </summary>
    public sealed class ExportBook
    {
        private ExportBook(CatalogInfo catalog, IReadOnlyList<LanguageInfo> languages, IReadOnlyList<ExportTable> tables)
        {
            Catalog = catalog;
            Languages = languages;
            Tables = tables;
            for (int i = 0; i < tables.Count; i++)
            {
                RowCount += tables[i].Rows.Count;
            }
        }

        /// <summary>The exported catalog.</summary>
        public CatalogInfo Catalog { get; }

        /// <summary>The catalog's source language, whose text every row holds.</summary>
        public LanguageInfo SourceLanguage => Catalog.SourceLanguage;

        /// <summary>The exported translation languages, in catalog order; each row has a cell per language.</summary>
        public IReadOnlyList<LanguageInfo> Languages { get; }

        /// <summary>The exported tables in natural order, each with at least one row.</summary>
        public IReadOnlyList<ExportTable> Tables { get; }

        /// <summary>How many rows the tables hold together.</summary>
        public int RowCount { get; }

        /// <summary>Lays out <paramref name="tables"/> of <paramref name="catalog"/> as <paramref name="options"/> asks.</summary>
        /// <param name="catalog">The catalog, which names the source language and orders the languages.</param>
        /// <param name="tables">The catalog's tables with their files, read.</param>
        /// <param name="options">What to export; null exports everything.</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        public static ExportBook Collect(CatalogInfo catalog, IReadOnlyList<ValidatedTable> tables, ExportOptions options)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            options ??= new ExportOptions();
            HashSet<string> wantedLanguages = options.Languages != null ? new HashSet<string>(options.Languages, StringComparer.OrdinalIgnoreCase) : null;
            HashSet<string> wantedTables = options.Tables != null ? new HashSet<string>(options.Tables, StringComparer.OrdinalIgnoreCase) : null;
            List<LanguageInfo> languages = new();
            for (int i = 0; i < catalog.Languages.Count; i++)
            {
                LanguageInfo language = catalog.Languages[i];
                if (language != catalog.SourceLanguage && (wantedLanguages == null || wantedLanguages.Contains(language.Name)))
                {
                    languages.Add(language);
                }
            }

            List<ValidatedTable> sorted = new(tables ?? Array.Empty<ValidatedTable>());
            sorted.Sort((left, right) => NaturalOrder.Instance.Compare(left.Name, right.Name));
            List<ExportTable> exported = new();
            for (int i = 0; i < sorted.Count; i++)
            {
                if (wantedTables != null && !wantedTables.Contains(sorted[i].Name))
                {
                    continue;
                }
                ExportTable table = CollectTable(catalog, sorted[i], languages, options.Entries);
                if (table != null)
                {
                    exported.Add(table);
                }
            }
            return new ExportBook(catalog, languages, exported);
        }

        private static ExportTable CollectTable(CatalogInfo catalog, ValidatedTable table, List<LanguageInfo> languages, ExportedEntries wanted)
        {
            TableDocument source = FindDocument(table, catalog.SourceLanguage.Name);
            if (source == null)
            {
                return null;
            }
            TableDocument[] translations = new TableDocument[languages.Count];
            for (int l = 0; l < languages.Count; l++)
            {
                translations[l] = FindDocument(table, languages[l].Name);
            }
            List<TableDocumentEntry> entries = new(source.Entries);
            entries.Sort((left, right) => NaturalOrder.Instance.Compare(left.Key, right.Key));
            List<ExportRow> rows = new();
            for (int i = 0; i < entries.Count; i++)
            {
                TableDocumentEntry entry = entries[i];
                uint fingerprint = Hashing.ComputeFingerprint(entry.Value);
                ExportCell[] cells = new ExportCell[languages.Count];
                bool isWanted = wanted == ExportedEntries.All;
                for (int l = 0; l < languages.Count; l++)
                {
                    cells[l] = CreateCell(translations[l], entry.Key, fingerprint);
                    isWanted |= IsWanted(wanted, cells[l].State);
                }
                if (!isWanted)
                {
                    continue;
                }
                int maximumLength = entry.TryGetAttribute(DocumentNames.MaximumLength, out string limit) && MaximumLength.TryParse(limit, out int parsed) ? parsed : 0;
                rows.Add(new ExportRow(entry.Key, string.Join("\n", entry.Comments), maximumLength, entry.Value, cells));
            }
            return rows.Count > 0 ? new ExportTable(table.Name, rows) : null;
        }

        private static ExportCell CreateCell(TableDocument translation, string key, uint sourceFingerprint)
        {
            if (translation == null || !translation.TryGetEntry(key.AsSpan(), out TableDocumentEntry entry))
            {
                return new ExportCell(null, TranslationState.Missing);
            }
            if (!entry.HasFingerprint)
            {
                return new ExportCell(entry.Value, TranslationState.Unverified);
            }
            return new ExportCell(entry.Value, entry.Fingerprint == sourceFingerprint ? TranslationState.Current : TranslationState.Outdated);
        }

        private static bool IsWanted(ExportedEntries wanted, TranslationState state)
        {
            switch (wanted)
            {
                case ExportedEntries.Missing:
                    return state == TranslationState.Missing;
                case ExportedEntries.Outdated:
                    return state == TranslationState.Outdated || state == TranslationState.Unverified;
                case ExportedEntries.MissingOrOutdated:
                    return state != TranslationState.Current;
                default:
                    return true;
            }
        }

        private static TableDocument FindDocument(ValidatedTable table, string language)
        {
            for (int i = 0; i < table.Files.Count; i++)
            {
                if (string.Equals(table.Files[i].LanguageName, language, StringComparison.OrdinalIgnoreCase))
                {
                    return table.Files[i].Document;
                }
            }
            return null;
        }
    }
}
