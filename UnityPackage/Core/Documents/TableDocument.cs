using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// A table file, <c>Shop.English.lang</c>, read into its parts: the settings at its top, its entries with their
    /// comments and attributes, and every problem found while reading it.
    /// </summary>
    /// <remarks>
    /// Reading never throws. A line that can't be used is reported in <see cref="Issues"/> and left out, and
    /// everything else is still read, so one typo never hides the rest of a file.
    /// </remarks>
    public sealed class TableDocument
    {
        private readonly Dictionary<ulong, int> _entryIndices;

        internal TableDocument(IReadOnlyList<string> headerComments, IReadOnlyList<DocumentProperty> settings,
            IReadOnlyList<TableDocumentEntry> entries, IReadOnlyList<string> trailingComments,
            IReadOnlyList<DocumentIssue> issues)
        {
            HeaderComments = headerComments;
            Settings = settings;
            Entries = entries;
            TrailingComments = trailingComments;
            Issues = issues;
            _entryIndices = new Dictionary<ulong, int>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                _entryIndices[Hashing.ComputeNameHash(entries[i].Key)] = i;
            }
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == IssueSeverity.Error)
                {
                    HasErrors = true;
                    break;
                }
            }
        }

        /// <summary>Comments that open the file, separated from what follows by a blank line or followed by the table settings.</summary>
        public IReadOnlyList<string> HeaderComments { get; }

        /// <summary>The table settings at the top of the file, such as <c>@loading</c>, in file order.</summary>
        public IReadOnlyList<DocumentProperty> Settings { get; }

        /// <summary>The entries, in file order. Lines with errors are not among them.</summary>
        public IReadOnlyList<TableDocumentEntry> Entries { get; }

        /// <summary>Comments after the last entry, kept so that rewriting the file never drops them.</summary>
        public IReadOnlyList<string> TrailingComments { get; }

        /// <summary>Every problem found while reading, in file order.</summary>
        public IReadOnlyList<DocumentIssue> Issues { get; }

        /// <summary>Whether any issue is an <see cref="IssueSeverity.Error"/>.</summary>
        public bool HasErrors { get; }

        /// <summary>Reads a table file's text. A null or empty text is an empty table.</summary>
        public static TableDocument Parse(string text) => new TableDocumentParser(text).Parse();

        /// <summary>Returns the entry with <paramref name="key"/>, ignoring case.</summary>
        public bool TryGetEntry(ReadOnlySpan<char> key, out TableDocumentEntry entry)
        {
            if (_entryIndices.TryGetValue(Hashing.ComputeNameHash(key), out int index))
            {
                entry = Entries[index];
                return true;
            }
            entry = null;
            return false;
        }

        /// <summary>Returns the value of the table setting named <paramref name="name"/>, ignoring case.</summary>
        public bool TryGetSetting(string name, out string value)
        {
            for (int i = 0; i < Settings.Count; i++)
            {
                if (string.Equals(Settings[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = Settings[i].Value;
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
