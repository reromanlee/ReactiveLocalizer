using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// Identifies one entry of one table. Generated code hands one out per entry, such as
    /// <c>LocalizationKeys.Shop.Purchase</c>, so a call site never spells a table or an entry as text.
    /// </summary>
    /// <remarks>
    /// Equality ignores case, like the naming rule does. The default value names no entry, which
    /// <see cref="IsEmpty"/> tells apart.
    /// </remarks>
    public readonly struct EntryKey : IEquatable<EntryKey>
    {
        /// <summary>Creates the key of the entry named <paramref name="name"/> in <paramref name="table"/>.</summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="table"/> is empty, or <paramref name="name"/> breaks the naming rule.
        /// </exception>
        public EntryKey(TableKey table, string name)
        {
            if (table.IsEmpty)
            {
                throw new ArgumentException("An entry key needs the key of its table.", nameof(table));
            }
            NameRules.ThrowIfInvalid(name, nameof(name));
            Table = table;
            Name = name;
            Hash = Hashing.ComputeNameHash(name);
        }

        /// <summary>Creates the key of the entry named <paramref name="entryName"/> in the table <paramref name="tableName"/>.</summary>
        /// <exception cref="ArgumentException">Either name breaks the naming rule.</exception>
        public EntryKey(string tableName, string entryName) : this(new TableKey(tableName), entryName)
        {
            // Both names are validated by the constructors this one chains to.
        }

        /// <summary>Key of the table the entry belongs to.</summary>
        public TableKey Table { get; }

        /// <summary>Name of the entry, as the table's files write it.</summary>
        public string Name { get; }

        /// <summary>The 64-bit hash of <see cref="Name"/> that lookups compare within <see cref="Table"/>.</summary>
        public ulong Hash { get; }

        /// <summary>Whether this is the default value, which names no entry.</summary>
        public bool IsEmpty => Name == null;

        /// <inheritdoc/>
        public bool Equals(EntryKey other) => Hash == other.Hash && IsEmpty == other.IsEmpty && Table.Equals(other.Table);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is EntryKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Table.Hash, Hash);

        /// <summary>Returns <c>Table.Entry</c>, the way markers and messages name an entry.</summary>
        public override string ToString() => IsEmpty ? string.Empty : $"{Table.Name}.{Name}";

        public static bool operator ==(EntryKey left, EntryKey right) => left.Equals(right);

        public static bool operator !=(EntryKey left, EntryKey right) => !left.Equals(right);
    }
}
