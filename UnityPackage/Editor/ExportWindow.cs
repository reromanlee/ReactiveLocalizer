using reromanlee.ReactiveLocalizer.Authoring;
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
    /// <c>Tools/ReactiveLocalizer/Export</c>: exports a catalog's translations for translators, as an XLSX workbook,
    /// CSV or XLIFF files, or an LLM handoff copied to the clipboard. It picks the tables, the languages, and whether
    /// every entry or only what needs translating goes out, and says how much that is before exporting.
    /// </summary>
    internal sealed class ExportWindow : EditorWindow
    {
        private static readonly ExchangeFormat[] Formats = { ExchangeFormat.Xlsx, ExchangeFormat.Csv, ExchangeFormat.Xliff12, ExchangeFormat.Xliff20, ExchangeFormat.LlmHandoff };
        private static readonly ExportedEntries[] EntryChoices = { ExportedEntries.MissingOrOutdated, ExportedEntries.All, ExportedEntries.Missing, ExportedEntries.Outdated };

        [SerializeField] private string _catalogName;
        [SerializeField] private ExchangeFormat _format;
        [SerializeField] private ExportedEntries _entries = ExportedEntries.MissingOrOutdated;
        [SerializeField] private List<string> _excludedTables = new();
        [SerializeField] private List<string> _excludedLanguages = new();

        private Label _summary;
        private Button _export;
        private IVisualElementScheduledItem _pendingSummary;

        [MenuItem("Tools/ReactiveLocalizer/Export...", priority = 20)]
        public static void Open() => Open(null, null);

        /// <summary>Opens the window on <paramref name="catalogName"/>, with only <paramref name="tableName"/> picked when one is given.</summary>
        public static void Open(string catalogName, string tableName)
        {
            ExportWindow window = GetWindow<ExportWindow>(true, "Export Translations");
            window.minSize = new Vector2(440f, 440f);
            if (catalogName != null)
            {
                window._catalogName = catalogName;
                window._excludedTables.Clear();
                IndexedCatalog catalog = EntryPreview.FindCatalog(catalogName);
                for (int i = 0; tableName != null && catalog != null && i < catalog.Tables.Count; i++)
                {
                    if (!string.Equals(catalog.Tables[i].Name, tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        window._excludedTables.Add(catalog.Tables[i].Name);
                    }
                }
            }
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
                root.Add(Wrapped(new Label("The project has no usable catalog to export. Create one with Assets > Create > ReactiveLocalizer > Catalog.")));
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
                _excludedTables.Clear();
                Rebuild();
            });
            root.Add(catalogField);

            PopupField<ExchangeFormat> format = new("Format", new List<ExchangeFormat>(Formats), _format, LocalizationExchange.GetDisplayName, LocalizationExchange.GetDisplayName)
            {
                tooltip = "XLSX suits spreadsheets, as it keeps every text exactly. Spreadsheets take CSV texts starting with = for formulas. XLIFF suits translation tools, and the LLM handoff a chat with a language model."
            };
            format.RegisterValueChangedCallback(change =>
            {
                _format = change.newValue;
                ScheduleSummary();
            });
            root.Add(format);

            PopupField<ExportedEntries> entries = new("Entries", new List<ExportedEntries>(EntryChoices), _entries, DescribeEntries, DescribeEntries)
            {
                tooltip = "Outdated entries include translations without a fingerprint, since nothing tells which source text they follow."
            };
            entries.RegisterValueChangedCallback(change =>
            {
                _entries = change.newValue;
                ScheduleSummary();
            });
            root.Add(entries);

            List<string> tableNames = new();
            for (int i = 0; i < catalog.Tables.Count; i++)
            {
                tableNames.Add(catalog.Tables[i].Name);
            }
            tableNames.Sort(NaturalOrder.Instance);
            AddChecklist(root, "Tables", tableNames, _excludedTables);

            List<string> languageNames = new();
            for (int i = 0; i < catalog.Info.Languages.Count; i++)
            {
                if (catalog.Info.Languages[i] != catalog.Info.SourceLanguage)
                {
                    languageNames.Add(catalog.Info.Languages[i].Name);
                }
            }
            AddChecklist(root, $"Languages, each beside {catalog.Info.SourceLanguage.Name}", languageNames, _excludedLanguages);

            _summary = Wrapped(new Label());
            _summary.style.marginTop = 6f;
            root.Add(_summary);
            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.style.marginTop = 4f;
            buttons.Add(new Button(Close) { text = "Close" });
            _export = new Button(Export);
            buttons.Add(_export);
            root.Add(buttons);
            UpdateSummary();
        }

        private void AddChecklist(VisualElement root, string title, List<string> names, List<string> excluded)
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
            header.Add(new Button(() => SetAll(list, excluded, names, true)) { text = "All" });
            header.Add(new Button(() => SetAll(list, excluded, names, false)) { text = "None" });
            root.Add(header);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                Toggle toggle = new(name) { value = !excluded.Contains(name) };
                toggle.RegisterValueChangedCallback(change =>
                {
                    excluded.Remove(name);
                    if (!change.newValue)
                    {
                        excluded.Add(name);
                    }
                    ScheduleSummary();
                });
                list.Add(toggle);
            }
            if (names.Count == 0)
            {
                list.Add(new Label("None") { style = { color = LocalizationColors.Muted } });
            }
            root.Add(list);
        }

        private void SetAll(ScrollView list, List<string> excluded, List<string> names, bool isIncluded)
        {
            excluded.Clear();
            if (!isIncluded)
            {
                excluded.AddRange(names);
            }
            foreach (VisualElement child in list.Children())
            {
                (child as Toggle)?.SetValueWithoutNotify(isIncluded);
            }
            ScheduleSummary();
        }

        private void ScheduleSummary()
        {
            // Toggling several boxes in a row counts the entries once.
            _pendingSummary?.Pause();
            _pendingSummary = rootVisualElement.schedule.Execute(UpdateSummary).StartingIn(150);
        }

        /// <summary>Says how much the export holds, or why it can't run, and names the button after what it does.</summary>
        private void UpdateSummary()
        {
            IndexedCatalog catalog = Catalog;
            if (catalog == null || _summary == null)
            {
                return;
            }
            ExportOptions options = CreateOptions(catalog);
            string problem = null;
            if (options.Tables.Count == 0)
            {
                problem = "Pick at least one table.";
            }
            else if (_format == ExchangeFormat.LlmHandoff && options.Languages.Count != 1)
            {
                problem = "The LLM handoff asks for one language at a time, so pick exactly one.";
            }
            else if (_format != ExchangeFormat.Xlsx && _format != ExchangeFormat.Csv && options.Languages.Count == 0)
            {
                problem = "XLIFF files hold a translation language each, so pick at least one.";
            }
            int rows = problem == null ? LocalizationExchange.Collect(catalog, options).RowCount : 0;
            if (problem == null && rows == 0)
            {
                problem = _entries == ExportedEntries.All ? "The tables picked have no entries." : "Nothing in the tables and languages picked needs translating.";
            }
            _summary.text = problem ?? $"Exports {rows} {(rows == 1 ? "entry" : "entries")}.";
            _summary.style.color = problem != null ? LocalizationColors.Warning : LocalizationColors.Muted;
            _export.text = _format == ExchangeFormat.LlmHandoff ? "Copy to Clipboard" : "Export...";
            _export.SetEnabled(problem == null);
        }

        private ExportOptions CreateOptions(IndexedCatalog catalog)
        {
            List<string> tables = new();
            for (int i = 0; i < catalog.Tables.Count; i++)
            {
                if (!_excludedTables.Contains(catalog.Tables[i].Name))
                {
                    tables.Add(catalog.Tables[i].Name);
                }
            }
            List<string> languages = new();
            for (int i = 0; i < catalog.Info.Languages.Count; i++)
            {
                LanguageInfo language = catalog.Info.Languages[i];
                if (language != catalog.Info.SourceLanguage && !_excludedLanguages.Contains(language.Name))
                {
                    languages.Add(language.Name);
                }
            }
            return new ExportOptions { Tables = tables, Languages = languages, Entries = _entries };
        }

        private void Export()
        {
            IndexedCatalog catalog = Catalog;
            if (catalog == null)
            {
                return;
            }
            ExportOptions options = CreateOptions(catalog);
            if (_format == ExchangeFormat.LlmHandoff)
            {
                string language = new List<string>(options.Languages)[0];
                EditorGUIUtility.systemCopyBuffer = LocalizationExchange.CreateHandoff(catalog, options, language);
                _summary.text = $"Copied the {language} handoff to the clipboard. Paste it into a chat with a language model, copy its whole reply, then use Tools > ReactiveLocalizer > Import LLM Reply.";
                _summary.style.color = LocalizationColors.Added;
                return;
            }
            string folder = LocalizationUserSettings.instance.ExchangeFolder;
            string path = LocalizationExchange.IsSingleFile(_format)
                ? EditorUtility.SaveFilePanel("Export Workbook", folder, $"{catalog.Name}.xlsx", "xlsx")
                : EditorUtility.SaveFolderPanel("Export to Folder", folder, string.Empty);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            LocalizationUserSettings.instance.ExchangeFolder = LocalizationExchange.IsSingleFile(_format) ? Path.GetDirectoryName(path) : path;
            List<string> problems = new();
            List<string> written;
            try
            {
                written = LocalizationExchange.Export(catalog, _format, options, path, problems);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog("Export Translations", $"The export couldn't be written: {exception.Message}", "OK");
                return;
            }
            for (int i = 0; i < problems.Count; i++)
            {
                Debug.LogWarning($"[ReactiveLocalizer] {problems[i]}");
            }
            Debug.Log($"[ReactiveLocalizer] Exported '{catalog.Name}' to {written.Count} file(s):\n{string.Join("\n", written)}");
            _summary.text = $"Exported to {written.Count} file(s){(problems.Count > 0 ? $", leaving out {problems.Count} text(s) the Console lists" : string.Empty)}.";
            _summary.style.color = problems.Count > 0 ? LocalizationColors.Warning : LocalizationColors.Added;
            if (written.Count > 0)
            {
                EditorUtility.RevealInFinder(written[0]);
            }
        }

        private static string DescribeEntries(ExportedEntries entries)
        {
            return entries switch
            {
                ExportedEntries.All => "All entries",
                ExportedEntries.Missing => "Missing only",
                ExportedEntries.Outdated => "Outdated only",
                _ => "Missing or outdated"
            };
        }

        private static Label Wrapped(Label label)
        {
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
