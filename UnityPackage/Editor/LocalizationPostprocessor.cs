using System;
using System.Collections.Generic;
using UnityEditor;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Watches localization files. Whenever a catalog changes, ownership is rescanned, which reimports the tables it
    /// moved between catalogs; whenever any localization file changes, the index is rebuilt on next use, generated
    /// code is brought up to date, and every live localizer reloads, so bound text shows the edit in Play Mode and
    /// Edit Mode alike.
    /// </summary>
    internal sealed class LocalizationPostprocessor : AssetPostprocessor
    {
        private static readonly HashSet<string> TranslationsToImport = new(StringComparer.Ordinal);
        private static bool _isRefreshScheduled;
        private static bool _isTranslationImportScheduled;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool hasCatalogChanged = HasFile(importedAssets, LocalizationFiles.CatalogExtension) ||
                                     HasFile(deletedAssets, LocalizationFiles.CatalogExtension) ||
                                     HasFile(movedAssets, LocalizationFiles.CatalogExtension) ||
                                     HasFile(movedFromAssetPaths, LocalizationFiles.CatalogExtension);
            bool hasTableChanged = HasFile(importedAssets, LocalizationFiles.TableExtension) ||
                                   HasFile(deletedAssets, LocalizationFiles.TableExtension) ||
                                   HasFile(movedAssets, LocalizationFiles.TableExtension);
            if (hasCatalogChanged)
            {
                // A changed layout gets a new dependency hash, which marks every table for reimport; registering an
                // unchanged hash reimports nothing. The refresh that performs the reimport can't run from inside a
                // postprocessor, so it runs right after.
                CatalogLayout.Refresh();
                ScheduleRefresh();
            }
            if (hasCatalogChanged || hasTableChanged)
            {
                CatalogLayout.ForgetTableFiles();
                CatalogIndex.Invalidate();
                KeysGenerator.Schedule();
                // Imports are done by now, so the localizers read the new files right away.
                Localizer.ReloadAll();
            }
            if (hasTableChanged)
            {
                ScheduleTranslationImports(importedAssets, movedAssets);
            }
        }

        /// <summary>
        /// Queues the translations of every source-language file that just arrived, unless they were imported along
        /// with it: a translation imported before its source existed has never been checked against it.
        /// </summary>
        private static void ScheduleTranslationImports(string[] importedAssets, string[] movedAssets)
        {
            HashSet<string> imported = new(importedAssets, StringComparer.Ordinal);
            foreach (string path in Concatenate(importedAssets, movedAssets))
            {
                if (!LocalizationFiles.TryParseTableFileName(path, out string tableName, out string languageName))
                {
                    continue;
                }
                IndexedCatalog catalog = FindCatalog(path);
                if (catalog?.Info == null || !string.Equals(catalog.Info.SourceLanguage.Name, languageName, StringComparison.OrdinalIgnoreCase) ||
                    !catalog.TryGetTable(new TableKey(tableName), out IndexedTable table))
                {
                    continue;
                }
                foreach (string translation in table.FilePaths)
                {
                    if (translation != path && !imported.Contains(translation))
                    {
                        TranslationsToImport.Add(translation);
                    }
                }
            }
            if (TranslationsToImport.Count == 0 || _isTranslationImportScheduled)
            {
                return;
            }
            _isTranslationImportScheduled = true;
            EditorApplication.delayCall += ImportTranslations;
        }

        private static void ImportTranslations()
        {
            _isTranslationImportScheduled = false;
            string[] paths = new string[TranslationsToImport.Count];
            TranslationsToImport.CopyTo(paths);
            TranslationsToImport.Clear();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in paths)
                {
                    AssetDatabase.ImportAsset(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        private static IndexedCatalog FindCatalog(string tablePath)
        {
            string catalogPath = CatalogLayout.FindOwner(tablePath);
            if (catalogPath == null)
            {
                return null;
            }
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.Path == catalogPath)
                {
                    return catalog;
                }
            }
            return null;
        }

        private static IEnumerable<string> Concatenate(string[] first, string[] second)
        {
            foreach (string path in first)
            {
                yield return path;
            }
            foreach (string path in second)
            {
                yield return path;
            }
        }

        private static void ScheduleRefresh()
        {
            if (_isRefreshScheduled)
            {
                return;
            }
            _isRefreshScheduled = true;
            EditorApplication.delayCall += () =>
            {
                _isRefreshScheduled = false;
                AssetDatabase.Refresh();
            };
        }

        private static bool HasFile(string[] paths, string extension)
        {
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i].EndsWith("." + extension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
