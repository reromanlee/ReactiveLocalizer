using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// Identifies a table of a catalog by its name. Generated code hands one out per table, such as
    /// <c>LocalizationKeys.Shop.TableKey</c>.
    /// </summary>
    /// <remarks>
    /// Equality ignores case, like the naming rule does. The default value names no table, which
    /// <see cref="IsEmpty"/> tells apart.
    /// </remarks>
    public readonly struct TableKey : IEquatable<TableKey>
    {
        /// <summary>Creates the key of the table named <paramref name="name"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="name"/> breaks the naming rule.</exception>
        public TableKey(string name)
        {
            NameRules.ThrowIfInvalid(name, nameof(name));
            Name = name;
            Hash = Hashing.ComputeNameHash(name);
        }

        /// <summary>Name of the table, as its files are named.</summary>
        public string Name { get; }

        /// <summary>The 64-bit hash of <see cref="Name"/> that lookups compare.</summary>
        public ulong Hash { get; }

        /// <summary>Whether this is the default value, which names no table.</summary>
        public bool IsEmpty => Name == null;

        /// <inheritdoc/>
        public bool Equals(TableKey other) => Hash == other.Hash && IsEmpty == other.IsEmpty;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is TableKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Hash.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => Name ?? string.Empty;

        public static bool operator ==(TableKey left, TableKey right) => left.Equals(right);

        public static bool operator !=(TableKey left, TableKey right) => !left.Equals(right);
    }
}
