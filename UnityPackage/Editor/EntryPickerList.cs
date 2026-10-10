using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The rows an entry picker lists. With no query: a row to pick nothing, the recent picks, then every entry. With
    /// a query: the matches, best first.
    /// </summary>
    internal sealed class EntryPickerList
    {
        private readonly IReadOnlyList<EntrySearchItem> _items;
        private readonly List<EntrySearchItem> _recent = new();
        private readonly List<EntrySearchItem> _matches = new();
        private readonly List<EntryPickerRow> _rows = new();

        /// <summary>Lists the entries of <paramref name="catalogs"/>, with those of <paramref name="recent"/> they have first.</summary>
        public EntryPickerList(IReadOnlyList<IndexedCatalog> catalogs, IReadOnlyList<EntryReference> recent)
        {
            IsNamingCatalogs = catalogs.Count > 1;
            if (catalogs.Count == 1)
            {
                _items = catalogs[0].SearchItems;
            }
            else
            {
                List<EntrySearchItem> items = new();
                for (int i = 0; i < catalogs.Count; i++)
                {
                    items.AddRange(catalogs[i].SearchItems);
                }
                _items = items;
            }
            for (int i = 0; i < recent.Count; i++)
            {
                EntrySearchItem item = Find(catalogs, recent[i]);
                if (item != null)
                {
                    _recent.Add(item);
                }
            }
        }

        /// <summary>The rows for the last query, as a list a <c>ListView</c> takes.</summary>
        public List<EntryPickerRow> Rows => _rows;

        /// <summary>Whether entries come from more than one catalog, so rows name theirs.</summary>
        public bool IsNamingCatalogs { get; }

        /// <summary>How many entries the last query matched.</summary>
        public int MatchCount => _matches.Count;

        /// <summary>Lists what <paramref name="query"/> finds.</summary>
        public void Search(string query)
        {
            _rows.Clear();
            bool isEmpty = string.IsNullOrWhiteSpace(query);
            EntrySearch.Find(_items, query, _matches, int.MaxValue);
            if (!isEmpty)
            {
                AddEntries(_matches);
                return;
            }
            _rows.Add(new EntryPickerRow(EntryPickerRowKind.None, "None", null));
            if (_recent.Count > 0)
            {
                _rows.Add(new EntryPickerRow(EntryPickerRowKind.Header, "Recent", null));
                AddEntries(_recent);
                _rows.Add(new EntryPickerRow(EntryPickerRowKind.Header, "All entries", null));
            }
            AddEntries(_matches);
        }

        /// <summary>Returns the first row picking <paramref name="reference"/>, or the first row that can be picked; -1 when no row can.</summary>
        public int FindRow(EntryReference reference)
        {
            if (!reference.IsEmpty)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    EntrySearchItem item = _rows[i].Item;
                    if (item != null &&
                        string.Equals(item.TableName, reference.TableName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(item.EntryName, reference.EntryName, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrEmpty(reference.CatalogName) || string.Equals(item.CatalogName, reference.CatalogName, StringComparison.OrdinalIgnoreCase)))
                    {
                        return i;
                    }
                }
            }
            return Step(-1, 1);
        }

        /// <summary>Returns the next row that can be picked from <paramref name="row"/> in <paramref name="direction"/>, or <paramref name="row"/> when there is none.</summary>
        public int Step(int row, int direction)
        {
            for (int i = row + direction; i >= 0 && i < _rows.Count; i += direction)
            {
                if (_rows[i].Kind != EntryPickerRowKind.Header)
                {
                    return i;
                }
            }
            return row;
        }

        private void AddEntries(List<EntrySearchItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                _rows.Add(new EntryPickerRow(EntryPickerRowKind.Entry, null, items[i]));
            }
        }

        private static EntrySearchItem Find(IReadOnlyList<IndexedCatalog> catalogs, EntryReference reference)
        {
            for (int i = 0; i < catalogs.Count; i++)
            {
                IndexedCatalog catalog = catalogs[i];
                if (!string.Equals(catalog.Name, reference.CatalogName, StringComparison.OrdinalIgnoreCase) ||
                    !NameRules.IsValid(reference.TableName) || !catalog.TryGetTable(new TableKey(reference.TableName), out IndexedTable table) ||
                    table.SourceDocument == null || !table.SourceDocument.TryGetEntry(reference.EntryName, out TableDocumentEntry entry))
                {
                    continue;
                }
                return new EntrySearchItem(catalog.Name, table.Name, entry.Key, entry.Value);
            }
            return null;
        }
    }

    /// <summary>A row of an entry picker: an entry, the choice of none, or a header above a group of entries.</summary>
    internal readonly struct EntryPickerRow
    {
        public EntryPickerRow(EntryPickerRowKind kind, string label, EntrySearchItem item)
        {
            Kind = kind;
            Label = label;
            Item = item;
        }

        public EntryPickerRowKind Kind { get; }

        /// <summary>What a header or the choice of none says; null for an entry.</summary>
        public string Label { get; }

        /// <summary>The entry; null for other rows.</summary>
        public EntrySearchItem Item { get; }
    }

    /// <summary>What a row of an entry picker is.</summary>
    internal enum EntryPickerRowKind
    {
        Entry = 0,
        None = 1,
        Header = 2
    }
}
