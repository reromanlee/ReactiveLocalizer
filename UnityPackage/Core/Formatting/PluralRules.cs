namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// Finds the CLDR plural rules of a culture and applies them. Rules are referred to by the number of their rule
    /// set in the generated data, where <see cref="OtherOnly"/> is the set of languages without plural forms.
    /// </summary>
    internal static class PluralRules
    {
        /// <summary>The rule set that chooses <see cref="PluralCategory.Other"/> for every number.</summary>
        public const int OtherOnly = 0;

        /// <summary>Finds the cardinal rules of a culture, trying the tag and then each shorter form of it.</summary>
        public static bool TryFindCardinal(string culture, out int rules) => TryFind(culture, false, out rules);

        /// <summary>Finds the ordinal rules of a culture, trying the tag and then each shorter form of it.</summary>
        public static bool TryFindOrdinal(string culture, out int rules) => TryFind(culture, true, out rules);

        /// <summary>Returns the form a count takes: <c>one</c> for 1 apple, <c>few</c> for 2 Russian apples.</summary>
        public static PluralCategory SelectCardinal(int rules, in PluralOperands operands) => CldrPluralRules.SelectCardinal(rules, in operands);

        /// <summary>Returns the form a position takes: <c>two</c> for the English 2nd.</summary>
        public static PluralCategory SelectOrdinal(int rules, in PluralOperands operands) => CldrPluralRules.SelectOrdinal(rules, in operands);

        /// <summary>Returns the forms a cardinal rule set uses, as a set of <see cref="PluralCategories"/>.</summary>
        public static int GetCardinalCategories(int rules) => CldrPluralRules.GetCardinalCategories(rules);

        /// <summary>Returns the forms an ordinal rule set uses, as a set of <see cref="PluralCategories"/>.</summary>
        public static int GetOrdinalCategories(int rules) => CldrPluralRules.GetOrdinalCategories(rules);

        private static bool TryFind(string culture, bool isOrdinal, out int rules)
        {
            string tag = LanguageTags.Normalize(culture);
            while (!string.IsNullOrEmpty(tag))
            {
                int found = isOrdinal ? CldrPluralRules.FindOrdinal(tag) : CldrPluralRules.FindCardinal(tag);
                if (found >= 0)
                {
                    rules = found;
                    return true;
                }
                tag = LanguageTags.Truncate(tag);
            }
            rules = OtherOnly;
            return false;
        }
    }
}
