using System;
using UnityEditor;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Watches localization files. Whenever a catalog changes, ownership is rescanned, which reimports the tables it
    /// moved between catalogs; whenever any localization file changes, the index is rebuilt on next use and generated
    /// code is brought up to date.
    /// </summary>
    internal sealed class LocalizationPostprocessor : AssetPostprocessor
    {
        private static bool _isRefreshScheduled;

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
                CatalogIndex.Invalidate();
                KeysGenerator.Schedule();
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
