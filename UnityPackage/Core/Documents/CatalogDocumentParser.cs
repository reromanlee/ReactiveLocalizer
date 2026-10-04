using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// Reads a catalog file line by line into a <see cref="CatalogDocument"/>. Each line is a blank line, a <c>#</c>
    /// comment, an <c>@</c> attribute before the first section, a <c>[Language]</c> section header or a
    /// <c>Field = Value</c> line inside a section; anything else is an error that costs only its own line.
    /// </summary>
    internal sealed class CatalogDocumentParser
    {
        private readonly string _text;
        private readonly List<string> _headerComments = new();
        private readonly List<DocumentProperty> _attributes = new();
        private readonly List<CatalogDocumentLanguage> _languages = new();
        private readonly List<DocumentIssue> _issues = new();
        private readonly List<string> _pendingComments = new();
        private readonly List<DocumentProperty> _sectionFields = new();
        private readonly StringBuilder _valueBuilder = new();

        // The section being read. Its fields are collected until the next header or the end of the file.
        private string _sectionName;
        private int _sectionLine;
        private IReadOnlyList<string> _sectionComments;

        // Set while the lines under a rejected header are skipped: their errors are already reported by the header.
        private bool _isSkippingSection;
        private bool _hasContent;

        public CatalogDocumentParser(string text)
        {
            _text = text ?? string.Empty;
        }

        public CatalogDocument Parse()
        {
            DocumentLineReader reader = new(_text);
            while (reader.TryReadLine(out int lineNumber, out int start, out int length))
            {
                ReadLine(lineNumber, _text.AsSpan(start, length));
            }
            CloseSection();
            return new CatalogDocument(_headerComments.ToArray(), _attributes.ToArray(), _languages.ToArray(),
                _pendingComments.ToArray(), _issues.ToArray());
        }

        private void ReadLine(int lineNumber, ReadOnlySpan<char> line)
        {
            if (DocumentSyntax.IsMergeMarker(line))
            {
                AddIssue(IssueSeverity.Error, lineNumber, 1, "This is a merge conflict marker. Resolve the conflict and remove the marker lines.");
                return;
            }
            int start = DocumentSyntax.SkipBlanks(line, 0);
            if (start == line.Length)
            {
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
            else if (first == '[')
            {
                ReadSectionHeader(lineNumber, line, start);
            }
            else
            {
                ReadField(lineNumber, line, start);
            }
        }

        private void ReadAttribute(int lineNumber, ReadOnlySpan<char> line, int start)
        {
            _hasContent = true;
            if (_sectionName != null || _isSkippingSection || _languages.Count > 0)
            {
                AddIssue(IssueSeverity.Error, lineNumber, start + 1, "Catalog attributes go at the top of the file, before the first language section.");
                _pendingComments.Clear();
                return;
            }
            int nameStart = start + 1;
            int nameEnd = DocumentSyntax.SkipIdentifier(line, nameStart);
            if (nameEnd == nameStart || (nameEnd < line.Length && !DocumentSyntax.IsBlank(line[nameEnd])))
            {
                AddIssue(IssueSeverity.Error, lineNumber, start + 1, "An attribute is a name right after '@' and a value, such as '@source English'.");
                _pendingComments.Clear();
                return;
            }
            ReadOnlySpan<char> name = line.Slice(nameStart, nameEnd - nameStart);
            string attribute = DocumentNames.FindCatalogAttribute(name);
            if (attribute == null)
            {
                attribute = name.ToString();
                AddIssue(IssueSeverity.Warning, lineNumber, start + 1, $"'@{attribute}' is not a catalog attribute this version knows; it is kept but has no effect.");
            }
            for (int i = 0; i < _attributes.Count; i++)
            {
                if (string.Equals(_attributes[i].Name, attribute, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(IssueSeverity.Error, lineNumber, start + 1, $"'@{attribute}' is already set on line {_attributes[i].Line}.");
                    _pendingComments.Clear();
                    return;
                }
            }
            int valueStart = DocumentSyntax.SkipBlanks(line, nameEnd);
            string value = line.Slice(valueStart, DocumentSyntax.TrimEnd(line, valueStart) - valueStart).ToString();
            _attributes.Add(new DocumentProperty(attribute, value, lineNumber, TakePendingComments()));
        }

        private void ReadSectionHeader(int lineNumber, ReadOnlySpan<char> line, int start)
        {
            _hasContent = true;
            CloseSection();
            // A header is a language name in brackets, with nothing but blanks after the closing bracket.
            int closeOffset = line.Slice(start).IndexOf(']');
            if (closeOffset < 0 || DocumentSyntax.SkipBlanks(line, start + closeOffset + 1) != line.Length)
            {
                RejectSection(lineNumber, start + 1, "A language section starts with its name in brackets on a line of its own, such as '[English]'.");
                return;
            }
            // Blanks inside the brackets are forgiven: '[ English ]' names English, as the writer would put it.
            int nameStart = DocumentSyntax.SkipBlanks(line, start + 1);
            int nameEnd = start + closeOffset;
            while (nameEnd > nameStart && DocumentSyntax.IsBlank(line[nameEnd - 1]))
            {
                nameEnd--;
            }
            ReadOnlySpan<char> name = line.Slice(nameStart, nameEnd - nameStart);
            if (!NameRules.IsValid(name))
            {
                RejectSection(lineNumber, nameStart + 1, $"'{name.ToString()}' is not a valid language name: {NameRules.Description}.");
                return;
            }
            for (int i = 0; i < _languages.Count; i++)
            {
                if (name.Equals(_languages[i].Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    RejectSection(lineNumber, nameStart + 1, $"'{name.ToString()}' is already defined on line {_languages[i].Line}; this section is ignored.");
                    return;
                }
            }
            _sectionName = name.ToString();
            _sectionLine = lineNumber;
            _sectionComments = TakePendingComments();
            _isSkippingSection = false;
        }

        private void ReadField(int lineNumber, ReadOnlySpan<char> line, int start)
        {
            _hasContent = true;
            if (_isSkippingSection)
            {
                _pendingComments.Clear();
                return;
            }
            if (_sectionName == null)
            {
                AddIssue(IssueSeverity.Error, lineNumber, start + 1, "A field goes inside a language section, under a header such as '[English]'.");
                _pendingComments.Clear();
                return;
            }
            int nameEnd = DocumentSyntax.SkipIdentifier(line, start);
            int position = DocumentSyntax.SkipBlanks(line, nameEnd);
            if (nameEnd == start || position >= line.Length || line[position] != '=')
            {
                AddIssue(IssueSeverity.Error, lineNumber, start + 1, "A language field is written as 'Field = Value', such as 'DisplayName = English'.");
                _pendingComments.Clear();
                return;
            }
            ReadOnlySpan<char> name = line.Slice(start, nameEnd - start);
            string field = DocumentNames.FindLanguageField(name);
            if (field == null)
            {
                field = name.ToString();
                AddIssue(IssueSeverity.Warning, lineNumber, start + 1, $"'{field}' is not a language field this version knows; it is kept but has no effect.");
            }
            for (int i = 0; i < _sectionFields.Count; i++)
            {
                if (string.Equals(_sectionFields[i].Name, field, StringComparison.OrdinalIgnoreCase))
                {
                    AddIssue(IssueSeverity.Error, lineNumber, start + 1, $"'{field}' is already set on line {_sectionFields[i].Line}.");
                    _pendingComments.Clear();
                    return;
                }
            }
            int valueStart = DocumentSyntax.SkipBlanks(line, position + 1);
            int valueEnd = DocumentSyntax.TrimEnd(line, valueStart);
            string value = TextEscaping.Unescape(line.Slice(valueStart, valueEnd - valueStart), lineNumber, valueStart + 1, _issues, _valueBuilder);
            _sectionFields.Add(new DocumentProperty(field, value, lineNumber, TakePendingComments()));
        }

        /// <summary>Stores the section being read, if any, with the fields collected for it.</summary>
        private void CloseSection()
        {
            if (_sectionName == null)
            {
                return;
            }
            _languages.Add(new CatalogDocumentLanguage(_sectionName, _sectionFields.ToArray(), _sectionComments, _sectionLine));
            _sectionName = null;
            _sectionComments = null;
            _sectionFields.Clear();
        }

        /// <summary>Reports a header that can't be used and skips the fields under it until the next header.</summary>
        private void RejectSection(int line, int column, string message)
        {
            AddIssue(IssueSeverity.Error, line, column, message);
            _pendingComments.Clear();
            _isSkippingSection = true;
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

        private void AddIssue(IssueSeverity severity, int line, int column, string message)
        {
            _issues.Add(new DocumentIssue(severity, line, column, message));
        }
    }
}
