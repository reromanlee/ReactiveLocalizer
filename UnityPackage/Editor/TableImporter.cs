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
    /// <remarks>
    /// An import depends on its catalog, whose cultures decide the plural forms each message needs, and a translation
    /// also on its table's source-language file, whose messages it is checked against. Changing either imports the
    /// file again, so a translation is never left checked against an outdated source.
    /// </remarks>
    [ScriptedImporter(Version, LocalizationFiles.TableExtension)]
    internal sealed class TableImporter : ScriptedImporter
    {
        /// <summary>Raised whenever the import's output changes, so every table file is imported again.</summary>
        public const int Version = 3;

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

            context.DependsOnSourceAsset(catalogPath);
            TableDocument document = TableDocument.Parse(LocalizationFiles.ReadAllText(path));
            List<DocumentIssue> issues = new(document.Issues);
            TableKey table = new(tableName);
            LanguageKey language = new(languageName);
            // The catalog's own import reports why it's unusable; its tables still compile, with their syntax checked.
            CatalogInfo catalog = CatalogInfo.FromDocument(new CatalogKey(catalogName), CatalogDocument.Parse(LocalizationFiles.ReadAllText(catalogPath)), null, null);
            byte[] data;
            if (catalog == null)
            {
                data = TableCompiler.Compile(new CatalogKey(catalogName), table, language, document, issues);
            }
            else
            {
                TableDocument source = null;
                if (language != catalog.SourceLanguage.Key)
                {
                    string sourcePath = CatalogLayout.FindTableFile(catalogPath, tableName, catalog.SourceLanguage.Name);
                    if (sourcePath != null)
                    {
                        context.DependsOnSourceAsset(sourcePath);
                        source = TableDocument.Parse(LocalizationFiles.ReadAllText(sourcePath));
                    }
                }
                data = TableCompiler.Compile(catalog, table, language, document, source, issues);
            }
            ImportReports.Report(context, issues);
            asset.Initialize(catalogName, tableName, languageName, data);
        }
    }
}
