using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>A table of an indexed catalog: its file per language, its settings and its source-language document.</summary>
    internal sealed class IndexedTable
    {
        private readonly Dictionary<ulong, string> _files = new();

        public IndexedTable(string name)
        {
            Name = name;
            Key = new TableKey(name);
        }

        public string Name { get; }

        public TableKey Key { get; }

        /// <summary>Asset path of the source-language file, or null when the table has none.</summary>
        public string SourcePath { get; private set; }

        /// <summary>The source-language file, read; null when the table has none.</summary>
        public TableDocument SourceDocument { get; private set; }

        public TableSettings Settings { get; private set; } = TableSettings.Default;

        /// <summary>Identifies the keys of the source-language file, which complete translations are compiled with; zero without one.</summary>
        public ulong KeysHash { get; private set; }

        public TableInfo Info => new(Key, Settings.Loading, Settings.Delivery, KeysHash);

        /// <summary>Asset paths of the table's files, one per language.</summary>
        public IEnumerable<string> FilePaths => _files.Values;

        public void AddFile(LanguageKey language, string assetPath)
        {
            _files[language.Hash] = assetPath;
        }

        public bool TryGetFile(LanguageKey language, out string assetPath) => _files.TryGetValue(language.Hash, out assetPath);

        /// <summary>Reads the source-language file, which defines the table's keys and chooses its settings.</summary>
        public void ReadSource(LanguageKey sourceLanguage, List<DocumentIssue> issues)
        {
            if (!TryGetFile(sourceLanguage, out string path))
            {
                return;
            }
            SourcePath = path;
            SourceDocument = CatalogIndex.ReadTableDocument(path);
            Settings = TableSettings.Read(SourceDocument, issues);
            KeysHash = TableCompiler.ComputeKeysHash(SourceDocument);
        }
    }
}
