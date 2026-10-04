using System;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// One table in one language, loaded from its compiled form. Lookups are a binary search over sorted hashes, and
    /// each entry's string is created the first time it is asked for, then returned as the same instance every time.
    /// </summary>
    /// <remarks>
    /// Never changes after it is read, so any number of threads can read it at once. Two threads asking for the same
    /// string for the first time at once may both create it; only one is kept, and both read equal text.
    /// </remarks>
    internal sealed class CompiledTable
    {
        private readonly ulong[] _hashes;
        private readonly int[] _starts;
        private readonly int[] _lengths;
        private readonly ulong[] _aliasHashes;
        private readonly int[] _aliasTargets;
        private readonly char[] _characters;
        private readonly string[] _strings;

        private CompiledTable(ulong catalogHash, ulong tableHash, ulong languageHash, ulong[] hashes, int[] starts,
            int[] lengths, ulong[] aliasHashes, int[] aliasTargets, char[] characters)
        {
            CatalogHash = catalogHash;
            TableHash = tableHash;
            LanguageHash = languageHash;
            _hashes = hashes;
            _starts = starts;
            _lengths = lengths;
            _aliasHashes = aliasHashes;
            _aliasTargets = aliasTargets;
            _characters = characters;
            _strings = new string[hashes.Length];
        }

        public ulong CatalogHash { get; }

        public ulong TableHash { get; }

        public ulong LanguageHash { get; }

        public int EntryCount => _hashes.Length;

        /// <summary>
        /// Finds the entry with <paramref name="entryHash"/>, or the entry an alias with that hash points to.
        /// </summary>
        public bool TryFind(ulong entryHash, out int index)
        {
            index = BinarySearch(_hashes, entryHash);
            if (index >= 0)
            {
                return true;
            }
            int aliasIndex = BinarySearch(_aliasHashes, entryHash);
            if (aliasIndex >= 0)
            {
                index = _aliasTargets[aliasIndex];
                return true;
            }
            index = -1;
            return false;
        }

        /// <summary>Returns the text of the entry at <paramref name="index"/>, creating its string on first use only.</summary>
        public string GetString(int index)
        {
            string text = Volatile.Read(ref _strings[index]);
            if (text != null)
            {
                return text;
            }
            int length = _lengths[index];
            text = length == 0 ? string.Empty : new string(_characters, _starts[index], length);
            // Whichever thread stores its string first wins; a thread that lost returns the stored one instead.
            return Interlocked.CompareExchange(ref _strings[index], text, null) ?? text;
        }

        /// <summary>Returns the characters of the entry at <paramref name="index"/> without creating a string.</summary>
        public ReadOnlyMemory<char> GetMemory(int index) => new(_characters, _starts[index], _lengths[index]);

        /// <summary>
        /// Reads a compiled table, checking every count and range against the data, so damaged data fails with a
        /// reason instead of crashing or reading out of bounds.
        /// </summary>
        public static bool TryRead(ReadOnlySpan<byte> data, out CompiledTable table, out string error)
        {
            table = null;
            ByteReader reader = new(data);

            // Header.
            if (!reader.TryReadUInt32(out uint magic) || magic != CompiledTableFormat.Magic)
            {
                error = "The data is not a compiled table.";
                return false;
            }
            if (!reader.TryReadUInt16(out ushort version) || version != CompiledTableFormat.Version)
            {
                error = $"The table was compiled in format version {version}, but this version reads {CompiledTableFormat.Version}; reimport it.";
                return false;
            }
            if (!reader.TryReadUInt16(out _) ||
                !reader.TryReadUInt64(out ulong catalogHash) ||
                !reader.TryReadUInt64(out ulong tableHash) ||
                !reader.TryReadUInt64(out ulong languageHash) ||
                !reader.TryReadCount(16, out int entryCount) ||
                !reader.TryReadInt32(out int aliasCount) ||
                !reader.TryReadInt32(out int characterCount) ||
                aliasCount < 0 ||
                characterCount < 0)
            {
                error = "The table's header is damaged.";
                return false;
            }

            // Entries: hashes, then where each entry's text starts and how long it is.
            ulong[] hashes = new ulong[entryCount];
            int[] starts = new int[entryCount];
            int[] lengths = new int[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                if (!reader.TryReadUInt64(out hashes[i]))
                {
                    error = "The table ends inside its key hashes.";
                    return false;
                }
                // Binary search needs strictly ascending hashes; a file that breaks this would find wrong entries.
                if (i > 0 && hashes[i] <= hashes[i - 1])
                {
                    error = "The table's key hashes are out of order.";
                    return false;
                }
            }
            for (int i = 0; i < entryCount; i++)
            {
                if (!reader.TryReadInt32(out starts[i]))
                {
                    error = "The table ends inside its text positions.";
                    return false;
                }
            }
            for (int i = 0; i < entryCount; i++)
            {
                if (!reader.TryReadInt32(out lengths[i]) ||
                    starts[i] < 0 ||
                    lengths[i] < 0 ||
                    (long)starts[i] + lengths[i] > characterCount)
                {
                    error = "A text of the table lies outside its character buffer.";
                    return false;
                }
            }

            // Aliases: sorted hashes, then the entry each one points to.
            ulong[] aliasHashes = aliasCount == 0 ? Array.Empty<ulong>() : new ulong[aliasCount];
            int[] aliasTargets = aliasCount == 0 ? Array.Empty<int>() : new int[aliasCount];
            for (int i = 0; i < aliasCount; i++)
            {
                if (!reader.TryReadUInt64(out aliasHashes[i]) || (i > 0 && aliasHashes[i] <= aliasHashes[i - 1]))
                {
                    error = "The table's aliases are damaged.";
                    return false;
                }
            }
            for (int i = 0; i < aliasCount; i++)
            {
                if (!reader.TryReadInt32(out aliasTargets[i]) || (uint)aliasTargets[i] >= (uint)entryCount)
                {
                    error = "An alias of the table points outside it.";
                    return false;
                }
            }

            // Texts.
            char[] characters = characterCount == 0 ? Array.Empty<char>() : new char[characterCount];
            if (!reader.TryReadCharacters(characters))
            {
                error = "The table ends inside its texts.";
                return false;
            }
            table = new CompiledTable(catalogHash, tableHash, languageHash, hashes, starts, lengths, aliasHashes, aliasTargets, characters);
            error = null;
            return true;
        }

        private static int BinarySearch(ulong[] values, ulong value)
        {
            int low = 0;
            int high = values.Length - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) >> 1);
                ulong candidate = values[middle];
                if (candidate == value)
                {
                    return middle;
                }
                if (candidate < value)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }
            return -1;
        }
    }
}
