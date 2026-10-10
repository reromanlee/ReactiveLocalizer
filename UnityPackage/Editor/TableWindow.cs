using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// <c>Window/ReactiveLocalizer/Tables</c>: every catalog's tables in one window. A sidebar lists the tables with
    /// badges for what needs attention, a grid shows a table's entries in every language with status tints, and a
    /// detail pane edits the selected entry. Every edit saves at once as one step Undo can take back, and bound text
    /// follows it live.
    /// </summary>
    internal sealed class TableWindow : EditorWindow, ITableEditor
    {
        private const string TableTemplate = "# One entry per line, written as Key = Text. Comments right above an entry are the context translators see.\n";

        [SerializeField] private string _catalogName;
        [SerializeField] private string _tableName;
        [SerializeField] private string _selectedKey;
        [SerializeField] private string _query;
        [SerializeField] private TableFilterKind _filter;
        [SerializeField] private string _missingLanguage;
        [SerializeField] private List<string> _hiddenLanguages = new();

        private readonly List<TableSheetRow> _visibleRows = new();
        private TableSheet _sheet;
        private TableSidebar _sidebar;
        private TableGrid _grid;
        private TableDetail _detail;
        private ToolbarMenu _filterMenu;
        private ToolbarMenu _languagesMenu;
        private Label _tableProblems;
        private Label _status;
        private IVisualElementScheduledItem _pendingSearch;
        private bool _isRefreshScheduled;

        [MenuItem("Window/ReactiveLocalizer/Tables", priority = 2000)]
        public static void Open()
        {
            TableWindow window = GetWindow<TableWindow>();
            window.titleContent = new GUIContent("Tables");
            window.minSize = new Vector2(640f, 320f);
        }

        /// <summary>Opens the window on <paramref name="key"/> of <paramref name="tableName"/> in the catalog <paramref name="catalogName"/>.</summary>
        public static void Open(string catalogName, string tableName, string key)
        {
            Open();
            TableWindow window = GetWindow<TableWindow>();
            window._catalogName = catalogName;
            window._tableName = tableName;
            window._selectedKey = key;
            if (window._sidebar != null)
            {
                window.Refresh(true);
            }
        }

        private void OnEnable()
        {
            CatalogIndex.Invalidated += ScheduleRefresh;
        }

        private void OnDisable()
        {
            CatalogIndex.Invalidated -= ScheduleRefresh;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Add(CreateToolbar());

            _tableProblems = new Label();
            _tableProblems.style.whiteSpace = WhiteSpace.Normal;
            _tableProblems.style.color = LocalizationColors.Problem;
            _tableProblems.style.paddingLeft = 6f;
            _tableProblems.style.paddingTop = 2f;
            _tableProblems.style.paddingBottom = 2f;
            root.Add(_tableProblems);

            _sidebar = new TableSidebar();
            _sidebar.TableChosen += (catalog, table) => OpenTable(catalog.Name, table.Name, null);
            _sidebar.NewTableRequested += (catalog, bounds) => NewTable(catalog, ToScreen(bounds));
            _grid = new TableGrid();
            _grid.RowSelected += row =>
            {
                _selectedKey = row?.Key;
                _detail.Show(_sheet, row);
            };
            _grid.TextCommitted += (row, language, text) => SetText(row.Key, language, text);
            _grid.DetailEditRequested += (row, language) => _detail.FocusLanguage(language);
            _detail = new TableDetail(this);

            TwoPaneSplitView content = new(1, 340f, TwoPaneSplitViewOrientation.Horizontal);
            content.Add(_grid);
            content.Add(_detail);
            TwoPaneSplitView main = new(0, 200f, TwoPaneSplitViewOrientation.Horizontal);
            main.Add(_sidebar);
            main.Add(content);
            main.style.flexGrow = 1f;
            root.Add(main);

            _status = new Label();
            _status.style.paddingLeft = 6f;
            _status.style.paddingTop = 2f;
            _status.style.paddingBottom = 2f;
            _status.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_status);

            Refresh(true);
        }

        private Toolbar CreateToolbar()
        {
            Toolbar toolbar = new();
            ToolbarSearchField search = new();
            search.style.flexGrow = 1f;
            search.style.flexShrink = 1f;
            search.style.width = StyleKeyword.Auto;
            search.SetValueWithoutNotify(_query ?? string.Empty);
            search.tooltip = "Search keys, context and text in every language.";
            search.RegisterValueChangedCallback(change =>
            {
                _query = change.newValue;
                // Typing in a large table searches once the typing pauses.
                _pendingSearch?.Pause();
                _pendingSearch = rootVisualElement.schedule.Execute(ApplyFilter).StartingIn(120);
            });
            toolbar.Add(search);
            _filterMenu = new ToolbarMenu();
            toolbar.Add(_filterMenu);
            _languagesMenu = new ToolbarMenu { text = "Languages", tooltip = "Show or hide the column of each language." };
            toolbar.Add(_languagesMenu);
            ToolbarButton add = new() { text = "New Entry", tooltip = "Add an entry to the table." };
            add.clicked += () => AddEntry(ToScreen(add.worldBound));
            toolbar.Add(add);
            toolbar.Add(new ToolbarButton(LocalizationValidation.ValidateProject) { text = "Validate", tooltip = "Validate every catalog and reference, listing problems in the Console." });
            return toolbar;
        }

        private static Rect ToScreen(Rect bounds) => GUIUtility.GUIToScreenRect(bounds);

        private void ScheduleRefresh()
        {
            if (_isRefreshScheduled || rootVisualElement == null)
            {
                return;
            }
            _isRefreshScheduled = true;
            // Files change in batches; the window catches up once they are all imported.
            rootVisualElement.schedule.Execute(() =>
            {
                _isRefreshScheduled = false;
                Refresh(false);
            });
        }

        /// <summary>Lists the tables again and opens the table again when its files changed elsewhere, or always when <paramref name="isReopening"/>.</summary>
        private void Refresh(bool isReopening)
        {
            if (_sidebar == null)
            {
                return;
            }
            _sidebar.Rebuild(_catalogName, _tableName);
            if (isReopening || _sheet == null || !_sheet.IsCurrent())
            {
                OpenTable(_catalogName, _tableName, _selectedKey);
            }
        }

        private void OpenTable(string catalogName, string tableName, string key)
        {
            _catalogName = catalogName;
            _tableName = tableName;
            _selectedKey = key;
            _sheet = null;
            ShowStatus(null, false);
            IndexedCatalog catalog = string.IsNullOrEmpty(catalogName) ? null : EntryPreview.FindCatalog(catalogName);
            if (catalog?.Info == null || !NameRules.IsValid(tableName) || !catalog.TryGetTable(new TableKey(tableName), out _))
            {
                _visibleRows.Clear();
                _grid.Show(Array.Empty<LanguageInfo>(), _visibleRows, _hiddenLanguages);
                _detail.ShowMessage(CatalogIndex.Catalogs.Count == 0
                    ? "The project has no catalog yet. Create one with Assets > Create > ReactiveLocalizer > Catalog."
                    : "Pick a table on the left.");
                _tableProblems.style.display = DisplayStyle.None;
                UpdateMenus();
                return;
            }
            _sheet = new TableSheet(catalog, tableName);
            _sidebar.Rebuild(_catalogName, _tableName);
            UpdateMenus();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (_sheet == null)
            {
                return;
            }
            TableFilter.Apply(_sheet.Rows, _query, _filter, FindLanguage(_missingLanguage), _visibleRows);
            _grid.Show(_sheet.Languages, _visibleRows, _hiddenLanguages);
            _grid.Select(_selectedKey);
            _detail.Show(_sheet, _sheet.Find(_selectedKey));
            List<string> problems = new(_sheet.FileProblems);
            if (_sheet.HasUnreadableFiles)
            {
                problems.Insert(0, "A file of this table has lines that can't be read, so the window can't change it. Fix them in a text editor; the Console lists them.");
            }
            _tableProblems.text = string.Join("\n", problems);
            _tableProblems.style.display = problems.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private int FindLanguage(string name)
        {
            if (_sheet == null || string.IsNullOrEmpty(name))
            {
                return -1;
            }
            for (int i = 0; i < _sheet.Languages.Count; i++)
            {
                if (string.Equals(_sheet.Languages[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpdateMenus()
        {
            _filterMenu.text = _filter switch
            {
                TableFilterKind.Missing => string.IsNullOrEmpty(_missingLanguage) ? "Missing" : $"Missing in {_missingLanguage}",
                TableFilterKind.Outdated => "Outdated",
                TableFilterKind.Problems => "Problems",
                _ => "All Entries"
            };
            ClearMenu(_filterMenu.menu);
            AddFilter("All Entries", TableFilterKind.All, null);
            AddFilter("Missing/In Any Language", TableFilterKind.Missing, null);
            ClearMenu(_languagesMenu.menu);
            if (_sheet == null)
            {
                return;
            }
            for (int i = 1; i < _sheet.Languages.Count; i++)
            {
                AddFilter($"Missing/In {_sheet.Languages[i].Name}", TableFilterKind.Missing, _sheet.Languages[i].Name);
            }
            AddFilter("Outdated", TableFilterKind.Outdated, null);
            AddFilter("Problems", TableFilterKind.Problems, null);
            for (int i = 0; i < _sheet.Languages.Count; i++)
            {
                string language = _sheet.Languages[i].Name;
                _languagesMenu.menu.AppendAction(language, _ =>
                {
                    if (!_hiddenLanguages.Remove(language))
                    {
                        _hiddenLanguages.Add(language);
                    }
                    ApplyFilter();
                }, _ => _hiddenLanguages.Contains(language) ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Checked);
            }
        }

        private void AddFilter(string name, TableFilterKind kind, string language)
        {
            _filterMenu.menu.AppendAction(name, _ =>
            {
                _filter = kind;
                _missingLanguage = language;
                UpdateMenus();
                ApplyFilter();
            }, _ => _filter == kind && string.Equals(_missingLanguage ?? string.Empty, language ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                ? DropdownMenuAction.Status.Checked
                : DropdownMenuAction.Status.Normal);
        }

        private static void ClearMenu(DropdownMenu menu)
        {
            for (int i = menu.MenuItems().Count - 1; i >= 0; i--)
            {
                menu.RemoveItemAt(i);
            }
        }

        private void ShowStatus(string message, bool isProblem)
        {
            if (_status == null)
            {
                return;
            }
            _status.text = message ?? string.Empty;
            _status.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
            _status.style.color = isProblem ? LocalizationColors.Problem : LocalizationColors.Muted;
        }

        private void Apply(string undoName, TableSheet.Operation operation)
        {
            if (_sheet == null)
            {
                return;
            }
            if (!_sheet.TryApply(undoName, operation, out string problem))
            {
                ShowStatus(problem, true);
                return;
            }
            ShowStatus(null, false);
            ApplyFilter();
        }

        private string LanguageName(int language) => _sheet.Languages[language].Name;

        public void SetText(string key, int language, string text)
        {
            string name = LanguageName(language);
            Apply($"Edit {_tableName}.{key} in {name}", (TableFileSet files, out string problem) => files.TrySetText(name, key, text, out problem));
        }

        public void MarkCurrent(string key, int language)
        {
            string name = LanguageName(language);
            Apply($"Mark {_tableName}.{key} current in {name}", (TableFileSet files, out string problem) => files.TryMarkCurrent(name, key, out problem));
        }

        public void RemoveTranslation(string key, int language)
        {
            string name = LanguageName(language);
            Apply($"Remove {_tableName}.{key} from {name}", (TableFileSet files, out string problem) => files.TryRemoveTranslation(name, key, out problem));
        }

        public void SetContext(string key, string context)
        {
            List<string> lines = new();
            foreach (string line in (context ?? string.Empty).Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                {
                    lines.Add(trimmed);
                }
            }
            Apply($"Edit the context of {_tableName}.{key}", (TableFileSet files, out string problem) => files.TrySetContext(key, lines, out problem));
        }

        public void SetMaximumLength(string key, string value)
        {
            value = value?.Trim() ?? string.Empty;
            int? limit = null;
            if (value.Length > 0)
            {
                if (!MaximumLength.TryParse(value, out int parsed))
                {
                    ShowStatus($"'{value}' isn't a number of characters, such as 16.", true);
                    return;
                }
                limit = parsed;
            }
            Apply($"Set the maximum length of {_tableName}.{key}", (TableFileSet files, out string problem) => files.TrySetMaximumLength(key, limit, out problem));
        }

        public void Delete(string key)
        {
            Apply($"Delete {_tableName}.{key}", (TableFileSet files, out string problem) => files.TryDelete(key, out problem));
        }

        public void Rename(string key, Rect activator)
        {
            TablePrompt.Show(ToScreen(activator), $"Rename {_tableName}.{key}", "Rename", new[] { "Key" }, new[] { key },
                values => NameRules.IsValid(values[0]) ? null : $"A key {NameRules.Description}.",
                values =>
                {
                    string newKey = values[0];
                    if (_sheet == null)
                    {
                        return "The table is no longer open.";
                    }
                    if (!_sheet.TryApply($"Rename {_tableName}.{key}", (TableFileSet files, out string problem) => files.TryRename(key, newKey, out problem), out string failure))
                    {
                        return failure;
                    }
                    _selectedKey = newKey;
                    ApplyFilter();
                    return null;
                });
        }

        public void Move(string key, Rect activator)
        {
            IndexedCatalog catalog = _sheet?.Catalog;
            if (catalog == null)
            {
                return;
            }
            string source = _tableName;
            TablePrompt.Show(ToScreen(activator), $"Move {source}.{key} to another table", "Move", new[] { "Table" }, new[] { string.Empty },
                values => !NameRules.IsValid(values[0]) ? $"A table name {NameRules.Description}."
                    : string.Equals(values[0], source, StringComparison.OrdinalIgnoreCase) ? "That is the table it is in." : null,
                values =>
                {
                    if (_sheet == null)
                    {
                        return "The table is no longer open.";
                    }
                    TableEdit target = new(catalog, values[0]);
                    if (!_sheet.Edit.Files.TryMoveTo(key, target.Files, out string problem))
                    {
                        return problem;
                    }
                    TableEdit.Save($"Move {source}.{key} to {values[0]}", _sheet.Edit, target);
                    OpenTable(catalog.Name, target.Files.TableName, key);
                    return null;
                });
        }

        private void AddEntry(Rect activator)
        {
            if (_sheet == null)
            {
                ShowStatus("Pick a table to add the entry to.", true);
                return;
            }
            TablePrompt.Show(activator, $"New entry in {_tableName}", "Add", new[] { "Key", "Text" }, new[] { string.Empty, string.Empty },
                values => values[0].Length == 0 ? "Name the entry after its role, such as PlayButton."
                    : NameRules.IsValid(values[0]) ? null : $"A key {NameRules.Description}.",
                values =>
                {
                    string key = values[0];
                    string text = values[1];
                    if (_sheet == null)
                    {
                        return "The table is no longer open.";
                    }
                    if (!_sheet.TryApply($"Add {_tableName}.{key}", (TableFileSet files, out string problem) => files.TryAddEntry(key, text, out problem), out string failure))
                    {
                        return failure;
                    }
                    _selectedKey = key;
                    ApplyFilter();
                    return null;
                });
        }

        private void NewTable(IndexedCatalog catalog, Rect activator)
        {
            if (catalog?.Info == null || !catalog.IsWritable)
            {
                ShowStatus("Pick a catalog that can be written to.", true);
                return;
            }
            TablePrompt.Show(activator, $"New table in {catalog.Name}", "Create", new[] { "Name" }, new[] { string.Empty },
                values => values[0].Length == 0 ? "Name the table after what owns its texts, such as MainMenu."
                    : !NameRules.IsValid(values[0]) ? $"A table name {NameRules.Description}."
                    : catalog.TryGetTable(new TableKey(values[0]), out _) ? $"'{values[0]}' already exists." : null,
                values =>
                {
                    string path = $"{catalog.Folder}/{values[0]}.{catalog.Info.SourceLanguage.Name}.{LocalizationFiles.TableExtension}";
                    _catalogName = catalog.Name;
                    _tableName = values[0];
                    _selectedKey = null;
                    TableFileUndo.Write(new[] { new KeyValuePair<string, string>(path, TableTemplate) }, $"Create the table {values[0]}");
                    Refresh(true);
                    return null;
                });
        }
    }
}
