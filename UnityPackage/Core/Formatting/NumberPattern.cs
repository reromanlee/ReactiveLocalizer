namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// How one style of number is written in a language: the text around it, its digit grouping and how many
    /// fraction digits it shows, as read from a CLDR pattern such as <c>#,##0.###</c> or <c>#,##0&#160;%</c>.
    /// </summary>
    internal sealed class NumberPattern
    {
        /// <param name="positivePrefix">Text before a positive number.</param>
        /// <param name="positiveSuffix">Text after a positive number, such as the percent sign.</param>
        /// <param name="negativePrefix">Text before a negative number, including the minus sign.</param>
        /// <param name="negativeSuffix">Text after a negative number.</param>
        /// <param name="primaryGrouping">Digits in the group next to the decimal separator: 3 in <c>1,234,567</c>.</param>
        /// <param name="secondaryGrouping">Digits in every further group: 2 in the Indian <c>12,34,567</c>.</param>
        /// <param name="maximumFractionDigits">Fraction digits shown at most; the number is rounded half to even.</param>
        public NumberPattern(string positivePrefix, string positiveSuffix, string negativePrefix, string negativeSuffix,
            int primaryGrouping, int secondaryGrouping, int maximumFractionDigits)
        {
            PositivePrefix = positivePrefix ?? string.Empty;
            PositiveSuffix = positiveSuffix ?? string.Empty;
            NegativePrefix = negativePrefix ?? string.Empty;
            NegativeSuffix = negativeSuffix ?? string.Empty;
            PrimaryGrouping = primaryGrouping;
            SecondaryGrouping = secondaryGrouping;
            MaximumFractionDigits = maximumFractionDigits;
        }

        public string PositivePrefix { get; }

        public string PositiveSuffix { get; }

        public string NegativePrefix { get; }

        public string NegativeSuffix { get; }

        public int PrimaryGrouping { get; }

        public int SecondaryGrouping { get; }

        public int MaximumFractionDigits { get; }
    }
}
