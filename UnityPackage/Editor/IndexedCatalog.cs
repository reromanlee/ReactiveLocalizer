using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
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
            List<(string TableName, TableDocument Source)> sources = new(_tableList.Count);
            for (int i = 0; i < _tableList.Count; i++)
            {
                _tableList[i].ReadSource(withoutTables.SourceLanguage.Key, ignored);
                tables.Add(_tableList[i].Info);
                sources.Add((_tableList[i].Name, _tableList[i].SourceDocument));
            }
            Info = new CatalogInfo(Key, withoutTables.SourceLanguage.Key, withoutTables.Languages, tables, MovedEntries.Collect(sources));
        }

        /// <summary>
        /// Returns what the catalog's generated code is made of. The namespace is the catalog's <c>@namespace</c>, else
        /// the root namespace of the assembly owning the catalog's folder, else the project's root namespace.
        /// <paramref name="brokenMessage"/> names the first source entry whose message has errors, or is null.
        /// </summary>
        public KeysScript CreateKeysScript(out string brokenMessage)
        {
            brokenMessage = null;
            List<string> languages = new();
            if (Info != null)
            {
                for (int i = 0; i < Info.Languages.Count; i++)
                {
                    languages.Add(Info.Languages[i].Name);
                }
            }
            // Per former table, the entries that moved from it, which its generated class keeps under their former names.
            Dictionary<string, List<KeysScriptMovedEntry>> movedFrom = new(StringComparer.OrdinalIgnoreCase);
            List<(IndexedTable Table, List<KeysScriptEntry> Entries, List<KeyValuePair<string, string>> Aliases)> generated = new();
            for (int i = 0; i < _tableList.Count; i++)
            {
                IndexedTable table = _tableList[i];
                if (table.SourceDocument == null)
                {
                    continue;
                }
                List<KeysScriptEntry> entries = new();
                List<KeyValuePair<string, string>> aliases = new();
                IReadOnlyList<TableDocumentEntry> documentEntries = table.SourceDocument.Entries;
                for (int e = 0; e < documentEntries.Count; e++)
                {
                    TableDocumentEntry entry = documentEntries[e];
                    KeysScriptEntry scriptEntry = KeysScriptEntry.FromSource(entry.Key, entry.Value);
                    entries.Add(scriptEntry);
                    if (scriptEntry.HasMessageErrors && brokenMessage == null && table.Settings.GeneratesCode)
                    {
                        brokenMessage = $"'{table.Name}.{entry.Key}' in {table.SourcePath}, line {entry.Line}";
                    }
                    for (int a = 0; a < entry.Attributes.Count; a++)
                    {
                        if (entry.Attributes[a].Name != DocumentNames.Formerly)
                        {
                            continue;
                        }
                        string alias = entry.Attributes[a].Value;
                        if (MovedEntries.TryParseQualified(alias, out string formerTable, out string formerEntry))
                        {
                            if (!string.Equals(formerTable, table.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!movedFrom.TryGetValue(formerTable, out List<KeysScriptMovedEntry> moved))
                                {
                                    moved = new List<KeysScriptMovedEntry>();
                                    movedFrom.Add(formerTable, moved);
                                }
                                moved.Add(new KeysScriptMovedEntry(formerEntry, table.Name, scriptEntry));
                                continue;
                            }
                            alias = formerEntry;
                        }
                        if (NameRules.IsValid(alias))
                        {
                            aliases.Add(new KeyValuePair<string, string>(alias, entry.Key));
                        }
                    }
                }
                generated.Add((table, entries, aliases));
            }

            List<KeysScriptTable> tables = new();
            for (int i = 0; i < generated.Count; i++)
            {
                IndexedTable table = generated[i].Table;
                movedFrom.TryGetValue(table.Name, out List<KeysScriptMovedEntry> moved);
                movedFrom.Remove(table.Name);
                if (table.Settings.GeneratesCode)
                {
                    tables.Add(new KeysScriptTable(table.Name, generated[i].Entries, generated[i].Aliases, moved));
                }
            }
            // A table every entry moved out of keeps a class of its own, holding only their former names.
            foreach (KeyValuePair<string, List<KeysScriptMovedEntry>> former in movedFrom)
            {
                tables.Add(new KeysScriptTable(former.Key, Array.Empty<KeysScriptEntry>(), null, former.Value));
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
