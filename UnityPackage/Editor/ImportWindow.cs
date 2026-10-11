using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// <c>Tools/ReactiveLocalizer/Import</c>: previews what importing translators' files changes, then writes it as one
    /// step Undo can take back. Before anything is written it lists every text that is new, updated, arrives outdated,
    /// is confirmed or is rejected, and lets the person pick the catalog, say which language each of the files'
    /// languages is, and choose whether source texts the files changed are imported too.
    /// </summary>
    internal sealed class ImportWindow : EditorWindow
    {
        private const string Skip = "Skip";
        private const int TextPreviewLimit = 200;

        private readonly List<ImportChange> _changes = new();
        private ImportSession _session;
        private ImportPlan _plan;
        private Label _summary;
        private Label _problems;
        private MultiColumnListView _list;
        private Button _import;

        [MenuItem("Tools/ReactiveLocalizer/Import File...", priority = 21)]
        public static void ImportFile()
        {
            string path = EditorUtility.OpenFilePanelWithFilters("Import Translations", LocalizationUserSettings.instance.ExchangeFolder,
                new[] { "Translations", string.Join(",", LocalizationExchange.Extensions), "All files", "*" });
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            LocalizationUserSettings.instance.ExchangeFolder = Path.GetDirectoryName(path);
            Show(new[] { LocalizationExchange.Read(path) });
        }

        [MenuItem("Tools/ReactiveLocalizer/Import Folder...", priority = 22)]
        public static void ImportFolder()
        {
            string folder = EditorUtility.OpenFolderPanel("Import Translations", LocalizationUserSettings.instance.ExchangeFolder, string.Empty);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            LocalizationUserSettings.instance.ExchangeFolder = folder;
            List<ImportedFile> files = LocalizationExchange.ReadFolder(folder);
            if (files.Count == 0)
            {
                EditorUtility.DisplayDialog("Import Translations", $"{folder} holds no {string.Join(", ", LocalizationExchange.Extensions)} files.", "OK");
                return;
            }
            Show(files);
        }

        [MenuItem("Tools/ReactiveLocalizer/Import LLM Reply", priority = 23)]
        public static void ImportReply()
        {
            string reply = EditorGUIUtility.systemCopyBuffer;
            ImportedFile file = LlmHandoff.Read("The reply", reply);
            if (file.Rows.Count == 0)
            {
                EditorUtility.DisplayDialog("Import LLM Reply",
                    "The clipboard holds no reply to a handoff. Export one with Tools > ReactiveLocalizer > Export, paste it into a chat, then copy the model's whole reply and try again.", "OK");
                return;
            }
            Show(new[] { file });
        }

        /// <summary>Opens the preview of importing <paramref name="files"/>.</summary>
        public static void Show(IReadOnlyList<ImportedFile> files)
        {
            // The session is set before the window first draws, which a window opened with GetWindow would do without it.
            ImportWindow[] open = Resources.FindObjectsOfTypeAll<ImportWindow>();
            ImportWindow window = open.Length > 0 ? open[0] : CreateInstance<ImportWindow>();
            window._session = new ImportSession(files);
            window.titleContent = new GUIContent("Import Translations");
            window.minSize = new Vector2(640f, 420f);
            window.ShowUtility();
            window.Rebuild();
            window.Focus();
        }

        private void CreateGUI()
        {
            if (_session == null)
            {
                // A domain reload dropped the files read; reading them again is quick.
                rootVisualElement.Add(Wrapped(new Label("Scripts were reloaded since the files were read. Import them again.")));
                return;
            }
            Rebuild();
        }

        private void Rebuild()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 10f;
            root.style.paddingRight = 10f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            List<string> names = new();
            for (int i = 0; i < _session.Files.Count; i++)
            {
                names.Add(_session.Files[i].Name);
            }
            root.Add(Wrapped(new Label(names.Count == 1 ? $"From {names[0]}" : $"From {names.Count} files: {string.Join(", ", names)}")));

            List<string> catalogs = new();
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.Info != null)
                {
                    catalogs.Add(catalog.Name);
                }
            }
            if (_session.Catalog != null)
            {
                PopupField<string> catalogField = new("Catalog", catalogs, _session.Catalog.Name);
                catalogField.RegisterValueChangedCallback(change =>
                {
                    _session.SetCatalog(EntryPreview.FindCatalog(change.newValue));
                    Rebuild();
                });
                root.Add(catalogField);
                AddLanguages(root);
                Toggle sourceChanges = new("Import source changes")
                {
                    value = _session.Options.IsImportingSourceChanges,
                    tooltip = $"Also take the {_session.Catalog.Info.SourceLanguage.Name} texts the files changed, as after proofreading. " +
                              "Leave it off for files exported earlier: their source texts are those of then, and would undo every change made since."
                };
                sourceChanges.RegisterValueChangedCallback(change =>
                {
                    _session.Options.IsImportingSourceChanges = change.newValue;
                    Replan();
                });
                root.Add(sourceChanges);
            }

            _summary = Wrapped(new Label());
            _summary.style.marginTop = 6f;
            _summary.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_summary);
            _problems = Wrapped(new Label());
            _problems.style.color = LocalizationColors.Warning;
            ScrollView problems = new() { style = { maxHeight = 90f, flexShrink = 0f } };
            problems.Add(_problems);
            root.Add(problems);
            _list = CreateList();
            root.Add(_list);

            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 4f;
            buttons.Add(new Button(Close) { text = "Cancel" });
            _import = new Button(Apply) { text = "Import" };
            buttons.Add(_import);
            root.Add(buttons);
            Replan();
        }

        /// <summary>Adds a choice of catalog language for each language the files hold, once per label.</summary>
        private void AddLanguages(VisualElement root)
        {
            List<string> choices = new() { Skip };
            for (int i = 0; i < _session.Catalog.Info.Languages.Count; i++)
            {
                choices.Add(_session.Catalog.Info.Languages[i].Name);
            }
            HashSet<string> shown = new(StringComparer.OrdinalIgnoreCase);
            for (int f = 0; f < _session.Files.Count; f++)
            {
                foreach (ImportedLanguage language in _session.Files[f].Languages)
                {
                    if (!shown.Add(language.Label))
                    {
                        continue;
                    }
                    string label = language.Label;
                    string caption = language.IsSourceOnly ? $"'{label}', its source" : $"'{label}'";
                    PopupField<string> field = new(caption, choices, language.LanguageName ?? Skip)
                    {
                        tooltip = language.IsSourceOnly
                            ? "The language the file's translations were made from. When it is the catalog's source language, its texts become their fingerprints."
                            : "The catalog language the file's texts in this language go to."
                    };
                    field.RegisterValueChangedCallback(change =>
                    {
                        _session.SetLanguage(label, change.newValue == Skip ? null : change.newValue);
                        Replan();
                    });
                    root.Add(field);
                }
            }
        }

        private MultiColumnListView CreateList()
        {
            MultiColumnListView list = new()
            {
                fixedItemHeight = 20f,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = _changes
            };
            list.style.flexGrow = 1f;
            list.style.marginTop = 4f;
            AddColumn(list, "Change", 84f, false, change => DescribeKind(change.Kind), change => KindColor(change.Kind));
            AddColumn(list, "Entry", 180f, false, change => $"{change.TableName}.{change.Key}", null);
            AddColumn(list, "Language", 80f, false, change => change.LanguageName ?? string.Empty, null);
            AddColumn(list, "Text", 220f, true, change => OneLine(change.NewText), null);
            AddColumn(list, "Note", 200f, true, change => OneLine(change.Message), _ => LocalizationColors.Muted);
            return list;
        }

        private void AddColumn(MultiColumnListView list, string title, float width, bool isStretched, Func<ImportChange, string> text, Func<ImportChange, Color> color)
        {
            list.columns.Add(new Column
            {
                title = title,
                width = width,
                stretchable = isStretched,
                makeCell = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } },
                bindCell = (element, index) =>
                {
                    ImportChange change = _changes[index];
                    Label label = (Label)element;
                    label.text = text(change);
                    label.tooltip = Describe(change);
                    label.style.color = color != null ? new StyleColor(color(change)) : new StyleColor(StyleKeyword.Null);
                }
            });
        }

        private void Replan()
        {
            _plan = _session.Plan();
            _changes.Clear();
            if (_plan != null)
            {
                _changes.AddRange(_plan.Changes);
            }
            _list.RefreshItems();
            string problem = _session.Problem;
            List<string> problems = new();
            if (problem != null)
            {
                problems.Add(problem);
            }
            for (int f = 0; _plan != null && f < _plan.Problems.Count; f++)
            {
                problems.Add(_plan.Problems[f]);
            }
            _problems.text = string.Join("\n", problems);
            _problems.style.display = problems.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _summary.text = _plan == null ? "Nothing to import." : Summarize(_plan);
            _import.SetEnabled(problem == null && _plan != null && _plan.HasChanges);
        }

        private void Apply()
        {
            ImportPlan plan = _session.Apply();
            if (plan == null)
            {
                return;
            }
            for (int i = 0; i < plan.Problems.Count; i++)
            {
                Debug.LogWarning($"[ReactiveLocalizer] {plan.Problems[i]}");
            }
            for (int i = 0; i < plan.Changes.Count; i++)
            {
                if (plan.Changes[i].Kind == ImportChangeKind.Rejected)
                {
                    Debug.LogWarning($"[ReactiveLocalizer] {plan.Changes[i].Location}: '{plan.Changes[i].TableName}.{plan.Changes[i].Key}' is rejected: {plan.Changes[i].Message}");
                }
            }
            Debug.Log($"[ReactiveLocalizer] Imported into '{_session.Catalog.Name}': {Summarize(plan)} Undo takes it back.");
            Close();
        }

        private static string Summarize(ImportPlan plan)
        {
            List<string> parts = new();
            AddCount(parts, plan.Count(ImportChangeKind.Added), "new");
            AddCount(parts, plan.Count(ImportChangeKind.Updated), "updated");
            AddCount(parts, plan.Count(ImportChangeKind.Outdated), "arriving outdated");
            AddCount(parts, plan.Count(ImportChangeKind.Confirmed), "confirmed");
            AddCount(parts, plan.Count(ImportChangeKind.SourceChanged), "source texts changed");
            AddCount(parts, plan.Count(ImportChangeKind.Rejected), "rejected");
            AddCount(parts, plan.UnchangedCount, "unchanged");
            return parts.Count == 0 ? "The files hold no translations." : string.Join(", ", parts) + ".";
        }

        private static void AddCount(List<string> parts, int count, string what)
        {
            if (count > 0)
            {
                parts.Add($"{count} {what}");
            }
        }

        private static string DescribeKind(ImportChangeKind kind)
        {
            return kind switch
            {
                ImportChangeKind.Added => "New",
                ImportChangeKind.Updated => "Updated",
                ImportChangeKind.Outdated => "Outdated",
                ImportChangeKind.Confirmed => "Confirmed",
                ImportChangeKind.SourceChanged => "Source",
                _ => "Rejected"
            };
        }

        private static Color KindColor(ImportChangeKind kind)
        {
            return kind switch
            {
                ImportChangeKind.Rejected => LocalizationColors.Problem,
                ImportChangeKind.Outdated => LocalizationColors.Warning,
                ImportChangeKind.Added => LocalizationColors.Added,
                _ => LocalizationColors.Muted
            };
        }

        private static string Describe(ImportChange change)
        {
            string text = change.OldText != null ? $"Before: {Limit(change.OldText)}\nAfter: {Limit(change.NewText)}" : Limit(change.NewText);
            string note = change.Message != null ? $"\n\n{change.Message}" : string.Empty;
            return $"{change.Location}\n{text}{note}";
        }

        private static string OneLine(string text) => text == null ? string.Empty : Limit(text).Replace('\n', ' ');

        private static string Limit(string text) => text == null ? string.Empty : text.Length > TextPreviewLimit ? text.Substring(0, TextPreviewLimit) + "..." : text;

        private static Label Wrapped(Label label)
        {
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
