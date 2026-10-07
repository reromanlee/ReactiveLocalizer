using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// Writes and reads the compiled form of a catalog, which builds ship next to their compiled tables.
    /// </summary>
    /// <remarks>
    /// Every value is little-endian, and strings are a length followed by UTF-16 code units.
    /// <code>
    /// uint32 magic, uint16 version, uint16 flags
    /// string catalog name, int32 source language index
    /// int32 language count, then per language:
    ///     string name, string display name, string culture, int32 fallback index or -1, byte direction, byte required,
    ///     then digits, decimal separator and group separator, each a byte telling whether it's set, then the string
    /// int32 table count, then per table: string name, byte loading, byte delivery
    /// </code>
    /// </remarks>
    internal static class CompiledCatalog
    {
        /// <summary>The bytes "RLCB", read as a little-endian 32-bit value.</summary>
        public const uint Magic = 0x42434C52;

        /// <summary>Raised whenever the layout changes, so an older file is rebuilt instead of misread.</summary>
        public const ushort Version = 2;

        public static byte[] Write(CatalogInfo catalog)
        {
            ByteWriter writer = new(256);
            writer.WriteUInt32(Magic);
            writer.WriteUInt16(Version);
            writer.WriteUInt16(0);
            writer.WriteString(catalog.Key.Name);
            writer.WriteInt32(IndexOf(catalog.Languages, catalog.SourceLanguage.Key));
            writer.WriteInt32(catalog.Languages.Count);
            for (int i = 0; i < catalog.Languages.Count; i++)
            {
                LanguageInfo language = catalog.Languages[i];
                writer.WriteString(language.Name);
                writer.WriteString(language.DisplayName);
                writer.WriteString(language.Culture);
                writer.WriteInt32(language.HasFallback ? IndexOf(catalog.Languages, language.Fallback) : -1);
                writer.WriteByte((byte)language.Direction);
                writer.WriteByte(language.IsRequired ? (byte)1 : (byte)0);
                WriteOptional(writer, language.Digits);
                WriteOptional(writer, language.DecimalSeparator);
                WriteOptional(writer, language.GroupSeparator);
            }
            writer.WriteInt32(catalog.Tables.Count);
            for (int i = 0; i < catalog.Tables.Count; i++)
            {
                TableInfo table = catalog.Tables[i];
                writer.WriteString(table.Key.Name);
                writer.WriteByte((byte)table.Loading);
                writer.WriteByte((byte)table.Delivery);
            }
            return writer.ToArray();
        }

        /// <summary>Reads a compiled catalog, failing with a reason on damaged data instead of throwing.</summary>
        public static bool TryRead(ReadOnlySpan<byte> data, out CatalogInfo catalog, out string error)
        {
            catalog = null;
            ByteReader reader = new(data);
            if (!reader.TryReadUInt32(out uint magic) || magic != Magic)
            {
                error = "The data is not a compiled catalog.";
                return false;
            }
            if (!reader.TryReadUInt16(out ushort version) || version != Version)
            {
                error = $"The catalog was compiled in format version {version}, but this version reads {Version}; rebuild it.";
                return false;
            }
            if (!reader.TryReadUInt16(out _) ||
                !reader.TryReadString(out string catalogName) ||
                !reader.TryReadInt32(out int sourceIndex) ||
                !reader.TryReadCount(21, out int languageCount) ||
                !NameRules.IsValid(catalogName) ||
                (uint)sourceIndex >= (uint)languageCount)
            {
                error = "The catalog's header is damaged.";
                return false;
            }

            // Fallbacks are stored as indices, so every name is read before any fallback is resolved.
            string[] names = new string[languageCount];
            string[] displayNames = new string[languageCount];
            string[] cultures = new string[languageCount];
            int[] fallbacks = new int[languageCount];
            byte[] directions = new byte[languageCount];
            byte[] requirements = new byte[languageCount];
            string[] digits = new string[languageCount];
            string[] decimalSeparators = new string[languageCount];
            string[] groupSeparators = new string[languageCount];
            for (int i = 0; i < languageCount; i++)
            {
                if (!reader.TryReadString(out names[i]) ||
                    !reader.TryReadString(out displayNames[i]) ||
                    !reader.TryReadString(out cultures[i]) ||
                    !reader.TryReadInt32(out fallbacks[i]) ||
                    !reader.TryReadByte(out directions[i]) ||
                    !reader.TryReadByte(out requirements[i]) ||
                    !TryReadOptional(ref reader, out digits[i]) ||
                    !TryReadOptional(ref reader, out decimalSeparators[i]) ||
                    !TryReadOptional(ref reader, out groupSeparators[i]) ||
                    !NameRules.IsValid(names[i]) ||
                    fallbacks[i] < -1 ||
                    fallbacks[i] >= languageCount ||
                    directions[i] > (byte)TextDirection.RightToLeft)
                {
                    error = "A language of the catalog is damaged.";
                    return false;
                }
            }
            if (!reader.TryReadCount(6, out int tableCount))
            {
                error = "The catalog's table list is damaged.";
                return false;
            }
            TableInfo[] tables = new TableInfo[tableCount];
            for (int i = 0; i < tableCount; i++)
            {
                if (!reader.TryReadString(out string tableName) ||
                    !reader.TryReadByte(out byte loading) ||
                    !reader.TryReadByte(out byte delivery) ||
                    !NameRules.IsValid(tableName) ||
                    loading > (byte)TableLoading.OnDemand ||
                    delivery > (byte)TableDelivery.Streaming)
                {
                    error = "A table of the catalog is damaged.";
                    return false;
                }
                tables[i] = new TableInfo(new TableKey(tableName), (TableLoading)loading, (TableDelivery)delivery);
            }

            // What the format can't express, such as two languages with one name, is still rejected by CatalogInfo.
            try
            {
                LanguageInfo[] languages = new LanguageInfo[languageCount];
                for (int i = 0; i < languageCount; i++)
                {
                    LanguageKey fallback = fallbacks[i] < 0 ? default : new LanguageKey(names[fallbacks[i]]);
                    languages[i] = new LanguageInfo(new LanguageKey(names[i]), displayNames[i], cultures[i], fallback,
                        (TextDirection)directions[i], requirements[i] != 0, digits[i], decimalSeparators[i], groupSeparators[i]);
                }
                catalog = new CatalogInfo(new CatalogKey(catalogName), new LanguageKey(names[sourceIndex]), languages, tables);
            }
            catch (ArgumentException exception)
            {
                error = $"The catalog is inconsistent: {exception.Message}";
                return false;
            }
            error = null;
            return true;
        }

        private static void WriteOptional(ByteWriter writer, string value)
        {
            writer.WriteByte(value != null ? (byte)1 : (byte)0);
            if (value != null)
            {
                writer.WriteString(value);
            }
        }

        private static bool TryReadOptional(ref ByteReader reader, out string value)
        {
            value = null;
            if (!reader.TryReadByte(out byte isSet) || isSet > 1)
            {
                return false;
            }
            return isSet == 0 || reader.TryReadString(out value);
        }

        private static int IndexOf(IReadOnlyList<LanguageInfo> languages, LanguageKey key)
        {
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i].Key == key)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
