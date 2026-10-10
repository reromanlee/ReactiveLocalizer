using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// What compiling a table file reports about each of its entries, exactly as its import and validation report it,
    /// for tools that show problems beside the entry they are about.
    /// </summary>
    public static class EntryIssues
    {
        /// <summary>
        /// Compiles <paramref name="document"/> as <paramref name="language"/> of <paramref name="tableName"/> and adds
        /// each issue to <paramref name="byKey"/>, under the key of the entry it is about, or to
        /// <paramref name="fileIssues"/> when it is about no entry, such as a malformed line.
        /// </summary>
        /// <param name="catalog">The catalog, whose cultures decide the plural forms checked.</param>
        /// <param name="tableName">The table the file belongs to.</param>
        /// <param name="language">The file's language.</param>
        /// <param name="document">The file, read.</param>
        /// <param name="source">The table's source-language file, read; null when <paramref name="document"/> is that file.</param>
        /// <param name="byKey">Where issues about entries go, by key; create it ignoring case.</param>
        /// <param name="fileIssues">Where issues about the file as a whole go.</param>
        public static void Collect(CatalogInfo catalog, string tableName, string language, TableDocument document, TableDocument source,
            Dictionary<string, List<DocumentIssue>> byKey, List<DocumentIssue> fileIssues)
        {
            List<DocumentIssue> found = new(document.Issues);
            if (NameRules.IsValid(tableName) && NameRules.IsValid(language))
            {
                TableCompiler.Compile(catalog, new TableKey(tableName), new LanguageKey(language), document, source, found);
            }
            // An entry spans its own line and the lines of its attributes.
            Dictionary<int, string> keyByLine = new();
            IReadOnlyList<TableDocumentEntry> entries = document.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                keyByLine[entries[i].Line] = entries[i].Key;
                for (int a = 0; a < entries[i].Attributes.Count; a++)
                {
                    keyByLine.TryAdd(entries[i].Attributes[a].Line, entries[i].Key);
                }
            }
            for (int i = 0; i < found.Count; i++)
            {
                if (!keyByLine.TryGetValue(found[i].Line, out string key))
                {
                    fileIssues.Add(found[i]);
                    continue;
                }
                if (!byKey.TryGetValue(key, out List<DocumentIssue> issues))
                {
                    issues = new List<DocumentIssue>(1);
                    byKey.Add(key, issues);
                }
                issues.Add(found[i]);
            }
        }
    }
}
