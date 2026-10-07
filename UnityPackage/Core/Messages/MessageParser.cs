using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Formatting;
using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// Reads ICU MessageFormat text, the classic syntax translation tools understand, and reports every problem at its
    /// position instead of throwing.
    /// </summary>
    /// <remarks>
    /// Quoting follows ICU: an apostrophe starts quoted text only right before a brace, or before <c>#</c> inside a
    /// plural form, so <c>Don't</c> needs nothing and <c>'{'</c> shows a brace; two apostrophes always show one.
    /// Arguments are named, since their names become the parameters of generated code; ICU's numbered arguments and
    /// its deprecated <c>choice</c> are rejected with an explanation.
    /// </remarks>
    internal sealed class MessageParser
    {
        /// <summary>How deeply plural and select messages may nest; real messages use two or three levels.</summary>
        public const int MaximumDepth = 12;

        /// <summary>How many different arguments one message may use.</summary>
        public const int MaximumArguments = 32;

        // Explicit values and offsets beyond this can't be compared exactly.
        private const double MaximumNumber = 1e15;

        private readonly string _text;
        private readonly List<MessageIssue> _issues = new();
        private readonly List<MessageArgumentInfo> _arguments = new();
        private readonly StringBuilder _literal = new();
        private int _index;

        private MessageParser(string text)
        {
            _text = text ?? string.Empty;
        }

        private enum Parent
        {
            Top,
            Plural,
            Select
        }

        /// <summary>Parses <paramref name="text"/>. Never throws; problems are in <see cref="ParsedMessage.Issues"/>.</summary>
        public static ParsedMessage Parse(string text)
        {
            MessageParser parser = new(text);
            MessageBody body = parser.ReadBody(0, Parent.Top, false);
            parser._arguments.Sort((left, right) =>
            {
                int comparison = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                return comparison != 0 ? comparison : string.CompareOrdinal(left.Name, right.Name);
            });
            return new ParsedMessage(body, parser._arguments.ToArray(), parser._issues.ToArray());
        }

        /// <summary>
        /// Reads text and arguments until the end of the message or, inside a form, until the brace that closes it,
        /// which is left for the caller.
        /// </summary>
        private MessageBody ReadBody(int depth, Parent parent, bool isInsidePlural)
        {
            List<MessagePart> parts = new();
            _literal.Clear();
            int textStart = _index;
            while (_index < _text.Length)
            {
                char character = _text[_index];
                if (character == '\'')
                {
                    ReadApostrophe(parent);
                }
                else if (character == '{')
                {
                    AddText(parts, textStart);
                    MessagePart argument = ReadArgument(depth, isInsidePlural || parent == Parent.Plural);
                    if (argument != null)
                    {
                        parts.Add(argument);
                    }
                    _literal.Clear();
                    textStart = _index;
                }
                else if (character == '}')
                {
                    if (depth > 0)
                    {
                        break;
                    }
                    AddIssue(IssueSeverity.Warning, _index, "This '}' closes nothing, so it shows as text. Write '}' in quotes ('}') if it's meant as text.");
                    _literal.Append(character);
                    _index++;
                }
                else if (character == '#' && parent == Parent.Plural)
                {
                    AddText(parts, textStart);
                    if (_index > 0 && _text[_index - 1] == '=')
                    {
                        AddIssue(IssueSeverity.Warning, _index, "'=#' inside a plural form shows the number after the '='. For a color such as <color=#FF0000>, write the '#' in quotes: '#'.");
                    }
                    parts.Add(new PoundPart(_index));
                    _index++;
                    _literal.Clear();
                    textStart = _index;
                }
                else
                {
                    if (character == '#' && isInsidePlural)
                    {
                        AddIssue(IssueSeverity.Warning, _index, "This '#' shows as text: only text directly inside a plural form shows the number, not text inside a select within it.");
                    }
                    _literal.Append(character);
                    _index++;
                }
            }
            AddText(parts, textStart);
            return new MessageBody(parts.ToArray());
        }

        /// <summary>Reads an apostrophe: a doubled one, quoted text, or a plain apostrophe as in <c>Don't</c>.</summary>
        private void ReadApostrophe(Parent parent)
        {
            int start = _index;
            char next = _index + 1 < _text.Length ? _text[_index + 1] : '\0';
            if (next == '\'')
            {
                _literal.Append('\'');
                _index += 2;
                return;
            }
            if (next != '{' && next != '}' && !(next == '#' && parent == Parent.Plural))
            {
                _literal.Append('\'');
                _index++;
                return;
            }
            // Quoted text: the character after the apostrophe is always literal, and the text runs to the next single apostrophe.
            _literal.Append(next);
            _index += 2;
            while (true)
            {
                if (_index >= _text.Length)
                {
                    AddIssue(IssueSeverity.Warning, start, "This apostrophe starts quoted text that never ends, so everything after it shows as written. End it with another apostrophe.");
                    return;
                }
                char character = _text[_index];
                if (character == '\'')
                {
                    if (_index + 1 < _text.Length && _text[_index + 1] == '\'')
                    {
                        _literal.Append('\'');
                        _index += 2;
                        continue;
                    }
                    _index++;
                    return;
                }
                _literal.Append(character);
                _index++;
            }
        }

        /// <summary>Reads an argument from its opening brace through its closing one. Returns null after an error.</summary>
        private MessagePart ReadArgument(int depth, bool isInsidePlural)
        {
            int start = _index;
            _index++;
            SkipWhiteSpace();
            int nameStart = _index;
            if (_index < _text.Length && IsDigit(_text[_index]))
            {
                return Fail(nameStart, "Arguments have names, such as {count}, which generated code turns into parameters; numbered arguments like {0} aren't supported.");
            }
            string name = ReadIdentifier();
            if (name.Length == 0)
            {
                return Fail(start, "An argument needs a name right after its brace, such as {count}.");
            }
            if (!NameRules.IsValid(name))
            {
                return Fail(nameStart, $"'{name}' is not a valid argument name: {NameRules.Description}.");
            }
            SkipWhiteSpace();
            if (_index >= _text.Length)
            {
                return Fail(start, $"The argument {{{name}}} is never closed with '}}'.");
            }
            if (_text[_index] == '}')
            {
                _index++;
                return RecordUse(name, nameStart, ArgumentUses.Plain)
                    ? new PlaceholderPart(start, name, PlaceholderFormat.Plain, NumberStyle.Default, null, null)
                    : null;
            }
            if (_text[_index] != ',')
            {
                return Fail(_index, $"After the argument name '{name}' comes '}}', or ',' and a type such as number, plural or select.");
            }
            _index++;
            SkipWhiteSpace();
            int typeStart = _index;
            string type = ReadIdentifier();
            if (type.Length == 0)
            {
                return Fail(typeStart, $"After '{{{name},' comes a type such as number, plural, select or a registered one like date.");
            }
            SkipWhiteSpace();
            if (_index >= _text.Length)
            {
                return Fail(start, $"The argument {{{name}, {type}}} is never closed with '}}'.");
            }

            if (IsType(type, "plural") || IsType(type, "selectordinal"))
            {
                bool isOrdinal = IsType(type, "selectordinal");
                if (!ExpectComma(start, name, type) || !RecordUse(name, nameStart, isOrdinal ? ArgumentUses.Ordinal : ArgumentUses.Plural))
                {
                    return null;
                }
                return ReadPlural(start, name, isOrdinal, depth);
            }
            if (IsType(type, "select"))
            {
                if (!ExpectComma(start, name, type) || !RecordUse(name, nameStart, ArgumentUses.Select))
                {
                    return null;
                }
                return ReadSelect(start, name, depth, isInsidePlural);
            }
            if (IsType(type, "choice"))
            {
                return Fail(typeStart, "ICU deprecated choice; use plural for counts, or select for keywords.");
            }

            string style = string.Empty;
            int styleStart = _index;
            if (_text[_index] == ',')
            {
                _index++;
                SkipWhiteSpace();
                styleStart = _index;
                if (!TryReadStyle(out style))
                {
                    return Fail(start, $"The argument {{{name}, {type}, ...}} is never closed with '}}'.");
                }
            }
            else if (_text[_index] == '}')
            {
                _index++;
            }
            else
            {
                return Fail(_index, $"After '{{{name}, {type}' comes '}}', or ',' and a style.");
            }

            if (IsType(type, "number"))
            {
                NumberStyle numberStyle = NumberStyle.Default;
                if (style.Length > 0)
                {
                    if (string.Equals(style, "integer", StringComparison.OrdinalIgnoreCase))
                    {
                        numberStyle = NumberStyle.Integer;
                    }
                    else if (string.Equals(style, "percent", StringComparison.OrdinalIgnoreCase))
                    {
                        numberStyle = NumberStyle.Percent;
                    }
                    else
                    {
                        return Fail(styleStart, $"'{style}' is not a number style here: they are integer and percent. Register a formatter for anything else, such as {{{name}, currency}}.");
                    }
                }
                return RecordUse(name, nameStart, ArgumentUses.Number)
                    ? new PlaceholderPart(start, name, PlaceholderFormat.Number, numberStyle, null, null)
                    : null;
            }
            if (!NameRules.IsValid(type))
            {
                return Fail(typeStart, $"'{type}' is not a valid type name: {NameRules.Description}.");
            }
            return RecordUse(name, nameStart, ArgumentUses.Formatter)
                ? new PlaceholderPart(start, name, PlaceholderFormat.Custom, NumberStyle.Default, type, style)
                : null;
        }

        private MessagePart ReadPlural(int start, string name, bool isOrdinal, int depth)
        {
            string type = isOrdinal ? "selectordinal" : "plural";
            if (depth + 1 > MaximumDepth)
            {
                return Fail(start, $"Plural and select messages nest more than {MaximumDepth} levels deep here.");
            }
            List<PluralCase> cases = new();
            double offset = 0d;
            bool hasOffset = false;
            bool hasOther = false;
            while (true)
            {
                SkipWhiteSpace();
                if (_index >= _text.Length)
                {
                    return Fail(start, $"The {type} of {{{name}}} is never closed with '}}'.");
                }
                if (_text[_index] == '}')
                {
                    _index++;
                    break;
                }
                int selectorStart = _index;
                bool isExplicit = false;
                double explicitValue = 0d;
                string keyword;
                if (_text[_index] == '=')
                {
                    _index++;
                    int valueStart = _index;
                    if (!TryReadNumber(out explicitValue))
                    {
                        return Fail(valueStart, "An exact form is '=' followed by a number, such as =0 or =1.");
                    }
                    isExplicit = true;
                    keyword = _text.Substring(selectorStart, _index - selectorStart);
                }
                else
                {
                    keyword = ReadIdentifier();
                    if (keyword.Length == 0)
                    {
                        return Fail(selectorStart, $"The {type} of {{{name}}} expects a form such as one, few or other, or an exact one such as =0.");
                    }
                    if (keyword == "offset" && _index < _text.Length && _text[_index] == ':')
                    {
                        _index++;
                        SkipWhiteSpace();
                        if (hasOffset || cases.Count > 0)
                        {
                            return Fail(selectorStart, "offset: goes once, before the first form.");
                        }
                        if (!TryReadNumber(out offset))
                        {
                            return Fail(_index, "offset: is followed by a number, such as offset:1.");
                        }
                        hasOffset = true;
                        continue;
                    }
                }
                SkipWhiteSpace();
                if (_index >= _text.Length || _text[_index] != '{')
                {
                    return Fail(selectorStart, $"The form '{keyword}' needs its text in braces, such as {keyword} {{# items}}.");
                }
                _index++;
                MessageBody body = ReadBody(depth + 1, Parent.Plural, true);
                if (_index >= _text.Length)
                {
                    return Fail(selectorStart, $"The text of the form '{keyword}' is never closed with '}}'.");
                }
                _index++;

                PluralCategory category = PluralCategory.Other;
                bool isCategory = !isExplicit && PluralCategories.TryParse(keyword.AsSpan(), out category);
                if (!isExplicit && !isCategory)
                {
                    AddIssue(IssueSeverity.Warning, selectorStart, $"'{keyword}' is not a plural form, so it's never chosen. The forms are zero, one, two, few, many and other, in lowercase.");
                }
                for (int i = 0; i < cases.Count; i++)
                {
                    bool isSame = isExplicit ? cases[i].IsExplicit && cases[i].ExplicitValue.Equals(explicitValue) : !cases[i].IsExplicit && cases[i].Keyword == keyword;
                    if (isSame)
                    {
                        AddIssue(IssueSeverity.Error, selectorStart, $"The form '{keyword}' appears twice in the {type} of {{{name}}}.");
                    }
                }
                hasOther |= isCategory && category == PluralCategory.Other;
                cases.Add(new PluralCase(selectorStart, isExplicit, explicitValue, keyword, isCategory, category, body));
            }
            if (!hasOther)
            {
                AddIssue(IssueSeverity.Error, start, $"The {type} of {{{name}}} needs an 'other' form, which every language uses.");
            }
            return new PluralPart(start, name, isOrdinal, offset, cases.ToArray());
        }

        private MessagePart ReadSelect(int start, string name, int depth, bool isInsidePlural)
        {
            if (depth + 1 > MaximumDepth)
            {
                return Fail(start, $"Plural and select messages nest more than {MaximumDepth} levels deep here.");
            }
            List<SelectCase> cases = new();
            bool hasOther = false;
            while (true)
            {
                SkipWhiteSpace();
                if (_index >= _text.Length)
                {
                    return Fail(start, $"The select of {{{name}}} is never closed with '}}'.");
                }
                if (_text[_index] == '}')
                {
                    _index++;
                    break;
                }
                int keywordStart = _index;
                string keyword = ReadKeyword();
                if (keyword.Length == 0)
                {
                    return Fail(keywordStart, $"The select of {{{name}}} expects a keyword such as female or other.");
                }
                SkipWhiteSpace();
                if (_index >= _text.Length || _text[_index] != '{')
                {
                    return Fail(keywordStart, $"The keyword '{keyword}' needs its text in braces, such as {keyword} {{...}}.");
                }
                _index++;
                MessageBody body = ReadBody(depth + 1, Parent.Select, isInsidePlural);
                if (_index >= _text.Length)
                {
                    return Fail(keywordStart, $"The text of the keyword '{keyword}' is never closed with '}}'.");
                }
                _index++;
                for (int i = 0; i < cases.Count; i++)
                {
                    if (cases[i].Keyword == keyword)
                    {
                        AddIssue(IssueSeverity.Error, keywordStart, $"The keyword '{keyword}' appears twice in the select of {{{name}}}.");
                    }
                }
                hasOther |= keyword == "other";
                cases.Add(new SelectCase(keywordStart, keyword, body));
            }
            if (!hasOther)
            {
                AddIssue(IssueSeverity.Error, start, $"The select of {{{name}}} needs an 'other' keyword, for every value the others don't name.");
            }
            return new SelectPart(start, name, cases.ToArray());
        }

        /// <summary>Records one use of an argument, rejecting uses that need different types.</summary>
        private bool RecordUse(string name, int position, ArgumentUses use)
        {
            ulong hash = Hashing.ComputeNameHash(name);
            MessageArgumentInfo argument = null;
            for (int i = 0; i < _arguments.Count; i++)
            {
                if (_arguments[i].Hash == hash)
                {
                    argument = _arguments[i];
                    break;
                }
            }
            if (argument == null)
            {
                if (_arguments.Count == MaximumArguments)
                {
                    AddIssue(IssueSeverity.Error, position, $"A message can use at most {MaximumArguments} different arguments.");
                    return false;
                }
                argument = new MessageArgumentInfo(name, hash, position);
                _arguments.Add(argument);
            }
            ArgumentUses uses = argument.Uses | use;
            int typedUses = ((uses & ArgumentUses.Numeric) != 0 ? 1 : 0) + ((uses & ArgumentUses.Select) != 0 ? 1 : 0) + ((uses & ArgumentUses.Formatter) != 0 ? 1 : 0);
            if (typedUses > 1)
            {
                AddIssue(IssueSeverity.Error, position, $"'{name}' is used as {Describe(use)} here, but as {Describe(argument.Uses & ~ArgumentUses.Plain)} elsewhere in the message; one argument has one type.");
                return false;
            }
            argument.Uses = uses;
            return true;
        }

        private static string Describe(ArgumentUses uses)
        {
            if ((uses & ArgumentUses.Numeric) != 0)
            {
                return "a number";
            }
            return (uses & ArgumentUses.Select) != 0 ? "a select keyword" : "a formatted value";
        }

        private bool ExpectComma(int start, string name, string type)
        {
            if (_index < _text.Length && _text[_index] == ',')
            {
                _index++;
                return true;
            }
            Fail(start, $"A {type} is written {{{name}, {type}, ...}}, with its forms after a second comma.");
            return false;
        }

        /// <summary>Reads a style up to the brace that closes the argument, keeping nested braces and quotes as written.</summary>
        private bool TryReadStyle(out string style)
        {
            int start = _index;
            int nesting = 0;
            bool isQuoted = false;
            while (_index < _text.Length)
            {
                char character = _text[_index];
                if (character == '\'')
                {
                    isQuoted = !isQuoted;
                }
                else if (!isQuoted && character == '{')
                {
                    nesting++;
                }
                else if (!isQuoted && character == '}')
                {
                    if (nesting == 0)
                    {
                        style = _text.Substring(start, _index - start).Trim();
                        _index++;
                        return true;
                    }
                    nesting--;
                }
                _index++;
            }
            style = string.Empty;
            return false;
        }

        /// <summary>Reads a number such as 1, -2 or 1.5, without exponents or grouping.</summary>
        private bool TryReadNumber(out double value)
        {
            value = 0d;
            int start = _index;
            bool isNegative = false;
            if (_index < _text.Length && (_text[_index] == '-' || _text[_index] == '+'))
            {
                isNegative = _text[_index] == '-';
                _index++;
            }
            int digitsStart = _index;
            double integer = 0d;
            while (_index < _text.Length && IsDigit(_text[_index]))
            {
                integer = integer * 10d + (_text[_index] - '0');
                _index++;
            }
            if (_index == digitsStart)
            {
                _index = start;
                return false;
            }
            double fraction = 0d;
            if (_index + 1 < _text.Length && _text[_index] == '.' && IsDigit(_text[_index + 1]))
            {
                _index++;
                double scale = 0.1d;
                while (_index < _text.Length && IsDigit(_text[_index]))
                {
                    fraction += (_text[_index] - '0') * scale;
                    scale /= 10d;
                    _index++;
                }
            }
            value = isNegative ? -(integer + fraction) : integer + fraction;
            if (Math.Abs(value) > MaximumNumber)
            {
                _index = start;
                return false;
            }
            return true;
        }

        /// <summary>Reads ASCII letters, digits and underscores: argument names, types and plural keywords.</summary>
        private string ReadIdentifier()
        {
            int start = _index;
            while (_index < _text.Length && (IsAsciiLetter(_text[_index]) || IsDigit(_text[_index]) || _text[_index] == '_'))
            {
                _index++;
            }
            return _text.Substring(start, _index - start);
        }

        /// <summary>Reads a select keyword, which may also use letters of any script, as ICU allows.</summary>
        private string ReadKeyword()
        {
            int start = _index;
            while (_index < _text.Length && (char.IsLetterOrDigit(_text[_index]) || _text[_index] == '_'))
            {
                _index++;
            }
            return _text.Substring(start, _index - start);
        }

        private void SkipWhiteSpace()
        {
            while (_index < _text.Length && IsPatternWhiteSpace(_text[_index]))
            {
                _index++;
            }
        }

        /// <summary>Stores the text read since <paramref name="textStart"/> as a part, joining it to a text part right before it.</summary>
        private void AddText(List<MessagePart> parts, int textStart)
        {
            if (_literal.Length == 0)
            {
                return;
            }
            string text = _literal.ToString();
            _literal.Clear();
            if (parts.Count > 0 && parts[parts.Count - 1] is TextPart previous)
            {
                parts[parts.Count - 1] = new TextPart(previous.Position, previous.Text + text);
                return;
            }
            parts.Add(new TextPart(textStart, text));
        }

        /// <summary>Reports an error, then skips to the end of the argument it occurred in, so parsing can go on.</summary>
        private MessagePart Fail(int index, string message)
        {
            AddIssue(IssueSeverity.Error, index, message);
            int nesting = 0;
            while (_index < _text.Length)
            {
                char character = _text[_index];
                _index++;
                if (character == '{')
                {
                    nesting++;
                }
                else if (character == '}')
                {
                    if (nesting == 0)
                    {
                        break;
                    }
                    nesting--;
                }
            }
            return null;
        }

        private void AddIssue(IssueSeverity severity, int index, string message)
        {
            _issues.Add(new MessageIssue(severity, index, message));
        }

        private static bool IsType(string type, string expected) => string.Equals(type, expected, StringComparison.OrdinalIgnoreCase);

        private static bool IsDigit(char character) => (uint)(character - '0') <= 9;

        private static bool IsAsciiLetter(char character) => (uint)((character | 0x20) - 'a') <= 'z' - 'a';

        /// <summary>Unicode's Pattern_White_Space, which ICU skips between the tokens of an argument.</summary>
        private static bool IsPatternWhiteSpace(char character)
        {
            return character == ' ' || (character >= (char)0x09 && character <= (char)0x0D) || character == (char)0x85 ||
                   character == (char)0x200E || character == (char)0x200F || character == (char)0x2028 || character == (char)0x2029;
        }
    }
}
