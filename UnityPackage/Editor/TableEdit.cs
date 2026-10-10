using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// A table of an indexed catalog opened for editing: its files across every language as a
    /// <see cref="TableFileSet"/>, and what each file said when opened, so saving writes only the files an edit
    /// changed, as one step Undo can take back.
    /// </summary>
    internal sealed class TableEdit
    {
        private readonly IndexedCatalog _catalog;
        private readonly Dictionary<string, string> _baseline = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Opens the files of <paramref name="tableName"/>, or nothing for a table the catalog doesn't have yet.</summary>
        /// <exception cref="ArgumentException">The catalog can't be used, or the table name breaks the naming rule.</exception>
        public TableEdit(IndexedCatalog catalog, string tableName)
        {
            if (catalog?.Info == null)
            {
                throw new ArgumentException("Only a usable catalog's tables can be edited.", nameof(catalog));
            }
            _catalog = catalog;
            Files = new TableFileSet(tableName, catalog.Info.SourceLanguage.Name);
            if (!catalog.TryGetTable(new TableKey(tableName), out IndexedTable table))
            {
                return;
            }
            foreach (string path in table.FilePaths)
            {
                if (LocalizationFiles.TryParseTableFileName(path, out _, out string language))
                {
                    TableFile file = TableFile.Read(LocalizationFiles.ReadAllText(path));
                    Files.Set(language, file);
                    _baseline[language] = file.Write();
                }
            }
        }

        /// <summary>The table's files, to edit through their operations.</summary>
        public TableFileSet Files { get; }

        /// <summary>Saves every file the edits changed as one step Undo can take back, named <paramref name="undoName"/>.</summary>
        public static void Save(string undoName, params TableEdit[] edits)
        {
            List<KeyValuePair<string, string>> changes = new();
            for (int i = 0; i < edits.Length; i++)
            {
                edits[i].CollectChanges(changes);
            }
            TableFileUndo.Write(changes, undoName);
        }

        /// <summary>Returns where the file of this table in <paramref name="language"/> is, or goes: next to the table's source file.</summary>
        public string GetPath(string language)
        {
            if (_catalog.TryGetTable(new TableKey(Files.TableName), out IndexedTable table))
            {
                if (table.TryGetFile(new LanguageKey(language), out string existing))
                {
                    return existing;
                }
                if (table.SourcePath != null)
                {
                    return $"{LocalizationFiles.GetFolder(table.SourcePath)}/{Files.TableName}.{language}.{LocalizationFiles.TableExtension}";
                }
            }
            return $"{_catalog.Folder}/{Files.TableName}.{language}.{LocalizationFiles.TableExtension}";
        }

        private void CollectChanges(List<KeyValuePair<string, string>> changes)
        {
            foreach (string language in Files.Languages)
            {
                TableFile file = Files.Get(language);
                // A file read with errors is never rewritten; every operation already refused to change it.
                if (file.HasErrors)
                {
                    continue;
                }
                string text = file.Write();
                _baseline.TryGetValue(language, out string baseline);
                if (!string.Equals(text, baseline, StringComparison.Ordinal))
                {
                    changes.Add(new KeyValuePair<string, string>(GetPath(language), text));
                    _baseline[language] = text;
                }
            }
        }
    }
}
