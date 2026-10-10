using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// One table's files, one per language, edited together: adding, rewording, translating, renaming, deleting and
    /// moving entries. An operation that touches several languages changes every file it needs, or none of them.
    /// </summary>
    /// <remarks>
    /// Keys exist only in the source language, so a translation is never added for a key the source doesn't have. A
    /// translation is stamped with the fingerprint of the source text it was made from, which is how a later change
    /// of the source marks it outdated. Renaming leaves <c>@formerly</c> in the source file and moving leaves
    /// <c>@formerly OldTable.OldName</c>, so every saved reference keeps resolving. A file read with errors is never
    /// rewritten, since that would drop its broken lines; the operation reports it instead.
    /// </remarks>
    public sealed class TableFileSet
    {
        private readonly Dictionary<string, TableFile> _files = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Creates the set of files of <paramref name="tableName"/>, whose source language is <paramref name="sourceLanguage"/>.</summary>
        /// <exception cref="ArgumentException">A name breaks the naming rule.</exception>
        public TableFileSet(string tableName, string sourceLanguage)
        {
            NameRules.ThrowIfInvalid(tableName, nameof(tableName));
            NameRules.ThrowIfInvalid(sourceLanguage, nameof(sourceLanguage));
            TableName = tableName;
            SourceLanguage = sourceLanguage;
        }

        /// <summary>Name of the table.</summary>
        public string TableName { get; }

        /// <summary>The language that defines the table's keys.</summary>
        public string SourceLanguage { get; }

        /// <summary>The source-language file, or null when the table has none yet.</summary>
        public TableFile Source => Get(SourceLanguage);

        /// <summary>The languages the table has a file for.</summary>
        public IEnumerable<string> Languages => _files.Keys;

        /// <summary>Returns the file of <paramref name="language"/>, or null when there is none.</summary>
        public TableFile Get(string language) => language != null && _files.TryGetValue(language, out TableFile file) ? file : null;

        /// <summary>Sets the file of <paramref name="language"/>, as read from disk.</summary>
        public void Set(string language, TableFile file)
        {
            NameRules.ThrowIfInvalid(language, nameof(language));
            _files[language] = file ?? throw new ArgumentNullException(nameof(file));
        }

        /// <summary>Returns how <paramref name="key"/> stands in <paramref name="language"/>.</summary>
        public TranslationState GetState(string language, string key)
        {
            TableFileEntry source = null;
            Source?.TryGetEntry(key, out source);
            TableFile file = Get(language);
            if (file == null || !file.TryGetEntry(key, out TableFileEntry entry))
            {
                return TranslationState.Missing;
            }
            if (source == null)
            {
                return TranslationState.Orphan;
            }
            if (IsSource(language))
            {
                return TranslationState.Current;
            }
            if (!entry.HasFingerprint)
            {
                return TranslationState.Unverified;
            }
            return entry.Fingerprint == Hashing.ComputeFingerprint(source.Value) ? TranslationState.Current : TranslationState.Outdated;
        }

        /// <summary>Adds an entry to the source language, creating its file when the table has none yet.</summary>
        public bool TryAddEntry(string key, string text, out string problem)
        {
            if (!CheckName(key, out problem) || !CheckWritable(SourceLanguage, out problem))
            {
                return false;
            }
            if (FindKeyOwner(key) is string owner)
            {
                problem = $"'{TableName}' already has '{key}' {owner}.";
                return false;
            }
            GetOrCreate(SourceLanguage).TryAddEntry(new TableFileEntry(key, text));
            return true;
        }

        /// <summary>
        /// Sets the text of <paramref name="key"/> in <paramref name="language"/>. A translation is stamped as made from
        /// the current source text; rewording the source leaves its translations to show as outdated.
        /// </summary>
        public bool TrySetText(string language, string key, string text, out string problem)
        {
            if (!TryGetSourceEntry(key, out TableFileEntry source, out problem) || !CheckWritable(language, out problem))
            {
                return false;
            }
            if (IsSource(language))
            {
                source.Value = text ?? string.Empty;
                return true;
            }
            TableFile file = GetOrCreate(language);
            if (!file.TryGetEntry(key, out TableFileEntry entry))
            {
                entry = new TableFileEntry(source.Key, text);
                file.TryAddEntry(entry);
            }
            entry.Value = text ?? string.Empty;
            entry.StampFingerprint(source.Value);
            return true;
        }

        /// <summary>Removes the translation of <paramref name="key"/> in <paramref name="language"/>, which then shows its fallback.</summary>
        public bool TryRemoveTranslation(string language, string key, out string problem)
        {
            if (IsSource(language))
            {
                problem = $"{language} is the source language of '{TableName}'; delete the entry instead.";
                return false;
            }
            TableFile file = Get(language);
            if (file == null || file.IndexOf(key) < 0)
            {
                problem = $"'{TableName}.{key}' has no {language} text.";
                return false;
            }
            if (!CheckWritable(language, out problem))
            {
                return false;
            }
            file.RemoveEntry(key);
            return true;
        }

        /// <summary>Marks the translation of <paramref name="key"/> as made from the current source text, as after checking it.</summary>
        public bool TryMarkCurrent(string language, string key, out string problem)
        {
            if (!TryGetSourceEntry(key, out TableFileEntry source, out problem) || !CheckWritable(language, out problem))
            {
                return false;
            }
            TableFile file = Get(language);
            if (IsSource(language) || file == null || !file.TryGetEntry(key, out TableFileEntry entry))
            {
                problem = $"'{TableName}.{key}' has no {language} translation to mark.";
                return false;
            }
            entry.StampFingerprint(source.Value);
            return true;
        }

        /// <summary>Sets the comments above <paramref name="key"/> in the source language: the context translators see.</summary>
        public bool TrySetContext(string key, IReadOnlyList<string> comments, out string problem)
        {
            if (!TryGetSourceEntry(key, out TableFileEntry source, out problem) || !CheckWritable(SourceLanguage, out problem))
            {
                return false;
            }
            source.Comments.Clear();
            if (comments != null)
            {
                for (int i = 0; i < comments.Count; i++)
                {
                    // A comment is one line; a line break would turn the rest into something else.
                    source.Comments.AddRange((comments[i] ?? string.Empty).Replace("\r\n", "\n").Split('\n'));
                }
            }
            return true;
        }

        /// <summary>Sets the longest text translations of <paramref name="key"/> may have, or removes the limit when null.</summary>
        public bool TrySetMaximumLength(string key, int? maximumLength, out string problem)
        {
            if (maximumLength <= 0)
            {
                problem = "A maximum length counts characters, so it is at least 1.";
                return false;
            }
            if (!TryGetSourceEntry(key, out TableFileEntry source, out problem) || !CheckWritable(SourceLanguage, out problem))
            {
                return false;
            }
            source.SetAttribute(DocumentNames.MaximumLength, maximumLength?.ToString(CultureInfo.InvariantCulture));
            return true;
        }

        /// <summary>
        /// Renames <paramref name="key"/> to <paramref name="newKey"/> in every language, leaving <c>@formerly</c> with the
        /// old key in the source file so saved references keep resolving.
        /// </summary>
        public bool TryRename(string key, string newKey, out string problem)
        {
            if (!CheckName(newKey, out problem) || !TryGetSourceEntry(key, out TableFileEntry source, out problem) || !CheckEveryFileWritable(key, out problem))
            {
                return false;
            }
            bool isCaseOnly = string.Equals(source.Key, newKey, StringComparison.OrdinalIgnoreCase);
            if (!isCaseOnly && FindKeyOwner(newKey, source.Key) is string owner)
            {
                problem = $"'{TableName}' already has '{newKey}' {owner}.";
                return false;
            }
            string oldKey = source.Key;
            foreach (TableFile file in _files.Values)
            {
                file.RenameEntry(oldKey, newKey);
            }
            // Lookups ignore case, so a change of case needs no alias; renaming back drops the alias it would restore.
            RemoveFormerly(source, newKey);
            if (!isCaseOnly)
            {
                source.Attributes.Add(new DocumentProperty(DocumentNames.Formerly, oldKey, 0, null));
            }
            return true;
        }

        /// <summary>Deletes <paramref name="key"/> from every language. References to it, and to its aliases, stop resolving.</summary>
        public bool TryDelete(string key, out string problem)
        {
            if (!TryGetSourceEntry(key, out _, out problem) || !CheckEveryFileWritable(key, out problem))
            {
                return false;
            }
            foreach (TableFile file in _files.Values)
            {
                file.RemoveEntry(key);
            }
            return true;
        }

        /// <summary>
        /// Moves <paramref name="key"/>, with its text in every language, to <paramref name="target"/>, a table of the same
        /// catalog. The entry keeps its key, and its new source file keeps <c>@formerly ThisTable.Key</c>, so saved
        /// references keep resolving.
        /// </summary>
        public bool TryMoveTo(string key, TableFileSet target, out string problem)
        {
            if (target == null || string.Equals(target.TableName, TableName, StringComparison.OrdinalIgnoreCase))
            {
                problem = "An entry moves to another table of its catalog.";
                return false;
            }
            if (!string.Equals(target.SourceLanguage, SourceLanguage, StringComparison.OrdinalIgnoreCase))
            {
                problem = $"'{target.TableName}' is written in {target.SourceLanguage} and '{TableName}' in {SourceLanguage}, so they aren't tables of one catalog.";
                return false;
            }
            if (!TryGetSourceEntry(key, out TableFileEntry source, out problem) || !CheckEveryFileWritable(key, out problem))
            {
                return false;
            }
            if (target.FindKeyOwner(source.Key) is string owner)
            {
                problem = $"'{target.TableName}' already has '{source.Key}' {owner}.";
                return false;
            }
            foreach (KeyValuePair<string, TableFile> file in _files)
            {
                if (file.Value.IndexOf(key) >= 0 && !target.CheckWritable(file.Key, out problem))
                {
                    return false;
                }
            }

            foreach (KeyValuePair<string, TableFile> file in _files)
            {
                if (!file.Value.TryGetEntry(key, out TableFileEntry entry))
                {
                    continue;
                }
                TableFileEntry moved = entry.Clone();
                if (IsSource(file.Key))
                {
                    QualifyAliases(moved, target.TableName);
                }
                target.GetOrCreate(file.Key).TryAddEntry(moved);
                file.Value.RemoveEntry(key);
            }
            return true;
        }

        /// <summary>Whether any file of the set was read with errors, which keeps operations from rewriting it.</summary>
        public bool HasErrors
        {
            get
            {
                foreach (TableFile file in _files.Values)
                {
                    if (file.HasErrors)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private TableFile GetOrCreate(string language)
        {
            if (!_files.TryGetValue(language, out TableFile file))
            {
                file = new TableFile();
                // A new translation follows the line endings its source file uses.
                if (Source != null)
                {
                    file.NewLine = Source.NewLine;
                }
                _files.Add(language, file);
            }
            return file;
        }

        private bool IsSource(string language) => string.Equals(language, SourceLanguage, StringComparison.OrdinalIgnoreCase);

        private bool TryGetSourceEntry(string key, out TableFileEntry entry, out string problem)
        {
            entry = null;
            if (Source == null || !Source.TryGetEntry(key, out entry))
            {
                problem = $"'{TableName}' has no entry '{key}'.";
                return false;
            }
            problem = null;
            return true;
        }

        /// <summary>
        /// Returns where <paramref name="key"/> is already taken in this table: as an entry of the source, as an alias, or
        /// as an orphan of a translation that renaming into it would merge with. Null when it is free. The entry
        /// <paramref name="ignoredKey"/> doesn't count, as when an entry is renamed back to a name it had.
        /// </summary>
        private string FindKeyOwner(string key, string ignoredKey = null)
        {
            TableFile source = Source;
            if (source != null)
            {
                for (int i = 0; i < source.Entries.Count; i++)
                {
                    TableFileEntry entry = source.Entries[i];
                    if (string.Equals(entry.Key, ignoredKey, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return "as an entry";
                    }
                    List<string> aliases = entry.GetAttributeValues(DocumentNames.Formerly);
                    for (int a = 0; a < aliases.Count; a++)
                    {
                        string alias = aliases[a];
                        if (MovedEntries.TryParseQualified(alias, out string table, out string name) &&
                            string.Equals(table, TableName, StringComparison.OrdinalIgnoreCase))
                        {
                            alias = name;
                        }
                        if (string.Equals(alias, key, StringComparison.OrdinalIgnoreCase))
                        {
                            return $"as a former name of '{entry.Key}'";
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, TableFile> file in _files)
            {
                if (!IsSource(file.Key) && file.Value.IndexOf(key) >= 0)
                {
                    return $"in its {file.Key} file, with no source text";
                }
            }
            return null;
        }

        private bool CheckWritable(string language, out string problem)
        {
            TableFile file = Get(language);
            if (file != null && file.HasErrors)
            {
                problem = $"The {language} file of '{TableName}' has errors. Fix them first, since rewriting the file would drop the lines that have them.";
                return false;
            }
            problem = null;
            return true;
        }

        /// <summary>Checks every file that has <paramref name="key"/>, since renaming, deleting and moving touch all of them.</summary>
        private bool CheckEveryFileWritable(string key, out string problem)
        {
            foreach (KeyValuePair<string, TableFile> file in _files)
            {
                if ((IsSource(file.Key) || file.Value.IndexOf(key) >= 0) && !CheckWritable(file.Key, out problem))
                {
                    return false;
                }
            }
            problem = null;
            return true;
        }

        private static bool CheckName(string name, out string problem)
        {
            if (NameRules.IsValid(name))
            {
                problem = null;
                return true;
            }
            problem = $"'{name}' can't be a key: {NameRules.Description}.";
            return false;
        }

        /// <summary>
        /// Rewrites the aliases of an entry moving to <paramref name="targetTable"/>: its in-table aliases and its own key
        /// become aliases qualified with this table, and qualified aliases of the target table become plain ones there.
        /// </summary>
        private void QualifyAliases(TableFileEntry entry, string targetTable)
        {
            List<DocumentProperty> aliases = new();
            for (int i = entry.Attributes.Count - 1; i >= 0; i--)
            {
                if (entry.Attributes[i].Name == DocumentNames.Formerly)
                {
                    aliases.Insert(0, entry.Attributes[i]);
                    entry.Attributes.RemoveAt(i);
                }
            }
            HashSet<string> written = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < aliases.Count; i++)
            {
                string alias = aliases[i].Value;
                if (!MovedEntries.TryParseQualified(alias, out string table, out string name))
                {
                    alias = $"{TableName}.{alias}";
                }
                else if (string.Equals(table, targetTable, StringComparison.OrdinalIgnoreCase))
                {
                    alias = name;
                }
                AddFormerly(entry, alias, written);
            }
            AddFormerly(entry, $"{TableName}.{entry.Key}", written);
        }

        private static void AddFormerly(TableFileEntry entry, string alias, HashSet<string> written)
        {
            // An alias equal to the entry's own key would name the entry twice.
            if (!string.Equals(alias, entry.Key, StringComparison.OrdinalIgnoreCase) && written.Add(alias))
            {
                entry.Attributes.Add(new DocumentProperty(DocumentNames.Formerly, alias, 0, null));
            }
        }

        private void RemoveFormerly(TableFileEntry entry, string key)
        {
            for (int i = entry.Attributes.Count - 1; i >= 0; i--)
            {
                DocumentProperty attribute = entry.Attributes[i];
                if (attribute.Name != DocumentNames.Formerly)
                {
                    continue;
                }
                string alias = attribute.Value;
                if (MovedEntries.TryParseQualified(alias, out string table, out string name) && string.Equals(table, TableName, StringComparison.OrdinalIgnoreCase))
                {
                    alias = name;
                }
                if (string.Equals(alias, key, StringComparison.OrdinalIgnoreCase))
                {
                    entry.Attributes.RemoveAt(i);
                }
            }
        }
    }
}
