using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Collects the characters the project's languages can show, for font assets that cover them: every text of
    /// every table in the language, the fallback text its missing entries show, every plural and select form, and the
    /// symbols its numbers are written with.
    /// </summary>
    /// <remarks>
    /// Text that arguments and formatters bring at runtime, such as player names and dates, isn't in the files, so a
    /// font may need more characters than these.
    /// </remarks>
    public static class CharacterSets
    {
        /// <summary>
        /// Returns <paramref name="catalog"/> as its sets are collected: its languages and their fallbacks, then the
        /// pseudo language unless it defines one itself. Null when the project has no usable catalog of that name.
        /// </summary>
        /// <param name="catalog">The catalog; empty for the default catalog.</param>
        public static CatalogInfo GetCatalog(CatalogKey catalog)
        {
            IndexedCatalog indexed = Find(catalog);
            return indexed != null ? PseudoTables.GetCatalog(indexed, true) : null;
        }

        /// <summary>
        /// Returns every character <paramref name="language"/> of <paramref name="catalog"/> can show. Null when the
        /// project has no usable catalog of that name, or the catalog no such language.
        /// </summary>
        /// <param name="catalog">The catalog; empty for the default catalog.</param>
        /// <param name="language">One of the catalog's languages, or the pseudo language.</param>
        public static CharacterSet Collect(CatalogKey catalog, LanguageKey language)
        {
            IndexedCatalog indexed = Find(catalog);
            if (indexed == null)
            {
                return null;
            }
            CatalogInfo info = PseudoTables.GetCatalog(indexed, true);
            if (!info.TryGetLanguage(language, out LanguageInfo found))
            {
                return null;
            }
            IReadOnlyList<LanguageInfo> chain = info.GetFallbackChain(found);
            ReadOnlyMemory<byte>[] layers = new ReadOnlyMemory<byte>[chain.Count];
            CharacterSet set = new();
            foreach (IndexedTable table in indexed.Tables)
            {
                for (int i = 0; i < chain.Count; i++)
                {
                    layers[i] = Read(indexed, table, chain[i].Key);
                }
                if (!set.AddTable(layers, out string problem))
                {
                    Debug.LogWarning($"[ReactiveLocalizer] The {found.Name} character set leaves out part of the table '{table.Name}': {problem}");
                }
            }
            set.AddNumberSymbols(info, found);
            return set;
        }

        private static ReadOnlyMemory<byte> Read(IndexedCatalog catalog, IndexedTable table, LanguageKey language)
        {
            if (PseudoTables.IsPseudo(catalog, language))
            {
                return PseudoTables.Compile(catalog, table, out _);
            }
            if (!table.TryGetFile(language, out string path))
            {
                return ReadOnlyMemory<byte>.Empty;
            }
            TableAsset asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
            return asset != null ? asset.Data : ReadOnlyMemory<byte>.Empty;
        }

        private static IndexedCatalog Find(CatalogKey catalog)
        {
            IndexedCatalog indexed = catalog.IsEmpty ? CatalogIndex.DefaultCatalog : CatalogIndex.Find(catalog);
            return indexed?.Info != null ? indexed : null;
        }
    }
}
