using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// <c>Tools/ReactiveLocalizer/Character Sets</c>: collects every character the picked languages of a catalog can
    /// show, for TextMeshPro's Font Asset Creator or any other font tool, and copies it or saves it as a text file.
    /// Picking several languages collects the characters a font they share needs.
    /// </summary>
    internal sealed class CharacterSetWindow : EditorWindow
    {
        private const string PrintableAscii = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~";

        // A preview of tens of thousands of characters would slow the window down without showing anything more.
        private const int PreviewLimit = 4000;

        [SerializeField] private string _catalogName;
        [SerializeField] private List<string> _excludedLanguages = new() { PseudoLocalization.LanguageName };
        [SerializeField] private bool _isAddingAscii;
        [SerializeField] private string _extraCharacters = string.Empty;

        private Label _summary;
        private TextField _preview;
        private Button _copy;
        private Button _save;
        private IVisualElementScheduledItem _pendingUpdate;
        private string _characters = string.Empty;
        private readonly List<string> _pickedLanguages = new();

        [MenuItem("Tools/ReactiveLocalizer/Character Sets...", priority = 40)]
        public static void Open()
        {
            CharacterSetWindow window = GetWindow<CharacterSetWindow>(true, "Character Sets");
            window.minSize = new Vector2(440f, 460f);
            window.Rebuild();
        }

        private void CreateGUI() => Rebuild();

        private IndexedCatalog Catalog
        {
            get
            {
                IndexedCatalog catalog = string.IsNullOrEmpty(_catalogName) ? null : EntryPreview.FindCatalog(_catalogName);
                if (catalog?.Info != null)
                {
                    return catalog;
                }
                foreach (IndexedCatalog candidate in CatalogIndex.Catalogs)
                {
                    if (candidate.Info != null)
                    {
                        _catalogName = candidate.Name;
                        return candidate;
                    }
                }
                return null;
            }
        }

        private void Rebuild()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            IndexedCatalog catalog = Catalog;
            if (catalog == null)
            {
                root.Add(Wrapped(new Label("The project has no usable catalog. Create one with Assets > Create > ReactiveLocalizer > Catalog.")));
                return;
            }

            List<string> catalogNames = new();
            foreach (IndexedCatalog candidate in CatalogIndex.Catalogs)
            {
                if (candidate.Info != null)
                {
                    catalogNames.Add(candidate.Name);
                }
            }
            PopupField<string> catalogField = new("Catalog", catalogNames, catalog.Name);
            catalogField.RegisterValueChangedCallback(change =>
            {
                _catalogName = change.newValue;
                Rebuild();
            });
            root.Add(catalogField);

            IReadOnlyList<LanguageInfo> languages = CharacterSets.GetCatalog(catalog.Key)?.Languages ?? Array.Empty<LanguageInfo>();
            List<string> languageNames = new(languages.Count);
            for (int i = 0; i < languages.Count; i++)
            {
                languageNames.Add(languages[i].Name);
            }
            AddChecklist(root, "Languages, which share one font when several are picked", languageNames);

            Toggle ascii = new("Add printable ASCII") { value = _isAddingAscii, tooltip = "For text players type or code brings at runtime, such as names, which isn't in the files." };
            ascii.style.marginTop = 8f;
            ascii.RegisterValueChangedCallback(change =>
            {
                _isAddingAscii = change.newValue;
                ScheduleUpdate();
            });
            root.Add(ascii);
            TextField extra = new("Extra characters") { value = _extraCharacters, tooltip = "Characters arguments and formatters bring at runtime, such as a currency sign or the digits of dates." };
            extra.RegisterValueChangedCallback(change =>
            {
                _extraCharacters = change.newValue ?? string.Empty;
                ScheduleUpdate();
            });
            root.Add(extra);

            _summary = Wrapped(new Label());
            _summary.style.marginTop = 6f;
            root.Add(_summary);
            _preview = new TextField { multiline = true, isReadOnly = true };
            _preview.style.whiteSpace = WhiteSpace.Normal;
            _preview.style.flexGrow = 1f;
            _preview.style.minHeight = 80f;
            root.Add(_preview);

            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 4f;
            buttons.Add(new Button(Close) { text = "Close" });
            _copy = new Button(Copy) { text = "Copy to Clipboard" };
            buttons.Add(_copy);
            _save = new Button(Save) { text = "Save As..." };
            buttons.Add(_save);
            root.Add(buttons);
            UpdateCharacters();
        }

        private void AddChecklist(VisualElement root, string title, List<string> names)
        {
            VisualElement header = new();
            header.style.flexDirection = FlexDirection.Row;
            header.style.marginTop = 6f;
            Label label = new(title);
            label.style.flexGrow = 1f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(label);
            ScrollView list = new();
            list.style.maxHeight = 140f;
            list.style.minHeight = 40f;
            header.Add(new Button(() => SetAll(list, names, true)) { text = "All" });
            header.Add(new Button(() => SetAll(list, names, false)) { text = "None" });
            root.Add(header);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                Toggle toggle = new(name) { value = !_excludedLanguages.Contains(name) };
                if (name == PseudoLocalization.LanguageName)
                {
                    toggle.tooltip = "The accented letters of pseudo text, for a font that development builds show it with.";
                }
                toggle.RegisterValueChangedCallback(change =>
                {
                    _excludedLanguages.Remove(name);
                    if (!change.newValue)
                    {
                        _excludedLanguages.Add(name);
                    }
                    ScheduleUpdate();
                });
                list.Add(toggle);
            }
            root.Add(list);
        }

        private void SetAll(ScrollView list, List<string> names, bool isPicked)
        {
            _excludedLanguages.Clear();
            if (!isPicked)
            {
                _excludedLanguages.AddRange(names);
            }
            foreach (VisualElement child in list.Children())
            {
                (child as Toggle)?.SetValueWithoutNotify(isPicked);
            }
            ScheduleUpdate();
        }

        private void ScheduleUpdate()
        {
            // Toggling several boxes or typing several characters in a row collects once.
            _pendingUpdate?.Pause();
            _pendingUpdate = rootVisualElement.schedule.Execute(UpdateCharacters).StartingIn(200);
        }

        private void UpdateCharacters()
        {
            IndexedCatalog catalog = Catalog;
            if (catalog == null || _summary == null)
            {
                return;
            }
            CharacterSet characters = new();
            _pickedLanguages.Clear();
            IReadOnlyList<LanguageInfo> languages = CharacterSets.GetCatalog(catalog.Key)?.Languages ?? Array.Empty<LanguageInfo>();
            for (int i = 0; i < languages.Count; i++)
            {
                if (!_excludedLanguages.Contains(languages[i].Name))
                {
                    _pickedLanguages.Add(languages[i].Name);
                    characters.UnionWith(CharacterSets.Collect(catalog.Key, languages[i].Key));
                }
            }
            if (_isAddingAscii)
            {
                characters.AddText(PrintableAscii.AsSpan());
            }
            characters.AddText(_extraCharacters.AsSpan());
            _characters = characters.ToString();

            string problem = _pickedLanguages.Count == 0 ? "Pick at least one language." : characters.Count == 0 ? "The languages picked show no text yet." : null;
            _summary.text = problem ?? $"{characters.Count} characters, in code point order. Text arguments bring at runtime, such as player names, may need more.";
            _summary.style.color = problem != null ? LocalizationColors.Warning : LocalizationColors.Muted;
            _preview.SetValueWithoutNotify(_characters.Length > PreviewLimit ? _characters.Substring(0, PreviewLimit) + "\u2026" : _characters);
            _copy.SetEnabled(problem == null);
            _save.SetEnabled(problem == null);
        }

        private void Copy()
        {
            EditorGUIUtility.systemCopyBuffer = _characters;
            _summary.text = "Copied. In the Font Asset Creator, pick Character Set > Custom Characters and paste them.";
            _summary.style.color = LocalizationColors.Added;
        }

        private void Save()
        {
            string name = _pickedLanguages.Count == 1 ? $"{_catalogName}.{_pickedLanguages[0]}.characters" : $"{_catalogName}.characters";
            string path = EditorUtility.SaveFilePanelInProject("Save Character Set", name, "txt",
                "Choose where to save the characters. The Font Asset Creator reads them with Character Set > Characters from File.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            try
            {
                File.WriteAllText(path, _characters, new UTF8Encoding(false));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("Save Character Set", $"The file couldn't be written: {exception.Message}", "OK");
                return;
            }
            AssetDatabase.ImportAsset(path);
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(path));
            _summary.text = $"Saved {path}. In the Font Asset Creator, pick Character Set > Characters from File and this file.";
            _summary.style.color = LocalizationColors.Added;
        }

        private static Label Wrapped(Label label)
        {
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
