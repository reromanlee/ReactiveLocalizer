using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The localizers <see cref="EditModePreview"/> hands out: one per catalog, created on first use, in the preview
    /// language, or the catalog's source language when it lacks that one. They are disposed before Play Mode and
    /// before scripts reload, so nothing of them outlives Edit Mode.
    /// </summary>
    /// <remarks>
    /// It also keeps preview text out of saved files: before a scene or prefab saves, every component in it showing
    /// preview text puts its authored text back, and shows the preview again once the file is written.
    /// </remarks>
    [InitializeOnLoad]
    internal static class PreviewLocalizers
    {
        private static readonly Dictionary<ulong, Localizer> Localizers = new();
        private static readonly List<Component> Restored = new();
        private static readonly List<ulong> Gone = new();
        private static bool _isPruneScheduled;

        static PreviewLocalizers()
        {
            EditModePreview.Provider = Get;
            LocalizationUserSettings.PreviewLanguageChanged += ApplyPreviewLanguage;
            EditorApplication.playModeStateChanged += change =>
            {
                if (change == PlayModeStateChange.ExitingEditMode)
                {
                    DisposeAll();
                }
            };
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            CatalogIndex.Invalidated += SchedulePrune;
            EditorSceneManager.sceneSaving += (scene, _) => RestoreAuthoredText(component => component.gameObject.scene == scene);
            EditorSceneManager.sceneSaved += _ => ShowPreviewAgain();
            PrefabStage.prefabSaving += root => RestoreAuthoredText(component => component.transform.IsChildOf(root.transform));
            PrefabStage.prefabSaved += _ => ShowPreviewAgain();
        }

        /// <summary>Returns the preview localizer of <paramref name="catalog"/>, creating it the first time; null when the project has no such catalog.</summary>
        private static ILocalizer Get(CatalogKey catalog)
        {
            IndexedCatalog indexed = catalog.IsEmpty ? CatalogIndex.DefaultCatalog : CatalogIndex.Find(catalog);
            if (indexed?.Info == null)
            {
                return null;
            }
            if (Localizers.TryGetValue(indexed.Key.Hash, out Localizer localizer))
            {
                return localizer;
            }
            localizer = new Localizer(indexed.Key, new UnityHost());
            Localizers.Add(indexed.Key.Hash, localizer);
            localizer.InitializeAsync();
            ApplyPreviewLanguage(localizer);
            return localizer;
        }

        private static void ApplyPreviewLanguage()
        {
            foreach (Localizer localizer in Localizers.Values)
            {
                ApplyPreviewLanguage(localizer);
            }
        }

        private static void ApplyPreviewLanguage(Localizer localizer)
        {
            CatalogInfo catalog = localizer.Catalog;
            if (catalog == null)
            {
                return;
            }
            string preview = LocalizationUserSettings.instance.PreviewLanguage;
            LanguageInfo language = catalog.SourceLanguage;
            if (NameRules.IsValid(preview) && catalog.TryGetLanguage(new LanguageKey(preview), out LanguageInfo found))
            {
                language = found;
            }
            localizer.SetLanguageAsync(language.Key);
        }

        private static void SchedulePrune()
        {
            if (_isPruneScheduled || Localizers.Count == 0)
            {
                return;
            }
            _isPruneScheduled = true;
            // The index is rebuilt once the imports that changed it are done.
            EditorApplication.delayCall += Prune;
        }

        /// <summary>Disposes the localizers of catalogs the project no longer has.</summary>
        private static void Prune()
        {
            _isPruneScheduled = false;
            foreach (KeyValuePair<ulong, Localizer> pair in Localizers)
            {
                if (CatalogIndex.Find(pair.Value.CatalogKey)?.Info == null)
                {
                    Gone.Add(pair.Key);
                }
            }
            for (int i = 0; i < Gone.Count; i++)
            {
                Localizers[Gone[i]].Dispose();
                Localizers.Remove(Gone[i]);
            }
            Gone.Clear();
        }

        private static void DisposeAll()
        {
            foreach (Localizer localizer in Localizers.Values)
            {
                localizer.Dispose();
            }
            Localizers.Clear();
        }

        private static void RestoreAuthoredText(Func<Component, bool> isSaved)
        {
            // Collected first: a component restoring its text may stop tracking itself.
            foreach (Component component in EditModePreview.Tracked.Keys)
            {
                if (component != null && isSaved(component))
                {
                    Restored.Add(component);
                }
            }
            for (int i = 0; i < Restored.Count; i++)
            {
                if (EditModePreview.Tracked.TryGetValue(Restored[i], out (Action Restore, Action Show) tracked))
                {
                    Run(tracked.Restore);
                }
            }
        }

        private static void ShowPreviewAgain()
        {
            for (int i = 0; i < Restored.Count; i++)
            {
                if (Restored[i] != null && EditModePreview.Tracked.TryGetValue(Restored[i], out (Action Restore, Action Show) tracked))
                {
                    Run(tracked.Show);
                }
            }
            Restored.Clear();
        }

        private static void Run(Action action)
        {
            // A component failing to restore its text must not stop the save of every other.
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
