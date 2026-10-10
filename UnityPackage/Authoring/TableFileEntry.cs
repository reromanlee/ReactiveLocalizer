using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>One entry of an editable <see cref="TableFile"/>: its key, text, fingerprint, comments and attributes.</summary>
    public sealed class TableFileEntry
    {
        /// <summary>Creates an entry with no comments, attributes or fingerprint.</summary>
        /// <exception cref="ArgumentException"><paramref name="key"/> breaks the naming rule.</exception>
        public TableFileEntry(string key, string value)
        {
            NameRules.ThrowIfInvalid(key, nameof(key));
            Key = key;
            Value = value ?? string.Empty;
        }

        /// <summary>The entry's key. Renaming goes through <see cref="TableFile.RenameEntry"/>, which keeps keys unique.</summary>
        public string Key { get; internal set; }

        /// <summary>The entry's text, with escapes resolved. Empty means intentionally empty.</summary>
        public string Value { get; set; }

        /// <summary>Whether the entry carries a fingerprint, which only translations do.</summary>
        public bool HasFingerprint { get; set; }

        /// <summary>The fingerprint of the source text the translation was made from, when it has one.</summary>
        public uint Fingerprint { get; set; }

        /// <summary>The comments above the entry, without their <c>#</c>; in a source file, the context translators see.</summary>
        public List<string> Comments { get; } = new();

        /// <summary>The attributes above the entry, such as <c>@formerly</c>, in file order.</summary>
        public List<DocumentProperty> Attributes { get; } = new();

        /// <summary>Whether a blank line separates the entry from its neighbors, as one with comments or attributes needs.</summary>
        internal bool IsSeparated => Comments.Count > 0 || Attributes.Count > 0;

        /// <summary>Stamps the entry as translated from <paramref name="sourceText"/>.</summary>
        public void StampFingerprint(string sourceText)
        {
            HasFingerprint = true;
            Fingerprint = Hashing.ComputeFingerprint(sourceText ?? string.Empty);
        }

        /// <summary>Returns the values of every attribute named <paramref name="name"/>, ignoring case, in file order.</summary>
        public List<string> GetAttributeValues(string name)
        {
            List<string> values = new();
            for (int i = 0; i < Attributes.Count; i++)
            {
                if (string.Equals(Attributes[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(Attributes[i].Value);
                }
            }
            return values;
        }

        /// <summary>Sets the single attribute named <paramref name="name"/>, or removes it when <paramref name="value"/> is null.</summary>
        public void SetAttribute(string name, string value)
        {
            int index = Attributes.FindIndex(attribute => string.Equals(attribute.Name, name, StringComparison.OrdinalIgnoreCase));
            if (value == null)
            {
                if (index >= 0)
                {
                    Attributes.RemoveAt(index);
                }
                return;
            }
            DocumentProperty property = new(name, value, 0, null);
            if (index >= 0)
            {
                Attributes[index] = property;
            }
            else
            {
                Attributes.Add(property);
            }
        }

        /// <summary>Returns a copy, as moving an entry to another table needs.</summary>
        public TableFileEntry Clone()
        {
            TableFileEntry copy = new(Key, Value) { HasFingerprint = HasFingerprint, Fingerprint = Fingerprint };
            copy.Comments.AddRange(Comments);
            copy.Attributes.AddRange(Attributes);
            return copy;
        }

        internal static TableFileEntry FromDocument(TableDocumentEntry entry)
        {
            TableFileEntry copy = new(entry.Key, entry.Value) { HasFingerprint = entry.HasFingerprint, Fingerprint = entry.Fingerprint };
            copy.Comments.AddRange(entry.Comments);
            copy.Attributes.AddRange(entry.Attributes);
            return copy;
        }
    }
}
