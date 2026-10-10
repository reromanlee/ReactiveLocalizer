using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The table window's list of catalogs and their tables, each table with badges counting its errors, missing
    /// translations and outdated ones. Only visible rows are built, and counted, so projects of any size list quickly.
    /// </summary>
    internal sealed class TableSidebar : VisualElement
    {
        private const float RowHeight = 20f;

        private readonly ListView _list;
        private readonly List<Item> _items = new();
        private readonly Button _newTableButton;
        private bool _isNotifying = true;

        public TableSidebar()
        {
            style.flexGrow = 1f;
            _list = new ListView
            {
                fixedItemHeight = RowHeight,
                makeItem = MakeRow,
                bindItem = BindRow,
                selectionType = SelectionType.Single,
                itemsSource = _items
            };
            _list.style.flexGrow = 1f;
            _list.selectionChanged += OnSelectionChanged;
            Add(_list);
            _newTableButton = new Button(() => NewTableRequested?.Invoke(SelectedCatalog, _newTableButton.worldBound)) { text = "New Table", tooltip = "Create a table in the selected catalog" };
            _newTableButton.style.marginTop = 2f;
            _newTableButton.style.marginBottom = 4f;
            Add(_newTableButton);
        }

        /// <summary>Raised when a table is picked, with its catalog.</summary>
        public event Action<IndexedCatalog, IndexedTable> TableChosen;

        /// <summary>Raised to create a table in a catalog, with the button's rectangle in the window.</summary>
        public event Action<IndexedCatalog, Rect> NewTableRequested;

        /// <summary>The catalog of the selected row, or the only catalog; null when there is none.</summary>
        public IndexedCatalog SelectedCatalog
        {
            get
            {
                if (_list.selectedIndex >= 0 && _list.selectedIndex < _items.Count)
                {
                    return _items[_list.selectedIndex].Catalog;
                }
                return _items.Count > 0 ? _items[0].Catalog : null;
            }
        }

        /// <summary>Lists the project's catalogs and tables again, selecting <paramref name="tableName"/> of <paramref name="catalogName"/> without raising events.</summary>
        public void Rebuild(string catalogName, string tableName)
        {
            _items.Clear();
            int selected = -1;
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                _items.Add(new Item(catalog, null));
                List<IndexedTable> tables = new(catalog.Tables);
                tables.Sort((left, right) => NaturalOrder.Instance.Compare(left.Name, right.Name));
                for (int i = 0; i < tables.Count; i++)
                {
                    if (string.Equals(catalog.Name, catalogName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(tables[i].Name, tableName, StringComparison.OrdinalIgnoreCase))
                    {
                        selected = _items.Count;
                    }
                    _items.Add(new Item(catalog, tables[i]));
                }
            }
            _list.RefreshItems();
            _isNotifying = false;
            if (selected >= 0)
            {
                _list.SetSelection(selected);
                _list.ScrollToItem(selected);
            }
            else
            {
                _list.ClearSelection();
            }
            _isNotifying = true;
            _newTableButton.SetEnabled(SelectedCatalog?.Info != null && SelectedCatalog.IsWritable);
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            _newTableButton.SetEnabled(SelectedCatalog?.Info != null && SelectedCatalog.IsWritable);
            if (!_isNotifying || _list.selectedIndex < 0 || _list.selectedIndex >= _items.Count)
            {
                return;
            }
            Item item = _items[_list.selectedIndex];
            if (item.Table != null)
            {
                TableChosen?.Invoke(item.Catalog, item.Table);
            }
        }

        private static VisualElement MakeRow()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingRight = 4f;
            Label name = new();
            name.style.flexGrow = 1f;
            name.style.flexShrink = 1f;
            name.style.overflow = Overflow.Hidden;
            name.style.textOverflow = TextOverflow.Ellipsis;
            name.style.whiteSpace = WhiteSpace.NoWrap;
            row.Add(name);
            row.Add(MakeBadge());
            row.Add(MakeBadge());
            row.Add(MakeBadge());
            return row;
        }

        private static Label MakeBadge()
        {
            Label badge = new();
            badge.style.fontSize = 10f;
            badge.style.marginLeft = 3f;
            badge.style.paddingLeft = 4f;
            badge.style.paddingRight = 4f;
            badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 6f;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            return badge;
        }

        private void BindRow(VisualElement element, int index)
        {
            Item item = _items[index];
            Label name = (Label)element[0];
            if (item.Table == null)
            {
                name.text = item.Catalog.Info == null ? $"{item.Catalog.Name} (can't be used)" : item.Catalog.Name;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.paddingLeft = 2f;
                name.style.color = item.Catalog.Info == null ? new StyleColor(LocalizationColors.Problem) : new StyleColor(StyleKeyword.Null);
                name.tooltip = item.Catalog.Path;
                SetBadge(element[1], 0, null, default);
                SetBadge(element[2], 0, null, default);
                SetBadge(element[3], 0, null, default);
                return;
            }
            name.text = item.Table.Name;
            name.style.unityFontStyleAndWeight = FontStyle.Normal;
            name.style.paddingLeft = 14f;
            name.style.color = StyleKeyword.Null;
            name.tooltip = item.Table.SourcePath;
            TableStatus status = item.Catalog.Info != null ? TableStatus.Of(item.Catalog, item.Table) : default;
            SetBadge(element[1], status.Errors, "error(s): broken messages, orphans or malformed lines", LocalizationColors.Problem);
            SetBadge(element[2], status.Missing, "missing translation(s)", LocalizationColors.Muted);
            SetBadge(element[3], status.Outdated, "outdated translation(s)", LocalizationColors.Warning);
        }

        private static void SetBadge(VisualElement element, int count, string what, Color color)
        {
            Label badge = (Label)element;
            badge.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (count <= 0)
            {
                return;
            }
            badge.text = count > 999 ? "999+" : count.ToString();
            badge.tooltip = $"{count} {what}";
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.3f);
        }

        private readonly struct Item
        {
            public Item(IndexedCatalog catalog, IndexedTable table)
            {
                Catalog = catalog;
                Table = table;
            }

            public IndexedCatalog Catalog { get; }

            /// <summary>The table; null for the catalog's own row.</summary>
            public IndexedTable Table { get; }
        }
    }
}
