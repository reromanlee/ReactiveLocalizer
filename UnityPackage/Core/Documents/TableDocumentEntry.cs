using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>One <c>Key = Value</c> entry of a table file, with the comments and attributes written above it.</summary>
    public sealed class TableDocumentEntry
    {
        /// <summary>Creates an entry read from a 1-based <paramref name="line"/>.</summary>
        public TableDocumentEntry(string key, string value, bool hasFingerprint, uint fingerprint,
            IReadOnlyList<string> comments, IReadOnlyList<DocumentProperty> attributes, int line)
        {
            Key = key;
            Value = value ?? string.Empty;
            HasFingerprint = hasFingerprint;
            Fingerprint = hasFingerprint ? fingerprint : 0u;
            Comments = comments ?? Array.Empty<string>();
            Attributes = attributes ?? Array.Empty<DocumentProperty>();
            Line = line;
        }

        /// <summary>The entry's key, as written.</summary>
        public string Key { get; }

        /// <summary>The entry's text with its escapes resolved. Empty when the entry is intentionally empty.</summary>
        public string Value { get; }

        /// <summary>Whether the line carries a fingerprint tag, which only translation files write.</summary>
        public bool HasFingerprint { get; }

        /// <summary>
        /// Fingerprint of the source text this translation was made from: the top 24 bits of its text hash. Zero
        /// when <see cref="HasFingerprint"/> is false.
        /// </summary>
        public uint Fingerprint { get; }

        /// <summary>
        /// The <c>#</c> comments written above the entry, without their <c>#</c>. In a source file they are the
        /// context translators see. Never null.
        /// </summary>
        public IReadOnlyList<string> Comments { get; }

        /// <summary>The <c>@</c> attributes written above the entry, in file order. Never null.</summary>
        public IReadOnlyList<DocumentProperty> Attributes { get; }

        /// <summary>Line the entry was read from, counted from 1.</summary>
        public int Line { get; }

        /// <summary>Returns the value of the first attribute named <paramref name="name"/>, ignoring case.</summary>
        public bool TryGetAttribute(string name, out string value)
        {
            for (int i = 0; i < Attributes.Count; i++)
            {
                if (string.Equals(Attributes[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = Attributes[i].Value;
                    return true;
                }
            }
            value = null;
            return false;
        }
    }
}
