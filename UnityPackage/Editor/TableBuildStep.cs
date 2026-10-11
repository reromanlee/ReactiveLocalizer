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
    /// on demand. <see cref="TableDelivery.Custom"/> tables stay out, for the project's own source to deliver.
    /// Development builds also get the pseudo language, unless the project settings turn it off. The packed files exist
    /// only while a build runs.
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
            bool isPseudoIncluded = (report.summary.options & BuildOptions.Development) != 0 && LocalizationSettings.instance.IsOfferingPseudoLocalization;
            Pack($"{GeneratedFolder}/Resources/{EmbeddedTableSource.ResourcesRoot}", StreamingFolder, isPseudoIncluded);
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
        /// <param name="resourcesRoot">The Resources folder the catalogs and Embedded tables go to.</param>
        /// <param name="streamingRoot">The folder the Streaming tables go to.</param>
        /// <param name="isPseudoIncluded">Whether catalogs get the pseudo language, with its tables made from the source-language files.</param>
        public static void Pack(string resourcesRoot, string streamingRoot, bool isPseudoIncluded)
        {
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.IsEditorOnly || catalog.Info == null)
                {
                    continue;
                }
                string catalogFolder = $"{resourcesRoot}/{catalog.Name}";
                CatalogInfo packed = PseudoTables.GetCatalog(catalog, isPseudoIncluded);
                Write($"{catalogFolder}/{EmbeddedTableSource.CatalogFileName}.bytes", CompiledCatalog.Write(packed));
                List<(string Language, string Table)> streamed = new();
                foreach (IndexedTable table in catalog.Tables)
                {
                    if (table.Settings.Delivery == TableDelivery.Custom)
                    {
                        continue;
                    }
                    foreach (LanguageInfo language in packed.Languages)
                    {
                        byte[] data = ReadTable(catalog, table, language.Key);
                        if (data == null || data.Length == 0)
                        {
                            continue;
                        }
                        if (table.Settings.Delivery == TableDelivery.Streaming)
                        {
                            Write($"{streamingRoot}/{StreamingTableSource.GetTablePath(catalog.Name, language.Name, table.Name)}", data);
                            streamed.Add((language.Name, table.Name));
                        }
                        else
                        {
                            Write($"{catalogFolder}/{language.Name}/{table.Name}.bytes", data);
                        }
                    }
                }
                if (streamed.Count > 0)
                {
                    Write($"{catalogFolder}/{StreamingTableSource.ManifestFileName}.bytes", StreamingTableSource.WriteManifest(streamed));
                }
            }
        }

        /// <summary>Returns the compiled table of <paramref name="table"/> in <paramref name="language"/>, or null when there is none.</summary>
        private static byte[] ReadTable(IndexedCatalog catalog, IndexedTable table, LanguageKey language)
        {
            if (PseudoTables.IsPseudo(catalog, language))
            {
                byte[] pseudo = PseudoTables.Compile(catalog, table, out string problem);
                if (problem != null)
                {
                    UnityEngine.Debug.LogWarning(problem);
                }
                return pseudo;
            }
            if (!table.TryGetFile(language, out string path))
            {
                return null;
            }
            TableAsset asset = AssetDatabase.LoadAssetAtPath<TableAsset>(path);
            return asset != null ? asset.Data.ToArray() : null;
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
