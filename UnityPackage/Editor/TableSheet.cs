using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// A table laid out for the table window: a row per entry with its context and, per language, its text, how it
    /// stands against the source text, and what compiling finds wrong with it. Edits go through the table's files and
    /// save at once, as one step Undo can take back.
    /// </summary>
    /// <remarks>
    /// After its own edits the sheet rebuilds its rows from the files it holds, reading back only the files it wrote;
    /// a file changed elsewhere, as by Undo or a text editor, makes <see cref="IsCurrent"/> false, and the window
    /// opens the table again.
    /// </remarks>
    internal sealed class TableSheet
    {
        /// <summary>Changes the files of a table, or says why it can't.</summary>
        public delegate bool Operation(TableFileSet files, out string problem);

        private readonly Dictionary<string, DateTime> _writeTimes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, TableSheetRow> _rowsByKey = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<LanguageInfo> _languages = new();

        /// <summary>Opens <paramref name="tableName"/> of <paramref name="catalog"/>, which must be usable.</summary>
        public TableSheet(IndexedCatalog catalog, string tableName)
        {
            Catalog = catalog;
            TableName = tableName;
            _languages.Add(catalog.Info.SourceLanguage);
            for (int i = 0; i < catalog.Info.Languages.Count; i++)
            {
                if (catalog.Info.Languages[i] != catalog.Info.SourceLanguage)
                {
                    _languages.Add(catalog.Info.Languages[i]);
                }
            }
            Edit = new TableEdit(catalog, tableName);
            RememberWriteTimes();
            Build();
        }

        public IndexedCatalog Catalog { get; }

        public string TableName { get; }

        /// <summary>The table's files, which operations change.</summary>
        public TableEdit Edit { get; }

        /// <summary>The catalog's languages, the source language first.</summary>
        public IReadOnlyList<LanguageInfo> Languages => _languages;

        /// <summary>One row per entry of the source language in natural order, then the orphans of the translations.</summary>
        public List<TableSheetRow> Rows { get; } = new();

        /// <summary>Problems of the table's files that concern no single entry, such as malformed lines.</summary>
        public List<string> FileProblems { get; } = new();

        /// <summary>Whether a file of the table was read with errors, which keeps edits from rewriting it.</summary>
        public bool HasUnreadableFiles => Edit.Files.HasErrors;

        /// <summary>Returns the row of <paramref name="key"/>, ignoring case, or null.</summary>
        public TableSheetRow Find(string key) => key != null && _rowsByKey.TryGetValue(key, out TableSheetRow row) ? row : null;

        /// <summary>Whether the table's files on disk are still the ones this sheet read or wrote last.</summary>
        public bool IsCurrent()
        {
            IndexedCatalog catalog = EntryPreview.FindCatalog(Catalog.Name);
            if (catalog == null)
            {
                return false;
            }
            int count = 0;
            if (catalog.TryGetTable(new TableKey(TableName), out IndexedTable table))
            {
                foreach (string path in table.FilePaths)
                {
                    count++;
                    if (!_writeTimes.TryGetValue(path, out DateTime known) || known != LocalizationFiles.GetLastWriteTimeUtc(path))
                    {
                        return false;
                    }
                }
            }
            return count == _writeTimes.Count;
        }

        /// <summary>Runs <paramref name="operation"/> on the table's files and saves what it changed, as the Undo step <paramref name="undoName"/>.</summary>
        public bool TryApply(string undoName, Operation operation, out string problem)
        {
            if (!operation(Edit.Files, out problem))
            {
                return false;
            }
            TableEdit.Save(undoName, Edit);
            RememberWriteTimes();
            Build();
            return true;
        }

        private void RememberWriteTimes()
        {
            _writeTimes.Clear();
            for (int i = 0; i < _languages.Count; i++)
            {
                if (Edit.Files.Get(_languages[i].Name) == null)
                {
                    continue;
                }
                string path = Edit.GetPath(_languages[i].Name);
                if (LocalizationFiles.Exists(path))
                {
                    _writeTimes[path] = LocalizationFiles.GetLastWriteTimeUtc(path);
                }
            }
        }

        private void Build()
        {
            Rows.Clear();
            _rowsByKey.Clear();
            FileProblems.Clear();
            TableFile source = Edit.Files.Source;
            if (source != null)
            {
                List<TableFileEntry> entries = new(source.Entries);
                entries.Sort((left, right) => NaturalOrder.Instance.Compare(left.Key, right.Key));
                for (int i = 0; i < entries.Count; i++)
                {
                    AddRow(entries[i].Key, entries[i]);
                }
            }
            for (int l = 1; l < _languages.Count; l++)
            {
                TableFile file = Edit.Files.Get(_languages[l].Name);
                for (int i = 0; file != null && i < file.Entries.Count; i++)
                {
                    if (Find(file.Entries[i].Key) == null)
                    {
                        AddRow(file.Entries[i].Key, null);
                    }
                }
            }
            for (int r = 0; r < Rows.Count; r++)
            {
                FillCells(Rows[r]);
            }
            AddIssues();
        }

        private void AddRow(string key, TableFileEntry source)
        {
            TableSheetRow row = new(key, source, _languages.Count);
            Rows.Add(row);
            _rowsByKey.Add(key, row);
        }

        private void FillCells(TableSheetRow row)
        {
            uint sourceFingerprint = row.Source != null ? Hashing.ComputeFingerprint(row.Source.Value) : 0;
            int limit = 0;
            bool hasLimit = row.Source != null && row.Source.GetAttributeValues(DocumentNames.MaximumLength) is { Count: > 0 } limits &&
                            MaximumLength.TryParse(limits[0], out limit);
            for (int l = 0; l < _languages.Count; l++)
            {
                TableFile file = Edit.Files.Get(_languages[l].Name);
                TableFileEntry entry = null;
                file?.TryGetEntry(row.Key, out entry);
                TranslationState state;
                if (entry == null)
                {
                    state = TranslationState.Missing;
                }
                else if (row.Source == null)
                {
                    state = TranslationState.Orphan;
                }
                else if (l == 0)
                {
                    state = TranslationState.Current;
                }
                else if (!entry.HasFingerprint)
                {
                    state = TranslationState.Unverified;
                }
                else
                {
                    state = entry.Fingerprint == sourceFingerprint ? TranslationState.Current : TranslationState.Outdated;
                }
                TableSheetCell cell = new(entry?.Value, state);
                if (hasLimit && entry != null && MaximumLength.IsExceededBy(entry.Value, limit))
                {
                    cell.AddProblem(TableSheetProblem.OverLimit, $"{entry.Value.Length} characters, over the maximum of {limit}.");
                }
                row.Cells[l] = cell;
            }
        }

        /// <summary>Adds what compiling each file finds, from the files on disk, which the sheet's own files match after every save.</summary>
        private void AddIssues()
        {
            Dictionary<string, List<DocumentIssue>> byKey = new(StringComparer.OrdinalIgnoreCase);
            List<DocumentIssue> fileIssues = new();
            TableDocument sourceDocument = ReadDocument(_languages[0].Name);
            for (int l = 0; l < _languages.Count; l++)
            {
                TableDocument document = l == 0 ? sourceDocument : ReadDocument(_languages[l].Name);
                if (document == null)
                {
                    continue;
                }
                byKey.Clear();
                fileIssues.Clear();
                EntryIssues.Collect(Catalog.Info, TableName, _languages[l].Name, document, l == 0 ? null : sourceDocument, byKey, fileIssues);
                foreach (KeyValuePair<string, List<DocumentIssue>> issues in byKey)
                {
                    TableSheetRow row = Find(issues.Key);
                    for (int i = 0; row != null && i < issues.Value.Count; i++)
                    {
                        DocumentIssue issue = issues.Value[i];
                        row.Cells[l].AddProblem(issue.Severity == IssueSeverity.Error ? TableSheetProblem.Error : TableSheetProblem.Warning, issue.Message);
                    }
                }
                for (int i = 0; i < fileIssues.Count; i++)
                {
                    FileProblems.Add($"{_languages[l].Name}, line {fileIssues[i].Line}: {fileIssues[i].Message}");
                }
            }
        }

        private TableDocument ReadDocument(string language)
        {
            if (Edit.Files.Get(language) == null)
            {
                return null;
            }
            string path = Edit.GetPath(language);
            return LocalizationFiles.Exists(path) ? CatalogIndex.ReadTableDocument(path) : null;
        }
    }

    /// <summary>An entry of a <see cref="TableSheet"/>: its key, its source entry, and a cell per language.</summary>
    internal sealed class TableSheetRow
    {
        public TableSheetRow(string key, TableFileEntry source, int languageCount)
        {
            Key = key;
            Source = source;
            Cells = new TableSheetCell[languageCount];
            Context = source == null || source.Comments.Count == 0 ? string.Empty : string.Join(" ", source.Comments);
        }

        public string Key { get; }

        /// <summary>The entry in the source language; null for an orphan.</summary>
        public TableFileEntry Source { get; }

        /// <summary>The comments above the source entry, on one line: the context translators see.</summary>
        public string Context { get; }

        /// <summary>One cell per language of the sheet, in its order.</summary>
        public TableSheetCell[] Cells { get; }

        /// <summary>Whether no language has a problem with the entry and every translation is current.</summary>
        public bool IsClean
        {
            get
            {
                for (int i = 0; i < Cells.Length; i++)
                {
                    if (Cells[i].Problems != TableSheetProblem.None || Cells[i].State != TranslationState.Current)
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }

    /// <summary>An entry in one language: its text, how it stands against the source, and its problems.</summary>
    internal struct TableSheetCell
    {
        public TableSheetCell(string text, TranslationState state)
        {
            Text = text;
            State = state;
            Problems = TableSheetProblem.None;
            Message = null;
        }

        /// <summary>The text; null when the language has none.</summary>
        public string Text { get; }

        public TranslationState State { get; }

        public TableSheetProblem Problems { get; private set; }

        /// <summary>What the problems are, one per line; null without any.</summary>
        public string Message { get; private set; }

        public void AddProblem(TableSheetProblem problem, string message)
        {
            Problems |= problem;
            Message = Message == null ? message : Message + "\n" + message;
        }
    }

    /// <summary>The problems a cell of a <see cref="TableSheet"/> can have.</summary>
    [Flags]
    internal enum TableSheetProblem
    {
        None = 0,

        /// <summary>Compiling the text fails, as with broken ICU or arguments the source doesn't have.</summary>
        Error = 1,

        /// <summary>Compiling the text warns, as about a plural form the language doesn't use.</summary>
        Warning = 2,

        /// <summary>The text is longer than the entry's <c>@maximumLength</c>.</summary>
        OverLimit = 4
    }
}
