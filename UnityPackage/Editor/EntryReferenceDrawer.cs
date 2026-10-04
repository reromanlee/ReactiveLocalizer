using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Draws an <see cref="EntryReference"/> as a dropdown of the project's entries, with the picked entry's source
    /// text below it, or the problem when the entry doesn't exist. Picking an entry also stores its catalog.
    /// </summary>
    [CustomPropertyDrawer(typeof(EntryReference))]
    internal sealed class EntryReferenceDrawer : PropertyDrawer
    {
        private const string CatalogField = "_catalog";
        private const string TableField = "_table";
        private const string EntryField = "_entry";

        private static GUIStyle _problemStyle;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty catalog = property.FindPropertyRelative(CatalogField);
            SerializedProperty table = property.FindPropertyRelative(TableField);
            SerializedProperty entry = property.FindPropertyRelative(EntryField);
            EditorGUI.BeginProperty(position, label, property);

            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect field = EditorGUI.PrefixLabel(line, label);
            string picked = property.hasMultipleDifferentValues
                ? "Mixed"
                : string.IsNullOrEmpty(entry.stringValue) ? "None" : $"{table.stringValue}.{entry.stringValue}";
            if (EditorGUI.DropdownButton(field, new GUIContent(picked, catalog.stringValue), FocusType.Keyboard))
            {
                ShowMenu(field, property, catalog.stringValue, table.stringValue, entry.stringValue);
            }

            Rect preview = new(field.x, line.yMax + EditorGUIUtility.standardVerticalSpacing, field.width, EditorGUIUtility.singleLineHeight);
            if (!property.hasMultipleDifferentValues)
            {
                string text = DescribeEntry(catalog.stringValue, table.stringValue, entry.stringValue, out bool isProblem);
                EditorGUI.LabelField(preview, text, isProblem ? GetProblemStyle() : EditorStyles.miniLabel);
            }
            EditorGUI.EndProperty();
        }

        /// <summary>Returns the entry's source text, or what is wrong with the reference.</summary>
        private static string DescribeEntry(string catalogName, string tableName, string entryName, out bool isProblem)
        {
            isProblem = false;
            if (string.IsNullOrEmpty(entryName))
            {
                return "Pick an entry.";
            }
            isProblem = true;
            IndexedCatalog catalog = FindCatalog(catalogName);
            if (catalog == null)
            {
                return string.IsNullOrEmpty(catalogName)
                    ? "No default catalog. Create one with Assets > Create > ReactiveLocalizer > Catalog."
                    : $"There is no catalog '{catalogName}'.";
            }
            if (!NameRules.IsValid(tableName) || !catalog.TryGetTable(new TableKey(tableName), out IndexedTable table) || table.SourceDocument == null)
            {
                return $"The catalog '{catalog.Name}' has no table '{tableName}'.";
            }
            if (!table.SourceDocument.TryGetEntry(entryName, out TableDocumentEntry found))
            {
                return $"The table '{tableName}' has no entry '{entryName}'.";
            }
            isProblem = false;
            return found.Value.Length == 0 ? "(intentionally empty)" : found.Value.Replace('\n', ' ');
        }

        private static IndexedCatalog FindCatalog(string catalogName)
        {
            if (string.IsNullOrEmpty(catalogName))
            {
                return CatalogIndex.DefaultCatalog;
            }
            return NameRules.IsValid(catalogName) ? CatalogIndex.Find(new CatalogKey(catalogName)) : null;
        }

        private static void ShowMenu(Rect field, SerializedProperty property, string pickedCatalog, string pickedTable, string pickedEntry)
        {
            SerializedObject serializedObject = property.serializedObject;
            string propertyPath = property.propertyPath;
            GenericMenu menu = new();
            menu.AddItem(new GUIContent("None"), string.IsNullOrEmpty(pickedEntry), () => Assign(serializedObject, propertyPath, null, null, null));
            IReadOnlyList<IndexedCatalog> catalogs = CatalogIndex.Catalogs;
            // With one catalog its name is noise; with several, it is the first level of the menu.
            bool isNamingCatalogs = catalogs.Count > 1;
            foreach (IndexedCatalog catalog in catalogs)
            {
                menu.AddSeparator(string.Empty);
                string prefix = isNamingCatalogs ? catalog.Name + "/" : string.Empty;
                foreach (IndexedTable table in catalog.Tables)
                {
                    if (table.SourceDocument == null)
                    {
                        continue;
                    }
                    List<string> keys = new();
                    foreach (TableDocumentEntry entry in table.SourceDocument.Entries)
                    {
                        keys.Add(entry.Key);
                    }
                    keys.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (string key in keys)
                    {
                        string catalogName = catalog.Name;
                        string tableName = table.Name;
                        bool isPicked = string.Equals(catalogName, pickedCatalog, StringComparison.OrdinalIgnoreCase) &&
                                        string.Equals(tableName, pickedTable, StringComparison.OrdinalIgnoreCase) &&
                                        string.Equals(key, pickedEntry, StringComparison.OrdinalIgnoreCase);
                        menu.AddItem(new GUIContent($"{prefix}{tableName}/{key}"), isPicked, () => Assign(serializedObject, propertyPath, catalogName, tableName, key));
                    }
                }
            }
            menu.DropDown(field);
        }

        private static void Assign(SerializedObject serializedObject, string propertyPath, string catalogName, string tableName, string entryName)
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            property.FindPropertyRelative(CatalogField).stringValue = catalogName ?? string.Empty;
            property.FindPropertyRelative(TableField).stringValue = tableName ?? string.Empty;
            property.FindPropertyRelative(EntryField).stringValue = entryName ?? string.Empty;
            serializedObject.ApplyModifiedProperties();
        }

        private static GUIStyle GetProblemStyle()
        {
            if (_problemStyle == null)
            {
                _problemStyle = new GUIStyle(EditorStyles.miniLabel);
                _problemStyle.normal.textColor = new Color(0.9f, 0.35f, 0.3f);
            }
            return _problemStyle;
        }
    }
}
