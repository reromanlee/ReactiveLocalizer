using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// A language as a project defines it in its catalog: invented and fan-made languages are as valid as real ones,
    /// and standard codes are optional.
    /// </summary>
    public sealed class LanguageInfo
    {
        /// <summary>Creates a language definition.</summary>
        /// <param name="key">Identity of the language.</param>
        /// <param name="displayName">What a language picker shows; the name when empty.</param>
        /// <param name="culture">Language tag that plural rules, number formatting and exports follow; empty for none.</param>
        /// <param name="fallback">Language missing entries come from; empty for none, which falls back to the source.</param>
        /// <param name="direction">Direction the language is written in.</param>
        /// <param name="isRequired">Whether builds fail while an entry is missing in this language.</param>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty, or the language falls back to itself.</exception>
        public LanguageInfo(LanguageKey key, string displayName, string culture, LanguageKey fallback, TextDirection direction, bool isRequired)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A language needs a key.", nameof(key));
            }
            if (fallback == key)
            {
                throw new ArgumentException($"'{key.Name}' can't fall back to itself.", nameof(fallback));
            }
            Key = key;
            DisplayName = string.IsNullOrEmpty(displayName) ? key.Name : displayName;
            Culture = culture ?? string.Empty;
            Fallback = fallback;
            Direction = direction;
            IsRequired = isRequired;
        }

        /// <summary>Identity of the language, as generated code hands it out.</summary>
        public LanguageKey Key { get; }

        /// <summary>Name of the language, as table files and saved settings write it.</summary>
        public string Name => Key.Name;

        /// <summary>What a language picker shows. The name when the catalog gives none.</summary>
        public string DisplayName { get; }

        /// <summary>The language tag plural rules and number formatting follow, such as <c>pt-BR</c>. Empty for none.</summary>
        public string Culture { get; }

        /// <summary>The language missing entries come from before the source language. Empty for none.</summary>
        public LanguageKey Fallback { get; }

        /// <summary>Whether the language names a <see cref="Fallback"/>.</summary>
        public bool HasFallback => !Fallback.IsEmpty;

        /// <summary>The direction the language is written in.</summary>
        public TextDirection Direction { get; }

        /// <summary>Whether builds fail while an entry is missing in this language.</summary>
        public bool IsRequired { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
