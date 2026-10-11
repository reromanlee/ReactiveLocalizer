using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>One entry as an exchange file holds it: its table and key, and its text in each of the file's languages.</summary>
    public sealed class ImportedRow
    {
        /// <summary>Creates a row read from <paramref name="location"/>.</summary>
        /// <param name="location">Where the row is, as problems name it, such as <c>Shop.csv, row 4</c>.</param>
        /// <param name="tableName">The table the row names.</param>
        /// <param name="key">The key the row names.</param>
        /// <param name="texts">The row's text in each of the file's languages, in their order; null where it has none.</param>
        public ImportedRow(string location, string tableName, string key, IReadOnlyList<string> texts)
        {
            Location = location ?? string.Empty;
            TableName = tableName ?? string.Empty;
            Key = key ?? string.Empty;
            Texts = texts ?? Array.Empty<string>();
        }

        /// <summary>Where the row is, as problems name it.</summary>
        public string Location { get; }

        /// <summary>The table the row names, as written.</summary>
        public string TableName { get; }

        /// <summary>The key the row names, as written.</summary>
        public string Key { get; }

        /// <summary>The row's text in each of the file's languages, in their order; null where it has none.</summary>
        public IReadOnlyList<string> Texts { get; }

        /// <summary>Whether the row carries the fingerprint of the source text its translation was made from, as an LLM reply does.</summary>
        public bool HasFingerprint { get; set; }

        /// <summary>The fingerprint the row carries, when it has one.</summary>
        public uint Fingerprint { get; set; }

        /// <summary>Whether the file marks the row's translation as checked, as an XLIFF state can.</summary>
        public bool IsConfirmed { get; set; }

        /// <summary>Returns the text in the file's language at <paramref name="index"/>; null where the row has none.</summary>
        public string GetText(int index) => index >= 0 && index < Texts.Count ? Texts[index] : null;

        /// <inheritdoc/>
        public override string ToString() => $"{TableName}.{Key}";
    }
}
