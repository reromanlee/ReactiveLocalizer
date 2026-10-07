using System;
using System.IO;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>The naming rules of localization files: <c>Shop.English.lang</c> tables and <c>Localization.catalog</c> catalogs.</summary>
    internal static class LocalizationFiles
    {
        /// <summary>Extension of table files, without its dot.</summary>
        public const string TableExtension = "lang";

        /// <summary>Extension of catalog files, without its dot.</summary>
        public const string CatalogExtension = "catalog";

        /// <summary>Where the default catalog is created on first use.</summary>
        public const string DefaultCatalogFolder = "Assets/Localization";

        /// <summary>Name of the default catalog, which makes its generated classes <c>LocalizationKeys</c> and <c>LocalizationLanguages</c>.</summary>
        public const string DefaultCatalogName = "Localization";

        /// <summary>
        /// Reads the table and language names from a <c>&lt;Table&gt;.&lt;Language&gt;.lang</c> path. Both must follow the
        /// naming rule.
        /// </summary>
        public static bool TryParseTableFileName(string assetPath, out string tableName, out string languageName)
        {
            tableName = null;
            languageName = null;
            if (!HasExtension(assetPath, TableExtension))
            {
                return false;
            }
            string stem = Path.GetFileNameWithoutExtension(assetPath);
            int separator = stem.LastIndexOf('.');
            if (separator <= 0 || separator == stem.Length - 1)
            {
                return false;
            }
            string table = stem.Substring(0, separator);
            string language = stem.Substring(separator + 1);
            if (!NameRules.IsValid(table) || !NameRules.IsValid(language))
            {
                return false;
            }
            tableName = table;
            languageName = language;
            return true;
        }

        /// <summary>Reads the catalog name from a <c>&lt;Catalog&gt;.catalog</c> path. It must follow the naming rule.</summary>
        public static bool TryGetCatalogName(string assetPath, out string catalogName)
        {
            catalogName = null;
            if (!HasExtension(assetPath, CatalogExtension))
            {
                return false;
            }
            string name = Path.GetFileNameWithoutExtension(assetPath);
            if (!NameRules.IsValid(name))
            {
                return false;
            }
            catalogName = name;
            return true;
        }

        /// <summary>Returns whether a path lies inside an <c>Editor</c> folder, which keeps a catalog out of player builds.</summary>
        public static bool IsInEditorFolder(string assetPath)
        {
            string[] folders = assetPath.Replace('\\', '/').Split('/');
            // The last part is the file itself.
            for (int i = 0; i < folders.Length - 1; i++)
            {
                if (string.Equals(folders[i], "Editor", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Returns the folder of an asset path, with forward slashes and no trailing slash.</summary>
        public static string GetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            int separator = normalized.LastIndexOf('/');
            return separator < 0 ? string.Empty : normalized.Substring(0, separator);
        }

        /// <summary>
        /// Reads a file by its asset path. Package files may live outside the project, in the package cache, so the
        /// path is resolved the way Unity resolves package paths first.
        /// </summary>
        public static string ReadAllText(string assetPath) => File.ReadAllText(Path.GetFullPath(assetPath));

        /// <summary>Returns whether the file at an asset path exists, resolving package paths like <see cref="ReadAllText"/>.</summary>
        public static bool Exists(string assetPath) => File.Exists(Path.GetFullPath(assetPath));

        /// <summary>Returns when the file at an asset path was last written, resolving package paths like <see cref="ReadAllText"/>.</summary>
        public static DateTime GetLastWriteTimeUtc(string assetPath) => File.GetLastWriteTimeUtc(Path.GetFullPath(assetPath));

        private static bool HasExtension(string path, string extension)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase);
        }
    }
}
