using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// Identifies a language by its name. Generated code hands one out per language, such as
    /// <c>LocalizationLanguages.Russian</c>, which is what a project's OS-language switch maps to.
    /// </summary>
    /// <remarks>
    /// Equality ignores case, like the naming rule does. The default value names no language, which
    /// <see cref="IsEmpty"/> tells apart.
    /// </remarks>
    public readonly struct LanguageKey : IEquatable<LanguageKey>
    {
        /// <summary>Creates the key of the language named <paramref name="name"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="name"/> breaks the naming rule.</exception>
        public LanguageKey(string name)
        {
            NameRules.ThrowIfInvalid(name, nameof(name));
            Name = name;
            Hash = Hashing.ComputeNameHash(name);
        }

        /// <summary>Name of the language, as table files and saved settings write it.</summary>
        public string Name { get; }

        /// <summary>The 64-bit hash of <see cref="Name"/> that lookups compare.</summary>
        public ulong Hash { get; }

        /// <summary>Whether this is the default value, which names no language.</summary>
        public bool IsEmpty => Name == null;

        /// <inheritdoc/>
        public bool Equals(LanguageKey other) => Hash == other.Hash && IsEmpty == other.IsEmpty;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is LanguageKey other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Hash.GetHashCode();

        /// <inheritdoc/>
        public override string ToString() => Name ?? string.Empty;

        public static bool operator ==(LanguageKey left, LanguageKey right) => left.Equals(right);

        public static bool operator !=(LanguageKey left, LanguageKey right) => !left.Equals(right);
    }
}
