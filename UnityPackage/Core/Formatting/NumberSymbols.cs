using System;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// How a language writes numbers: its separators, its digits and its patterns, as CLDR defines them for a locale
    /// or as a catalog overrides them for one of its languages.
    /// </summary>
    /// <remarks>Never changes after it is created, so any number of threads can format with it at once.</remarks>
    internal sealed class NumberSymbols
    {
        private static readonly NumberSymbols[] Shared = new NumberSymbols[CldrNumberSymbols.SetCount];

        /// <param name="decimalSeparator">Text between the integer and fraction digits.</param>
        /// <param name="groupSeparator">Text between digit groups; empty to never group.</param>
        /// <param name="digits">The digits zero to nine: ten characters, or ten surrogate pairs outside the BMP.</param>
        /// <param name="minimumGrouping">Digits the leftmost group needs before grouping starts: 2 writes 1234 but 12.345.</param>
        /// <param name="decimalPattern">How plain numbers are written.</param>
        /// <param name="percentPattern">How percentages are written.</param>
        public NumberSymbols(string decimalSeparator, string groupSeparator, string digits, int minimumGrouping,
            NumberPattern decimalPattern, NumberPattern percentPattern)
        {
            DecimalSeparator = decimalSeparator ?? string.Empty;
            GroupSeparator = groupSeparator ?? string.Empty;
            Digits = digits;
            MinimumGrouping = Math.Max(1, minimumGrouping);
            Decimal = decimalPattern;
            Percent = percentPattern;
        }

        /// <summary>The symbols of CLDR's root locale, for languages with neither a culture nor a fallback.</summary>
        public static NumberSymbols Root => GetShared(CldrNumberSymbols.RootSet);

        public string DecimalSeparator { get; }

        public string GroupSeparator { get; }

        /// <summary>The digits zero to nine, one or two characters each, as <see cref="GetDigit"/> reads them.</summary>
        public string Digits { get; }

        public int MinimumGrouping { get; }

        public NumberPattern Decimal { get; }

        public NumberPattern Percent { get; }

        /// <summary>Returns whether <paramref name="digits"/> holds the ten digits zero to nine, each one character or one surrogate pair.</summary>
        public static bool AreValidDigits(string digits)
        {
            if (digits == null)
            {
                return false;
            }
            if (digits.Length == 10)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (char.IsSurrogate(digits[i]))
                    {
                        return false;
                    }
                }
                return true;
            }
            if (digits.Length == 20)
            {
                for (int i = 0; i < 20; i += 2)
                {
                    if (!char.IsSurrogatePair(digits[i], digits[i + 1]))
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Finds the symbols of a culture, trying the tag and then each shorter form of it, such as <c>de-CH</c> then
        /// <c>de</c>. Fails for a culture CLDR doesn't know, so its language can inherit its fallback's symbols instead.
        /// </summary>
        public static bool TryFind(string culture, out NumberSymbols symbols)
        {
            string tag = LanguageTags.Normalize(culture);
            while (!string.IsNullOrEmpty(tag))
            {
                int set = CldrNumberSymbols.Find(tag);
                if (set >= 0)
                {
                    symbols = GetShared(set);
                    return true;
                }
                tag = LanguageTags.Truncate(tag);
            }
            symbols = null;
            return false;
        }

        /// <summary>Returns the digit <paramref name="digit"/> (0 to 9) in this language's digits.</summary>
        public ReadOnlySpan<char> GetDigit(int digit)
        {
            int width = Digits.Length / 10;
            return Digits.AsSpan(digit * width, width);
        }

        /// <summary>
        /// Returns these symbols with the given ones replaced; a null argument keeps the current symbol. Returns the
        /// same instance when nothing changes.
        /// </summary>
        public NumberSymbols WithOverrides(string digits, string decimalSeparator, string groupSeparator)
        {
            string newDigits = digits ?? Digits;
            string newDecimal = decimalSeparator ?? DecimalSeparator;
            string newGroup = groupSeparator ?? GroupSeparator;
            if (newDigits == Digits && newDecimal == DecimalSeparator && newGroup == GroupSeparator)
            {
                return this;
            }
            return new NumberSymbols(newDecimal, newGroup, newDigits, MinimumGrouping, Decimal, Percent);
        }

        private static NumberSymbols GetShared(int set)
        {
            NumberSymbols symbols = Volatile.Read(ref Shared[set]);
            if (symbols != null)
            {
                return symbols;
            }
            // Two threads may both create a set; either result is equal, and one of them is kept.
            symbols = CldrNumberSymbols.Create(set);
            return Interlocked.CompareExchange(ref Shared[set], symbols, null) ?? symbols;
        }
    }
}
