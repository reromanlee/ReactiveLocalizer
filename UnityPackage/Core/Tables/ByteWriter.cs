using System;
using System.Buffers.Binary;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// Appends little-endian values to a growing byte buffer. The compiled formats are written with it, so their byte
    /// order never depends on the machine that compiled them.
    /// </summary>
    internal sealed class ByteWriter
    {
        private byte[] _buffer;
        private int _length;

        public ByteWriter(int initialCapacity)
        {
            _buffer = new byte[Math.Max(initialCapacity, 64)];
        }

        public int Length => _length;

        public void WriteUInt16(ushort value)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(Reserve(2), value);
        }

        public void WriteInt32(int value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(Reserve(4), value);
        }

        public void WriteUInt32(uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(Reserve(4), value);
        }

        public void WriteUInt64(ulong value)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(Reserve(8), value);
        }

        public void WriteByte(byte value)
        {
            Reserve(1)[0] = value;
        }

        /// <summary>Writes UTF-16 code units, low byte first, with no length in front of them.</summary>
        public void WriteCharacters(ReadOnlySpan<char> characters)
        {
            Span<byte> target = Reserve(characters.Length * 2);
            for (int i = 0; i < characters.Length; i++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(target.Slice(i * 2, 2), characters[i]);
            }
        }

        /// <summary>Writes a string as its length in code units, then its UTF-16 code units. A null string writes as empty.</summary>
        public void WriteString(string value)
        {
            string text = value ?? string.Empty;
            WriteInt32(text.Length);
            WriteCharacters(text);
        }

        /// <summary>Returns a copy of the written bytes, exactly as long as what was written.</summary>
        public byte[] ToArray()
        {
            byte[] result = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, result, 0, _length);
            return result;
        }

        private Span<byte> Reserve(int count)
        {
            if (_length + count > _buffer.Length)
            {
                byte[] grown = new byte[Math.Max(_buffer.Length * 2, _length + count)];
                Buffer.BlockCopy(_buffer, 0, grown, 0, _length);
                _buffer = grown;
            }
            Span<byte> span = _buffer.AsSpan(_length, count);
            _length += count;
            return span;
        }
    }
}
