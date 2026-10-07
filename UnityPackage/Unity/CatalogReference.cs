using System;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// A catalog picked in the Inspector, for components that work with a whole catalog, such as a language picker.
    /// Empty means the project's default catalog.
    /// </summary>
    /// <remarks>A saved name that breaks the naming rule turns into an empty key instead of throwing.</remarks>
    [Serializable]
    public struct CatalogReference : IEquatable<CatalogReference>
    {
        [SerializeField] private string _catalog;

        /// <summary>Creates a reference to the catalog named <paramref name="catalogName"/>.</summary>
        public CatalogReference(string catalogName)
        {
            _catalog = catalogName;
        }

        /// <summary>Name of the catalog, as saved. Empty for the default catalog.</summary>
        public string CatalogName => _catalog;

        /// <summary>Whether the reference names no catalog, which means the default one.</summary>
        public bool IsEmpty => string.IsNullOrEmpty(_catalog);

        /// <summary>Returns the key of the catalog, or an empty key for the default catalog or an invalid name.</summary>
        public CatalogKey ToKey() => NameRules.IsValid(_catalog) ? new CatalogKey(_catalog) : default;

        /// <inheritdoc/>
        public bool Equals(CatalogReference other) => string.Equals(_catalog ?? string.Empty, other._catalog ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is CatalogReference other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Hashing.ComputeNameHash(_catalog).GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => _catalog ?? string.Empty;

        public static bool operator ==(CatalogReference left, CatalogReference right) => left.Equals(right);

        public static bool operator !=(CatalogReference left, CatalogReference right) => !left.Equals(right);
    }
}
