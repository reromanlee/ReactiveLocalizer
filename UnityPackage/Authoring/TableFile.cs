using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// A table file opened for editing: its header comments, settings, entries and trailing comments, written back in
    /// the one canonical layout, so every tool, person and AI agent that edits a file ends up with the same text.
    /// </summary>
    /// <remarks>
    /// The canonical layout: header comments, a blank line, the settings in their fixed order, a blank line, then the
    /// entries in natural key order, with a blank line around every entry that has comments or attributes, and the
    /// trailing comments after a blank line. Values are escaped as <see cref="TextEscaping"/> defines. The file keeps
    /// the line endings it was read with; a new file uses LF. Reading a canonical file and writing it again gives
    /// exactly the same text.
    /// </remarks>
    public sealed class TableFile
    {
        private static readonly string[] SettingOrder = { DocumentNames.Loading, DocumentNames.Delivery, DocumentNames.GenerateCode };

        // Where each key's entry was last seen in Entries. Entries can be changed from outside, so every hit is checked
        // against the list, and a changed count rebuilds the index.
        private Dictionary<string, int> _index;
        private int _indexedCount;

        /// <summary>Creates an empty table file that writes LF line endings.</summary>
        public TableFile() : this("\n", false)
        {
        }

        private TableFile(string newLine, bool hasErrors)
        {
            NewLine = newLine;
            HasErrors = hasErrors;
        }

        /// <summary>Comments that open the file.</summary>
        public List<string> HeaderComments { get; } = new();

        /// <summary>The table settings, such as <c>@loading</c>; only a source-language file has any.</summary>
        public List<DocumentProperty> Settings { get; } = new();

        /// <summary>
        /// The entries. <see cref="Write"/> sorts them, so their order here never matters. Lookups notice entries added,
        /// removed or reordered here, but not one replaced in place by an entry with another key.
        /// </summary>
        public List<TableFileEntry> Entries { get; } = new();

        /// <summary>Comments after the last entry.</summary>
        public List<string> TrailingComments { get; } = new();

        /// <summary>The line ending the file is written with: the one it was read with, or LF for a new file.</summary>
        public string NewLine { get; set; }

        /// <summary>
        /// Whether reading found errors. Lines with errors aren't part of the file's entries, so writing such a file
        /// back would drop them; editing tools refuse to until the errors are fixed.
        /// </summary>
        public bool HasErrors { get; }

        /// <summary>Reads a table file's text for editing, keeping its line endings.</summary>
        public static TableFile Read(string text)
        {
            TableDocument document = TableDocument.Parse(text);
            TableFile file = new(DetectNewLine(text), document.HasErrors);
            file.HeaderComments.AddRange(document.HeaderComments);
            file.Settings.AddRange(document.Settings);
            for (int i = 0; i < document.Entries.Count; i++)
            {
                file.Entries.Add(TableFileEntry.FromDocument(document.Entries[i]));
            }
            file.TrailingComments.AddRange(document.TrailingComments);
            return file;
        }

        /// <summary>Returns the entry with <paramref name="key"/>, ignoring case.</summary>
        public bool TryGetEntry(string key, out TableFileEntry entry)
        {
            int index = IndexOf(key);
            entry = index >= 0 ? Entries[index] : null;
            return entry != null;
        }

        /// <summary>Returns the position of the entry with <paramref name="key"/>, ignoring case, or -1. Takes constant time.</summary>
        public int IndexOf(string key)
        {
            if (key == null)
            {
                return -1;
            }
            if (_index == null || _indexedCount != Entries.Count)
            {
                RebuildIndex();
            }
            if (!_index.TryGetValue(key, out int index))
            {
                return -1;
            }
            if (index < Entries.Count && string.Equals(Entries[index].Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
            // The list was reordered or changed from outside since the index was built.
            RebuildIndex();
            return _index.TryGetValue(key, out index) ? index : -1;
        }

        /// <summary>Adds an entry. Returns false, adding nothing, when the key is already taken, ignoring case.</summary>
        public bool TryAddEntry(TableFileEntry entry)
        {
            if (entry == null || IndexOf(entry.Key) >= 0)
            {
                return false;
            }
            Entries.Add(entry);
            // The lookup above left the index current, so it takes the new entry without a rebuild.
            if (_index != null && _indexedCount == Entries.Count - 1)
            {
                _index.TryAdd(entry.Key, Entries.Count - 1);
                _indexedCount = Entries.Count;
            }
            return true;
        }

        /// <summary>Removes the entry with <paramref name="key"/>. Returns false when there is none.</summary>
        public bool RemoveEntry(string key)
        {
            int index = IndexOf(key);
            if (index < 0)
            {
                return false;
            }
            Entries.RemoveAt(index);
            _index = null;
            return true;
        }

        /// <summary>
        /// Gives the entry with <paramref name="oldKey"/> the key <paramref name="newKey"/>. Returns false when there is
        /// no such entry, or another entry already has the new key. Aliases are the caller's to add.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="newKey"/> breaks the naming rule.</exception>
        public bool RenameEntry(string oldKey, string newKey)
        {
            NameRules.ThrowIfInvalid(newKey, nameof(newKey));
            int index = IndexOf(oldKey);
            int taken = IndexOf(newKey);
            if (index < 0 || (taken >= 0 && taken != index))
            {
                return false;
            }
            Entries[index].Key = newKey;
            _index = null;
            return true;
        }

        private void RebuildIndex()
        {
            _index ??= new Dictionary<string, int>(Entries.Count, StringComparer.OrdinalIgnoreCase);
            _index.Clear();
            for (int i = 0; i < Entries.Count; i++)
            {
                // The first of two entries differing only in case wins, as a reader finds it first.
                _index.TryAdd(Entries[i].Key, i);
            }
            _indexedCount = Entries.Count;
        }

        /// <summary>Returns the value of the setting named <paramref name="name"/>, ignoring case, or null.</summary>
        public string GetSetting(string name)
        {
            for (int i = 0; i < Settings.Count; i++)
            {
                if (string.Equals(Settings[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return Settings[i].Value;
                }
            }
            return null;
        }

        /// <summary>Sets the setting named <paramref name="name"/>, or removes it when <paramref name="value"/> is null.</summary>
        public void SetSetting(string name, string value)
        {
            int index = Settings.FindIndex(setting => string.Equals(setting.Name, name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                Settings.RemoveAt(index);
            }
            if (value != null)
            {
                Settings.Add(new DocumentProperty(name, value, 0, null));
            }
        }

        /// <summary>Writes the file in its canonical layout.</summary>
        public string Write()
        {
            StringBuilder builder = new();
            bool hasSection = false;
            if (HeaderComments.Count > 0)
            {
                AppendComments(builder, HeaderComments);
                hasSection = true;
            }
            List<DocumentProperty> settings = SortSettings();
            if (settings.Count > 0)
            {
                AppendSeparator(builder, ref hasSection);
                for (int i = 0; i < settings.Count; i++)
                {
                    AppendProperty(builder, settings[i]);
                }
            }
            List<TableFileEntry> entries = new(Entries);
            entries.Sort((left, right) => NaturalOrder.Instance.Compare(left.Key, right.Key));
            for (int i = 0; i < entries.Count; i++)
            {
                TableFileEntry entry = entries[i];
                // The first entry is set apart from what comes before it; later ones only when they or the one before carry more than a line.
                if (i == 0 || entry.IsSeparated || entries[i - 1].IsSeparated)
                {
                    AppendSeparator(builder, ref hasSection);
                }
                AppendComments(builder, entry.Comments);
                for (int a = 0; a < entry.Attributes.Count; a++)
                {
                    AppendProperty(builder, entry.Attributes[a]);
                }
                AppendEntry(builder, entry);
            }
            if (TrailingComments.Count > 0)
            {
                AppendSeparator(builder, ref hasSection);
                AppendComments(builder, TrailingComments);
            }
            return builder.ToString();
        }

        /// <summary>Returns the line ending a text uses first, or LF for a text without one.</summary>
        internal static string DetectNewLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return "\n";
            }
            int index = text.IndexOfAny(new[] { '\r', '\n' });
            if (index < 0 || text[index] == '\n')
            {
                return "\n";
            }
            return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
        }

        private List<DocumentProperty> SortSettings()
        {
            List<DocumentProperty> sorted = new(Settings.Count);
            for (int order = 0; order < SettingOrder.Length; order++)
            {
                for (int i = 0; i < Settings.Count; i++)
                {
                    if (string.Equals(Settings[i].Name, SettingOrder[order], StringComparison.OrdinalIgnoreCase))
                    {
                        sorted.Add(new DocumentProperty(SettingOrder[order], Settings[i].Value, 0, null));
                        break;
                    }
                }
            }
            return sorted;
        }

        private void AppendSeparator(StringBuilder builder, ref bool hasSection)
        {
            if (hasSection)
            {
                builder.Append(NewLine);
            }
            hasSection = true;
        }

        private void AppendComments(StringBuilder builder, List<string> comments)
        {
            for (int i = 0; i < comments.Count; i++)
            {
                builder.Append('#');
                if (comments[i].Length > 0)
                {
                    builder.Append(' ').Append(comments[i]);
                }
                builder.Append(NewLine);
            }
        }

        private void AppendProperty(StringBuilder builder, DocumentProperty property)
        {
            builder.Append('@').Append(property.Name);
            if (!string.IsNullOrEmpty(property.Value))
            {
                builder.Append(' ').Append(property.Value);
            }
            builder.Append(NewLine);
        }

        private void AppendEntry(StringBuilder builder, TableFileEntry entry)
        {
            builder.Append(entry.Key);
            if (entry.HasFingerprint)
            {
                builder.Append(" [").Append((entry.Fingerprint & 0xFFFFFF).ToString("x6")).Append(']');
            }
            builder.Append(" =");
            if (entry.Value.Length > 0)
            {
                builder.Append(' ');
                TextEscaping.Escape(entry.Value.AsSpan(), builder);
            }
            builder.Append(NewLine);
        }
    }
}
