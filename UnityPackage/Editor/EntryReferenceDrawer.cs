using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Draws an <see cref="EntryReference"/> as <c>Table.Entry</c> opening the entry picker, with the entry's text in
    /// the preview language below it. A broken reference shows red; one found by a former name offers to update
    /// itself. Drawn natively in both UI Toolkit and IMGUI inspectors, and limited to one catalog by
    /// <see cref="EntryCatalogAttribute"/>.
    /// </summary>
    [CustomPropertyDrawer(typeof(EntryReference))]
    internal sealed class EntryReferenceDrawer : PropertyDrawer
    {
        private const string CatalogField = "_catalog";
        private const string TableField = "_table";
        private const string EntryField = "_entry";
        private const float FixButtonWidth = 56f;

        private static readonly Dictionary<EntryPreviewKind, GUIStyle> PreviewStyles = new();
        private static bool _previewStylesSkin;

        private string _limitedCatalog;
        private bool _hasReadLimit;

        /// <summary>The only catalog the field takes, from its <see cref="EntryCatalogAttribute"/>; null for any.</summary>
        private string LimitedCatalog
        {
            get
            {
                if (!_hasReadLimit)
                {
                    _hasReadLimit = true;
                    _limitedCatalog = fieldInfo?.GetCustomAttribute<EntryCatalogAttribute>()?.ResolveCatalogName();
                }
                return _limitedCatalog;
            }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new EntryReferenceField(property, property.displayName, LimitedCatalog, fieldInfo?.Name);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EntryReference value = Read(property);
            bool isMixed = property.hasMultipleDifferentValues;
            label = EditorGUI.BeginProperty(position, label, property);
            Rect line = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect field = EditorGUI.PrefixLabel(line, label);
            if (EditorGUI.DropdownButton(field, new GUIContent(GetDisplayName(value, isMixed), value.CatalogName), FocusType.Keyboard))
            {
                OpenPicker(GUIUtility.GUIToScreenRect(field), property.serializedObject.targetObjects, property.propertyPath, value, LimitedCatalog, fieldInfo?.Name);
            }
            if (!isMixed)
            {
                Rect preview = new(field.x, line.yMax + EditorGUIUtility.standardVerticalSpacing, field.width, EditorGUIUtility.singleLineHeight);
                EntryDescription description = EntryPreview.Describe(value, LimitedCatalog);
                if (description.Kind == EntryPreviewKind.Renamed)
                {
                    Rect button = new(preview.xMax - FixButtonWidth, preview.y, FixButtonWidth, preview.height);
                    preview.xMax = button.x - 2f;
                    if (GUI.Button(button, new GUIContent("Update", $"Point the field at {description.Fix}."), EditorStyles.miniButton))
                    {
                        Write(property, description.Fix);
                    }
                }
                GUI.Label(preview, new GUIContent(description.Text, description.Tooltip ?? description.Text), GetPreviewStyle(description.Kind));
            }
            EditorGUI.EndProperty();
        }

        /// <summary>Reads the reference <paramref name="property"/> holds.</summary>
        public static EntryReference Read(SerializedProperty property)
        {
            return new EntryReference(
                property.FindPropertyRelative(CatalogField).stringValue,
                property.FindPropertyRelative(TableField).stringValue,
                property.FindPropertyRelative(EntryField).stringValue);
        }

        /// <summary>Returns what the field's button says: <c>Table.Entry</c>, None, or a dash for several different values.</summary>
        public static string GetDisplayName(EntryReference value, bool isMixed)
        {
            return isMixed ? "\u2014" : value.IsEmpty ? "None" : value.ToString();
        }

        /// <summary>
        /// Opens the entry picker below <paramref name="screenRect"/>, and stores the pick in the property at
        /// <paramref name="propertyPath"/> of every object of <paramref name="targets"/> still alive by then.
        /// </summary>
        public static void OpenPicker(Rect screenRect, Object[] targets, string propertyPath, EntryReference current, string limitedCatalog, string fieldName)
        {
            GameObject context = targets.Length > 0 ? targets[0] switch
            {
                Component component => component.gameObject,
                GameObject gameObject => gameObject,
                _ => null
            } : null;
            EntryPicker.Show(screenRect, new EntryPickerRequest(current, limitedCatalog, context, fieldName), picked => Assign(targets, propertyPath, picked));
        }

        /// <summary>Stores <paramref name="value"/> in the property at <paramref name="propertyPath"/> of the objects of <paramref name="targets"/> still alive, as one step Undo can take back.</summary>
        public static void Assign(Object[] targets, string propertyPath, EntryReference value)
        {
            List<Object> alive = new(targets.Length);
            for (int i = 0; i < targets.Length; i++)
            {
                if (targets[i] != null)
                {
                    alive.Add(targets[i]);
                }
            }
            if (alive.Count == 0)
            {
                return;
            }
            using SerializedObject serializedObject = new(alive.ToArray());
            SerializedProperty property = serializedObject.FindProperty(propertyPath);
            if (property == null)
            {
                return;
            }
            Write(property, value);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Returns the color the preview of <paramref name="kind"/> is drawn in.</summary>
        public static Color GetPreviewColor(EntryPreviewKind kind)
        {
            return kind switch
            {
                EntryPreviewKind.Broken => LocalizationColors.Problem,
                EntryPreviewKind.Renamed => LocalizationColors.Warning,
                _ => LocalizationColors.Muted
            };
        }

        private static void Write(SerializedProperty property, EntryReference value)
        {
            property.FindPropertyRelative(CatalogField).stringValue = value.CatalogName ?? string.Empty;
            property.FindPropertyRelative(TableField).stringValue = value.TableName ?? string.Empty;
            property.FindPropertyRelative(EntryField).stringValue = value.EntryName ?? string.Empty;
        }

        private static GUIStyle GetPreviewStyle(EntryPreviewKind kind)
        {
            // The colors follow the editor skin, which can change while the styles are cached.
            if (_previewStylesSkin != EditorGUIUtility.isProSkin)
            {
                PreviewStyles.Clear();
                _previewStylesSkin = EditorGUIUtility.isProSkin;
            }
            if (!PreviewStyles.TryGetValue(kind, out GUIStyle style))
            {
                style = new GUIStyle(EditorStyles.miniLabel) { clipping = TextClipping.Clip };
                style.normal.textColor = GetPreviewColor(kind);
                PreviewStyles.Add(kind, style);
            }
            return style;
        }
    }
}
