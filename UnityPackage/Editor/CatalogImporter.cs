using reromanlee.ReactiveLocalizer.Documents;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Imports a <c>Localization.catalog</c> file, reporting every problem of its syntax and its languages at its line.
    /// </summary>
    [ScriptedImporter(Version, LocalizationFiles.CatalogExtension)]
    internal sealed class CatalogImporter : ScriptedImporter
    {
        /// <summary>Raised whenever the import's checks change, so every catalog file is imported again.</summary>
        public const int Version = 2;

        public override void OnImportAsset(AssetImportContext context)
        {
            string path = context.assetPath;
            CatalogAsset asset = ScriptableObject.CreateInstance<CatalogAsset>();
            context.AddObjectToAsset("catalog", asset);
            context.SetMainObject(asset);

            if (!LocalizationFiles.TryGetCatalogName(path, out string catalogName))
            {
                context.LogImportError($"{path}: a catalog file is named <Catalog>.catalog, such as Localization.catalog, and the name follows the naming rule: {NameRules.Description}.");
                return;
            }
            asset.Initialize(catalogName);

            CatalogDocument document = CatalogDocument.Parse(LocalizationFiles.ReadAllText(path));
            List<DocumentIssue> issues = new(document.Issues);
            CatalogInfo.FromDocument(new CatalogKey(catalogName), document, null, issues);
            string folder = LocalizationFiles.GetFolder(path);
            foreach (string other in CatalogLayout.CatalogPaths)
            {
                if (other != path && string.Equals(LocalizationFiles.GetFolder(other), folder, System.StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(new DocumentIssue(IssueSeverity.Error, 1, 1, $"'{other}' is another catalog in the same folder, so it's unclear which owns its tables. Keep one catalog per folder."));
                }
            }
            ImportReports.Report(context, issues);
        }
    }
}
