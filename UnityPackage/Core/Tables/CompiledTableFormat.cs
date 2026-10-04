namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// The layout of a compiled table: one table in one language, as the importer writes it and localizers load it.
    /// </summary>
    /// <remarks>
    /// Every value is little-endian. After a fixed header come the sorted 64-bit key hashes, the start and length of
    /// each entry's text, the sorted alias hashes with the entry each one points to, and finally one UTF-16 character
    /// buffer holding every text. A loaded table is therefore a handful of arrays, however many entries it has.
    /// <code>
    /// uint32 magic, uint16 version, uint16 flags
    /// uint64 catalog hash, uint64 table hash, uint64 language hash
    /// int32 entry count, int32 alias count, int32 character count
    /// uint64[entries] hashes, int32[entries] starts, int32[entries] lengths
    /// uint64[aliases] alias hashes, int32[aliases] alias targets
    /// char[characters] texts
    /// </code>
    /// </remarks>
    internal static class CompiledTableFormat
    {
        /// <summary>The bytes "RLTB", read as a little-endian 32-bit value.</summary>
        public const uint Magic = 0x42544C52;

        /// <summary>Raised whenever the layout changes, so an older file is reimported instead of misread.</summary>
        public const ushort Version = 1;
    }
}
