namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// Everything messages need to format text in one language: its plural rules and how it writes numbers. A catalog
    /// resolves one per language from its culture, its fallback and its own overrides.
    /// </summary>
    internal sealed class LanguageFormat
    {
        /// <summary>The format of a language with neither a culture nor a fallback: CLDR's root locale, and only the <c>other</c> form.</summary>
        public static readonly LanguageFormat Root = new(NumberSymbols.Root, PluralRules.OtherOnly, PluralRules.OtherOnly);

        public LanguageFormat(NumberSymbols numbers, int cardinalRules, int ordinalRules)
        {
            Numbers = numbers;
            CardinalRules = cardinalRules;
            OrdinalRules = ordinalRules;
        }

        public NumberSymbols Numbers { get; }

        /// <summary>The CLDR rule set that chooses plural forms.</summary>
        public int CardinalRules { get; }

        /// <summary>The CLDR rule set that chooses selectordinal forms.</summary>
        public int OrdinalRules { get; }

        /// <summary>The forms plural messages in this language need, as a set of <see cref="PluralCategories"/>.</summary>
        public int CardinalCategories => PluralRules.GetCardinalCategories(CardinalRules);

        /// <summary>The forms selectordinal messages in this language need, as a set of <see cref="PluralCategories"/>.</summary>
        public int OrdinalCategories => PluralRules.GetOrdinalCategories(OrdinalRules);

        /// <summary>
        /// Resolves the format of a language: from its culture when CLDR knows it, else the format it inherits from its
        /// fallback language, then with its own number symbols applied over either.
        /// </summary>
        /// <param name="culture">The language's culture, possibly empty.</param>
        /// <param name="inherited">The format of the language's fallback, or <see cref="Root"/> without one.</param>
        /// <param name="digits">Digits the language overrides, or null.</param>
        /// <param name="decimalSeparator">Decimal separator the language overrides, or null.</param>
        /// <param name="groupSeparator">Group separator the language overrides, or null.</param>
        /// <param name="isCultureKnown">Whether CLDR knows the culture; false for an empty one.</param>
        public static LanguageFormat Resolve(string culture, LanguageFormat inherited, string digits, string decimalSeparator,
            string groupSeparator, out bool isCultureKnown)
        {
            NumberSymbols numbers = inherited.Numbers;
            int cardinal = inherited.CardinalRules;
            int ordinal = inherited.OrdinalRules;
            // A culture is known when CLDR has its numbers, which it has for every locale it has anything for. A known
            // culture without plural data uses CLDR's root rules rather than its fallback's, as CLDR itself would.
            isCultureKnown = NumberSymbols.TryFind(culture, out NumberSymbols found);
            if (isCultureKnown)
            {
                numbers = found;
                PluralRules.TryFindCardinal(culture, out cardinal);
                PluralRules.TryFindOrdinal(culture, out ordinal);
            }
            numbers = numbers.WithOverrides(digits, decimalSeparator, groupSeparator);
            if (numbers == inherited.Numbers && cardinal == inherited.CardinalRules && ordinal == inherited.OrdinalRules)
            {
                return inherited;
            }
            return new LanguageFormat(numbers, cardinal, ordinal);
        }
    }
}
