using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The Scene view's preview language: picking one updates every text bound for preview in the open scenes, and the
    /// preview of every entry field, live in Edit Mode.
    /// </summary>
    [Overlay(typeof(SceneView), Id, "Preview Language", true)]
    internal sealed class PreviewLanguageOverlay : Overlay
    {
        private const string Id = "reromanlee-reactivelocalizer-preview-language";
        private const string SourceChoice = "Source language";

        private DropdownField _field;

        public override VisualElement CreatePanelContent()
        {
            VisualElement root = new();
            root.style.minWidth = 170f;
            _field = new DropdownField { tooltip = "The language text is previewed in outside Play Mode. Catalogs without it show their source language." };
            _field.RegisterValueChangedCallback(change =>
                LocalizationUserSettings.instance.PreviewLanguage = change.newValue == SourceChoice ? string.Empty : change.newValue);
            root.Add(_field);
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                LocalizationUserSettings.PreviewLanguageChanged += Refresh;
                CatalogIndex.Invalidated += Refresh;
                Refresh();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                LocalizationUserSettings.PreviewLanguageChanged -= Refresh;
                CatalogIndex.Invalidated -= Refresh;
            });
            return root;
        }

        /// <summary>Lists the source language and every language of the project's catalogs, selecting the preview language.</summary>
        private void Refresh()
        {
            List<string> choices = new() { SourceChoice };
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.Info == null)
                {
                    continue;
                }
                for (int i = 0; i < catalog.Info.Languages.Count; i++)
                {
                    if (seen.Add(catalog.Info.Languages[i].Name))
                    {
                        choices.Add(catalog.Info.Languages[i].Name);
                    }
                }
            }
            string preview = LocalizationUserSettings.instance.PreviewLanguage;
            string selected = choices.Find(choice => string.Equals(choice, preview, StringComparison.OrdinalIgnoreCase)) ?? SourceChoice;
            _field.choices = choices;
            _field.SetValueWithoutNotify(selected);
        }
    }
}
