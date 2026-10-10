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

        /// <summary>What a new table file starts with.</summary>
        internal const string TableTemplate =
            "# One entry per line, written as Key = Text. Comments right above an entry are the context translators see.\n";

        /// <summary>Asks for the new catalog's name and source language, then creates it in the selected folder.</summary>
        [MenuItem(MenuRoot + "Catalog", priority = 81)]
        private static void CreateCatalog()
        {
            CatalogCreationWindow.Show(GetSelectedFolder(), null);
        }

        /// <summary>
        /// Creates a table file for the source language of the catalog that owns the selected folder. In a project
        /// with no catalog yet, the default catalog is created first, asking for its source language.
        /// </summary>
        [MenuItem(MenuRoot + "Table", priority = 82)]
        private static void CreateTable()
        {
            string folder = GetSelectedFolder();
            string catalogPath = CatalogLayout.FindOwner($"{folder}/Table.Language.{LocalizationFiles.TableExtension}");
            if (catalogPath == null && CatalogLayout.CatalogPaths.Count == 0)
            {
                CatalogCreationWindow.Show(LocalizationFiles.DefaultCatalogFolder, created => CreateTableFile(LocalizationFiles.DefaultCatalogFolder, created));
                return;
            }
            if (catalogPath == null)
            {
                Debug.LogWarning($"[ReactiveLocalizer] The folder '{folder}' belongs to no catalog. Create the table under a catalog's folder, or keep exactly one catalog in Assets.");
                return;
            }
            CreateTableFile(folder, catalogPath);
        }

        private static void CreateTableFile(string folder, string catalogPath)
        {
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
