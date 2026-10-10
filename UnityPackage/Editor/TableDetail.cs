using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Messages;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>What the table window's detail pane asks of the window, each change saved at once with Undo.</summary>
    internal interface ITableEditor
    {
        void SetText(string key, int language, string text);

        void MarkCurrent(string key, int language);

        void RemoveTranslation(string key, int language);

        void SetContext(string key, string context);

        void SetMaximumLength(string key, string value);

        void Rename(string key, Rect activator);

        void Move(string key, Rect activator);

        void Delete(string key);
    }

    /// <summary>
    /// The table window's detail pane for the selected entry: every language stacked with a full multi-line editor,
    /// how each translation stands with the actions that fit it, the entry's context, maximum length and attributes,
    /// and a preview of its message with sample values, rendered as each language would show it while typing.
    /// </summary>
    internal sealed class TableDetail : VisualElement
    {
        private const int SampleLimit = 6;
        private static readonly Dictionary<string, string> Samples = new(StringComparer.OrdinalIgnoreCase);
        private static readonly char[] SampleSeparators = { ',', ';' };

        private readonly ITableEditor _editor;
        private readonly ScrollView _scroll;
        private readonly Label _empty;
        private readonly List<LanguageView> _languages = new();
        private TableSheet _sheet;
        private string _key;
        private TextField _context;
        private TextField _maximumLength;
        private VisualElement _preview;
        private VisualElement _previewResults;
        private readonly List<TextField> _sampleFields = new();
        private string _previewSignature;

        public TableDetail(ITableEditor editor)
        {
            _editor = editor;
            style.flexGrow = 1f;
            _empty = new Label("Pick an entry to edit it here.");
            _empty.style.color = LocalizationColors.Muted;
            _empty.style.paddingLeft = 8f;
            _empty.style.paddingTop = 8f;
            _empty.style.whiteSpace = WhiteSpace.Normal;
            Add(_empty);
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1f;
            _scroll.contentContainer.style.paddingLeft = 8f;
            _scroll.contentContainer.style.paddingRight = 8f;
            _scroll.contentContainer.style.paddingBottom = 8f;
            Add(_scroll);
            ShowMessage(null);
        }

        /// <summary>Shows <paramref name="row"/> of <paramref name="sheet"/>; null shows nothing. The same entry again only updates what isn't being typed in.</summary>
        public void Show(TableSheet sheet, TableSheetRow row)
        {
            if (row == null)
            {
                ShowMessage("Pick an entry to edit it here.");
                return;
            }
            if (sheet == _sheet && string.Equals(row.Key, _key, StringComparison.Ordinal) && _languages.Count == sheet.Languages.Count)
            {
                Update(row);
                return;
            }
            Build(sheet, row);
        }

        /// <summary>Shows <paramref name="message"/> in place of an entry; null shows nothing.</summary>
        public void ShowMessage(string message)
        {
            _sheet = null;
            _key = null;
            _languages.Clear();
            _scroll.Clear();
            _scroll.style.display = DisplayStyle.None;
            _empty.text = message ?? string.Empty;
            _empty.style.display = DisplayStyle.Flex;
        }

        /// <summary>Puts the cursor in the editor of <paramref name="language"/>.</summary>
        public void FocusLanguage(int language)
        {
            if (language >= 0 && language < _languages.Count)
            {
                _languages[language].Editor.Focus();
            }
        }

        private void Build(TableSheet sheet, TableSheetRow row)
        {
            _sheet = sheet;
            _key = row.Key;
            _languages.Clear();
            _scroll.Clear();
            _empty.style.display = DisplayStyle.None;
            _scroll.style.display = DisplayStyle.Flex;

            VisualElement header = Row();
            header.style.marginTop = 6f;
            Label title = new($"{sheet.TableName}.{row.Key}");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 13f;
            title.style.flexGrow = 1f;
            title.style.whiteSpace = WhiteSpace.Normal;
            header.Add(title);
            if (row.Source != null)
            {
                Button rename = SmallButton("Rename", "Give the entry a new key; references to the old one keep working.", null);
                rename.clicked += () => _editor.Rename(_key, rename.worldBound);
                Button move = SmallButton("Move", "Move the entry to another table; references to it keep working.", null);
                move.clicked += () => _editor.Move(_key, move.worldBound);
                header.Add(rename);
                header.Add(move);
                header.Add(SmallButton("Delete", "Delete the entry from every language.", () => _editor.Delete(_key)));
            }
            _scroll.Add(header);

            if (row.Source != null)
            {
                _context = Field("Context", "What translators need to know: where the text shows, what it refers to, how much room it has.", true);
                _context.RegisterCallback<FocusOutEvent>(_ => CommitIfChanged(_context, ContextOf(_sheet.Find(_key)), value => _editor.SetContext(_key, value)));
                _maximumLength = Field("Maximum length", "The most characters any language's text may have; empty for no limit.", false);
                _maximumLength.RegisterCallback<FocusOutEvent>(_ => CommitIfChanged(_maximumLength, MaximumLengthOf(_sheet.Find(_key)), value => _editor.SetMaximumLength(_key, value)));
                Label attributes = new() { name = "attributes" };
                attributes.style.color = LocalizationColors.Muted;
                attributes.style.whiteSpace = WhiteSpace.Normal;
                attributes.style.marginTop = 2f;
                _scroll.Add(attributes);
            }
            else
            {
                Label orphan = new("The source language has no such key, so no language shows this text. Remove it, or add the key to the source language.");
                orphan.style.color = LocalizationColors.Problem;
                orphan.style.whiteSpace = WhiteSpace.Normal;
                orphan.style.marginTop = 4f;
                _scroll.Add(orphan);
            }

            for (int l = 0; l < sheet.Languages.Count; l++)
            {
                _languages.Add(BuildLanguage(sheet.Languages[l], l));
            }
            _preview = new VisualElement();
            _preview.style.marginTop = 10f;
            _previewResults = new VisualElement();
            _previewSignature = null;
            _scroll.Add(_preview);
            Update(row);
        }

        private LanguageView BuildLanguage(LanguageInfo language, int index)
        {
            LanguageView view = new();
            VisualElement header = Row();
            header.style.marginTop = 10f;
            Label name = new(index == 0 ? $"{language.Name} (source)" : language.Name);
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(name);
            view.State = new Label();
            view.State.style.marginLeft = 6f;
            view.State.style.flexGrow = 1f;
            header.Add(view.State);
            view.ShowChange = SmallButton("Show source change", "Find the source text this was translated from in git, and compare it with the current one.", () => ShowSourceChange(view, index));
            view.MarkCurrent = SmallButton("Mark as current", "The translation still fits the current source text.", () => _editor.MarkCurrent(_key, index));
            view.Remove = SmallButton("Remove", "Remove the translation, so the fallback language's text shows.", () => _editor.RemoveTranslation(_key, index));
            header.Add(view.ShowChange);
            header.Add(view.MarkCurrent);
            header.Add(view.Remove);
            _scroll.Add(header);

            view.Editor = new TextField { multiline = true };
            view.Editor.style.whiteSpace = WhiteSpace.Normal;
            view.Editor.style.minHeight = 38f;
            view.Editor.style.marginLeft = 0f;
            view.Editor.style.marginRight = 0f;
            view.Editor.RegisterValueChangedCallback(_ => UpdatePreview());
            view.Editor.RegisterCallback<FocusOutEvent>(_ =>
                CommitIfChanged(view.Editor, _sheet?.Find(_key)?.Cells[index].Text ?? string.Empty, value => _editor.SetText(_key, index, value)));
            view.Editor.RegisterCallback<KeyDownEvent>(key =>
            {
                // Enter makes a new line in a paragraph; Ctrl+Enter or Cmd+Enter commits.
                if ((key.keyCode == KeyCode.Return || key.keyCode == KeyCode.KeypadEnter) && (key.ctrlKey || key.commandKey))
                {
                    key.StopPropagation();
                    view.Editor.Blur();
                }
            }, TrickleDown.TrickleDown);
            _scroll.Add(view.Editor);

            view.Problem = new Label();
            view.Problem.style.whiteSpace = WhiteSpace.Normal;
            view.Problem.style.fontSize = 10f;
            _scroll.Add(view.Problem);

            view.Change = new Label();
            view.Change.style.whiteSpace = WhiteSpace.Normal;
            view.Change.style.display = DisplayStyle.None;
            view.Change.style.marginTop = 2f;
            _scroll.Add(view.Change);
            return view;
        }

        private void Update(TableSheetRow row)
        {
            if (_context != null && row.Source != null)
            {
                SetIfIdle(_context, ContextOf(row));
                SetIfIdle(_maximumLength, MaximumLengthOf(row));
                _scroll.Q<Label>("attributes").text = DescribeAttributes(row.Source);
            }
            for (int l = 0; l < _languages.Count; l++)
            {
                LanguageView view = _languages[l];
                TableSheetCell cell = row.Cells[l];
                SetIfIdle(view.Editor, cell.Text ?? string.Empty);
                bool isSource = l == 0;
                view.State.text = DescribeState(cell.State, isSource, row.Source != null);
                view.State.style.color = LocalizationColors.Of(cell.State);
                view.MarkCurrent.style.display = !isSource && (cell.State == TranslationState.Outdated || cell.State == TranslationState.Unverified) ? DisplayStyle.Flex : DisplayStyle.None;
                view.ShowChange.style.display = cell.State == TranslationState.Outdated ? DisplayStyle.Flex : DisplayStyle.None;
                if (cell.State != TranslationState.Outdated)
                {
                    view.Change.style.display = DisplayStyle.None;
                }
                view.Remove.style.display = (!isSource || row.Source == null) && cell.Text != null ? DisplayStyle.Flex : DisplayStyle.None;
                view.Problem.text = cell.Message ?? string.Empty;
                view.Problem.style.display = cell.Message != null ? DisplayStyle.Flex : DisplayStyle.None;
                view.Problem.style.color = (cell.Problems & TableSheetProblem.Error) != 0 ? LocalizationColors.Problem : LocalizationColors.Warning;
                view.Editor.style.backgroundColor = LocalizationColors.Tint(cell, row.Source != null);
            }
            UpdatePreview();
        }

        /// <summary>Shows the source message rendered with the sample values in every language, building the sample fields only when its arguments change.</summary>
        private void UpdatePreview()
        {
            if (_preview == null || _sheet == null || _languages.Count == 0)
            {
                return;
            }
            IReadOnlyList<(string Name, MessageArgumentKind Kind)> arguments = MessagePreview.GetArguments(_languages[0].Editor.value);
            _preview.style.display = arguments.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (arguments.Count == 0)
            {
                return;
            }
            string signature = string.Join(",", arguments);
            if (signature != _previewSignature)
            {
                _previewSignature = signature;
                BuildSampleFields(arguments);
            }
            RenderPreview(arguments);
        }

        private void BuildSampleFields(IReadOnlyList<(string Name, MessageArgumentKind Kind)> arguments)
        {
            _preview.Clear();
            _sampleFields.Clear();
            Label heading = new("Preview");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            _preview.Add(heading);
            for (int i = 0; i < arguments.Count; i++)
            {
                (string name, MessageArgumentKind kind) = arguments[i];
                if (!Samples.TryGetValue(name, out string written))
                {
                    written = kind switch
                    {
                        MessageArgumentKind.Number => "1, 2, 5, 21",
                        MessageArgumentKind.Keyword => "other",
                        _ => name
                    };
                }
                TextField field = new(name) { value = written, tooltip = "Sample values, separated by commas; each preview line takes the next one." };
                field.labelElement.style.minWidth = 70f;
                field.RegisterValueChangedCallback(change =>
                {
                    Samples[name] = change.newValue;
                    UpdatePreview();
                });
                _sampleFields.Add(field);
                _preview.Add(field);
            }
            _preview.Add(_previewResults);
        }

        private void RenderPreview(IReadOnlyList<(string Name, MessageArgumentKind Kind)> arguments)
        {
            _previewResults.Clear();
            List<string[]> samples = new(arguments.Count);
            int rows = 1;
            for (int i = 0; i < arguments.Count; i++)
            {
                string[] written = _sampleFields[i].value.Split(SampleSeparators, StringSplitOptions.RemoveEmptyEntries);
                samples.Add(written.Length > 0 ? written : new[] { string.Empty });
                rows = Math.Max(rows, Math.Min(written.Length, SampleLimit));
            }
            MessageArgument[] values = new MessageArgument[arguments.Count];
            List<string> lines = new(rows);
            for (int l = 0; l < _languages.Count; l++)
            {
                string text = _languages[l].Editor.value;
                if (l > 0 && text.Length == 0)
                {
                    continue;
                }
                lines.Clear();
                for (int r = 0; r < rows; r++)
                {
                    for (int a = 0; a < arguments.Count; a++)
                    {
                        values[a] = MessagePreview.CreateArgument(arguments[a].Name, samples[a][r % samples[a].Length]);
                    }
                    string rendered = MessagePreview.Render(_sheet.Catalog.Info, _sheet.Languages[l], text, values, out string problem);
                    lines.Add(rendered ?? problem);
                    if (rendered == null)
                    {
                        break;
                    }
                }
                Label result = new($"{_sheet.Languages[l].Name}:  {string.Join("  \u00B7  ", lines)}");
                result.style.whiteSpace = WhiteSpace.Normal;
                result.style.marginTop = 2f;
                _previewResults.Add(result);
            }
        }

        /// <summary>Looks up the source text the translation in <paramref name="language"/> was made from, and shows what changed since.</summary>
        private void ShowSourceChange(LanguageView view, int language)
        {
            TableSheetRow row = _sheet?.Find(_key);
            TableFile file = _sheet?.Edit.Files.Get(_sheet.Languages[language].Name);
            if (row?.Source == null || file == null || !file.TryGetEntry(_key, out TableFileEntry translation) || !translation.HasFingerprint)
            {
                return;
            }
            string key = _key;
            string current = row.Source.Value;
            string physicalPath = FileUtil.GetPhysicalPath(_sheet.Edit.GetPath(_sheet.Languages[0].Name));
            view.Change.text = "Looking for the source text it was translated from\u2026";
            view.Change.style.color = LocalizationColors.Muted;
            view.Change.style.display = DisplayStyle.Flex;
            SourceHistory.FindAsync(physicalPath, key, translation.Fingerprint).ContinueWith(search =>
            {
                // The pane may show another entry by now, or be gone.
                if (view.Change.panel == null || key != _key)
                {
                    return;
                }
                (string text, string problem) = search.IsFaulted ? (null, "Looking it up failed: " + search.Exception?.GetBaseException().Message) : search.Result;
                view.Change.style.color = text == null ? new StyleColor(LocalizationColors.Muted) : new StyleColor(StyleKeyword.Null);
                view.Change.text = text == null ? problem : DescribeChange(text, current);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        /// <summary>Writes the old source text with its removed words marked, then the current one with its added words marked.</summary>
        private static string DescribeChange(string before, string after)
        {
            List<TextDiffPart> parts = TextDiff.Compare(before, after);
            string removed = ColorUtility.ToHtmlStringRGB(LocalizationColors.Problem);
            string added = ColorUtility.ToHtmlStringRGB(LocalizationColors.Added);
            StringBuilder then = new("Translated from:  ");
            StringBuilder now = new("Source now:  ");
            for (int i = 0; i < parts.Count; i++)
            {
                // Rich text reads '<' as a tag, so the texts show it as a look-alike.
                string text = parts[i].Text.Replace('<', '\u2039');
                switch (parts[i].Kind)
                {
                    case TextDiffKind.Removed:
                        then.Append("<b><color=#").Append(removed).Append('>').Append(text).Append("</color></b>");
                        break;
                    case TextDiffKind.Added:
                        now.Append("<b><color=#").Append(added).Append('>').Append(text).Append("</color></b>");
                        break;
                    default:
                        then.Append(text);
                        now.Append(text);
                        break;
                }
            }
            return then.Append('\n').Append(now).ToString();
        }

        private static string DescribeState(TranslationState state, bool isSource, bool hasSource)
        {
            return state switch
            {
                TranslationState.Missing when !hasSource => string.Empty,
                TranslationState.Missing when isSource => "missing",
                TranslationState.Missing => "missing: shows the fallback language's text",
                TranslationState.Outdated => "outdated: the source text changed since",
                TranslationState.Unverified => "unverified: no fingerprint says which source text it follows",
                TranslationState.Orphan => "orphan: never shown",
                _ => string.Empty
            };
        }

        private static string ContextOf(TableSheetRow row) => row?.Source == null ? string.Empty : string.Join("\n", row.Source.Comments);

        private static string MaximumLengthOf(TableSheetRow row)
        {
            List<string> values = row?.Source?.GetAttributeValues(DocumentNames.MaximumLength);
            return values is { Count: > 0 } ? values[0] : string.Empty;
        }

        private static string DescribeAttributes(TableFileEntry source)
        {
            List<string> lines = new();
            for (int i = 0; i < source.Attributes.Count; i++)
            {
                if (!string.Equals(source.Attributes[i].Name, DocumentNames.MaximumLength, StringComparison.OrdinalIgnoreCase))
                {
                    lines.Add($"@{source.Attributes[i].Name} {source.Attributes[i].Value}");
                }
            }
            return string.Join("\n", lines);
        }

        private static void CommitIfChanged(TextField field, string current, Action<string> commit)
        {
            if (!string.Equals(field.value, current, StringComparison.Ordinal))
            {
                commit(field.value);
            }
        }

        /// <summary>Shows <paramref name="value"/> in <paramref name="field"/> unless someone is typing in it.</summary>
        private static void SetIfIdle(TextField field, string value)
        {
            if (field.focusController?.focusedElement != field && !string.Equals(field.value, value, StringComparison.Ordinal))
            {
                field.SetValueWithoutNotify(value);
            }
        }

        private TextField Field(string label, string tooltip, bool isMultiline)
        {
            TextField field = new(label) { multiline = isMultiline, tooltip = tooltip };
            field.labelElement.style.minWidth = 100f;
            field.style.whiteSpace = WhiteSpace.Normal;
            field.style.marginLeft = 0f;
            field.style.marginRight = 0f;
            field.style.marginTop = 4f;
            _scroll.Add(field);
            return field;
        }

        private static VisualElement Row()
        {
            VisualElement row = new();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            return row;
        }

        private static Button SmallButton(string text, string tooltip, Action clicked)
        {
            Button button = clicked != null ? new Button(clicked) : new Button();
            button.text = text;
            button.tooltip = tooltip;
            button.style.fontSize = 10f;
            button.style.marginLeft = 2f;
            button.style.marginRight = 0f;
            return button;
        }

        private sealed class LanguageView
        {
            public Label State;
            public Button ShowChange;
            public Label Change;
            public Button MarkCurrent;
            public Button Remove;
            public TextField Editor;
            public Label Problem;
        }
    }
}
