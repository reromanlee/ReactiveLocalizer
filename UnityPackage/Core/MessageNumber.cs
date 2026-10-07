using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// A number a message shows or chooses a plural form by. Every numeric type converts to it implicitly and without
    /// boxing, so a generated method such as <c>LocalizationKeys.Shop.CoinBalance(coins: 5)</c> takes any of them.
    /// </summary>
    /// <remarks>
    /// Integers and decimals are kept exactly. A <see cref="float"/> keeps 7 significant digits and a
    /// <see cref="double"/> 15, so 0.1 + 0.2 shows as 0.3. Infinities, NaN and doubles beyond the range of
    /// <see cref="decimal"/> are kept as they are and formatted as such.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct MessageNumber : IEquatable<MessageNumber>
    {
        // Doubles beyond this can't become decimals; they're kept as doubles and are whole numbers anyway.
        private const double DecimalLimit = 7.9e28;

        [FieldOffset(0)] private readonly decimal _decimal;
        [FieldOffset(0)] private readonly double _real;
        [FieldOffset(16)] private readonly bool _isReal;

        private MessageNumber(decimal value)
        {
            _real = 0d;
            _isReal = false;
            _decimal = value;
        }

        private MessageNumber(double value)
        {
            _decimal = 0m;
            _real = value;
            _isReal = true;
        }

        /// <summary>Whether the number is finite: neither an infinity nor NaN.</summary>
        public bool IsFinite => !_isReal || (!double.IsNaN(_real) && !double.IsInfinity(_real));

        /// <summary>Whether the number is held exactly as a decimal, which every finite number in decimal's range is.</summary>
        internal bool IsDecimal => !_isReal;

        internal decimal DecimalValue => _decimal;

        internal double RealValue => _real;

        /// <summary>Returns the number as a decimal. Fails for infinities, NaN and doubles beyond the range of decimal.</summary>
        public bool TryGetDecimal(out decimal value)
        {
            value = _isReal ? 0m : _decimal;
            return !_isReal;
        }

        /// <summary>Returns the number as a double, which may round decimals with more than 15 significant digits.</summary>
        public double ToDouble() => _isReal ? _real : (double)_decimal;

        /// <inheritdoc/>
        public bool Equals(MessageNumber other) => _isReal ? other._isReal && _real.Equals(other._real) : !other._isReal && _decimal == other._decimal;

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is MessageNumber other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => _isReal ? _real.GetHashCode() : _decimal.GetHashCode();

        /// <summary>Returns the number with invariant symbols, for logs and the debugger.</summary>
        public override string ToString() => _isReal ? _real.ToString("R", CultureInfo.InvariantCulture) : _decimal.ToString(CultureInfo.InvariantCulture);

        public static bool operator ==(MessageNumber left, MessageNumber right) => left.Equals(right);

        public static bool operator !=(MessageNumber left, MessageNumber right) => !left.Equals(right);

        public static implicit operator MessageNumber(sbyte value) => new((decimal)value);

        public static implicit operator MessageNumber(byte value) => new((decimal)value);

        public static implicit operator MessageNumber(short value) => new((decimal)value);

        public static implicit operator MessageNumber(ushort value) => new((decimal)value);

        public static implicit operator MessageNumber(int value) => new((decimal)value);

        public static implicit operator MessageNumber(uint value) => new((decimal)value);

        public static implicit operator MessageNumber(long value) => new((decimal)value);

        public static implicit operator MessageNumber(ulong value) => new((decimal)value);

        public static implicit operator MessageNumber(decimal value) => new(value);

        public static implicit operator MessageNumber(float value) =>
            IsInDecimalRange(value) ? new MessageNumber(new decimal(value)) : new MessageNumber((double)value);

        public static implicit operator MessageNumber(double value) =>
            IsInDecimalRange(value) ? new MessageNumber(new decimal(value)) : new MessageNumber(value);

        private static bool IsInDecimalRange(double value) => value > -DecimalLimit && value < DecimalLimit;
    }
}
