using System;
using System.Buffers;

namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// Builds text in a buffer the caller provides, usually on the stack, and only rents a larger one from the shared
    /// pool when the text outgrows it. Formatting a message therefore allocates nothing but the final string, and
    /// nothing at all when the caller copies the characters out.
    /// </summary>
    /// <remarks>Dispose it to return a rented buffer to the pool; the text must not be read afterwards.</remarks>
    internal ref struct TextBuilder
    {
        private Span<char> _characters;
        private char[] _rented;
        private int _length;

        public TextBuilder(Span<char> initialBuffer)
        {
            _characters = initialBuffer;
            _rented = null;
            _length = 0;
        }

        /// <summary>How many characters were written.</summary>
        public int Length => _length;

        /// <summary>Whether the text outgrew the initial buffer, so it no longer lives there.</summary>
        public bool HasOutgrownInitialBuffer => _rented != null;

        /// <summary>The characters written so far.</summary>
        public ReadOnlySpan<char> Text => _characters.Slice(0, _length);

        public void Append(char character)
        {
            if (_length == _characters.Length)
            {
                Grow(1);
            }
            _characters[_length] = character;
            _length++;
        }

        public void Append(ReadOnlySpan<char> text)
        {
            if (text.Length > _characters.Length - _length)
            {
                Grow(text.Length);
            }
            text.CopyTo(_characters.Slice(_length));
            _length += text.Length;
        }

        public void Append(string text)
        {
            if (text != null)
            {
                Append(text.AsSpan());
            }
        }

        /// <summary>Returns the free space after the text, at least <paramref name="minimumLength"/> characters long.</summary>
        public Span<char> GetFreeSpace(int minimumLength)
        {
            if (minimumLength > _characters.Length - _length)
            {
                Grow(minimumLength);
            }
            return _characters.Slice(_length);
        }

        /// <summary>Counts <paramref name="count"/> characters written into the space <see cref="GetFreeSpace"/> returned.</summary>
        public void Advance(int count)
        {
            if (count < 0 || count > _characters.Length - _length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            _length += count;
        }

        /// <summary>Removes the characters written after the first <paramref name="length"/>.</summary>
        public void Truncate(int length)
        {
            if ((uint)length <= (uint)_length)
            {
                _length = length;
            }
        }

        public override string ToString() => _length == 0 ? string.Empty : new string(_characters.Slice(0, _length));

        public void Dispose()
        {
            char[] rented = _rented;
            _rented = null;
            _characters = default;
            _length = 0;
            if (rented != null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }

        private void Grow(int additionalLength)
        {
            int capacity = Math.Max(Math.Max(_characters.Length * 2, 256), _length + additionalLength);
            char[] grown = ArrayPool<char>.Shared.Rent(capacity);
            _characters.Slice(0, _length).CopyTo(grown);
            char[] previous = _rented;
            _rented = grown;
            _characters = grown;
            if (previous != null)
            {
                ArrayPool<char>.Shared.Return(previous);
            }
        }
    }
}
