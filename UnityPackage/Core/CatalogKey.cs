using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// Identifies a catalog by its name. Generated code hands one out as <c>LocalizationKeys.CatalogKey</c>, so a call
    /// site never spells a catalog name as text.
    /// </summary>
    /// <remarks>
    /// Equality ignores case, like the naming rule does. The default value names no catalog, which
    /// <see cref="IsEmpty"/> tells apart.
    /// </remarks>
    public readonly struct CatalogKey : IEquatable<CatalogKey>
    {
        /// <summary>Creates the key of the catalog named <paramref name="name"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="name"/> breaks the naming rule.</exception>
        public CatalogKey(string name)
        {
            NameRules.ThrowIfInvalid(name, nameof(name));
            Name = name;
            Hash = Hashing.ComputeNameHash(name);
        }

        /// <summary>Name of the catalog, as its file is named.</summary>
        public string Name { get; }

        /// <summary>The 64-bit hash of <see cref="Name"/> that lookups compare.</summary>
        public ulong Hash { get; }

        /// <summary>Whether this is the default value, which names no catalog.</summary>
        public bool IsEmpty => Name == null;

        /// <inheritdoc/>
        public bool Equals(CatalogKey other) => Hash == other.Hash && IsEmpty == other.IsEmpty;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is CatalogKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Hash.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => Name ?? string.Empty;

        public static bool operator ==(CatalogKey left, CatalogKey right) => left.Equals(right);

        public static bool operator !=(CatalogKey left, CatalogKey right) => !left.Equals(right);
    }
}
