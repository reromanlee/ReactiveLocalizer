using reromanlee.ReactiveLocalizer.Formatting;
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Asks what a new catalog is called and which language its texts are written in, the source language that defines
    /// every key, then creates it: <c>Assets/Create/ReactiveLocalizer/Catalog</c>, and the first table of a project
    /// that has no catalog yet.
    /// </summary>
    internal sealed class CatalogCreationWindow : EditorWindow
    {
        private string _folder;
        private Action<string> _onCreated;
        private TextField _name;
        private TextField _language;
        private TextField _culture;
        private TextField _displayName;
        private Label _hint;
        private Button _create;
        private bool _isDisplayNameEdited;

        /// <summary>Opens the dialog for a catalog in <paramref name="folder"/>; <paramref name="onCreated"/> receives the new catalog's path.</summary>
        public static void Show(string folder, Action<string> onCreated)
        {
            CatalogCreationWindow window = CreateInstance<CatalogCreationWindow>();
            window._folder = folder;
            window._onCreated = onCreated;
            window.titleContent = new GUIContent("New Catalog");
            window.minSize = new Vector2(420f, 230f);
            window.maxSize = new Vector2(640f, 230f);
            window.ShowUtility();
        }

        /// <summary>Returns a catalog file's text for one source language. An empty culture leaves the culture out.</summary>
        public static string CreateText(string language, string culture, string displayName)
        {
            string text = $"@source {language}\n\n[{language}]\nDisplayName = {displayName}\n";
            return string.IsNullOrEmpty(culture) ? text : text + $"Culture = {culture}\n";
        }

        private void CreateGUI()
        {
            if (_folder == null)
            {
                // A domain reload dropped where the catalog was to go.
                EditorApplication.delayCall += Close;
                return;
            }
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            Label intro = new($"A catalog in {_folder}. Its source language is the one its texts are written in first; every key is defined there.");
            intro.style.whiteSpace = WhiteSpace.Normal;
            intro.style.marginBottom = 6f;
            root.Add(intro);
            _name = AddField(root, "Name", LocalizationFiles.DefaultCatalogName, "Generated classes are named after it, such as LocalizationKeys.");
            _language = AddField(root, "Source language", "English", "The name files and code use for the language, such as English or Japanese.");
            _culture = AddField(root, "Culture", "en", "The language's CLDR culture, such as en, ja or pt-BR. It decides plural forms and how numbers are written.");
            _displayName = AddField(root, "Display name", "English", "How the language names itself in a language menu, such as Deutsch.");
            _language.RegisterValueChangedCallback(change =>
            {
                if (!_isDisplayNameEdited)
                {
                    _displayName.SetValueWithoutNotify(change.newValue);
                }
            });
            _displayName.RegisterValueChangedCallback(_ => _isDisplayNameEdited = true);

            _hint = new Label();
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.style.flexGrow = 1f;
            _hint.style.marginTop = 4f;
            root.Add(_hint);

            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.Add(new Button(Close) { text = "Cancel" });
            _create = new Button(Create) { text = "Create" };
            buttons.Add(_create);
            root.Add(buttons);

            root.RegisterCallback<KeyDownEvent>(key =>
            {
                if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
                {
                    Create();
                }
                else if (key.keyCode == KeyCode.Escape)
                {
                    Close();
                }
            }, TrickleDown.TrickleDown);
            UpdateHint();
            root.schedule.Execute(() => _language.Focus());
        }

        private TextField AddField(VisualElement root, string label, string value, string tooltip)
        {
            TextField field = new(label) { value = value, tooltip = tooltip };
            field.labelElement.style.minWidth = 110f;
            field.RegisterValueChangedCallback(_ => UpdateHint());
            root.Add(field);
            return field;
        }

        private string CatalogPath => $"{_folder}/{_name.value.Trim()}.{LocalizationFiles.CatalogExtension}";

        /// <summary>Says what is wrong with the values, or what an unknown culture means; enables Create when nothing is wrong.</summary>
        private void UpdateHint()
        {
            string problem = Check(out string note);
            _hint.text = problem ?? note ?? string.Empty;
            _hint.style.color = problem != null ? LocalizationColors.Problem : LocalizationColors.Muted;
            _create.SetEnabled(problem == null);
        }

        private string Check(out string note)
        {
            note = null;
            string name = _name.value.Trim();
            string language = _language.value.Trim();
            string culture = _culture.value.Trim();
            if (!NameRules.IsValid(name))
            {
                return $"A catalog's name {NameRules.Description}.";
            }
            if (!NameRules.IsValid(language))
            {
                return $"A language's name {NameRules.Description}.";
            }
            if (_displayName.value.IndexOf('\n') >= 0)
            {
                return "The display name has to fit on one line.";
            }
            if (File.Exists(CatalogPath))
            {
                return $"{CatalogPath} already exists.";
            }
            if (culture.Length == 0)
            {
                note = "Without a culture, plural messages use only their 'other' form and numbers are written the invariant way.";
            }
            else if (!NumberSymbols.TryFind(culture, out _))
            {
                note = $"CLDR doesn't know the culture '{culture}', so plural messages use only their 'other' form. Check the spelling, such as pt-BR.";
            }
            return null;
        }

        private void Create()
        {
            if (Check(out _) != null)
            {
                return;
            }
            string path = CatalogPath;
            Directory.CreateDirectory(_folder);
            File.WriteAllText(path, CreateText(_language.value.Trim(), _culture.value.Trim(), _displayName.value.Trim()));
            // The layout is scanned first, so tables already in the folder find their catalog in this very import.
            CatalogLayout.Refresh();
            AssetDatabase.ImportAsset(path);
            UnityEngine.Object created = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
            Action<string> onCreated = _onCreated;
            _onCreated = null;
            Close();
            onCreated?.Invoke(path);
        }
    }
}
