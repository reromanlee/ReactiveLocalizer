using System;
using System.Globalization;

namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// Writes numbers the way a language writes them, from CLDR data alone, so the same message formats the same way
    /// on every device whatever its culture settings. Rounds half to even, as ICU does.
    /// </summary>
    /// <remarks>Allocates nothing for numbers within the range of <see cref="decimal"/>.</remarks>
    internal static class NumberFormatter
    {
        private const char Infinity = (char)0x221E;

        private static readonly ulong[] WholePowersOfTen =
        {
            1UL, 10UL, 100UL, 1000UL, 10000UL, 100000UL, 1000000UL, 10000000UL, 100000000UL, 1000000000UL, 10000000000UL,
            100000000000UL, 1000000000000UL, 10000000000000UL, 100000000000000UL, 1000000000000000UL, 10000000000000000UL,
            100000000000000000UL, 1000000000000000000UL, 10000000000000000000UL
        };

        private static readonly decimal[] PowersOfTen =
        {
            1m, 10m, 100m, 1000m, 10000m, 100000m, 1000000m, 10000000m, 100000000m, 1000000000m, 10000000000m,
            100000000000m, 1000000000000m, 10000000000000m, 100000000000000m, 1000000000000000m, 10000000000000000m,
            100000000000000000m, 1000000000000000000m
        };

        /// <summary>Appends <paramref name="number"/> in <paramref name="style"/> with the symbols of a language.</summary>
        public static void Append(ref TextBuilder output, in MessageNumber number, NumberStyle style, NumberSymbols symbols)
        {
            NumberPattern pattern = style == NumberStyle.Percent ? symbols.Percent : symbols.Decimal;
            int maximumFraction = style == NumberStyle.Integer ? 0 : Math.Min(pattern.MaximumFractionDigits, PowersOfTen.Length - 1);
            if (number.IsDecimal)
            {
                decimal value = number.DecimalValue;
                if (style != NumberStyle.Percent)
                {
                    AppendDecimal(ref output, value, pattern, maximumFraction, symbols);
                    return;
                }
                if (Math.Abs(value) < 7.9e26m)
                {
                    AppendDecimal(ref output, value * 100m, pattern, maximumFraction, symbols);
                    return;
                }
            }
            double real = number.ToDouble();
            AppendBeyondDecimal(ref output, style == NumberStyle.Percent ? real * 100d : real, pattern, symbols);
        }

        /// <summary>
        /// Returns the plural operands of <paramref name="number"/> as the default style shows it, rounded to the same
        /// fraction digits, so the form always matches the digits shown. Fails for infinities and NaN, which take
        /// the <c>other</c> form.
        /// </summary>
        public static bool TryGetOperands(in MessageNumber number, NumberSymbols symbols, out PluralOperands operands)
        {
            if (number.IsDecimal)
            {
                int maximumFraction = Math.Min(symbols.Decimal.MaximumFractionDigits, PowersOfTen.Length - 1);
                Round(number.DecimalValue, maximumFraction, out decimal integerPart, out ulong fraction, out int fractionDigits);
                ulong integer = integerPart < PluralOperands.LargeIntegerMarker
                    ? (ulong)integerPart
                    : (ulong)decimal.Remainder(integerPart, PluralOperands.LargeIntegerMarker) + PluralOperands.LargeIntegerMarker;
                operands = new PluralOperands(integer, fractionDigits, fraction, 0);
                return true;
            }
            double real = Math.Abs(number.RealValue);
            if (double.IsNaN(real) || double.IsInfinity(real))
            {
                operands = default;
                return false;
            }
            // Doubles beyond decimal's range are whole numbers; their last 18 digits decide every CLDR rule.
            operands = new PluralOperands((ulong)(real % 1e18) + PluralOperands.LargeIntegerMarker, 0, 0, 0);
            return true;
        }

        /// <summary>Returns <paramref name="number"/> minus a plural message's offset.</summary>
        public static MessageNumber Subtract(in MessageNumber number, double offset)
        {
            if (offset == 0d)
            {
                return number;
            }
            if (number.IsDecimal && Math.Abs(number.DecimalValue) < 7.9e27m)
            {
                return number.DecimalValue - (decimal)offset;
            }
            return number.ToDouble() - offset;
        }

        /// <summary>Returns whether <paramref name="number"/> equals a plural message's explicit value, such as <c>=0</c>.</summary>
        public static bool IsEqual(in MessageNumber number, double value)
        {
            if (number.IsDecimal)
            {
                return Math.Abs(value) < 7.9e27 && number.DecimalValue == (decimal)value;
            }
            return number.RealValue.Equals(value);
        }

        private static void AppendDecimal(ref TextBuilder output, decimal value, NumberPattern pattern, int maximumFraction, NumberSymbols symbols)
        {
            // A negative number that rounds to zero keeps its sign, as ICU writes it: -0.
            bool isNegative = value < 0m;
            Round(value, maximumFraction, out decimal integerPart, out ulong fraction, out int fractionDigits);

            output.Append(isNegative ? pattern.NegativePrefix : pattern.PositivePrefix);
            if (integerPart <= ulong.MaxValue)
            {
                AppendGrouped(ref output, (ulong)integerPart, pattern, symbols);
            }
            else
            {
                AppendGrouped(ref output, GetDigits(integerPart), pattern, symbols);
            }
            if (fractionDigits > 0)
            {
                output.Append(symbols.DecimalSeparator);
                for (int i = fractionDigits - 1; i >= 0; i--)
                {
                    output.Append(symbols.GetDigit((int)(fraction / WholePowersOfTen[i] % 10)));
                }
            }
            output.Append(isNegative ? pattern.NegativeSuffix : pattern.PositiveSuffix);
        }

        /// <summary>Formats numbers decimal can't hold: infinities, NaN and doubles of 29 digits and more.</summary>
        private static void AppendBeyondDecimal(ref TextBuilder output, double value, NumberPattern pattern, NumberSymbols symbols)
        {
            if (double.IsNaN(value))
            {
                output.Append("NaN");
                return;
            }
            bool isNegative = value < 0d;
            output.Append(isNegative ? pattern.NegativePrefix : pattern.PositivePrefix);
            if (double.IsInfinity(value))
            {
                output.Append(Infinity);
            }
            else
            {
                // Rare enough to allocate: the shortest round-trip form, such as 1.2E+30, spelled out digit by digit.
                string text = Math.Abs(value).ToString("R", CultureInfo.InvariantCulture);
                int exponentStart = text.IndexOf('E');
                string mantissa = exponentStart < 0 ? text : text.Substring(0, exponentStart);
                int exponent = exponentStart < 0 ? 0 : int.Parse(text.Substring(exponentStart + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                int point = mantissa.IndexOf('.');
                string significant = point < 0 ? mantissa : mantissa.Remove(point, 1);
                int integerDigits = (point < 0 ? mantissa.Length : point) + exponent;
                byte[] digits = new byte[Math.Max(integerDigits, 1)];
                for (int i = 0; i < digits.Length; i++)
                {
                    digits[i] = i < significant.Length ? (byte)(significant[i] - '0') : (byte)0;
                }
                AppendGrouped(ref output, digits, pattern, symbols);
            }
            output.Append(isNegative ? pattern.NegativeSuffix : pattern.PositiveSuffix);
        }

        /// <summary>Rounds the magnitude of <paramref name="value"/> half to even, then splits it into its integer part and its fraction digits without trailing zeros.</summary>
        private static void Round(decimal value, int maximumFraction, out decimal integerPart, out ulong fraction, out int fractionDigits)
        {
            decimal rounded = decimal.Round(Math.Abs(value), maximumFraction, MidpointRounding.ToEven);
            integerPart = decimal.Truncate(rounded);
            fraction = (ulong)((rounded - integerPart) * PowersOfTen[maximumFraction]);
            fractionDigits = maximumFraction;
            while (fractionDigits > 0 && fraction % 10 == 0)
            {
                fraction /= 10;
                fractionDigits--;
            }
        }

        /// <summary>Returns the digits of a whole number too large for <see cref="ulong"/>, most significant first.</summary>
        private static byte[] GetDigits(decimal integerPart)
        {
            // Rare enough to allocate: only decimals of 20 digits and more come here.
            byte[] reversed = new byte[30];
            int count = 0;
            decimal remaining = integerPart;
            while (remaining != 0m && count < reversed.Length)
            {
                decimal digit = decimal.Remainder(remaining, 10m);
                reversed[count++] = (byte)digit;
                remaining = (remaining - digit) / 10m;
            }
            byte[] digits = new byte[Math.Max(count, 1)];
            for (int i = 0; i < count; i++)
            {
                digits[i] = reversed[count - 1 - i];
            }
            return digits;
        }

        /// <summary>Appends a whole number in the language's digits, with group separators where the pattern places them.</summary>
        private static void AppendGrouped(ref TextBuilder output, ulong value, NumberPattern pattern, NumberSymbols symbols)
        {
            int count = 1;
            while (count < WholePowersOfTen.Length && value >= WholePowersOfTen[count])
            {
                count++;
            }
            bool isGrouped = IsGrouped(count, pattern, symbols);
            for (int i = 0; i < count; i++)
            {
                int following = count - 1 - i;
                output.Append(symbols.GetDigit((int)(value / WholePowersOfTen[following] % 10)));
                if (isGrouped && IsGroupEnd(following, pattern))
                {
                    output.Append(symbols.GroupSeparator);
                }
            }
        }

        /// <summary>Appends digits in the language's digits, with group separators where the pattern places them.</summary>
        private static void AppendGrouped(ref TextBuilder output, byte[] digits, NumberPattern pattern, NumberSymbols symbols)
        {
            bool isGrouped = IsGrouped(digits.Length, pattern, symbols);
            for (int i = 0; i < digits.Length; i++)
            {
                int following = digits.Length - 1 - i;
                output.Append(symbols.GetDigit(digits[i]));
                if (isGrouped && IsGroupEnd(following, pattern))
                {
                    output.Append(symbols.GroupSeparator);
                }
            }
        }

        /// <summary>Whether a number of <paramref name="digitCount"/> integer digits is grouped: the leftmost group needs the language's minimum digits.</summary>
        private static bool IsGrouped(int digitCount, NumberPattern pattern, NumberSymbols symbols) =>
            pattern.PrimaryGrouping > 0 && symbols.GroupSeparator.Length > 0 && digitCount >= pattern.PrimaryGrouping + symbols.MinimumGrouping;

        /// <summary>Whether a group separator follows the digit that has <paramref name="following"/> digits after it.</summary>
        private static bool IsGroupEnd(int following, NumberPattern pattern)
        {
            int primary = pattern.PrimaryGrouping;
            int secondary = pattern.SecondaryGrouping > 0 ? pattern.SecondaryGrouping : primary;
            return following == primary || (following > primary && (following - primary) % secondary == 0);
        }
    }
}
