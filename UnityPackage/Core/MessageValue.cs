using System;
using System.Globalization;
using System.Runtime.InteropServices;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The value of a message argument: a string, a number of any numeric type, a date, a duration or any object.
    /// Strings, numbers, dates and durations convert to it implicitly and without boxing.
    /// </summary>
    /// <remarks>Formatters registered for types such as <c>date</c> read it with the <c>TryGet</c> methods.</remarks>
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct MessageValue : IEquatable<MessageValue>
    {
        [FieldOffset(0)] private readonly decimal _decimal;
        [FieldOffset(0)] private readonly double _real;
        [FieldOffset(0)] private readonly long _ticks;
        [FieldOffset(16)] private readonly object _reference;
        [FieldOffset(24)] private readonly Storage _storage;

        private MessageValue(Storage storage, decimal number, double real, long ticks, object reference)
        {
            _decimal = 0m;
            _real = 0d;
            _ticks = 0L;
            // The three share their storage, so only the one this kind uses is written last.
            switch (storage)
            {
                case Storage.Decimal:
                    _decimal = number;
                    break;
                case Storage.Real:
                    _real = real;
                    break;
                case Storage.DateTime:
                case Storage.TimeSpan:
                    _ticks = ticks;
                    break;
            }
            _reference = reference;
            _storage = storage;
        }

        /// <summary>What the value holds.</summary>
        public MessageValueKind Kind => _storage switch
        {
            Storage.Text => MessageValueKind.Text,
            Storage.Decimal or Storage.Real => MessageValueKind.Number,
            Storage.DateTime => MessageValueKind.DateTime,
            Storage.TimeSpan => MessageValueKind.TimeSpan,
            Storage.Object => MessageValueKind.Object,
            _ => MessageValueKind.Empty
        };

        /// <summary>
        /// Creates a value from any object: strings, boxed numbers, dates and durations become the matching kind, null
        /// becomes empty, and anything else is kept as an object for a registered formatter.
        /// </summary>
        public static MessageValue FromObject(object value)
        {
            return value switch
            {
                null => default,
                string text => text,
                MessageValue messageValue => messageValue,
                MessageNumber number => number,
                int number => number,
                long number => number,
                double number => number,
                float number => number,
                decimal number => number,
                short number => number,
                ushort number => number,
                uint number => number,
                ulong number => number,
                byte number => number,
                sbyte number => number,
                DateTime dateTime => dateTime,
                TimeSpan timeSpan => timeSpan,
                _ => new MessageValue(Storage.Object, 0m, 0d, 0L, value)
            };
        }

        /// <summary>Returns the string the value holds.</summary>
        public bool TryGetText(out string text)
        {
            text = _storage == Storage.Text ? (string)_reference : null;
            return text != null;
        }

        /// <summary>Returns the number the value holds.</summary>
        public bool TryGetNumber(out MessageNumber number)
        {
            number = _storage switch
            {
                Storage.Decimal => _decimal,
                Storage.Real => _real,
                _ => default
            };
            return _storage == Storage.Decimal || _storage == Storage.Real;
        }

        /// <summary>Returns the date the value holds, with its <see cref="DateTimeKind"/>.</summary>
        public bool TryGetDateTime(out DateTime dateTime)
        {
            dateTime = _storage == Storage.DateTime ? DateTime.FromBinary(_ticks) : default;
            return _storage == Storage.DateTime;
        }

        /// <summary>Returns the duration the value holds.</summary>
        public bool TryGetTimeSpan(out TimeSpan timeSpan)
        {
            timeSpan = _storage == Storage.TimeSpan ? new TimeSpan(_ticks) : default;
            return _storage == Storage.TimeSpan;
        }

        /// <summary>Returns the object a value made by <see cref="FromObject"/> holds, when it is none of the other kinds.</summary>
        public bool TryGetObject(out object value)
        {
            value = _storage == Storage.Object ? _reference : null;
            return _storage == Storage.Object;
        }

        /// <inheritdoc/>
        public bool Equals(MessageValue other)
        {
            if (_storage != other._storage)
            {
                return false;
            }
            return _storage switch
            {
                Storage.Text => string.Equals((string)_reference, (string)other._reference, StringComparison.Ordinal),
                Storage.Decimal => _decimal == other._decimal,
                Storage.Real => _real.Equals(other._real),
                Storage.DateTime or Storage.TimeSpan => _ticks == other._ticks,
                Storage.Object => Equals(_reference, other._reference),
                _ => true
            };
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is MessageValue other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => _storage switch
        {
            Storage.Text or Storage.Object => _reference?.GetHashCode() ?? 0,
            Storage.Decimal => _decimal.GetHashCode(),
            Storage.Real => _real.GetHashCode(),
            Storage.DateTime or Storage.TimeSpan => _ticks.GetHashCode(),
            _ => 0
        };

        /// <summary>Returns the value with invariant formatting, for logs and the debugger.</summary>
        public override string ToString() => _storage switch
        {
            Storage.Text => (string)_reference,
            Storage.Decimal => _decimal.ToString(CultureInfo.InvariantCulture),
            Storage.Real => _real.ToString("R", CultureInfo.InvariantCulture),
            Storage.DateTime => DateTime.FromBinary(_ticks).ToString("o", CultureInfo.InvariantCulture),
            Storage.TimeSpan => new TimeSpan(_ticks).ToString("c", CultureInfo.InvariantCulture),
            Storage.Object => _reference?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        public static bool operator ==(MessageValue left, MessageValue right) => left.Equals(right);

        public static bool operator !=(MessageValue left, MessageValue right) => !left.Equals(right);

        public static implicit operator MessageValue(string value) =>
            value == null ? default : new MessageValue(Storage.Text, 0m, 0d, 0L, value);

        public static implicit operator MessageValue(MessageNumber value) =>
            value.IsDecimal ? new MessageValue(Storage.Decimal, value.DecimalValue, 0d, 0L, null) : new MessageValue(Storage.Real, 0m, value.RealValue, 0L, null);

        public static implicit operator MessageValue(DateTime value) => new(Storage.DateTime, 0m, 0d, value.ToBinary(), null);

        public static implicit operator MessageValue(TimeSpan value) => new(Storage.TimeSpan, 0m, 0d, value.Ticks, null);

        public static implicit operator MessageValue(sbyte value) => (MessageNumber)value;

        public static implicit operator MessageValue(byte value) => (MessageNumber)value;

        public static implicit operator MessageValue(short value) => (MessageNumber)value;

        public static implicit operator MessageValue(ushort value) => (MessageNumber)value;

        public static implicit operator MessageValue(int value) => (MessageNumber)value;

        public static implicit operator MessageValue(uint value) => (MessageNumber)value;

        public static implicit operator MessageValue(long value) => (MessageNumber)value;

        public static implicit operator MessageValue(ulong value) => (MessageNumber)value;

        public static implicit operator MessageValue(float value) => (MessageNumber)value;

        public static implicit operator MessageValue(double value) => (MessageNumber)value;

        public static implicit operator MessageValue(decimal value) => (MessageNumber)value;

        private enum Storage : byte
        {
            Empty,
            Text,
            Decimal,
            Real,
            DateTime,
            TimeSpan,
            Object
        }
    }
}
