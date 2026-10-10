using reromanlee.ReactiveLocalizer.Tables;
using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Packs every runtime catalog and its tables into the build: <see cref="TableDelivery.Embedded"/> tables into the
    /// Resources <see cref="EmbeddedTableSource"/> reads synchronously on every platform, and
    /// <see cref="TableDelivery.Streaming"/> tables into the StreamingAssets <see cref="StreamingTableSource"/> reads
    /// on demand. The packed files exist only while a build runs.
    /// </summary>
    /// <remarks>
    /// Catalogs in Editor folders stay out. A failed build never reaches its postprocess step, so the first editor
    /// update after any build also removes the packed files, and a StreamingAssets folder created only for them.
    /// </remarks>
    internal sealed class TableBuildStep : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /// <summary>The folder that holds the Resources files during a build.</summary>
        public const string GeneratedFolder = "Assets/ReactiveLocalizerBuild";

        /// <summary>The folder that holds the streaming files during a build.</summary>
        public const string StreamingFolder = "Assets/StreamingAssets/" + StreamingTableSource.FolderName;

        private const string StreamingAssetsFolder = "Assets/StreamingAssets";
        private const string CreatedStreamingAssetsKey = "ReactiveLocalizer.CreatedStreamingAssets";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            RemovePackedFiles();
            CatalogIndex.Invalidate();
            if (!Directory.Exists(StreamingAssetsFolder))
            {
                SessionState.SetBool(CreatedStreamingAssetsKey, true);
            }
            Pack($"{GeneratedFolder}/Resources/{EmbeddedTableSource.ResourcesRoot}", StreamingFolder);
            AssetDatabase.Refresh();
            EditorApplication.update -= RemoveAfterBuild;
            EditorApplication.update += RemoveAfterBuild;
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            RemovePackedFiles();
        }

        /// <summary>
        /// Writes the compiled catalogs and Embedded tables under <paramref name="resourcesRoot"/>, and the Streaming
        /// tables under <paramref name="streamingRoot"/>, with each catalog's list of them next to it in Resources.
        /// </summary>
        public static void Pack(string resourcesRoot, string streamingRoot)
        {
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.IsEditorOnly || catalog.Info == null)
                {
                    continue;
                }
                string catalogFolder = $"{resourcesRoot}/{catalog.Name}";
                Write($"{catalogFolder}/{EmbeddedTableSource.CatalogFileName}.bytes", CompiledCatalog.Write(catalog.Info));
                List<(string Language, string Table)> streamed = new();
                foreach (IndexedTable table in catalog.Tables)
                {
                    foreach (LanguageInfo language in catalog.Info.Languages)
                    {
                        if (!table.TryGetFile(language.Key, out string path))
                        {
                            continue;
                        }
                        TableAsset asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
                        if (asset == null || asset.Data.IsEmpty)
                        {
                            continue;
                        }
                        if (table.Settings.Delivery == TableDelivery.Streaming)
                        {
                            Write($"{streamingRoot}/{StreamingTableSource.GetTablePath(catalog.Name, language.Name, table.Name)}", asset.Data.ToArray());
                            streamed.Add((language.Name, table.Name));
                        }
                        else
                        {
                            Write($"{catalogFolder}/{language.Name}/{table.Name}.bytes", asset.Data.ToArray());
                        }
                    }
                }
                if (streamed.Count > 0)
                {
                    Write($"{catalogFolder}/{StreamingTableSource.ManifestFileName}.bytes", StreamingTableSource.WriteManifest(streamed));
                }
            }
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
            RemovePackedFiles();
        }

        private static void RemovePackedFiles()
        {
            RemoveFolder(GeneratedFolder);
            RemoveFolder(StreamingFolder);
            // StreamingAssets goes too when the build step created it and nothing else was put in it since.
            if (SessionState.GetBool(CreatedStreamingAssetsKey, false) && Directory.Exists(StreamingAssetsFolder) &&
                Directory.GetFileSystemEntries(StreamingAssetsFolder).Length == 0)
            {
                RemoveFolder(StreamingAssetsFolder);
            }
            if (!Directory.Exists(StreamingAssetsFolder))
            {
                SessionState.EraseBool(CreatedStreamingAssetsKey);
            }
        }

        private static void RemoveFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.DeleteAsset(folder);
            }
            else if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
                File.Delete(folder + ".meta");
            }
        }

        private static void Write(string path, byte[] data)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}
