using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Samples.EditorTool.Editor
{
    /// <summary>
    /// An editor window localized with a catalog of its own, as a package or tool author would ship it: the catalog
    /// sits in an Editor folder, so its texts never mix with the game's and never reach a player build.
    /// </summary>
    /// <remarks>
    /// The window owns its localizer, created as it opens and disposed as it closes or scripts reload. The language
    /// picked in its toolbar is remembered per machine.
    /// </remarks>
    internal sealed class LocalizedToolWindow : EditorWindow
    {
        private const string SavedLanguageKey = "reromanlee.ReactiveLocalizer.Samples.EditorTool.Language";

        private Localizer _localizer;
        private TextBinding _clicksBinding;
        private int _clicks;

        [MenuItem("Tools/ReactiveLocalizer/Samples/Localized Tool Window")]
        private static void Open()
        {
            GetWindow<LocalizedToolWindow>();
        }

        private void CreateGUI()
        {
            _localizer?.Dispose();
            _localizer = new Localizer(ToolWindowKeys.CatalogKey, new UnityHost());
            // Editor tables load within the call, so the window's language can be picked right away.
            _localizer.InitializeAsync();
            IReadOnlyList<LanguageInfo> languages = _localizer.Languages;
            string saved = EditorPrefs.GetString(SavedLanguageKey, string.Empty);
            for (int i = 0; i < languages.Count; i++)
            {
                if (string.Equals(languages[i].Name, saved, System.StringComparison.OrdinalIgnoreCase))
                {
                    _localizer.SetLanguageAsync(languages[i].Key);
                }
            }

            VisualElement root = rootVisualElement;
            Toolbar toolbar = new();
            List<string> names = new(languages.Count);
            for (int i = 0; i < languages.Count; i++)
            {
                names.Add(languages[i].DisplayName);
            }
            int current = _localizer.CurrentLanguage != null ? IndexOf(languages, _localizer.CurrentLanguage) : 0;
            PopupField<string> picker = new(names, Mathf.Max(current, 0));
            picker.RegisterValueChangedCallback(change =>
            {
                LanguageInfo picked = languages[names.IndexOf(change.newValue)];
                EditorPrefs.SetString(SavedLanguageKey, picked.Name);
                _localizer.SetLanguageAsync(picked.Key);
            });
            toolbar.Add(picker);
            root.Add(toolbar);

            Label description = new();
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 8f;
            description.style.marginLeft = 6f;
            description.style.marginRight = 6f;
            root.Add(description);
            Label clicks = new();
            clicks.style.marginLeft = 6f;
            Button click = new(() =>
            {
                _clicks++;
                _clicksBinding.SetMessage(ToolWindowKeys.Window.Clicks(_clicks));
            });
            click.style.alignSelf = Align.FlexStart;
            click.style.marginTop = 8f;
            root.Add(click);
            root.Add(clicks);

            // Every text follows the language: bindings apply right away and again on every switch.
            _localizer.Bind(ToolWindowKeys.Window.Title, this, static (window, text) => window.titleContent = new GUIContent(text));
            _localizer.Bind(ToolWindowKeys.Window.Language, picker, static (field, text) => field.label = text);
            _localizer.Bind(ToolWindowKeys.Window.Description, description, static (label, text) => label.text = text);
            _localizer.Bind(ToolWindowKeys.Window.Click, click, static (button, text) => button.text = text);
            _clicksBinding = _localizer.Bind(ToolWindowKeys.Window.Clicks(_clicks), clicks, static (label, text) => label.text = text);
        }

        private void OnDisable()
        {
            // Disposing the localizer releases every binding it made.
            _localizer?.Dispose();
            _localizer = null;
        }

        private static int IndexOf(IReadOnlyList<LanguageInfo> languages, LanguageInfo language)
        {
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i] == language)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
