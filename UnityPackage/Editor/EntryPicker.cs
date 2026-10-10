using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The popup an entry field opens: a search over every entry's key and text with recent picks first, and a form
    /// creating the entry on the spot when nothing matches. Only visible rows are ever built, so catalogs of any size
    /// scroll smoothly. Arrows move, Enter picks or creates, Escape closes.
    /// </summary>
    internal sealed class EntryPicker : EditorWindow
    {
        private const float MinimumWidth = 360f;
        private const float WindowHeight = 380f;
        private const float RowHeight = 20f;
        private const int PreviewLimit = 200;

        private EntryPickerRequest _request;
        private Action<EntryReference> _onPicked;
        private EntryPickerList _list;
        private EntryDraft _draft;
        // Once the form is edited, a changing query no longer fills it in.
        private bool _isDraftEdited;
        // The form was opened with the + button rather than for a search that found nothing.
        private bool _isFormChosen;
        private bool _isFormShown;

        private ToolbarSearchField _search;
        private ListView _listView;
        private VisualElement _form;
        private Label _formTitle;
        private VisualElement _catalogButtons;
        private TextField _tableField;
        private TextField _keyField;
        private TextField _textField;
        private Label _hint;
        private Button _createButton;
        private Label _footer;

        /// <summary>Opens the picker below <paramref name="activator"/>, a rectangle in screen space, and calls <paramref name="onPicked"/> with the choice.</summary>
        public static void Show(Rect activator, EntryPickerRequest request, Action<EntryReference> onPicked)
        {
            EntryPicker picker = CreateInstance<EntryPicker>();
            picker._request = request;
            picker._onPicked = onPicked;
            picker.ShowAsDropDown(activator, new Vector2(Mathf.Max(activator.width, MinimumWidth), WindowHeight));
        }

        private void CreateGUI()
        {
            if (_onPicked == null)
            {
                // A domain reload dropped the field this picker was opened for.
                EditorApplication.delayCall += Close;
                return;
            }
            List<IndexedCatalog> catalogs = FindCatalogs(_request.LimitedCatalog);
            LocalizationUserSettings settings = LocalizationUserSettings.instance;
            _list = new EntryPickerList(catalogs, settings.RecentEntries);
            string preferredCatalog = !string.IsNullOrEmpty(_request.LimitedCatalog) ? _request.LimitedCatalog : _request.Current.CatalogName;
            _draft = new EntryDraft(catalogs, preferredCatalog, _request.Context, _request.FieldName, settings.LastTable);

            VisualElement root = rootVisualElement;
            SetBorder(root.style, 1f, LocalizationColors.Border);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            Toolbar toolbar = new();
            _search = new ToolbarSearchField();
            _search.style.flexGrow = 1f;
            _search.style.flexShrink = 1f;
            _search.style.width = StyleKeyword.Auto;
            _search.RegisterValueChangedCallback(change => OnQueryChanged(change.newValue));
            toolbar.Add(_search);
            toolbar.Add(new ToolbarButton(() => ShowForm(true)) { text = "+", tooltip = "Create an entry" });
            root.Add(toolbar);

            _listView = new ListView
            {
                fixedItemHeight = RowHeight,
                makeItem = MakeRow,
                bindItem = BindRow,
                selectionType = SelectionType.Single,
                itemsSource = _list.Rows
            };
            _listView.style.flexGrow = 1f;
            root.Add(_listView);

            _form = CreateForm();
            root.Add(_form);

            _footer = new Label();
            _footer.style.fontSize = 10f;
            _footer.style.color = LocalizationColors.Muted;
            _footer.style.paddingLeft = 6f;
            _footer.style.paddingTop = 2f;
            _footer.style.paddingBottom = 2f;
            root.Add(_footer);

            OnQueryChanged(string.Empty);
            root.schedule.Execute(FocusSearch);
        }

        private static List<IndexedCatalog> FindCatalogs(string limitedCatalog)
        {
            List<IndexedCatalog> catalogs = new();
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.Info != null && (string.IsNullOrEmpty(limitedCatalog) || string.Equals(catalog.Name, limitedCatalog, StringComparison.OrdinalIgnoreCase)))
                {
                    catalogs.Add(catalog);
                }
            }
            return catalogs;
        }

        private void OnQueryChanged(string query)
        {
            _list.Search(query);
            _listView.RefreshItems();
            if (!_isDraftEdited)
            {
                _draft.TakeQuery(query);
                FillForm();
            }
            bool hasNothing = _list.Rows.Count == 0;
            if (hasNothing)
            {
                ShowForm(false);
            }
            else if (!_isFormChosen)
            {
                ShowList();
            }
            Select(_list.FindRow(_request.Current));
        }

        private VisualElement MakeRow()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6f;
            row.style.paddingRight = 6f;
            Label name = new();
            name.style.flexShrink = 0f;
            name.style.unityTextAlign = TextAnchor.MiddleLeft;
            Label text = new();
            text.style.flexGrow = 1f;
            text.style.flexShrink = 1f;
            text.style.marginLeft = 8f;
            text.style.overflow = Overflow.Hidden;
            text.style.textOverflow = TextOverflow.Ellipsis;
            text.style.whiteSpace = WhiteSpace.NoWrap;
            text.style.unityTextAlign = TextAnchor.MiddleLeft;
            text.style.color = LocalizationColors.Muted;
            row.Add(name);
            row.Add(text);
            row.RegisterCallback<ClickEvent>(OnRowClicked);
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            element.userData = index;
            Label name = (Label)element[0];
            Label text = (Label)element[1];
            EntryPickerRow row = _list.Rows[index];
            bool isHeader = row.Kind == EntryPickerRowKind.Header;
            name.style.fontSize = isHeader ? new StyleLength(10f) : new StyleLength(StyleKeyword.Null);
            name.style.color = isHeader ? new StyleColor(LocalizationColors.Muted) : new StyleColor(StyleKeyword.Null);
            name.style.unityFontStyleAndWeight = row.Kind switch
            {
                EntryPickerRowKind.Header => FontStyle.Bold,
                EntryPickerRowKind.None => FontStyle.Italic,
                _ => FontStyle.Normal
            };
            if (row.Kind != EntryPickerRowKind.Entry)
            {
                name.text = row.Label;
                text.text = string.Empty;
                return;
            }
            name.text = _list.IsNamingCatalogs ? $"{row.Item.CatalogName} \u203A {row.Item.QualifiedName}" : row.Item.QualifiedName;
            text.text = ToPreview(row.Item.Text);
        }

        private void OnRowClicked(ClickEvent click)
        {
            if (click.currentTarget is VisualElement row && row.userData is int index)
            {
                Pick(index);
            }
        }

        private void Pick(int index)
        {
            if (index < 0 || index >= _list.Rows.Count)
            {
                return;
            }
            EntryPickerRow row = _list.Rows[index];
            if (row.Kind == EntryPickerRowKind.None)
            {
                Finish(default);
            }
            else if (row.Kind == EntryPickerRowKind.Entry)
            {
                Finish(new EntryReference(row.Item.CatalogName, row.Item.TableName, row.Item.EntryName));
            }
        }

        private void Finish(EntryReference picked)
        {
            Action<EntryReference> onPicked = _onPicked;
            _onPicked = null;
            LocalizationUserSettings.instance.AddRecentEntry(picked);
            try
            {
                onPicked?.Invoke(picked);
            }
            finally
            {
                // Importing a created entry may have closed the popup already, by taking its focus.
                if (this != null)
                {
                    Close();
                }
            }
        }

        private void OnKeyDown(KeyDownEvent key)
        {
            bool isInForm = _isFormShown && _form.Contains(rootVisualElement.panel?.focusController?.focusedElement as VisualElement);
            switch (key.keyCode)
            {
                case KeyCode.Escape:
                    if (_isFormShown && _isFormChosen)
                    {
                        _isFormChosen = false;
                        OnQueryChanged(_search.value);
                        FocusSearch();
                    }
                    else
                    {
                        Close();
                    }
                    break;
                case KeyCode.DownArrow when !_isFormShown:
                    Select(_list.Step(_listView.selectedIndex, 1));
                    break;
                case KeyCode.UpArrow when !_isFormShown:
                    Select(_list.Step(_listView.selectedIndex, -1));
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (isInForm)
                    {
                        Create();
                    }
                    else if (_isFormShown)
                    {
                        _keyField.Focus();
                    }
                    else
                    {
                        Pick(_listView.selectedIndex);
                    }
                    break;
                default:
                    return;
            }
            key.StopPropagation();
        }

        private void Select(int index)
        {
            if (index < 0)
            {
                _listView.ClearSelection();
                return;
            }
            _listView.selectedIndex = index;
            _listView.ScrollToItem(index);
        }

        private void FocusSearch()
        {
            _search?.Q<TextField>()?.Focus();
        }

        private VisualElement CreateForm()
        {
            VisualElement form = new();
            form.style.flexGrow = 1f;
            form.style.paddingLeft = 6f;
            form.style.paddingRight = 6f;
            form.style.paddingTop = 6f;

            _formTitle = new Label();
            _formTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _formTitle.style.marginBottom = 4f;
            _formTitle.style.whiteSpace = WhiteSpace.Normal;
            form.Add(_formTitle);

            _catalogButtons = new VisualElement();
            _catalogButtons.style.flexDirection = FlexDirection.Row;
            _catalogButtons.style.flexWrap = Wrap.Wrap;
            if (_draft.Catalogs.Count > 1)
            {
                foreach (IndexedCatalog catalog in _draft.Catalogs)
                {
                    _catalogButtons.Add(new Button(() =>
                    {
                        _draft.Catalog = catalog;
                        FillForm();
                    })
                    {
                        text = catalog.Name,
                        userData = catalog
                    });
                }
            }
            form.Add(_catalogButtons);

            _tableField = CreateFormField(form, "Table", value => _draft.TableName = value);
            _keyField = CreateFormField(form, "Key", value => _draft.Key = value);
            _textField = CreateFormField(form, "Text", value => _draft.Text = value);

            _hint = new Label();
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.style.marginTop = 4f;
            form.Add(_hint);

            _createButton = new Button(Create) { text = "Create" };
            _createButton.style.alignSelf = Align.FlexEnd;
            _createButton.style.marginTop = 6f;
            form.Add(_createButton);
            return form;
        }

        private TextField CreateFormField(VisualElement form, string label, Action<string> assign)
        {
            TextField field = new(label);
            field.labelElement.style.minWidth = 40f;
            field.labelElement.style.width = 40f;
            field.RegisterValueChangedCallback(change =>
            {
                assign(change.newValue);
                _isDraftEdited = true;
                UpdateHint(null);
            });
            form.Add(field);
            return field;
        }

        private void FillForm()
        {
            _tableField.SetValueWithoutNotify(_draft.TableName);
            _keyField.SetValueWithoutNotify(_draft.Key);
            _textField.SetValueWithoutNotify(_draft.Text);
            foreach (VisualElement child in _catalogButtons.Children())
            {
                child.style.unityFontStyleAndWeight = child.userData == _draft.Catalog ? FontStyle.Bold : FontStyle.Normal;
            }
            UpdateHint(null);
        }

        private void UpdateHint(string problem)
        {
            string hint = _draft.Describe(out bool canCreate);
            bool isProblem = problem != null || !canCreate;
            _hint.text = problem ?? hint;
            _hint.style.color = isProblem ? LocalizationColors.Problem : LocalizationColors.Muted;
            _createButton.SetEnabled(canCreate);
        }

        private void Create()
        {
            _draft.Describe(out bool canCreate);
            if (!canCreate)
            {
                return;
            }
            _draft.MatchTableSpelling();
            if (!EntryAuthoring.TryCreateEntry(_draft.Catalog.Name, _draft.TableName, _draft.Key, _draft.Text, out EntryReference created, out string problem))
            {
                UpdateHint(problem);
                return;
            }
            LocalizationUserSettings.instance.LastTable = _draft.TableName;
            Finish(created);
        }

        private void ShowForm(bool isChosen)
        {
            _isFormChosen |= isChosen;
            _isFormShown = true;
            _listView.style.display = DisplayStyle.None;
            _form.style.display = DisplayStyle.Flex;
            string query = _search.value?.Trim();
            _formTitle.text = _isFormChosen || string.IsNullOrEmpty(query) ? "New entry" : $"Nothing matches '{query}'. Create it?";
            _footer.text = _isFormChosen ? "Enter creates   Esc goes back" : "Enter creates   Esc closes";
            if (isChosen)
            {
                _keyField.Focus();
            }
        }

        private void ShowList()
        {
            _isFormShown = false;
            _form.style.display = DisplayStyle.None;
            _listView.style.display = DisplayStyle.Flex;
            _footer.text = "\u2191\u2193 move   Enter picks   Esc closes";
        }

        private static string ToPreview(string text)
        {
            if (text.Length > PreviewLimit)
            {
                text = text.Substring(0, PreviewLimit);
            }
            return text.IndexOf('\n') >= 0 ? text.Replace("\r\n", " ").Replace('\n', ' ') : text;
        }

        private static void SetBorder(IStyle style, float width, Color color)
        {
            style.borderTopWidth = width;
            style.borderBottomWidth = width;
            style.borderLeftWidth = width;
            style.borderRightWidth = width;
            style.borderTopColor = color;
            style.borderBottomColor = color;
            style.borderLeftColor = color;
            style.borderRightColor = color;
        }
    }

    /// <summary>What an entry picker is opened for.</summary>
    internal readonly struct EntryPickerRequest
    {
        /// <param name="current">The entry the field has now, which the picker selects.</param>
        /// <param name="limitedCatalog">The only catalog the field takes, or null for any.</param>
        /// <param name="context">The GameObject the field belongs to, which new entries are named after; null for assets.</param>
        /// <param name="fieldName">The field's name, which new entries' keys include when it says something.</param>
        public EntryPickerRequest(EntryReference current, string limitedCatalog, GameObject context, string fieldName)
        {
            Current = current;
            LimitedCatalog = limitedCatalog;
            Context = context;
            FieldName = fieldName;
        }

        public EntryReference Current { get; }

        public string LimitedCatalog { get; }

        public GameObject Context { get; }

        public string FieldName { get; }
    }
}
