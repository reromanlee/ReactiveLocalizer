namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// The layout of a compiled table: one table in one language, as the importer writes it and localizers load it.
    /// </summary>
    /// <remarks>
    /// Every value is little-endian. After a fixed header come the sorted 64-bit key hashes, the start and length of
    /// each entry's text, the sorted alias hashes with the entry each one points to, the compiled messages, and
    /// finally one UTF-16 character buffer holding every text. A loaded table is therefore a handful of arrays, however
    /// many entries it has. An entry with arguments has a negative length, the bitwise complement of its message's
    /// number; <see cref="Messages.MessageOperation"/> describes a message's operations.
    /// <para>
    /// The source keys hash identifies the keys of the table's source-language file. A table that has every one of
    /// them carries it, so a localizer that finds it matching the catalog's loads no fallback language for the table;
    /// any other table carries zero.
    /// </para>
    /// <code>
    /// uint32 magic, uint16 version, uint16 flags
    /// uint64 catalog hash, uint64 table hash, uint64 language hash, uint64 source keys hash
    /// int32 entry count, int32 alias count, int32 message count, int32 program length, int32 character count
    /// uint64[entries] hashes, int32[entries] starts, int32[entries] lengths
    /// uint64[aliases] alias hashes, int32[aliases] alias targets
    /// int32[messages] message starts, int32[program] operations
    /// char[characters] texts
    /// </code>
    /// </remarks>
    internal static class CompiledTableFormat
    {
        /// <summary>The bytes "RLTB", read as a little-endian 32-bit value.</summary>
        public const uint Magic = 0x42544C52;

        /// <summary>Raised whenever the layout changes, so an older file is reimported instead of misread.</summary>
        public const ushort Version = 3;

        /// <summary>The size of the fixed header, in bytes.</summary>
        public const int HeaderSize = 60;
    }
}
