using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System.Collections.Generic;
using UnityEditor.Compilation;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageSource = UnityEditor.PackageManager.PackageSource;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// A catalog of the project with everything the editor knows about it: its file, its tables and their files, and
    /// the catalog definition built from both.
    /// </summary>
    internal sealed class IndexedCatalog
    {
        private readonly Dictionary<ulong, IndexedTable> _tables = new();
        private readonly List<IndexedTable> _tableList = new();

        public IndexedCatalog(string path, string name)
        {
            Path = path;
            Name = name;
            Key = new CatalogKey(name);
            Folder = LocalizationFiles.GetFolder(path);
            IsEditorOnly = LocalizationFiles.IsInEditorFolder(path);
            PackageInfo package = PackageInfo.FindForAssetPath(path);
            // Assets and embedded or local packages can be written to; registry and git packages are read-only copies.
            IsWritable = package == null || package.source == PackageSource.Embedded || package.source == PackageSource.Local;
            Document = CatalogDocument.Parse(LocalizationFiles.ReadAllText(path));
        }

        public string Path { get; }

        public string Folder { get; }

        public string Name { get; }

        public CatalogKey Key { get; }

        /// <summary>Whether the catalog sits in an Editor folder, which keeps it out of player builds.</summary>
        public bool IsEditorOnly { get; }

        /// <summary>Whether the editor may write next to the catalog, which generated code needs.</summary>
        public bool IsWritable { get; }

        public CatalogDocument Document { get; }

        /// <summary>The catalog definition with its tables, or null when the catalog file can't be used.</summary>
        public CatalogInfo Info { get; private set; }

        public IReadOnlyList<IndexedTable> Tables => _tableList;

        /// <summary>Where the catalog's generated code goes: <c>LocalizationKeys.cs</c> next to the catalog.</summary>
        public string GeneratedCodePath => $"{Folder}/{Name}Keys.cs";

        public void AddFile(string tableName, string languageName, string assetPath)
        {
            TableKey key = new(tableName);
            if (!_tables.TryGetValue(key.Hash, out IndexedTable table))
            {
                table = new IndexedTable(tableName);
                _tables.Add(key.Hash, table);
                _tableList.Add(table);
            }
            table.AddFile(new LanguageKey(languageName), assetPath);
        }

        public bool TryGetTable(TableKey key, out IndexedTable table) => _tables.TryGetValue(key.Hash, out table);

        /// <summary>Builds the catalog definition once every table file is added. Problems are reported by the imports, not here.</summary>
        public void Complete()
        {
            _tableList.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            List<DocumentIssue> ignored = new();
            CatalogInfo withoutTables = CatalogInfo.FromDocument(Key, Document, null, ignored);
            if (withoutTables == null)
            {
                return;
            }
            List<TableInfo> tables = new(_tableList.Count);
            for (int i = 0; i < _tableList.Count; i++)
            {
                _tableList[i].ReadSource(withoutTables.SourceLanguage.Key, ignored);
                tables.Add(_tableList[i].Info);
            }
            Info = new CatalogInfo(Key, withoutTables.SourceLanguage.Key, withoutTables.Languages, tables);
        }

        /// <summary>
        /// Returns what the catalog's generated code is made of. The namespace is the catalog's <c>@namespace</c>, else
        /// the root namespace of the assembly owning the catalog's folder, else the project's root namespace.
        /// </summary>
        public KeysScript CreateKeysScript()
        {
            List<string> languages = new();
            if (Info != null)
            {
                for (int i = 0; i < Info.Languages.Count; i++)
                {
                    languages.Add(Info.Languages[i].Name);
                }
            }
            List<KeysScriptTable> tables = new();
            for (int i = 0; i < _tableList.Count; i++)
            {
                IndexedTable table = _tableList[i];
                if (table.SourceDocument == null || !table.Settings.GeneratesCode)
                {
                    continue;
                }
                List<string> entries = new();
                List<KeyValuePair<string, string>> aliases = new();
                IReadOnlyList<TableDocumentEntry> documentEntries = table.SourceDocument.Entries;
                for (int e = 0; e < documentEntries.Count; e++)
                {
                    TableDocumentEntry entry = documentEntries[e];
                    entries.Add(entry.Key);
                    for (int a = 0; a < entry.Attributes.Count; a++)
                    {
                        if (entry.Attributes[a].Name == DocumentNames.Formerly && NameRules.IsValid(entry.Attributes[a].Value))
                        {
                            aliases.Add(new KeyValuePair<string, string>(entry.Attributes[a].Value, entry.Key));
                        }
                    }
                }
                tables.Add(new KeysScriptTable(table.Name, entries, aliases));
            }
            return new KeysScript(Name, ResolveNamespace(), languages, tables);
        }

        private string ResolveNamespace()
        {
            if (Document.TryGetAttribute(DocumentNames.Namespace, out DocumentProperty attribute) && attribute.Value.Length > 0)
            {
                return attribute.Value;
            }
            string assemblyNamespace = CompilationPipeline.GetAssemblyRootNamespaceFromScriptPath(GeneratedCodePath);
            if (!string.IsNullOrEmpty(assemblyNamespace))
            {
                return assemblyNamespace;
            }
            return UnityEditor.EditorSettings.projectGenerationRootNamespace;
        }
    }
}
