using reromanlee.ReactiveLocalizer.Authoring;
using System.Collections.Generic;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// An entry about to be created from an entry picker: where it goes, its key and its text, filled in with
    /// suggestions, and what creating it would do.
    /// </summary>
    internal sealed class EntryDraft
    {
        private readonly IReadOnlyList<IndexedCatalog> _catalogs;

        /// <summary>
        /// Starts a draft in one of <paramref name="catalogs"/>, preferring <paramref name="preferredCatalog"/>. The table
        /// is named after the prefab or scene of <paramref name="context"/>, else <paramref name="lastTable"/>; the key
        /// after <paramref name="context"/> and <paramref name="fieldName"/>.
        /// </summary>
        /// <param name="catalogs">The catalogs the entry may go to; only usable and writable ones are offered.</param>
        /// <param name="preferredCatalog">The catalog to start in, such as the field's current one; null for the default catalog.</param>
        /// <param name="context">The GameObject showing the text, if any.</param>
        /// <param name="fieldName">The name of the field the entry is for.</param>
        /// <param name="lastTable">The table an entry was last created in.</param>
        public EntryDraft(IReadOnlyList<IndexedCatalog> catalogs, string preferredCatalog, GameObject context, string fieldName, string lastTable)
        {
            List<IndexedCatalog> writable = new();
            for (int i = 0; i < catalogs.Count; i++)
            {
                if (catalogs[i].Info != null && catalogs[i].IsWritable)
                {
                    writable.Add(catalogs[i]);
                }
            }
            _catalogs = writable;
            Catalog = Pick(writable, preferredCatalog);
            TableName = EntryAuthoring.SuggestOwnerTable(context) ?? (NameRules.IsValid(lastTable) ? lastTable : EntryAuthoring.SuggestTable(null));
            Key = context != null ? EntryAuthoring.SuggestKey(context.name, fieldName) : EntryAuthoring.SuggestKey(fieldName);
            Text = string.Empty;
        }

        /// <summary>The catalogs the entry may go to.</summary>
        public IReadOnlyList<IndexedCatalog> Catalogs => _catalogs;

        /// <summary>The catalog the entry goes to; null when no catalog can take new entries.</summary>
        public IndexedCatalog Catalog { get; set; }

        public string TableName { get; set; }

        public string Key { get; set; }

        public string Text { get; set; }

        /// <summary>
        /// Takes what was searched for: <c>Table.Entry</c> names the entry, anything else is its text.
        /// </summary>
        public void TakeQuery(string query)
        {
            query = query?.Trim() ?? string.Empty;
            int dot = query.IndexOf('.');
            if (dot > 0 && query.IndexOf(' ') < 0 && NameRules.IsValid(query.Substring(0, dot)) && NameRules.IsValid(query.Substring(dot + 1)))
            {
                TableName = query.Substring(0, dot);
                Key = query.Substring(dot + 1);
                Text = string.Empty;
                return;
            }
            Text = query;
        }

        /// <summary>
        /// Says what creating the entry would do, such as making a new table or numbering a taken key.
        /// <paramref name="canCreate"/> is false when it can't be created as it is.
        /// </summary>
        public string Describe(out bool canCreate)
        {
            canCreate = false;
            if (Catalog == null)
            {
                return _catalogs.Count == 0 && CatalogIndex.Catalogs.Count == 0
                    ? "The project has no catalog yet. Create one with Assets > Create > ReactiveLocalizer > Catalog."
                    : "No catalog here can take new entries: they are read-only or have errors.";
            }
            if (!NameRules.IsValid(TableName))
            {
                return $"'{TableName}' can't be a table name: {NameRules.Description}.";
            }
            if (!NameRules.IsValid(Key))
            {
                return $"'{Key}' can't be a key: {NameRules.Description}.";
            }
            canCreate = true;
            if (!Catalog.TryGetTable(new TableKey(TableName), out IndexedTable table))
            {
                return $"Creates the table '{TableName}' in the catalog '{Catalog.Name}'.";
            }
            string uniqueKey = FindUniqueKey(table.Name);
            return uniqueKey == Key
                ? $"Adds '{table.Name}.{Key}' to the catalog '{Catalog.Name}'."
                : $"'{table.Name}.{Key}' is taken, so this adds '{table.Name}.{uniqueKey}'.";
        }

        /// <summary>Uses the table's name as the catalog spells it, so a reference matches the table's files exactly.</summary>
        public void MatchTableSpelling()
        {
            if (Catalog != null && NameRules.IsValid(TableName) && Catalog.TryGetTable(new TableKey(TableName), out IndexedTable table))
            {
                TableName = table.Name;
            }
        }

        /// <summary>Returns the key creating would give the entry: its own, or with the first number free appended, as <see cref="EntryAuthoring"/> does.</summary>
        private string FindUniqueKey(string tableName)
        {
            KeyResolver resolver = Catalog.Resolver;
            string key = Key;
            for (int number = 2; IsTaken(resolver.Resolve(tableName, key).Kind) && number < 1000; number++)
            {
                key = Key + number;
            }
            return key;
        }

        // The table's own keys and former names are taken; a key that moved to another table is free again here.
        private static bool IsTaken(KeyResolutionKind kind) => kind == KeyResolutionKind.Found || kind == KeyResolutionKind.Renamed;

        private static IndexedCatalog Pick(List<IndexedCatalog> catalogs, string preferredCatalog)
        {
            IndexedCatalog preferred = EntryPreview.FindCatalog(preferredCatalog);
            for (int i = 0; i < catalogs.Count; i++)
            {
                if (catalogs[i] == preferred)
                {
                    return preferred;
                }
            }
            return catalogs.Count > 0 ? catalogs[0] : null;
        }
    }
}
