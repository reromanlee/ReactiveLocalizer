using reromanlee.ReactiveLocalizer.Authoring;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Fails a player build that would ship broken localization: errors in the runtime catalogs' files, entries missing
    /// in required languages, and references to keys that don't exist, in the built scenes or in Resources.
    /// </summary>
    /// <remarks>
    /// Catalogs are checked before the build starts. Each scene is checked as the build processes it, with the values
    /// its prefab instances actually have. Warnings are logged and never fail the build. Project Settings &gt;
    /// ReactiveLocalizer can turn the gate off, which ships whatever the errors break.
    /// </remarks>
    internal sealed class ValidationBuildStep : IPreprocessBuildWithReport, IProcessSceneWithReport
    {
        private const string LogPrefix = "[ReactiveLocalizer] ";

        // The resolvers of the runtime catalogs for the build in progress, by catalog name.
        private static Dictionary<string, KeyResolver> _resolvers;

        // Runs before the packing step, so a failing build never packs anything.
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            _resolvers = null;
            if (!LocalizationSettings.instance.IsFailingBuildsOnErrors)
            {
                return;
            }
            CatalogIndex.Invalidate();
            List<EntryReferenceScanner.FoundReference> resources = EntryReferenceScanner.ScanResources();
            Dictionary<string, KeyResolver> resolvers = new(System.StringComparer.OrdinalIgnoreCase);
            int errors = 0;
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.IsEditorOnly)
                {
                    continue;
                }
                ValidationReport validation = LocalizationValidation.Validate(catalog, resources, false);
                LocalizationValidation.Log(validation);
                errors += validation.ErrorCount;
                if (catalog.Info != null)
                {
                    resolvers[catalog.Name] = catalog.Resolver;
                }
            }
            errors += LocalizationValidation.ReportUnknownCatalogs(resources);
            if (errors > 0)
            {
                throw new BuildFailedException($"{LogPrefix}Localization validation found {errors} error(s); the Console lists each with its file and line. Fix them, or turn the check off in Project Settings > ReactiveLocalizer.");
            }
            _resolvers = resolvers;
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // Entering Play Mode processes scenes too, with no report; only builds are checked.
            if (report == null || _resolvers == null)
            {
                return;
            }
            int errors = 0;
            foreach (EntryReferenceScanner.FoundReference found in EntryReferenceScanner.ScanLoadedScene(scene))
            {
                if (!TryFindResolver(found, out KeyResolver resolver, out string catalogName))
                {
                    Debug.LogError($"{LogPrefix}{found.Location}: error: It refers to '{found.Reference}' of {catalogName}, which isn't a catalog of the build.");
                    errors++;
                    continue;
                }
                KeyResolution resolution = resolver.Resolve(found.Reference.TableName, found.Reference.EntryName);
                if (resolution.IsFound)
                {
                    continue;
                }
                string suggestion = resolution.Suggestion != null ? $" Did you mean '{resolution.Suggestion}'?" : string.Empty;
                Debug.LogError($"{LogPrefix}{found.Location}: error: It refers to '{found.Reference}', which doesn't exist.{suggestion}");
                errors++;
            }
            if (errors > 0)
            {
                throw new BuildFailedException($"{LogPrefix}{scene.path} refers to {errors} key(s) that don't exist; the Console lists each. Fix them, or turn the check off in Project Settings > ReactiveLocalizer.");
            }
        }

        private static bool TryFindResolver(EntryReferenceScanner.FoundReference found, out KeyResolver resolver, out string catalogName)
        {
            string name = found.Reference.CatalogName;
            if (string.IsNullOrEmpty(name))
            {
                IndexedCatalog fallback = CatalogIndex.DefaultCatalog;
                name = fallback?.Name;
                catalogName = "the default catalog";
            }
            else
            {
                catalogName = $"the catalog '{name}'";
            }
            resolver = null;
            return name != null && _resolvers.TryGetValue(name, out resolver);
        }
    }
}
