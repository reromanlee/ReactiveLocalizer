using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>How many entries of a table need attention, as the table window's sidebar badges show.</summary>
    internal readonly struct TableStatus
    {
        public TableStatus(int missing, int outdated, int errors)
        {
            Missing = missing;
            Outdated = outdated;
            Errors = errors;
        }

        /// <summary>Translations the catalog's languages lack, summed over the languages.</summary>
        public int Missing { get; }

        /// <summary>Translations made from an older source text.</summary>
        public int Outdated { get; }

        /// <summary>Errors compiling the table's files finds, such as broken messages and orphans.</summary>
        public int Errors { get; }

        /// <summary>
        /// Counts what needs attention in <paramref name="table"/>, reading files as compiled. Cached until a localization
        /// file changes, so a sidebar asks for every visible table each time it draws.
        /// </summary>
        public static TableStatus Of(IndexedCatalog catalog, IndexedTable table)
        {
            if (Cache.Version != CatalogIndex.Version)
            {
                Cache.Statuses.Clear();
                Cache.Version = CatalogIndex.Version;
            }
            string key = catalog.Name + "/" + table.Name;
            if (!Cache.Statuses.TryGetValue(key, out TableStatus status))
            {
                status = Count(catalog, table);
                Cache.Statuses.Add(key, status);
            }
            return status;
        }

        private static TableStatus Count(IndexedCatalog catalog, IndexedTable table)
        {
            TableDocument source = table.SourceDocument;
            if (catalog.Info == null || source == null)
            {
                return new TableStatus(0, 0, 1);
            }
            uint[] fingerprints = new uint[source.Entries.Count];
            for (int i = 0; i < fingerprints.Length; i++)
            {
                fingerprints[i] = Hashing.ComputeFingerprint(source.Entries[i].Value);
            }
            int missing = 0;
            int outdated = 0;
            int errors = CountErrors(catalog, table.Name, catalog.Info.SourceLanguage.Name, source, null);
            IReadOnlyList<LanguageInfo> languages = catalog.Info.Languages;
            for (int l = 0; l < languages.Count; l++)
            {
                if (languages[l] == catalog.Info.SourceLanguage)
                {
                    continue;
                }
                TableDocument document = table.TryGetFile(languages[l].Key, out string path) ? CatalogIndex.ReadTableDocument(path) : null;
                for (int i = 0; i < source.Entries.Count; i++)
                {
                    if (document == null || !document.TryGetEntry(source.Entries[i].Key, out TableDocumentEntry entry))
                    {
                        missing++;
                    }
                    else if (entry.HasFingerprint && entry.Fingerprint != fingerprints[i])
                    {
                        outdated++;
                    }
                }
                if (document != null)
                {
                    errors += CountErrors(catalog, table.Name, languages[l].Name, document, source);
                }
            }
            return new TableStatus(missing, outdated, errors);
        }

        private static int CountErrors(IndexedCatalog catalog, string tableName, string language, TableDocument document, TableDocument source)
        {
            Dictionary<string, List<DocumentIssue>> byKey = new(StringComparer.OrdinalIgnoreCase);
            List<DocumentIssue> fileIssues = new();
            EntryIssues.Collect(catalog.Info, tableName, language, document, source, byKey, fileIssues);
            int errors = 0;
            foreach (List<DocumentIssue> issues in byKey.Values)
            {
                errors += CountErrors(issues);
            }
            return errors + CountErrors(fileIssues);
        }

        private static int CountErrors(List<DocumentIssue> issues)
        {
            int errors = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == IssueSeverity.Error)
                {
                    errors++;
                }
            }
            return errors;
        }

        private static class Cache
        {
            public static readonly Dictionary<string, TableStatus> Statuses = new(StringComparer.OrdinalIgnoreCase);
            public static int Version = -1;
        }
    }
}
