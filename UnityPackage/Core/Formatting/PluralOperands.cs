using System;

namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// A number as CLDR plural rules see it: its integer digits and its visible fraction digits, so <c>1</c> and
    /// <c>1.0</c> can choose different forms, as they do in English.
    /// </summary>
    /// <remarks>
    /// The operands follow Unicode Technical Standard #35. The value itself (<c>n</c>) is implied: rules only compare
    /// it while its fraction is zero, where it equals <see cref="I"/>.
    /// </remarks>
    internal readonly struct PluralOperands
    {
        /// <summary>
        /// Added to integer parts longer than 18 digits, which keep only their last 18 digits. Rules only test small
        /// values and remainders of powers of ten, so such numbers still choose exactly the form their full digits would.
        /// </summary>
        public const ulong LargeIntegerMarker = 1_000_000_000_000_000_000UL;

        /// <summary>The largest number of fraction digits the operands keep.</summary>
        public const int MaximumFractionDigits = 18;

        /// <param name="integer">The integer digits, as <see cref="I"/>.</param>
        /// <param name="fractionDigitCount">How many fraction digits are visible, trailing zeros included.</param>
        /// <param name="fraction">The visible fraction digits as an integer, trailing zeros included.</param>
        /// <param name="exponent">The exponent of compact notation such as <c>1.2c6</c>; zero otherwise.</param>
        public PluralOperands(ulong integer, int fractionDigitCount, ulong fraction, int exponent)
        {
            I = integer;
            V = fractionDigitCount;
            F = fraction;
            int withoutZeros = fractionDigitCount;
            ulong trimmed = fraction;
            while (withoutZeros > 0 && trimmed % 10 == 0)
            {
                trimmed /= 10;
                withoutZeros--;
            }
            W = withoutZeros;
            T = trimmed;
            E = exponent;
        }

        /// <summary>The integer digits (<c>i</c>).</summary>
        public ulong I { get; }

        /// <summary>The number of visible fraction digits, with trailing zeros (<c>v</c>).</summary>
        public int V { get; }

        /// <summary>The number of visible fraction digits, without trailing zeros (<c>w</c>).</summary>
        public int W { get; }

        /// <summary>The visible fraction digits, with trailing zeros (<c>f</c>).</summary>
        public ulong F { get; }

        /// <summary>The visible fraction digits, without trailing zeros (<c>t</c>).</summary>
        public ulong T { get; }

        /// <summary>The exponent of compact notation (<c>e</c>), zero for plainly written numbers.</summary>
        public int E { get; }

        /// <summary>A deprecated synonym of <see cref="E"/> that CLDR still accepts (<c>c</c>).</summary>
        public int C => E;

        /// <summary>
        /// Reads operands from a number written in digits, such as <c>1.50</c>, <c>-3</c> or CLDR's compact samples
        /// like <c>1.1c6</c>. Fails on anything else, or on more than 18 fraction digits.
        /// </summary>
        public static bool TryParse(ReadOnlySpan<char> text, out PluralOperands operands)
        {
            operands = default;
            int position = 0;
            if (position < text.Length && text[position] == '-')
            {
                position++;
            }
            int integerStart = position;
            while (position < text.Length && IsDigit(text[position]))
            {
                position++;
            }
            ReadOnlySpan<char> integerDigits = text.Slice(integerStart, position - integerStart);
            ReadOnlySpan<char> fractionDigits = ReadOnlySpan<char>.Empty;
            if (position < text.Length && text[position] == '.')
            {
                int fractionStart = ++position;
                while (position < text.Length && IsDigit(text[position]))
                {
                    position++;
                }
                fractionDigits = text.Slice(fractionStart, position - fractionStart);
                if (fractionDigits.IsEmpty)
                {
                    return false;
                }
            }
            if (integerDigits.IsEmpty)
            {
                return false;
            }
            int exponent = 0;
            if (position < text.Length && (text[position] == 'c' || text[position] == 'e'))
            {
                int exponentStart = ++position;
                while (position < text.Length && IsDigit(text[position]) && exponent < 100)
                {
                    exponent = exponent * 10 + (text[position] - '0');
                    position++;
                }
                if (position == exponentStart)
                {
                    return false;
                }
            }
            if (position != text.Length)
            {
                return false;
            }

            // Compact notation moves the decimal point right: 1.1c6 is 1100000 with no visible fraction digits.
            int shifted = Math.Min(exponent, fractionDigits.Length);
            ReadOnlySpan<char> movedDigits = fractionDigits.Slice(0, shifted);
            ReadOnlySpan<char> remainingFraction = fractionDigits.Slice(shifted);
            if (remainingFraction.Length > MaximumFractionDigits)
            {
                return false;
            }
            ulong integer = AppendDigits(AppendDigits(0, false, integerDigits, out bool isLarge), isLarge, movedDigits, out isLarge);
            for (int i = shifted; i < exponent; i++)
            {
                integer = AppendDigit(integer, isLarge, 0, out isLarge);
            }
            ulong fraction = 0;
            for (int i = 0; i < remainingFraction.Length; i++)
            {
                fraction = fraction * 10 + (ulong)(remainingFraction[i] - '0');
            }
            operands = new PluralOperands(integer, remainingFraction.Length, fraction, exponent);
            return true;
        }

        /// <summary>Appends decimal digits to an integer part, keeping the last 18 digits of one that grows past them.</summary>
        internal static ulong AppendDigit(ulong integer, bool isLarge, int digit, out bool isNowLarge)
        {
            ulong value = (isLarge ? integer - LargeIntegerMarker : integer) * 10 + (ulong)digit;
            isNowLarge = isLarge || value >= LargeIntegerMarker;
            return isNowLarge ? value % LargeIntegerMarker + LargeIntegerMarker : value;
        }

        private static ulong AppendDigits(ulong integer, bool isLarge, ReadOnlySpan<char> digits, out bool isNowLarge)
        {
            for (int i = 0; i < digits.Length; i++)
            {
                integer = AppendDigit(integer, isLarge, digits[i] - '0', out isLarge);
            }
            isNowLarge = isLarge;
            return integer;
        }

        private static bool IsDigit(char character) => (uint)(character - '0') <= 9;
    }
}
