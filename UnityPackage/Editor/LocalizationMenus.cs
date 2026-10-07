using reromanlee.ReactiveLocalizer.Documents;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The <c>Assets/Create/ReactiveLocalizer</c> menu: new catalogs and new tables. Files are written directly, then
    /// selected and pinged, which behaves the same on every Unity version.
    /// </summary>
    internal static class LocalizationMenus
    {
        private const string MenuRoot = "Assets/Create/ReactiveLocalizer/";

        private const string CatalogTemplate =
            "@source English\n" +
            "\n" +
            "[English]\n" +
            "DisplayName = English\n" +
            "Culture = en\n";

        private const string TableTemplate =
            "# One entry per line, written as Key = Text. Comments right above an entry are the context translators see.\n";

        [MenuItem(MenuRoot + "Catalog", priority = 81)]
        private static void CreateCatalog()
        {
            string folder = GetSelectedFolder();
            CreateFile(AssetDatabase.GenerateUniqueAssetPath($"{folder}/{LocalizationFiles.DefaultCatalogName}.{LocalizationFiles.CatalogExtension}"), CatalogTemplate);
        }

        /// <summary>
        /// Creates a table file for the source language of the catalog that owns the selected folder. In a project
        /// with no catalog yet, the default catalog is created first.
        /// </summary>
        [MenuItem(MenuRoot + "Table", priority = 82)]
        private static void CreateTable()
        {
            string folder = GetSelectedFolder();
            string catalogPath = CatalogLayout.FindOwner($"{folder}/Table.Language.{LocalizationFiles.TableExtension}");
            if (catalogPath == null && CatalogLayout.CatalogPaths.Count == 0)
            {
                catalogPath = CreateDefaultCatalog();
                folder = LocalizationFiles.DefaultCatalogFolder;
            }
            if (catalogPath == null)
            {
                Debug.LogWarning($"[ReactiveLocalizer] The folder '{folder}' belongs to no catalog. Create the table under a catalog's folder, or keep exactly one catalog in Assets.");
                return;
            }
            string sourceLanguage = ReadSourceLanguage(catalogPath);
            CreateFile(AssetDatabase.GenerateUniqueAssetPath($"{folder}/NewTable.{sourceLanguage}.{LocalizationFiles.TableExtension}"), TableTemplate);
        }

        private static void CreateFile(string path, string content)
        {
            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path);
            Object created = AssetDatabase.LoadAssetAtPath<Object>(path);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        private static string CreateDefaultCatalog()
        {
            string path = $"{LocalizationFiles.DefaultCatalogFolder}/{LocalizationFiles.DefaultCatalogName}.{LocalizationFiles.CatalogExtension}";
            Directory.CreateDirectory(LocalizationFiles.DefaultCatalogFolder);
            File.WriteAllText(path, CatalogTemplate);
            CatalogLayout.Refresh();
            AssetDatabase.ImportAsset(path);
            Debug.Log($"[ReactiveLocalizer] Created the default catalog at {path}, written in English. Its generated classes are {LocalizationFiles.DefaultCatalogName}Keys and {LocalizationFiles.DefaultCatalogName}Languages.");
            return path;
        }

        private static string ReadSourceLanguage(string catalogPath)
        {
            CatalogDocument document = CatalogDocument.Parse(LocalizationFiles.ReadAllText(catalogPath));
            if (document.TryGetAttribute(DocumentNames.Source, out DocumentProperty source) && NameRules.IsValid(source.Value))
            {
                return source.Value;
            }
            return "English";
        }

        private static string GetSelectedFolder()
        {
            foreach (Object selected in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(selected);
                if (AssetDatabase.IsValidFolder(path))
                {
                    return path;
                }
                if (!string.IsNullOrEmpty(path))
                {
                    return LocalizationFiles.GetFolder(path);
                }
            }
            return "Assets";
        }
    }
}
