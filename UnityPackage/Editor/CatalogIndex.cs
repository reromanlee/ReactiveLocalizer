using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Every catalog of the project with its tables, built on first use and rebuilt after localization files change.
    /// The editor table source, generated code, the build step and the Inspector all read from it.
    /// </summary>
    /// <remarks>
    /// Rebuilding reads only the files that changed since they were last read, so a project with thousands of table
    /// files rebuilds in proportion to what was edited.
    /// </remarks>
    internal static class CatalogIndex
    {
        private static readonly Dictionary<string, (DateTime WriteTime, TableDocument Document)> Documents = new(StringComparer.Ordinal);
        private static List<IndexedCatalog> _catalogs;

        /// <summary>Raised whenever the index is forgotten, as localization files change, so views showing it can refresh.</summary>
        public static event Action Invalidated;

        public static IReadOnlyList<IndexedCatalog> Catalogs => _catalogs ??= Build();

        /// <summary>Counts how many times the index was forgotten, so what is read from it can be cached until it changes.</summary>
        public static int Version { get; private set; }

        /// <summary>The catalog tables outside every catalog folder belong to, or null when there is none or more than one.</summary>
        public static IndexedCatalog DefaultCatalog
        {
            get
            {
                string path = CatalogLayout.DefaultCatalogPath;
                if (path == null)
                {
                    return null;
                }
                foreach (IndexedCatalog catalog in Catalogs)
                {
                    if (catalog.Path == path)
                    {
                        return catalog;
                    }
                }
                return null;
            }
        }

        /// <summary>Forgets the index, so the next use rebuilds it.</summary>
        public static void Invalidate()
        {
            _catalogs = null;
            Version++;
            // A view failing to refresh must not stop the import that changed the files.
            try
            {
                Invalidated?.Invoke();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        public static IndexedCatalog Find(CatalogKey key)
        {
            foreach (IndexedCatalog catalog in Catalogs)
            {
                if (catalog.Key == key)
                {
                    return catalog;
                }
            }
            return null;
        }

        /// <summary>Reads a table file, reusing the last reading while the file is unchanged.</summary>
        public static TableDocument ReadTableDocument(string assetPath)
        {
            DateTime writeTime = LocalizationFiles.GetLastWriteTimeUtc(assetPath);
            if (Documents.TryGetValue(assetPath, out (DateTime WriteTime, TableDocument Document) cached) && cached.WriteTime == writeTime)
            {
                return cached.Document;
            }
            TableDocument document = TableDocument.Parse(LocalizationFiles.ReadAllText(assetPath));
            Documents[assetPath] = (writeTime, document);
            return document;
        }

        private static List<IndexedCatalog> Build()
        {
            Dictionary<string, IndexedCatalog> byPath = new(StringComparer.Ordinal);
            List<IndexedCatalog> catalogs = new();
            foreach (string path in CatalogLayout.CatalogPaths)
            {
                if (!LocalizationFiles.TryGetCatalogName(path, out string name) || !LocalizationFiles.Exists(path))
                {
                    continue;
                }
                IndexedCatalog catalog = new(path, name);
                byPath.Add(path, catalog);
                catalogs.Add(catalog);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TableAsset)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!LocalizationFiles.TryParseTableFileName(path, out string table, out string language))
                {
                    continue;
                }
                string owner = CatalogLayout.FindOwner(path);
                if (owner != null && byPath.TryGetValue(owner, out IndexedCatalog catalog))
                {
                    catalog.AddFile(table, language, path);
                }
            }
            for (int i = 0; i < catalogs.Count; i++)
            {
                catalogs[i].Complete();
            }
            return catalogs;
        }
    }
}
