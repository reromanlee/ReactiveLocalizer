using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// A small popup asking for a few values, such as a new entry's key and text. It checks them as they are typed,
    /// confirms with Enter and closes with Escape: the table window's add, rename, move and new-table actions.
    /// </summary>
    internal sealed class TablePrompt : EditorWindow
    {
        private const float MinimumWidth = 340f;
        private const float RowHeight = 22f;

        private string _heading;
        private string _confirmText;
        private string[] _labels;
        private string[] _values;
        private Func<string[], string> _check;
        private Func<string[], string> _confirm;
        private TextField[] _fields;
        private Label _hint;
        private Button _confirmButton;

        /// <summary>Opens the prompt below <paramref name="activator"/>, a rectangle in screen space.</summary>
        /// <param name="activator">What opened the prompt, in screen space.</param>
        /// <param name="heading">What the prompt asks for.</param>
        /// <param name="confirmText">The confirm button's text.</param>
        /// <param name="labels">One label per value.</param>
        /// <param name="values">The values to start with, one per label.</param>
        /// <param name="check">Returns what is wrong with the values, or null when they can be confirmed.</param>
        /// <param name="confirm">Acts on the values; returns what went wrong, or null to close the prompt.</param>
        public static void Show(Rect activator, string heading, string confirmText, string[] labels, string[] values, Func<string[], string> check, Func<string[], string> confirm)
        {
            TablePrompt prompt = CreateInstance<TablePrompt>();
            prompt._heading = heading;
            prompt._confirmText = confirmText;
            prompt._labels = labels;
            prompt._values = (string[])values.Clone();
            prompt._check = check;
            prompt._confirm = confirm;
            float height = 76f + labels.Length * RowHeight + 28f;
            prompt.ShowAsDropDown(activator, new Vector2(Mathf.Max(activator.width, MinimumWidth), height));
        }

        private void CreateGUI()
        {
            if (_confirm == null)
            {
                // A domain reload dropped what the prompt was for.
                EditorApplication.delayCall += Close;
                return;
            }
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 6f;
            root.style.paddingBottom = 6f;
            root.style.borderTopWidth = root.style.borderBottomWidth = root.style.borderLeftWidth = root.style.borderRightWidth = 1f;
            root.style.borderTopColor = root.style.borderBottomColor = root.style.borderLeftColor = root.style.borderRightColor = LocalizationColors.Border;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            Label heading = new(_heading);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.marginBottom = 4f;
            root.Add(heading);

            _fields = new TextField[_labels.Length];
            for (int i = 0; i < _labels.Length; i++)
            {
                int index = i;
                TextField field = new(_labels[i]) { value = _values[i] ?? string.Empty };
                field.labelElement.style.minWidth = 60f;
                field.labelElement.style.width = 60f;
                field.RegisterValueChangedCallback(change =>
                {
                    _values[index] = change.newValue;
                    UpdateHint(null);
                });
                _fields[i] = field;
                root.Add(field);
            }

            _hint = new Label();
            _hint.style.whiteSpace = WhiteSpace.Normal;
            _hint.style.marginTop = 4f;
            _hint.style.flexGrow = 1f;
            root.Add(_hint);

            VisualElement buttons = new();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.justifyContent = Justify.FlexEnd;
            buttons.Add(new Button(Close) { text = "Cancel" });
            _confirmButton = new Button(Confirm) { text = _confirmText };
            buttons.Add(_confirmButton);
            root.Add(buttons);

            UpdateHint(null);
            root.schedule.Execute(() =>
            {
                _fields[0].Focus();
                _fields[0].SelectAll();
            });
        }

        private void OnKeyDown(KeyDownEvent key)
        {
            if (key.keyCode == KeyCode.Escape)
            {
                Close();
                key.StopPropagation();
            }
            else if (key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter)
            {
                Confirm();
                key.StopPropagation();
            }
        }

        private void UpdateHint(string failure)
        {
            string problem = failure ?? _check(_values);
            _hint.text = problem ?? string.Empty;
            _hint.style.color = LocalizationColors.Problem;
            _confirmButton.SetEnabled(failure != null || problem == null);
        }

        private void Confirm()
        {
            if (_check(_values) != null)
            {
                return;
            }
            Func<string[], string> confirm = _confirm;
            string failure = confirm(_values);
            if (failure != null)
            {
                UpdateHint(failure);
                return;
            }
            _confirm = null;
            // Saving files may have taken the popup's focus, which closes it already.
            if (this != null)
            {
                Close();
            }
        }
    }
}
