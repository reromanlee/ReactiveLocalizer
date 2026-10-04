using System;
using System.Buffers.Binary;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// Reads little-endian values from compiled data, checking every read against the end of the data. Compiled data
    /// can come from a mod folder or an interrupted download, so a damaged file must fail a read, never crash it.
    /// </summary>
    internal ref struct ByteReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public ByteReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        /// <summary>Bytes left to read.</summary>
        public int Remaining => _data.Length - _position;

        public bool TryReadUInt16(out ushort value)
        {
            if (Remaining < 2)
            {
                value = 0;
                return false;
            }
            value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_position, 2));
            _position += 2;
            return true;
        }

        public bool TryReadInt32(out int value)
        {
            if (Remaining < 4)
            {
                value = 0;
                return false;
            }
            value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return true;
        }

        public bool TryReadUInt32(out uint value)
        {
            if (Remaining < 4)
            {
                value = 0;
                return false;
            }
            value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return true;
        }

        public bool TryReadUInt64(out ulong value)
        {
            if (Remaining < 8)
            {
                value = 0;
                return false;
            }
            value = BinaryPrimitives.ReadUInt64LittleEndian(_data.Slice(_position, 8));
            _position += 8;
            return true;
        }

        public bool TryReadByte(out byte value)
        {
            if (Remaining < 1)
            {
                value = 0;
                return false;
            }
            value = _data[_position];
            _position++;
            return true;
        }

        /// <summary>Reads a count written in front of a section, rejecting negative counts and counts the remaining bytes can't hold.</summary>
        public bool TryReadCount(int bytesPerItem, out int count)
        {
            if (!TryReadInt32(out count) || count < 0 || (long)count * bytesPerItem > Remaining)
            {
                count = 0;
                return false;
            }
            return true;
        }

        /// <summary>Reads <paramref name="target"/>.Length UTF-16 code units, low byte first.</summary>
        public bool TryReadCharacters(Span<char> target)
        {
            if ((long)target.Length * 2 > Remaining)
            {
                return false;
            }
            for (int i = 0; i < target.Length; i++)
            {
                target[i] = (char)BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_position + i * 2, 2));
            }
            _position += target.Length * 2;
            return true;
        }

        /// <summary>Reads a string written by <see cref="ByteWriter.WriteString"/>.</summary>
        public bool TryReadString(out string value)
        {
            if (!TryReadCount(2, out int length))
            {
                value = null;
                return false;
            }
            if (length == 0)
            {
                value = string.Empty;
                return true;
            }
            char[] characters = new char[length];
            if (!TryReadCharacters(characters))
            {
                value = null;
                return false;
            }
            value = new string(characters);
            return true;
        }
    }
}
