using reromanlee.ReactiveLocalizer.Tables;
using reromanlee.ReactiveLocalizer.Unity;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Packs every runtime catalog and its <see cref="TableDelivery.Embedded"/> tables into the build, as the Resources
    /// <see cref="EmbeddedTableSource"/> reads synchronously on every platform. The packed files exist only while a
    /// build runs.
    /// </summary>
    /// <remarks>
    /// Catalogs in Editor folders stay out. A failed build never reaches its postprocess step, so the first editor
    /// update after any build also removes the packed files.
    /// </remarks>
    internal sealed class EmbeddedBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>The folder that holds the packed files during a build.</summary>
        public const string GeneratedFolder = "Assets/ReactiveLocalizerBuild";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            RemoveGeneratedFolder();
            CatalogIndex.Invalidate();
            string root = $"{GeneratedFolder}/Resources/{EmbeddedTableSource.ResourcesRoot}";
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.IsEditorOnly || catalog.Info == null)
                {
                    continue;
                }
                string catalogFolder = $"{root}/{catalog.Name}";
                Write($"{catalogFolder}/{EmbeddedTableSource.CatalogFileName}.bytes", CompiledCatalog.Write(catalog.Info));
                foreach (IndexedTable table in catalog.Tables)
                {
                    if (table.Settings.Delivery != TableDelivery.Embedded)
                    {
                        continue;
                    }
                    foreach (LanguageInfo language in catalog.Info.Languages)
                    {
                        if (!table.TryGetFile(language.Key, out string path))
                        {
                            continue;
                        }
                        TableAsset asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
                        if (asset != null && !asset.Data.IsEmpty)
                        {
                            Write($"{catalogFolder}/{language.Name}/{table.Name}.bytes", asset.Data.ToArray());
                        }
                    }
                }
            }
            AssetDatabase.Refresh();
            EditorApplication.update -= RemoveAfterBuild;
            EditorApplication.update += RemoveAfterBuild;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RemoveGeneratedFolder();
        }

        [InitializeOnLoadMethod]
        private static void RemoveLeftovers()
        {
            EditorApplication.delayCall += RemoveAfterBuild;
        }

        private static void RemoveAfterBuild()
        {
            // The editor doesn't update while a build runs, so the first update after one finds it finished.
            if (BuildPipeline.isBuildingPlayer)
            {
                return;
            }
            EditorApplication.update -= RemoveAfterBuild;
            RemoveGeneratedFolder();
        }

        private static void RemoveGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedFolder))
            {
                AssetDatabase.DeleteAsset(GeneratedFolder);
            }
            else if (Directory.Exists(GeneratedFolder))
            {
                Directory.Delete(GeneratedFolder, true);
                File.Delete(GeneratedFolder + ".meta");
            }
        }

        private static void Write(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}
