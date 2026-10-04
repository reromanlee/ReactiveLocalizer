using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Imports a <c>Shop.English.lang</c> file as a <see cref="TableAsset"/>: the table compiled for the catalog that
    /// owns its folder, with every problem reported at its line.
    /// </summary>
    [ScriptedImporter(Version, LocalizationFiles.TableExtension)]
    internal sealed class TableImporter : ScriptedImporter
    {
        /// <summary>Raised whenever the import's output changes, so every table file is imported again.</summary>
        public const int Version = 1;

        public override void OnImportAsset(AssetImportContext context)
        {
            // Ownership depends on where the catalogs are, so this import reruns whenever catalogs are added or moved.
            context.DependsOnCustomDependency(CatalogLayout.DependencyName);
            string path = context.assetPath;
            TableAsset asset = ScriptableObject.CreateInstance<TableAsset>();
            context.AddObjectToAsset("table", asset);
            context.SetMainObject(asset);

            if (!LocalizationFiles.TryParseTableFileName(path, out string tableName, out string languageName))
            {
                context.LogImportError($"{path}: a table file is named <Table>.<Language>.lang, such as Shop.English.lang, and both names follow the naming rule: {NameRules.Description}.");
                asset.Initialize(null, null, null, null);
                return;
            }
            string catalogPath = CatalogLayout.FindOwner(path);
            if (catalogPath == null || !LocalizationFiles.TryGetCatalogName(catalogPath, out string catalogName))
            {
                context.LogImportWarning($"{path}: this table belongs to no catalog. Put a .catalog file in its folder or a parent folder, or keep exactly one catalog in Assets for tables outside every catalog folder.");
                asset.Initialize(null, tableName, languageName, null);
                return;
            }

            TableDocument document = TableDocument.Parse(LocalizationFiles.ReadAllText(path));
            List<DocumentIssue> issues = new(document.Issues);
            byte[] data = TableCompiler.Compile(new CatalogKey(catalogName), new TableKey(tableName), new LanguageKey(languageName), document, issues);
            ImportReports.Report(context, issues);
            asset.Initialize(catalogName, tableName, languageName, data);
        }
    }
}
