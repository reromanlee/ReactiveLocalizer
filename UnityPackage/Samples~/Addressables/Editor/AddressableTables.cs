using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.Addressables.Editor
{
    /// <summary>
    /// <c>Tools/ReactiveLocalizer/Samples/Mark Custom Tables Addressable</c>: makes every file of every table its
    /// source-language file marks <c>@delivery Custom</c> an Addressable at the address
    /// <see cref="AddressablesTableSource"/> loads it from, in the group <see cref="GroupName"/>.
    /// </summary>
    /// <remarks>
    /// Run it after adding a Custom table or one of its languages, then build the Addressables content as usual. Files
    /// already in another group move to this one; the group's other settings are the project's to choose.
    /// </remarks>
    internal static class AddressableTables
    {
        /// <summary>The Addressables group the tables go to, created on first use.</summary>
        public const string GroupName = "ReactiveLocalizer Tables";

        [MenuItem("Tools/ReactiveLocalizer/Samples/Mark Custom Tables Addressable")]
        private static void Mark()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AddressableAssetGroup group = settings.FindGroup(GroupName) ??
                settings.CreateGroup(GroupName, false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));

            // Every imported table file, grouped by its catalog and table; only the source file says how its table ships.
            Dictionary<(string Catalog, string Table), List<(string Guid, TableAsset Asset)>> tables = new();
            HashSet<(string Catalog, string Table)> custom = new();
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(TableAsset)}"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TableAsset asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
                if (asset == null || string.IsNullOrEmpty(asset.CatalogName) || string.IsNullOrEmpty(asset.TableName))
                {
                    continue;
                }
                (string, string) table = (asset.CatalogName, asset.TableName);
                if (!tables.TryGetValue(table, out List<(string Guid, TableAsset Asset)> files))
                {
                    files = new List<(string Guid, TableAsset Asset)>();
                    tables.Add(table, files);
                }
                files.Add((guid, asset));
                if (IsCustom(path))
                {
                    custom.Add(table);
                }
            }

            int marked = 0;
            foreach (KeyValuePair<(string Catalog, string Table), List<(string Guid, TableAsset Asset)>> table in tables)
            {
                if (!custom.Contains(table.Key))
                {
                    continue;
                }
                foreach ((string guid, TableAsset asset) in table.Value)
                {
                    AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
                    entry.SetAddress(AddressablesTableSource.GetAddress(asset.CatalogName, asset.LanguageName, asset.TableName), false);
                    marked++;
                }
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log(marked > 0
                ? $"[ReactiveLocalizer] Marked {marked} table file(s) of {custom.Count} Custom table(s) Addressable, in the group '{GroupName}'."
                : "[ReactiveLocalizer] No table is marked '@delivery Custom', so nothing was made Addressable.");
        }

        private static bool IsCustom(string path)
        {
            try
            {
                return TableSettings.Read(TableDocument.Parse(File.ReadAllText(path)), null).Delivery == TableDelivery.Custom;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[ReactiveLocalizer] {path} couldn't be read: {exception.Message}");
                return false;
            }
        }
    }
}
