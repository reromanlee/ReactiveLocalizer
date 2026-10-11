using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// The zip files XLSX workbooks are stored in: writing entries compressed with Deflate, and reading the stored or
    /// deflated entries every spreadsheet application writes. Encrypted, split and Zip64 archives are reported rather
    /// than misread.
    /// </summary>
    /// <remarks>
    /// Written entries carry a fixed date, so the same workbook always produces the same entries. Reading checks every
    /// entry's checksum, and refuses entries that claim to unpack beyond <see cref="MaximumEntrySize"/>.
    /// </remarks>
    internal static class ZipContainer
    {
        /// <summary>The largest entry reading unpacks, which keeps a damaged or hostile archive from exhausting memory.</summary>
        public const int MaximumEntrySize = 256 * 1024 * 1024;

        private const uint LocalHeaderSignature = 0x04034B50;
        private const uint CentralHeaderSignature = 0x02014B50;
        private const uint EndSignature = 0x06054B50;
        private const ushort Version = 20;
        private const ushort Stored = 0;
        private const ushort Deflated = 8;
        // MS-DOS date of 1980-01-01, the earliest a zip entry can carry.
        private const ushort FixedDate = (1 << 5) | 1;

        /// <summary>Returns an archive holding <paramref name="entries"/>, each a path inside the archive and its bytes, in order.</summary>
        public static byte[] Write(IReadOnlyList<KeyValuePair<string, byte[]>> entries)
        {
            using MemoryStream archive = new();
            using BinaryWriter writer = new(archive, Encoding.UTF8, true);
            List<(byte[] Name, uint Checksum, ushort Method, int PackedSize, int Size, int Offset)> written = new(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                byte[] name = Encoding.UTF8.GetBytes(entries[i].Key);
                byte[] data = entries[i].Value ?? Array.Empty<byte>();
                byte[] packed = Compress(data);
                // Data that doesn't shrink, as tiny entries don't, is stored as it is.
                ushort method = packed.Length < data.Length ? Deflated : Stored;
                byte[] content = method == Deflated ? packed : data;
                uint checksum = Crc32.Compute(data);
                int offset = (int)archive.Position;
                writer.Write(LocalHeaderSignature);
                WriteCommonHeader(writer, NameFlags(entries[i].Key), method, checksum, content.Length, data.Length, name.Length);
                writer.Write((ushort)0);
                writer.Write(name);
                writer.Write(content);
                written.Add((name, checksum, method, content.Length, data.Length, offset));
            }
            int directoryOffset = (int)archive.Position;
            for (int i = 0; i < written.Count; i++)
            {
                var entry = written[i];
                writer.Write(CentralHeaderSignature);
                writer.Write(Version);
                WriteCommonHeader(writer, NameFlags(entries[i].Key), entry.Method, entry.Checksum, entry.PackedSize, entry.Size, entry.Name.Length);
                writer.Write((ushort)0); // Extra field length.
                writer.Write((ushort)0); // Comment length.
                writer.Write((ushort)0); // Disk number.
                writer.Write((ushort)0); // Internal attributes.
                writer.Write(0u); // External attributes.
                writer.Write((uint)entry.Offset);
                writer.Write(entry.Name);
            }
            int directorySize = (int)archive.Position - directoryOffset;
            writer.Write(EndSignature);
            writer.Write((ushort)0);
            writer.Write((ushort)0);
            writer.Write((ushort)written.Count);
            writer.Write((ushort)written.Count);
            writer.Write((uint)directorySize);
            writer.Write((uint)directoryOffset);
            writer.Write((ushort)0);
            writer.Flush();
            return archive.ToArray();
        }

        /// <summary>
        /// Reads every file entry of <paramref name="archive"/> by its path, ignoring case as package parts do. False,
        /// with <paramref name="problem"/> saying why, when the archive can't be read.
        /// </summary>
        public static bool TryRead(byte[] archive, out Dictionary<string, byte[]> entries, out string problem)
        {
            entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                return TryReadEntries(archive, entries, out problem);
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is IOException || exception is ArgumentException)
            {
                problem = $"it is damaged ({exception.Message})";
                return false;
            }
        }

        private static bool TryReadEntries(byte[] archive, Dictionary<string, byte[]> entries, out string problem)
        {
            int end = FindEnd(archive);
            if (end < 0)
            {
                problem = "it isn't a zip archive, which every workbook is";
                return false;
            }
            int count = ReadUInt16(archive, end + 10);
            uint directorySize = ReadUInt32(archive, end + 12);
            uint directoryOffset = ReadUInt32(archive, end + 16);
            if (ReadUInt16(archive, end + 4) != 0 || count == 0xFFFF || directorySize == uint.MaxValue || directoryOffset == uint.MaxValue)
            {
                problem = "it is a split or Zip64 archive, which workbooks never are";
                return false;
            }
            int position = (int)directoryOffset;
            for (int i = 0; i < count; i++)
            {
                if (position + 46 > archive.Length || ReadUInt32(archive, position) != CentralHeaderSignature)
                {
                    problem = "its list of entries is damaged";
                    return false;
                }
                ushort flags = ReadUInt16(archive, position + 8);
                ushort method = ReadUInt16(archive, position + 10);
                uint checksum = ReadUInt32(archive, position + 16);
                uint packedSize = ReadUInt32(archive, position + 20);
                uint size = ReadUInt32(archive, position + 24);
                int nameLength = ReadUInt16(archive, position + 28);
                int extraLength = ReadUInt16(archive, position + 30);
                int commentLength = ReadUInt16(archive, position + 32);
                uint localOffset = ReadUInt32(archive, position + 42);
                string name = Encoding.UTF8.GetString(archive, position + 46, nameLength);
                position += 46 + nameLength + extraLength + commentLength;
                if (name.EndsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }
                if ((flags & 1) != 0)
                {
                    problem = "it is protected with a password";
                    return false;
                }
                if (method != Stored && method != Deflated)
                {
                    problem = $"its entry '{name}' is compressed in a way workbooks never are";
                    return false;
                }
                if (size > MaximumEntrySize || packedSize > (uint)archive.Length)
                {
                    problem = $"its entry '{name}' is too large to read";
                    return false;
                }
                if (localOffset + 30L > archive.Length || ReadUInt32(archive, (int)localOffset) != LocalHeaderSignature)
                {
                    problem = $"its entry '{name}' is damaged";
                    return false;
                }
                long start = localOffset + 30L + ReadUInt16(archive, (int)localOffset + 26) + ReadUInt16(archive, (int)localOffset + 28);
                if (start + packedSize > archive.Length)
                {
                    problem = $"its entry '{name}' is cut short";
                    return false;
                }
                byte[] data = method == Stored
                    ? Copy(archive, (int)start, (int)packedSize)
                    : Decompress(archive, (int)start, (int)packedSize, (int)size);
                if (data == null || Crc32.Compute(data) != checksum)
                {
                    problem = $"its entry '{name}' is damaged";
                    return false;
                }
                entries[name] = data;
            }
            problem = null;
            return true;
        }

        private static void WriteCommonHeader(BinaryWriter writer, ushort flags, ushort method, uint checksum, int packedSize, int size, int nameLength)
        {
            writer.Write(Version);
            writer.Write(flags);
            writer.Write(method);
            writer.Write((ushort)0); // Time.
            writer.Write(FixedDate);
            writer.Write(checksum);
            writer.Write((uint)packedSize);
            writer.Write((uint)size);
            writer.Write((ushort)nameLength);
        }

        /// <summary>Marks names outside ASCII as UTF-8, which zip tools otherwise read in an old DOS code page.</summary>
        private static ushort NameFlags(string name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                if (name[i] > 0x7F)
                {
                    return 1 << 11;
                }
            }
            return 0;
        }

        private static byte[] Compress(byte[] data)
        {
            using MemoryStream packed = new();
            using (DeflateStream deflate = new(packed, CompressionLevel.Optimal, true))
            {
                deflate.Write(data, 0, data.Length);
            }
            return packed.ToArray();
        }

        private static byte[] Decompress(byte[] archive, int start, int packedSize, int size)
        {
            byte[] data = new byte[size];
            using DeflateStream inflate = new(new MemoryStream(archive, start, packedSize, false), CompressionMode.Decompress);
            int read = 0;
            while (read < size)
            {
                int count = inflate.Read(data, read, size - read);
                if (count <= 0)
                {
                    return null;
                }
                read += count;
            }
            // More data than the entry declared means the declared size can't be trusted.
            return inflate.ReadByte() < 0 ? data : null;
        }

        private static byte[] Copy(byte[] archive, int start, int count)
        {
            byte[] data = new byte[count];
            Buffer.BlockCopy(archive, start, data, 0, count);
            return data;
        }

        /// <summary>Returns where the end of central directory record starts, searching back over a trailing comment; -1 without one.</summary>
        private static int FindEnd(byte[] archive)
        {
            int lowest = Math.Max(0, archive.Length - 22 - ushort.MaxValue);
            for (int position = archive.Length - 22; position >= lowest; position--)
            {
                if (ReadUInt32(archive, position) == EndSignature)
                {
                    return position;
                }
            }
            return -1;
        }

        private static ushort ReadUInt16(byte[] data, int offset) => (ushort)(data[offset] | (data[offset + 1] << 8));

        private static uint ReadUInt32(byte[] data, int offset) =>
            (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

        /// <summary>The CRC-32 checksum zip entries carry.</summary>
        private static class Crc32
        {
            private static readonly uint[] Table = CreateTable();

            public static uint Compute(byte[] data)
            {
                uint crc = uint.MaxValue;
                for (int i = 0; i < data.Length; i++)
                {
                    crc = Table[(crc ^ data[i]) & 0xFF] ^ (crc >> 8);
                }
                return ~crc;
            }

            private static uint[] CreateTable()
            {
                uint[] table = new uint[256];
                for (uint i = 0; i < table.Length; i++)
                {
                    uint value = i;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
                    }
                    table[i] = value;
                }
                return table;
            }
        }
    }
}
