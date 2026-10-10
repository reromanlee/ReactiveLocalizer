using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Messages;
using System;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// One table in one language, loaded from its compiled form. Lookups are a binary search over sorted hashes, and
    /// each entry's string is created the first time it is asked for, then returned as the same instance every time.
    /// Entries with arguments hold a compiled message instead of text.
    /// </summary>
    /// <remarks>
    /// Never changes after it is read, so any number of threads can read it at once. Two threads asking for the same
    /// string for the first time at once may both create it; only one is kept, and both read equal text.
    /// </remarks>
    internal sealed class CompiledTable
    {
        private readonly ulong[] _hashes;
        private readonly int[] _starts;
        // A negative length marks a message entry; its bitwise complement is the message's number.
        private readonly int[] _lengths;
        private readonly ulong[] _aliasHashes;
        private readonly int[] _aliasTargets;
        private readonly int[] _messageStarts;
        private readonly int[] _program;
        private readonly char[] _characters;
        private readonly string[] _strings;

        private CompiledTable(ulong catalogHash, ulong tableHash, ulong languageHash, ulong sourceKeysHash, ulong[] hashes, int[] starts,
            int[] lengths, ulong[] aliasHashes, int[] aliasTargets, int[] messageStarts, int[] program, char[] characters)
        {
            CatalogHash = catalogHash;
            TableHash = tableHash;
            LanguageHash = languageHash;
            SourceKeysHash = sourceKeysHash;
            _hashes = hashes;
            _starts = starts;
            _lengths = lengths;
            _aliasHashes = aliasHashes;
            _aliasTargets = aliasTargets;
            _messageStarts = messageStarts;
            _program = program;
            _characters = characters;
            _strings = new string[hashes.Length];
        }

        public ulong CatalogHash { get; }

        public ulong TableHash { get; }

        public ulong LanguageHash { get; }

        /// <summary>
        /// Identifies the keys of the source-language file when the table has every one of them; zero otherwise, as for
        /// a translation with gaps or a table compiled without its catalog.
        /// </summary>
        public ulong SourceKeysHash { get; }

        public int EntryCount => _hashes.Length;

        /// <summary>
        /// Returns whether the table has every key of its source-language file, as identified by
        /// <paramref name="expectedKeysHash"/> from the catalog, so its fallback languages are never needed.
        /// </summary>
        public bool IsComplete(ulong expectedKeysHash) => expectedKeysHash != 0 && SourceKeysHash == expectedKeysHash;

        /// <summary>How many distinct compiled messages the table holds.</summary>
        public int MessageCount => _messageStarts.Length;

        /// <summary>The compiled messages, which <see cref="TryGetMessage"/> gives the start of.</summary>
        public int[] Program => _program;

        /// <summary>Every text of the table, which entries and compiled messages point into.</summary>
        public char[] Characters => _characters;

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

        /// <summary>Returns whether the entry at <paramref name="index"/> is a message, and where its program starts.</summary>
        public bool TryGetMessage(int index, out int start)
        {
            int length = _lengths[index];
            start = length < 0 ? _messageStarts[~length] : -1;
            return length < 0;
        }

        /// <summary>
        /// Returns the text of the entry at <paramref name="index"/>, creating its string on first use only. A message
        /// read this way shows its arguments as <c>{name}</c>.
        /// </summary>
        public string GetString(int index)
        {
            string text = Volatile.Read(ref _strings[index]);
            if (text != null)
            {
                return text;
            }
            int length = _lengths[index];
            if (length < 0)
            {
                text = RenderWithoutArguments(_messageStarts[~length]);
            }
            else
            {
                text = length == 0 ? string.Empty : new string(_characters, _starts[index], length);
            }
            // Whichever thread stores its string first wins; a thread that lost returns the stored one instead.
            return Interlocked.CompareExchange(ref _strings[index], text, null) ?? text;
        }

        /// <summary>Returns the characters of the entry at <paramref name="index"/>, without creating a string for plain text.</summary>
        public ReadOnlyMemory<char> GetMemory(int index)
        {
            int length = _lengths[index];
            return length < 0 ? GetString(index).AsMemory() : new ReadOnlyMemory<char>(_characters, _starts[index], length);
        }

        /// <summary>
        /// Reads a compiled table, checking every count, range and message against the data, so damaged data fails with
        /// a reason instead of crashing, reading out of bounds or asking for more memory than the data could fill.
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
                !reader.TryReadUInt64(out ulong sourceKeysHash) ||
                !reader.TryReadInt32(out int entryCount) ||
                !reader.TryReadInt32(out int aliasCount) ||
                !reader.TryReadInt32(out int messageCount) ||
                !reader.TryReadInt32(out int programLength) ||
                !reader.TryReadInt32(out int characterCount) ||
                entryCount < 0 || aliasCount < 0 || messageCount < 0 || programLength < 0 || characterCount < 0 ||
                (long)entryCount * 16 + (long)aliasCount * 12 + (long)messageCount * 4 + (long)programLength * 4 + (long)characterCount * 2 > reader.Remaining)
            {
                error = "The table's header is damaged.";
                return false;
            }

            // Entries: hashes, then where each entry's text starts and how long it is, or which message it is.
            ulong[] hashes = new ulong[entryCount];
            int[] starts = new int[entryCount];
            int[] lengths = new int[entryCount];
            for (int i = 0; i < entryCount; i++)
            {
                reader.TryReadUInt64(out hashes[i]);
                // Binary search needs strictly ascending hashes; a file that breaks this would find wrong entries.
                if (i > 0 && hashes[i] <= hashes[i - 1])
                {
                    error = "The table's key hashes are out of order.";
                    return false;
                }
            }
            for (int i = 0; i < entryCount; i++)
            {
                reader.TryReadInt32(out starts[i]);
            }
            for (int i = 0; i < entryCount; i++)
            {
                reader.TryReadInt32(out lengths[i]);
                bool isValid = lengths[i] < 0
                    ? ~lengths[i] < messageCount
                    : starts[i] >= 0 && (long)starts[i] + lengths[i] <= characterCount;
                if (!isValid)
                {
                    error = "A text of the table lies outside its character buffer or its messages.";
                    return false;
                }
            }

            // Aliases: sorted hashes, then the entry each one points to.
            ulong[] aliasHashes = aliasCount == 0 ? Array.Empty<ulong>() : new ulong[aliasCount];
            int[] aliasTargets = aliasCount == 0 ? Array.Empty<int>() : new int[aliasCount];
            for (int i = 0; i < aliasCount; i++)
            {
                reader.TryReadUInt64(out aliasHashes[i]);
                if (i > 0 && aliasHashes[i] <= aliasHashes[i - 1])
                {
                    error = "The table's aliases are damaged.";
                    return false;
                }
            }
            for (int i = 0; i < aliasCount; i++)
            {
                reader.TryReadInt32(out aliasTargets[i]);
                if ((uint)aliasTargets[i] >= (uint)entryCount)
                {
                    error = "An alias of the table points outside it.";
                    return false;
                }
            }

            // Messages: where each one starts, then every message's operations.
            int[] messageStarts = messageCount == 0 ? Array.Empty<int>() : new int[messageCount];
            for (int i = 0; i < messageCount; i++)
            {
                reader.TryReadInt32(out messageStarts[i]);
            }
            int[] program = programLength == 0 ? Array.Empty<int>() : new int[programLength];
            for (int i = 0; i < programLength; i++)
            {
                reader.TryReadInt32(out program[i]);
            }

            // Texts.
            char[] characters = characterCount == 0 ? Array.Empty<char>() : new char[characterCount];
            reader.TryReadCharacters(characters);

            for (int i = 0; i < messageCount; i++)
            {
                if (!MessageProgram.TryVerify(program, messageStarts[i], characterCount, out error))
                {
                    return false;
                }
            }
            table = new CompiledTable(catalogHash, tableHash, languageHash, sourceKeysHash, hashes, starts, lengths, aliasHashes, aliasTargets,
                messageStarts, program, characters);
            error = null;
            return true;
        }

        private string RenderWithoutArguments(int start)
        {
            Span<char> buffer = stackalloc char[256];
            TextBuilder output = new(buffer);
            MessageProblems problems = default;
            EntryMessage noArguments = default;
            MessageRenderer.Render(_program, _characters, start, in noArguments, LanguageFormat.Root, FormatterTable.Empty, null, ref output, ref problems);
            string text = output.ToString();
            output.Dispose();
            return text;
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
