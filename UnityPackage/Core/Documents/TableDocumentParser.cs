using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// Reads a table file line by line into a <see cref="TableDocument"/>. Each line is a blank line, a <c>#</c>
    /// comment, an <c>@</c> attribute or a <c>Key [fingerprint] = Value</c> entry; anything else is an error that
    /// costs only its own line.
    /// </summary>
    internal sealed class TableDocumentParser
    {
        private readonly string _text;
        private readonly List<string> _headerComments = new();
        private readonly List<DocumentProperty> _settings = new();
        private readonly List<TableDocumentEntry> _entries = new();
        private readonly List<DocumentIssue> _issues = new();
        private readonly Dictionary<ulong, int> _firstLines = new();
        private readonly List<string> _pendingComments = new();
        private readonly List<DocumentProperty> _pendingAttributes = new();
        private readonly StringBuilder _valueBuilder = new();

        // Set once a setting, an attribute or an entry is read: comments after that no longer open the file.
        private bool _hasContent;

        public TableDocumentParser(string text)
        {
            _text = text ?? string.Empty;
        }

        public TableDocument Parse()
        {
            DocumentLineReader reader = new(_text);
            while (reader.TryReadLine(out int lineNumber, out int start, out int length))
            {
                ReadLine(lineNumber, _text.AsSpan(start, length));
            }
            // Attributes with no entry after them have nothing to describe; comments are kept at the end.
            for (int i = 0; i < _pendingAttributes.Count; i++)
            {
                DocumentProperty attribute = _pendingAttributes[i];
                AddIssue(IssueSeverity.Warning, attribute.Line, 1, $"'@{attribute.Name}' has no entry after it, so it has no effect.");
            }
            return new TableDocument(_headerComments.ToArray(), _settings.ToArray(), _entries.ToArray(),
                _pendingComments.ToArray(), _issues.ToArray());
        }

        private void ReadLine(int lineNumber, ReadOnlySpan<char> line)
        {
            // A merge conflict left in a file would otherwise read as a few odd entries and a lost translation.
            if (DocumentSyntax.IsMergeMarker(line))
            {
                AddIssue(IssueSeverity.Error, lineNumber, 1, "This is a merge conflict marker. Resolve the conflict and remove the marker lines.");
                return;
            }
            int start = DocumentSyntax.SkipBlanks(line, 0);
            if (start == line.Length)
            {
                // A blank line after comments that open the file, before anything else, makes them the file's header.
                if (!_hasContent && _pendingComments.Count > 0)
                {
                    _headerComments.AddRange(_pendingComments);
                    _pendingComments.Clear();
                }
                return;
            }
            char first = line[start];
            if (first == '#')
            {
                _pendingComments.Add(DocumentSyntax.ReadComment(line, start));
            }
            else if (first == '@')
            {
                ReadAttribute(lineNumber, line, start);
            }
            else
            {
                ReadEntry(lineNumber, line, start);
            }
        }

        private void ReadAttribute(int lineNumber, ReadOnlySpan<char> line, int start)
        {
            int nameStart = start + 1;
            int nameEnd = DocumentSyntax.SkipIdentifier(line, nameStart);
            if (nameEnd == nameStart || (nameEnd < line.Length && !DocumentSyntax.IsBlank(line[nameEnd])))
            {
                AddIssue(IssueSeverity.Error, lineNumber, start + 1, "An attribute is a name right after '@' and an optional value, such as '@maximumLength 16'.");
                return;
            }
            ReadOnlySpan<char> name = line.Slice(nameStart, nameEnd - nameStart);
            int valueStart = DocumentSyntax.SkipBlanks(line, nameEnd);
            string value = line.Slice(valueStart, DocumentSyntax.TrimEnd(line, valueStart) - valueStart).ToString();

            // Table settings describe the whole table, so they are only accepted before the first entry.
            string setting = DocumentNames.FindTableSetting(name);
            if (setting != null)
            {
                if (_entries.Count > 0 || _pendingAttributes.Count > 0)
                {
                    AddIssue(IssueSeverity.Error, lineNumber, start + 1, $"'@{setting}' is a table setting, so it goes at the top of the file, before the first entry.");
                    return;
                }
                for (int i = 0; i < _settings.Count; i++)
                {
                    if (_settings[i].Name == setting)
                    {
                        AddIssue(IssueSeverity.Error, lineNumber, start + 1, $"'@{setting}' is already set on line {_settings[i].Line}.");
                        return;
                    }
                }
                // Comments written above the settings describe the file, so they join its header.
                _headerComments.AddRange(_pendingComments);
                _pendingComments.Clear();
                _settings.Add(new DocumentProperty(setting, value, lineNumber, null));
                _hasContent = true;
                return;
            }

            // Every other attribute belongs to the entry that follows it. Unknown ones are kept, so a file written
            // by a newer version of the package loses nothing when an older one rewrites it.
            string attribute = DocumentNames.FindEntryAttribute(name);
            if (attribute == null)
            {
                attribute = name.ToString();
                AddIssue(IssueSeverity.Warning, lineNumber, start + 1, $"'@{attribute}' is not an attribute this version knows; it is kept but has no effect.");
            }
            _pendingAttributes.Add(new DocumentProperty(attribute, value, lineNumber, null));
            _hasContent = true;
        }

        private void ReadEntry(int lineNumber, ReadOnlySpan<char> line, int start)
        {
            // The key is the run of identifier characters the line starts with.
            int keyEnd = DocumentSyntax.SkipIdentifier(line, start);
            if (keyEnd == start)
            {
                int tokenEnd = DocumentSyntax.SkipToken(line, start, '=');
                string token = line.Slice(start, tokenEnd - start).ToString();
                RejectEntry(lineNumber, start + 1, $"'{token}' can't start a line: expected an entry such as 'Key = Value', a '#' comment or an '@' attribute.");
                return;
            }
            ReadOnlySpan<char> key = line.Slice(start, keyEnd - start);
            int position = DocumentSyntax.SkipBlanks(line, keyEnd);

            // An optional fingerprint tag follows the key: six hexadecimal digits in brackets.
            bool hasFingerprint = false;
            uint fingerprint = 0;
            if (position < line.Length && line[position] == '[')
            {
                int closeOffset = line.Slice(position).IndexOf(']');
                if (closeOffset < 0)
                {
                    RejectEntry(lineNumber, position + 1, $"The fingerprint of '{key.ToString()}' has no closing ']'.");
                    return;
                }
                ReadOnlySpan<char> digits = line.Slice(position + 1, closeOffset - 1);
                hasFingerprint = DocumentSyntax.TryParseFingerprint(digits, out fingerprint);
                if (!hasFingerprint)
                {
                    AddIssue(IssueSeverity.Warning, lineNumber, position + 1, $"A fingerprint is six hexadecimal digits in brackets, such as [8b0e47]; '[{digits.ToString()}]' is ignored and '{key.ToString()}' counts as unverified.");
                }
                position = DocumentSyntax.SkipBlanks(line, position + closeOffset + 1);
            }

            // The first '=' after the key ends it; everything after it is the value.
            if (position >= line.Length || line[position] != '=')
            {
                RejectEntry(lineNumber, position + 1, $"Expected '=' after '{key.ToString()}'. An entry is written as 'Key = Value'.");
                return;
            }
            if (!NameRules.IsValid(key))
            {
                RejectEntry(lineNumber, start + 1, $"'{key.ToString()}' is not a valid key: {NameRules.Description}.");
                return;
            }

            // Keys are unique ignoring case. The first definition wins, so a duplicate can't silently replace it.
            ulong hash = Hashing.ComputeNameHash(key);
            if (_firstLines.TryGetValue(hash, out int firstLine))
            {
                RejectEntry(lineNumber, start + 1, $"'{key.ToString()}' is already defined on line {firstLine}; this definition is ignored.");
                return;
            }

            // The value is trimmed of blanks at both ends before its escapes are resolved, so escaped spaces survive.
            int valueStart = DocumentSyntax.SkipBlanks(line, position + 1);
            int valueEnd = DocumentSyntax.TrimEnd(line, valueStart);
            string value = TextEscaping.Unescape(line.Slice(valueStart, valueEnd - valueStart), lineNumber, valueStart + 1, _issues, _valueBuilder);

            _entries.Add(new TableDocumentEntry(key.ToString(), value, hasFingerprint, fingerprint,
                TakePendingComments(), TakePendingAttributes(), lineNumber));
            _firstLines.Add(hash, lineNumber);
            _hasContent = true;
        }

        /// <summary>
        /// Reports an entry line that can't be used. The comments and attributes written above it described that
        /// entry, so they are dropped with it rather than attached to the next one.
        /// </summary>
        private void RejectEntry(int line, int column, string message)
        {
            AddIssue(IssueSeverity.Error, line, column, message);
            _pendingComments.Clear();
            _pendingAttributes.Clear();
        }

        private IReadOnlyList<string> TakePendingComments()
        {
            if (_pendingComments.Count == 0)
            {
                return Array.Empty<string>();
            }
            string[] comments = _pendingComments.ToArray();
            _pendingComments.Clear();
            return comments;
        }

        private IReadOnlyList<DocumentProperty> TakePendingAttributes()
        {
            if (_pendingAttributes.Count == 0)
            {
                return Array.Empty<DocumentProperty>();
            }
            DocumentProperty[] attributes = _pendingAttributes.ToArray();
            _pendingAttributes.Clear();
            return attributes;
        }

        private void AddIssue(IssueSeverity severity, int line, int column, string message)
        {
            _issues.Add(new DocumentIssue(severity, line, column, message));
        }
    }
}
