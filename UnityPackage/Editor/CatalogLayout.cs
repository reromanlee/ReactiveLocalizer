using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Where the catalogs are, and which catalog owns each table file: the nearest catalog in the file's folder or a
    /// parent folder, the way an asmdef claims scripts. A table in Assets outside every catalog folder belongs to the
    /// project's default catalog: <c>Assets/Localization/Localization.catalog</c> when it exists, otherwise the one
    /// catalog in Assets outside an Editor folder.
    /// </summary>
    /// <remarks>
    /// Catalogs are found by scanning the file system rather than the asset database, so table imports never depend
    /// on whether the catalogs imported first. Every table import depends on <see cref="DependencyName"/>, whose hash
    /// changes whenever catalogs are added, removed or moved, so ownership changes reimport the tables they affect.
    /// </remarks>
    internal static class CatalogLayout
    {
        /// <summary>The custom dependency every table import declares.</summary>
        public const string DependencyName = "reromanlee.ReactiveLocalizer/CatalogLayout";

        private static string[] _catalogPaths;

        /// <summary>Every catalog file in Assets and the packages, as asset paths, sorted.</summary>
        public static IReadOnlyList<string> CatalogPaths => _catalogPaths ??= Scan();

        /// <summary>
        /// The catalog that owns tables in Assets outside every catalog folder: the catalog at the default location
        /// when there is one, so a sample bringing its own catalog changes nothing; otherwise the one catalog in Assets
        /// outside an Editor folder. Null when neither exists, or there are several candidates.
        /// </summary>
        public static string DefaultCatalogPath
        {
            get
            {
                string defaultLocation = $"{LocalizationFiles.DefaultCatalogFolder}/{LocalizationFiles.DefaultCatalogName}.{LocalizationFiles.CatalogExtension}";
                string found = null;
                foreach (string path in CatalogPaths)
                {
                    if (string.Equals(path, defaultLocation, StringComparison.OrdinalIgnoreCase))
                    {
                        return path;
                    }
                }
                foreach (string path in CatalogPaths)
                {
                    if (!path.StartsWith("Assets/", StringComparison.Ordinal) || LocalizationFiles.IsInEditorFolder(path))
                    {
                        continue;
                    }
                    if (found != null)
                    {
                        return null;
                    }
                    found = path;
                }
                return found;
            }
        }

        /// <summary>Scans for catalogs again and updates the dependency, which reimports the tables when the layout changed.</summary>
        public static void Refresh()
        {
            _catalogPaths = Scan();
            RegisterDependency();
        }

        /// <summary>Returns the asset path of the catalog that owns the file at <paramref name="assetPath"/>, or null.</summary>
        public static string FindOwner(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            string root = GetRoot(normalized);
            if (root == null)
            {
                return null;
            }
            string folder = LocalizationFiles.GetFolder(normalized);
            while (folder.Length >= root.Length)
            {
                string catalog = FindCatalogIn(folder);
                if (catalog != null)
                {
                    return catalog;
                }
                if (folder.Length == root.Length)
                {
                    break;
                }
                folder = LocalizationFiles.GetFolder(folder);
            }
            // Only tables in Assets fall back to the default catalog; a package's tables always belong to its own.
            return root == "Assets" ? DefaultCatalogPath : null;
        }

        [InitializeOnLoadMethod]
        private static void RegisterDependency()
        {
            StringBuilder joined = new();
            foreach (string path in CatalogPaths)
            {
                joined.Append(path).Append('\n');
            }
            AssetDatabase.RegisterCustomDependency(DependencyName, Hash128.Compute(joined.ToString()));
        }

        private static string FindCatalogIn(string folder)
        {
            // Two catalogs in one folder are reported by their own imports; ownership goes to the first, in path order.
            foreach (string path in CatalogPaths)
            {
                if (string.Equals(LocalizationFiles.GetFolder(path), folder, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            return null;
        }

        /// <summary>Returns <c>Assets</c>, or the <c>Packages/name</c> root of a package path, or null for anything else.</summary>
        private static string GetRoot(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return "Assets";
            }
            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                int end = assetPath.IndexOf('/', "Packages/".Length);
                return end < 0 ? null : assetPath.Substring(0, end);
            }
            return null;
        }

        private static string[] Scan()
        {
            List<string> paths = new();
            ScanFolder("Assets", Path.GetFullPath("Assets"), paths);
            foreach (PackageInfo package in PackageInfo.GetAllRegisteredPackages())
            {
                // Unity's own packages hold thousands of files and never a catalog; scanning them would slow every domain reload.
                if (package.source == UnityEditor.PackageManager.PackageSource.BuiltIn ||
                    package.name.StartsWith("com.unity.", StringComparison.Ordinal))
                {
                    continue;
                }
                ScanFolder(package.assetPath, package.resolvedPath, paths);
            }
            paths.Sort(StringComparer.Ordinal);
            return paths.ToArray();
        }

        private static void ScanFolder(string assetRoot, string fullRoot, List<string> paths)
        {
            if (string.IsNullOrEmpty(fullRoot) || !Directory.Exists(fullRoot))
            {
                return;
            }
            string normalizedRoot = fullRoot.Replace('\\', '/').TrimEnd('/');
            foreach (string file in Directory.EnumerateFiles(fullRoot, "*." + LocalizationFiles.CatalogExtension, SearchOption.AllDirectories))
            {
                string relative = file.Replace('\\', '/').Substring(normalizedRoot.Length);
                if (!IsIgnoredByUnity(relative))
                {
                    paths.Add(assetRoot + relative);
                }
            }
        }

        /// <summary>Returns whether Unity skips a path: any part starting with a dot or ending with a tilde, like Samples~.</summary>
        private static bool IsIgnoredByUnity(string relativePath)
        {
            string[] parts = relativePath.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].StartsWith(".", StringComparison.Ordinal) || parts[i].EndsWith("~", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
