using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Validates the project's catalogs and the entry references its scenes, prefabs and assets hold, reporting every
    /// problem to the Console with its file and line: <c>Tools/ReactiveLocalizer/Validate</c>, and the build gate.
    /// </summary>
    internal static class LocalizationValidation
    {
        private const string LogPrefix = "[ReactiveLocalizer] ";

        /// <summary>Validates every catalog against every reference in the project, and reports what it finds.</summary>
        [MenuItem("Tools/ReactiveLocalizer/Validate", priority = 1)]
        public static void ValidateProject()
        {
            CatalogIndex.Invalidate();
            List<EntryReferenceScanner.FoundReference> references = EntryReferenceScanner.ScanProject();
            int errors = 0;
            int warnings = 0;
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                ValidationReport report = Validate(catalog, references, true);
                Log(report);
                errors += report.ErrorCount;
                warnings += report.WarningCount;
            }
            errors += ReportUnknownCatalogs(references);
            string summary = $"{LogPrefix}Validated {CatalogIndex.Catalogs.Count} catalog(s) and {references.Count} entry reference(s): {errors} error(s), {warnings} warning(s).";
            if (errors > 0)
            {
                Debug.LogError(summary);
            }
            else
            {
                Debug.Log(summary);
            }
        }

        /// <summary>
        /// Validates <paramref name="catalog"/>'s files, and the references among <paramref name="references"/> that point
        /// into it; former names nothing refers to are only reported when <paramref name="isProjectWide"/>, since a build
        /// sees only some of the project.
        /// </summary>
        public static ValidationReport Validate(IndexedCatalog catalog, IReadOnlyList<EntryReferenceScanner.FoundReference> references, bool isProjectWide)
        {
            if (catalog.Info == null)
            {
                return new ValidationReport(new[] { new ValidationIssue(IssueSeverity.Error, catalog.Path, $"The catalog '{catalog.Name}' can't be used; its file's own import reports why.") });
            }
            List<ValidatedTable> tables = new(catalog.Tables.Count);
            foreach (IndexedTable table in catalog.Tables)
            {
                List<ValidatedFile> files = new();
                foreach (string path in table.FilePaths)
                {
                    if (LocalizationFiles.TryParseTableFileName(path, out _, out string language))
                    {
                        files.Add(new ValidatedFile(path, language, CatalogIndex.ReadTableDocument(path)));
                    }
                }
                tables.Add(new ValidatedTable(table.Name, files));
            }
            List<EntryUse> uses = null;
            if (references != null)
            {
                uses = new List<EntryUse>();
                for (int i = 0; i < references.Count; i++)
                {
                    if (IsReferenceTo(catalog, references[i]))
                    {
                        uses.Add(new EntryUse(references[i].Reference.TableName, references[i].Reference.EntryName, references[i].Location));
                    }
                }
            }
            ValidationOptions options = new()
            {
                IsCheckingPascalCase = LocalizationSettings.instance.IsCheckingPascalCase,
                // A build sees only the references it ships, so a former name unused there may still be used elsewhere.
                IsCheckingUnusedAliases = isProjectWide
            };
            return CatalogValidator.Validate(catalog.Info, tables, uses, options);
        }

        /// <summary>Returns the resolver of the references into <paramref name="catalog"/>, as the build checks each scene with.</summary>
        public static KeyResolver CreateResolver(IndexedCatalog catalog)
        {
            List<(string TableName, TableDocument Source)> sources = new(catalog.Tables.Count);
            foreach (IndexedTable table in catalog.Tables)
            {
                sources.Add((table.Name, table.SourceDocument));
            }
            return new KeyResolver(sources);
        }

        /// <summary>Whether <paramref name="found"/> refers to <paramref name="catalog"/>: by its name, or by none when it is the default catalog.</summary>
        public static bool IsReferenceTo(IndexedCatalog catalog, EntryReferenceScanner.FoundReference found)
        {
            string name = found.Reference.CatalogName;
            return string.IsNullOrEmpty(name) ? catalog == CatalogIndex.DefaultCatalog : string.Equals(name, catalog.Name, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Reports references to catalogs the project doesn't have. Returns how many there were.</summary>
        public static int ReportUnknownCatalogs(IReadOnlyList<EntryReferenceScanner.FoundReference> references)
        {
            int count = 0;
            for (int i = 0; i < references.Count; i++)
            {
                string name = references[i].Reference.CatalogName;
                bool isKnown = string.IsNullOrEmpty(name)
                    ? CatalogIndex.DefaultCatalog != null
                    : NameRules.IsValid(name) && CatalogIndex.Find(new CatalogKey(name)) != null;
                if (isKnown)
                {
                    continue;
                }
                string catalog = string.IsNullOrEmpty(name) ? "the default catalog, which the project doesn't have" : $"the catalog '{name}', which the project doesn't have";
                Debug.LogError($"{LogPrefix}{references[i].Location}: error: It refers to '{references[i].Reference}' of {catalog}.");
                count++;
            }
            return count;
        }

        /// <summary>Logs every issue of <paramref name="report"/>: errors as errors, warnings as warnings.</summary>
        public static void Log(ValidationReport report)
        {
            for (int i = 0; i < report.Issues.Count; i++)
            {
                ValidationIssue issue = report.Issues[i];
                if (issue.Severity == IssueSeverity.Error)
                {
                    Debug.LogError(LogPrefix + issue);
                }
                else
                {
                    Debug.LogWarning(LogPrefix + issue);
                }
            }
        }
    }
}
