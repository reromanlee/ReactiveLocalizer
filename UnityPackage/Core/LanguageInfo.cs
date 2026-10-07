using reromanlee.ReactiveLocalizer.Formatting;
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
            : this(key, displayName, culture, fallback, direction, isRequired, null, null, null)
        {
        }

        /// <summary>Creates a language definition that writes numbers with symbols of its own.</summary>
        /// <param name="key">Identity of the language.</param>
        /// <param name="displayName">What a language picker shows; the name when empty.</param>
        /// <param name="culture">Language tag that plural rules, number formatting and exports follow; empty for none.</param>
        /// <param name="fallback">Language missing entries come from; empty for none, which falls back to the source.</param>
        /// <param name="direction">Direction the language is written in.</param>
        /// <param name="isRequired">Whether builds fail while an entry is missing in this language.</param>
        /// <param name="digits">The digits zero to nine to write numbers with, such as <c>0123456789</c> for Arabic with Latin digits; null to keep the culture's.</param>
        /// <param name="decimalSeparator">The decimal separator to use; null to keep the culture's.</param>
        /// <param name="groupSeparator">The digit group separator to use, or empty to never group; null to keep the culture's.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="key"/> is empty; the language falls back to itself; <paramref name="digits"/> isn't ten
        /// digits; or <paramref name="decimalSeparator"/> is empty.
        /// </exception>
        public LanguageInfo(LanguageKey key, string displayName, string culture, LanguageKey fallback, TextDirection direction, bool isRequired,
            string digits, string decimalSeparator, string groupSeparator)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A language needs a key.", nameof(key));
            }
            if (fallback == key)
            {
                throw new ArgumentException($"'{key.Name}' can't fall back to itself.", nameof(fallback));
            }
            if (digits != null && !NumberSymbols.AreValidDigits(digits))
            {
                throw new ArgumentException($"The digits of '{key.Name}' are the ten digits from zero to nine, such as 0123456789.", nameof(digits));
            }
            if (decimalSeparator != null && decimalSeparator.Length == 0)
            {
                throw new ArgumentException($"The decimal separator of '{key.Name}' can't be empty.", nameof(decimalSeparator));
            }
            Key = key;
            DisplayName = string.IsNullOrEmpty(displayName) ? key.Name : displayName;
            Culture = culture ?? string.Empty;
            Fallback = fallback;
            Direction = direction;
            IsRequired = isRequired;
            Digits = digits;
            DecimalSeparator = decimalSeparator;
            GroupSeparator = groupSeparator;
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

        /// <summary>The digits zero to nine the language writes numbers with instead of its culture's; null to keep those.</summary>
        public string Digits { get; }

        /// <summary>The decimal separator the language uses instead of its culture's; null to keep that.</summary>
        public string DecimalSeparator { get; }

        /// <summary>The group separator the language uses instead of its culture's, empty to never group; null to keep that.</summary>
        public string GroupSeparator { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }
}
