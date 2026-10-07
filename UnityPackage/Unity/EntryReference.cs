using System;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// An entry picked in the Inspector: a serializable field for components, storing the catalog, table and key as
    /// names so scene files stay readable in diffs. The Inspector fills the catalog in when an entry is picked.
    /// </summary>
    /// <remarks>
    /// Saved data is never trusted: names that break the naming rule, such as ones mangled by a bad merge, turn into
    /// an empty key instead of throwing, and an empty key shows empty text with one warning.
    /// </remarks>
    [Serializable]
    public struct EntryReference : IEquatable<EntryReference>
    {
        [SerializeField] private string _catalog;
        [SerializeField] private string _table;
        [SerializeField] private string _entry;

        /// <summary>Creates a reference to the entry named <paramref name="entryName"/> in <paramref name="tableName"/> of <paramref name="catalogName"/>.</summary>
        public EntryReference(string catalogName, string tableName, string entryName)
        {
            _catalog = catalogName;
            _table = tableName;
            _entry = entryName;
        }

        /// <summary>Creates a reference to the entry <paramref name="key"/> names, in <paramref name="catalog"/>.</summary>
        public EntryReference(CatalogKey catalog, in EntryKey key)
        {
            _catalog = catalog.Name;
            _table = key.IsEmpty ? null : key.Table.Name;
            _entry = key.Name;
        }

        /// <summary>Name of the catalog, as saved. Empty when the reference was made without one.</summary>
        public string CatalogName => _catalog;

        /// <summary>Name of the table, as saved.</summary>
        public string TableName => _table;

        /// <summary>Name of the entry, as saved.</summary>
        public string EntryName => _entry;

        /// <summary>Whether no entry is picked.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(_table) || string.IsNullOrEmpty(_entry);

        /// <summary>Returns the key of the catalog, or an empty key when the reference names none or an invalid one.</summary>
        public CatalogKey ToCatalogKey() => NameRules.IsValid(_catalog) ? new CatalogKey(_catalog) : default;

        /// <summary>Returns the key of the referenced entry, or an empty key when nothing valid is picked.</summary>
        public EntryKey ToKey()
        {
            if (!NameRules.IsValid(_table) || !NameRules.IsValid(_entry))
            {
                return default;
            }
            return new EntryKey(_table, _entry);
        }

        /// <inheritdoc/>
        public bool Equals(EntryReference other)
        {
            return string.Equals(_catalog ?? string.Empty, other._catalog ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(_table, other._table, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(_entry, other._entry, StringComparison.OrdinalIgnoreCase);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is EntryReference other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(Hashing.ComputeNameHash(_catalog), Hashing.ComputeNameHash(_table), Hashing.ComputeNameHash(_entry));
        }

        /// <summary>Returns <c>Table.Entry</c>, or an empty string when nothing is picked.</summary>
        public override string ToString() => IsEmpty ? string.Empty : $"{_table}.{_entry}";

        public static bool operator ==(EntryReference left, EntryReference right) => left.Equals(right);

        public static bool operator !=(EntryReference left, EntryReference right) => !left.Equals(right);
    }
}
