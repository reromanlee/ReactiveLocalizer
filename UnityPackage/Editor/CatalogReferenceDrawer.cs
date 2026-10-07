using reromanlee.ReactiveLocalizer.Unity;
using System;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>Draws a <see cref="CatalogReference"/> as a dropdown of the project's catalogs.</summary>
    [CustomPropertyDrawer(typeof(CatalogReference))]
    internal sealed class CatalogReferenceDrawer : PropertyDrawer
    {
        private const string CatalogField = "_catalog";
        private const string DefaultLabel = "Default catalog";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty catalog = property.FindPropertyRelative(CatalogField);
            EditorGUI.BeginProperty(position, label, property);
            Rect field = EditorGUI.PrefixLabel(position, label);
            string picked = property.hasMultipleDifferentValues
                ? "Mixed"
                : string.IsNullOrEmpty(catalog.stringValue) ? DefaultLabel : catalog.stringValue;
            if (EditorGUI.DropdownButton(field, new GUIContent(picked), FocusType.Keyboard))
            {
                SerializedObject serializedObject = property.serializedObject;
                string propertyPath = property.propertyPath;
                string current = catalog.stringValue;
                GenericMenu menu = new();
                menu.AddItem(new GUIContent(DefaultLabel), string.IsNullOrEmpty(current), () => Assign(serializedObject, propertyPath, string.Empty));
                menu.AddSeparator(string.Empty);
                foreach (IndexedCatalog indexed in CatalogIndex.Catalogs)
                {
                    string name = indexed.Name;
                    menu.AddItem(new GUIContent(name), string.Equals(name, current, StringComparison.OrdinalIgnoreCase), () => Assign(serializedObject, propertyPath, name));
                }
                menu.DropDown(field);
            }
            EditorGUI.EndProperty();
        }

        private static void Assign(SerializedObject serializedObject, string propertyPath, string catalogName)
        {
            serializedObject.Update();
            serializedObject.FindProperty(propertyPath).FindPropertyRelative(CatalogField).stringValue = catalogName;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
