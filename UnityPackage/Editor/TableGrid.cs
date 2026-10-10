using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The table window's grid: a row per entry, with columns for the key, the context and each language, tinted by
    /// how each text stands. Built on <see cref="MultiColumnListView"/>, which builds only the visible rows, so
    /// 100,000-entry tables scroll smoothly. Double-clicking a text edits it in place; Enter or leaving it saves.
    /// </summary>
    internal sealed class TableGrid : VisualElement
    {
        private const float RowHeight = 20f;
        private const int PreviewLimit = 300;

        private readonly MultiColumnListView _view;
        private readonly List<string> _columnLanguages = new();
        private List<TableSheetRow> _rows = new();
        private bool _isNotifying = true;

        public TableGrid()
        {
            style.flexGrow = 1f;
            _view = new MultiColumnListView
            {
                fixedItemHeight = RowHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                itemsSource = _rows
            };
            _view.style.flexGrow = 1f;
            _view.selectionChanged += OnSelectionChanged;
            Add(_view);
        }

        /// <summary>Raised when the selected row changes, with the row or null.</summary>
        public event Action<TableSheetRow> RowSelected;

        /// <summary>Raised when a text edited in place is committed: the row, the language's index, and the text.</summary>
        public event Action<TableSheetRow, int, string> TextCommitted;

        /// <summary>Raised when a text too long for its cell is to be edited, so the detail pane takes it: the row and the language's index.</summary>
        public event Action<TableSheetRow, int> DetailEditRequested;

        /// <summary>The row selected, or null.</summary>
        public TableSheetRow SelectedRow => _view.selectedIndex >= 0 && _view.selectedIndex < _rows.Count ? _rows[_view.selectedIndex] : null;

        /// <summary>Shows <paramref name="rows"/> of <paramref name="languages"/>, keeping the columns when the languages are the same.</summary>
        public void Show(IReadOnlyList<LanguageInfo> languages, List<TableSheetRow> rows, ICollection<string> hiddenLanguages)
        {
            if (!HasColumnsFor(languages))
            {
                BuildColumns(languages);
            }
            for (int i = 0; i < _columnLanguages.Count; i++)
            {
                _view.columns[i + 2].visible = !hiddenLanguages.Contains(_columnLanguages[i]);
            }
            _rows = rows;
            _view.itemsSource = _rows;
            _view.RefreshItems();
        }

        /// <summary>Redraws the visible rows, as after an edit changed them.</summary>
        public void Refresh() => _view.RefreshItems();

        /// <summary>Selects the row of <paramref name="key"/> and scrolls to it, without raising <see cref="RowSelected"/>.</summary>
        public void Select(string key)
        {
            _isNotifying = false;
            int index = key == null ? -1 : _rows.FindIndex(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                _view.SetSelection(index);
                _view.ScrollToItem(index);
            }
            else
            {
                _view.ClearSelection();
            }
            _isNotifying = true;
        }

        private bool HasColumnsFor(IReadOnlyList<LanguageInfo> languages)
        {
            if (_columnLanguages.Count != languages.Count)
            {
                return false;
            }
            for (int i = 0; i < languages.Count; i++)
            {
                if (!string.Equals(_columnLanguages[i], languages[i].Name, StringComparison.Ordinal))
                {
                    return false;
                }
            }
            return true;
        }

        private void BuildColumns(IReadOnlyList<LanguageInfo> languages)
        {
            _columnLanguages.Clear();
            _view.columns.Clear();
            _view.columns.Add(new Column
            {
                name = "key",
                title = "Key",
                width = 170f,
                minWidth = 60f,
                makeCell = MakeTextCell,
                bindCell = (element, index) => SetText((Label)element, _rows[index].Key, _rows[index].Key)
            });
            _view.columns.Add(new Column
            {
                name = "context",
                title = "Context",
                width = 150f,
                minWidth = 40f,
                makeCell = () =>
                {
                    Label label = MakeTextCell();
                    label.style.color = LocalizationColors.Muted;
                    return label;
                },
                bindCell = (element, index) => SetText((Label)element, _rows[index].Context, _rows[index].Context)
            });
            for (int i = 0; i < languages.Count; i++)
            {
                int language = i;
                _columnLanguages.Add(languages[i].Name);
                _view.columns.Add(new Column
                {
                    name = "language-" + languages[i].Name,
                    title = i == 0 ? $"{languages[i].Name} (source)" : languages[i].Name,
                    width = 220f,
                    minWidth = 60f,
                    stretchable = true,
                    makeCell = () => new Cell(this, language),
                    bindCell = (element, index) => ((Cell)element).Bind(_rows[index])
                });
            }
            _view.Rebuild();
        }

        private static Label MakeTextCell()
        {
            Label label = new();
            label.style.flexGrow = 1f;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.paddingLeft = 4f;
            return label;
        }

        private static void SetText(Label label, string text, string tooltip)
        {
            label.text = ToOneLine(text);
            label.tooltip = tooltip;
        }

        private static string ToOneLine(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            if (text.Length > PreviewLimit)
            {
                text = text.Substring(0, PreviewLimit);
            }
            return text.IndexOf('\n') >= 0 ? text.Replace("\r\n", " \u21B5 ").Replace("\n", " \u21B5 ") : text;
        }

        private void RequestDetailEdit(TableSheetRow row, int language) => DetailEditRequested?.Invoke(row, language);

        private void CommitText(TableSheetRow row, int language, string text) => TextCommitted?.Invoke(row, language, text);

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            if (_isNotifying)
            {
                RowSelected?.Invoke(SelectedRow);
            }
        }

        /// <summary>A language's cell: the text tinted by its state, turning into a text field when double-clicked.</summary>
        private sealed class Cell : VisualElement
        {
            private readonly TableGrid _grid;
            private readonly int _language;
            private readonly Label _label;
            private TextField _editor;
            private TableSheetRow _row;
            private bool _isEditing;

            public Cell(TableGrid grid, int language)
            {
                _grid = grid;
                _language = language;
                style.flexGrow = 1f;
                style.justifyContent = Justify.Center;
                _label = MakeTextCell();
                Add(_label);
                RegisterCallback<PointerDownEvent>(OnPointerDown);
            }

            public void Bind(TableSheetRow row)
            {
                if (_isEditing)
                {
                    Commit();
                }
                _row = row;
                TableSheetCell cell = row.Cells[_language];
                bool isMissing = cell.Text == null;
                _label.text = isMissing ? (row.Source != null ? "missing" : string.Empty) : ToOneLine(cell.Text);
                _label.style.unityFontStyleAndWeight = isMissing ? FontStyle.Italic : FontStyle.Normal;
                _label.style.color = isMissing ? new StyleColor(LocalizationColors.Muted) : new StyleColor(StyleKeyword.Null);
                style.backgroundColor = LocalizationColors.Tint(cell, row.Source != null);
                tooltip = Describe(cell, row.Source != null);
            }

            private static string Describe(in TableSheetCell cell, bool hasSource)
            {
                string state = cell.State switch
                {
                    TranslationState.Missing when hasSource => "Missing: shows the fallback language's text.",
                    TranslationState.Outdated => "Outdated: the source text changed since this was translated.",
                    TranslationState.Unverified => "Unverified: nothing tells whether this follows the current source text.",
                    TranslationState.Orphan => "Orphan: the source language has no such key, so this is never shown.",
                    _ => null
                };
                if (state == null)
                {
                    return cell.Message ?? cell.Text;
                }
                return cell.Message == null ? state : state + "\n" + cell.Message;
            }

            private void OnPointerDown(PointerDownEvent pointer)
            {
                if (pointer.button != 0 || pointer.clickCount != 2 || _row == null)
                {
                    return;
                }
                pointer.StopPropagation();
                string text = _row.Cells[_language].Text ?? string.Empty;
                if (text.IndexOf('\n') >= 0)
                {
                    _grid.RequestDetailEdit(_row, _language);
                    return;
                }
                BeginEdit(text);
            }

            private void BeginEdit(string text)
            {
                if (_editor == null)
                {
                    _editor = new TextField();
                    _editor.style.flexGrow = 1f;
                    _editor.style.marginLeft = 0f;
                    _editor.style.marginRight = 0f;
                    _editor.RegisterCallback<KeyDownEvent>(OnEditorKeyDown, TrickleDown.TrickleDown);
                    _editor.RegisterCallback<FocusOutEvent>(_ => Commit());
                    Add(_editor);
                }
                _isEditing = true;
                _editor.SetValueWithoutNotify(text);
                _editor.style.display = DisplayStyle.Flex;
                _label.style.display = DisplayStyle.None;
                _editor.schedule.Execute(() =>
                {
                    _editor.Focus();
                    _editor.SelectAll();
                });
            }

            private void OnEditorKeyDown(KeyDownEvent key)
            {
                if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
                {
                    key.StopPropagation();
                    Commit();
                }
                else if (key.keyCode == KeyCode.Escape)
                {
                    key.StopPropagation();
                    EndEdit();
                }
            }

            private void Commit()
            {
                if (!_isEditing)
                {
                    return;
                }
                string text = _editor.value;
                TableSheetRow row = _row;
                EndEdit();
                if (row != null && !string.Equals(text, row.Cells[_language].Text ?? string.Empty, StringComparison.Ordinal))
                {
                    _grid.CommitText(row, _language, text);
                }
            }

            private void EndEdit()
            {
                _isEditing = false;
                _editor.style.display = DisplayStyle.None;
                _label.style.display = DisplayStyle.Flex;
            }
        }
    }
}
