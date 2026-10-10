using reromanlee.ReactiveLocalizer.Unity;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The UI Toolkit drawing of an <see cref="EntryReference"/> field: a popup showing <c>Table.Entry</c> that opens
    /// the entry picker, and the entry's preview below it. It follows the property, the localization files and the
    /// preview language, refreshing at most once a frame.
    /// </summary>
    internal sealed class EntryReferenceField : BaseField<EntryReference>
    {
        private readonly Object[] _targets;
        private readonly string _propertyPath;
        private readonly string _limitedCatalog;
        private readonly string _fieldName;
        private readonly VisualElement _button;
        private readonly TextElement _buttonText;
        private readonly Label _preview;
        private readonly Button _fixButton;
        private EntryDescription _description;
        private bool _isMixed;
        private bool _isRefreshScheduled;

        public EntryReferenceField(SerializedProperty property, string label, string limitedCatalog, string fieldName) : base(label, CreateInput())
        {
            _targets = property.serializedObject.targetObjects;
            _propertyPath = property.propertyPath;
            _limitedCatalog = limitedCatalog;
            _fieldName = fieldName;
            AddToClassList(alignedFieldUssClassName);
            labelElement.style.alignSelf = Align.FlexStart;

            VisualElement input = this.Q(className: inputUssClassName);
            _button = input[0];
            _buttonText = (TextElement)_button[0];
            _preview = (Label)input[1][0];
            _fixButton = (Button)input[1][1];
            _button.RegisterCallback<PointerDownEvent>(OnButtonPointerDown);
            _button.RegisterCallback<KeyDownEvent>(OnButtonKeyDown);
            _fixButton.clicked += ApplyFix;

            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                CatalogIndex.Invalidated += ScheduleRefresh;
                LocalizationUserSettings.PreviewLanguageChanged += ScheduleRefresh;
            });
            RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                CatalogIndex.Invalidated -= ScheduleRefresh;
                LocalizationUserSettings.PreviewLanguageChanged -= ScheduleRefresh;
            });
            this.TrackPropertyValue(property, OnPropertyChanged);
            _isMixed = property.hasMultipleDifferentValues;
            SetValueWithoutNotify(EntryReferenceDrawer.Read(property));
        }

        public override void SetValueWithoutNotify(EntryReference newValue)
        {
            base.SetValueWithoutNotify(newValue);
            Refresh();
        }

        private static VisualElement CreateInput()
        {
            VisualElement input = new();
            input.style.flexDirection = FlexDirection.Column;
            input.style.flexGrow = 1f;
            input.style.flexShrink = 1f;

            // The popup look comes from the classes Unity's own popup fields use, on every editor skin and version.
            VisualElement button = new() { focusable = true, tabIndex = 0 };
            button.AddToClassList("unity-base-popup-field__input");
            button.AddToClassList("unity-popup-field__input");
            TextElement text = new();
            text.AddToClassList("unity-base-popup-field__text");
            VisualElement arrow = new();
            arrow.AddToClassList("unity-base-popup-field__arrow");
            button.Add(text);
            button.Add(arrow);
            input.Add(button);

            VisualElement previewRow = new();
            previewRow.style.flexDirection = FlexDirection.Row;
            previewRow.style.alignItems = Align.Center;
            previewRow.style.minHeight = EditorGUIUtility.singleLineHeight;
            Label preview = new();
            preview.style.flexGrow = 1f;
            preview.style.flexShrink = 1f;
            preview.style.fontSize = 10f;
            preview.style.overflow = Overflow.Hidden;
            preview.style.textOverflow = TextOverflow.Ellipsis;
            preview.style.whiteSpace = WhiteSpace.NoWrap;
            preview.style.paddingLeft = 2f;
            Button fix = new() { text = "Update" };
            fix.style.display = DisplayStyle.None;
            fix.style.fontSize = 10f;
            fix.style.flexShrink = 0f;
            previewRow.Add(preview);
            previewRow.Add(fix);
            input.Add(previewRow);
            return input;
        }

        private void OnPropertyChanged(SerializedProperty property)
        {
            _isMixed = property.hasMultipleDifferentValues;
            SetValueWithoutNotify(EntryReferenceDrawer.Read(property));
        }

        private void OnButtonPointerDown(PointerDownEvent pointer)
        {
            if (pointer.button != 0)
            {
                return;
            }
            OpenPicker();
            pointer.StopPropagation();
        }

        private void OnButtonKeyDown(KeyDownEvent key)
        {
            if (key.keyCode != KeyCode.Return && key.keyCode != KeyCode.KeypadEnter && key.keyCode != KeyCode.Space)
            {
                return;
            }
            OpenPicker();
            key.StopPropagation();
        }

        private void OpenPicker()
        {
            if (!enabledInHierarchy)
            {
                return;
            }
            Rect screenRect = GUIUtility.GUIToScreenRect(_button.worldBound);
            EntryReferenceDrawer.OpenPicker(screenRect, _targets, _propertyPath, value, _limitedCatalog, _fieldName);
        }

        private void ApplyFix()
        {
            if (!_description.Fix.IsEmpty)
            {
                EntryReferenceDrawer.Assign(_targets, _propertyPath, _description.Fix);
            }
        }

        private void ScheduleRefresh()
        {
            if (_isRefreshScheduled)
            {
                return;
            }
            _isRefreshScheduled = true;
            schedule.Execute(() =>
            {
                _isRefreshScheduled = false;
                Refresh();
            });
        }

        private void Refresh()
        {
            // The base constructor may set a value before this one has built the parts showing it.
            if (_buttonText == null)
            {
                return;
            }
            _buttonText.text = EntryReferenceDrawer.GetDisplayName(value, _isMixed);
            _button.tooltip = string.IsNullOrEmpty(value.CatalogName) ? null : $"In the catalog '{value.CatalogName}'.";
            if (_isMixed)
            {
                _description = default;
                _preview.text = string.Empty;
                _fixButton.style.display = DisplayStyle.None;
                return;
            }
            _description = EntryPreview.Describe(value, _limitedCatalog);
            _preview.text = _description.Text;
            _preview.tooltip = _description.Tooltip ?? _description.Text;
            _preview.style.color = EntryReferenceDrawer.GetPreviewColor(_description.Kind);
            _fixButton.style.display = _description.Kind == EntryPreviewKind.Renamed ? DisplayStyle.Flex : DisplayStyle.None;
            _fixButton.tooltip = _description.Kind == EntryPreviewKind.Renamed ? $"Point the field at {_description.Fix}." : null;
        }
    }
}
