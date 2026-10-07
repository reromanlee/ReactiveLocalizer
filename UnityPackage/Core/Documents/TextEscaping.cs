using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// The escapes values use in localization files: <c>\n</c>, <c>\t</c>, <c>\\</c> and <c>\uXXXX</c>. An escape the
    /// file format doesn't know is reported and kept exactly as written, so no text is ever lost to a typo.
    /// </summary>
    internal static class TextEscaping
    {
        /// <summary>
        /// Returns <paramref name="value"/> with its escapes resolved, reporting malformed ones as warnings at their
        /// position. <paramref name="firstColumn"/> is the 1-based column of the value's first character.
        /// </summary>
        public static string Unescape(ReadOnlySpan<char> value, int line, int firstColumn, List<DocumentIssue> issues, StringBuilder builder)
        {
            // Most values contain no backslash at all; they need no builder.
            int firstBackslash = value.IndexOf('\\');
            if (firstBackslash < 0)
            {
                return value.ToString();
            }
            builder.Clear();
            builder.Append(value.Slice(0, firstBackslash));
            int position = firstBackslash;
            while (position < value.Length)
            {
                char character = value[position];
                if (character != '\\')
                {
                    builder.Append(character);
                    position++;
                    continue;
                }
                int column = firstColumn + position;
                // A backslash that ends the value escapes nothing, so it stays a backslash.
                if (position + 1 >= value.Length)
                {
                    issues.Add(new DocumentIssue(IssueSeverity.Warning, line, column,
                        "A value ends with a lone '\\', which is kept as written. Write '\\\\' for a backslash."));
                    builder.Append('\\');
                    position++;
                    continue;
                }
                char escape = value[position + 1];
                switch (escape)
                {
                    case 'n':
                        builder.Append('\n');
                        position += 2;
                        break;
                    case 't':
                        builder.Append('\t');
                        position += 2;
                        break;
                    case '\\':
                        builder.Append('\\');
                        position += 2;
                        break;
                    case 'u':
                        // A \u escape is exactly four hexadecimal digits; anything shorter stays as written.
                        if (TryReadUnicodeEscape(value, position + 2, out char unicode))
                        {
                            builder.Append(unicode);
                            position += 6;
                        }
                        else
                        {
                            issues.Add(new DocumentIssue(IssueSeverity.Warning, line, column,
                                "A '\\u' escape needs four hexadecimal digits, such as '\\u00A0'; this one is kept as written."));
                            builder.Append("\\u");
                            position += 2;
                        }
                        break;
                    default:
                        issues.Add(new DocumentIssue(IssueSeverity.Warning, line, column,
                            $"'\\{escape}' is not an escape, so it is kept as written. Write '\\\\' for a backslash."));
                        builder.Append('\\').Append(escape);
                        position += 2;
                        break;
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Returns where, in a value as written, the character at <paramref name="valueIndex"/> of its resolved text
        /// starts, mirroring <see cref="Unescape"/>, so problems found in the text are reported at the right column.
        /// </summary>
        public static int FindWrittenOffset(ReadOnlySpan<char> written, int valueIndex)
        {
            int position = 0;
            for (int index = 0; index < valueIndex && position < written.Length; index++)
            {
                if (written[position] != '\\' || position + 1 >= written.Length)
                {
                    position++;
                    continue;
                }
                char escape = written[position + 1];
                if (escape == 'n' || escape == 't' || escape == '\\')
                {
                    position += 2;
                }
                else if (escape == 'u' && TryReadUnicodeEscape(written, position + 2, out _))
                {
                    position += 6;
                }
                else
                {
                    // An unknown or malformed escape is kept as written: its backslash is a character of the text too.
                    position++;
                }
            }
            return position;
        }

        /// <summary>
        /// Appends <paramref name="value"/> to <paramref name="builder"/> in its canonical written form: line breaks,
        /// tabs and backslashes escaped, invisible characters as <c>\uXXXX</c>, and spaces at either end escaped so
        /// trimming never removes them.
        /// </summary>
        public static void Escape(ReadOnlySpan<char> value, StringBuilder builder)
        {
            // Spaces at either end would be trimmed when the file is read back, so those are the only ones escaped.
            int leadingSpaces = 0;
            while (leadingSpaces < value.Length && value[leadingSpaces] == ' ')
            {
                leadingSpaces++;
            }
            int trailingStart = value.Length;
            while (trailingStart > leadingSpaces && value[trailingStart - 1] == ' ')
            {
                trailingStart--;
            }
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (character == ' ' && (i < leadingSpaces || i >= trailingStart))
                {
                    builder.Append("\\u0020");
                }
                else if (character == '\\')
                {
                    builder.Append("\\\\");
                }
                else if (character == '\n')
                {
                    builder.Append("\\n");
                }
                else if (character == '\t')
                {
                    builder.Append("\\t");
                }
                else if (IsInvisible(character))
                {
                    AppendUnicodeEscape(character, builder);
                }
                else
                {
                    builder.Append(character);
                }
            }
        }

        /// <summary>
        /// Returns whether a character would be invisible or misleading in a reviewed diff: control characters,
        /// unusual spaces, zero-width characters, bidirectional marks and the byte order mark.
        /// </summary>
        public static bool IsInvisible(char character)
        {
            if (character < ' ' || (character >= '\u007F' && character <= '\u009F'))
            {
                return true;
            }
            switch (character)
            {
                case '\u00A0': // No-break space.
                case '\u00AD': // Soft hyphen.
                case '\u061C': // Arabic letter mark.
                case '\u180E': // Mongolian vowel separator.
                case '\u2028': // Line separator.
                case '\u2029': // Paragraph separator.
                case '\u202F': // Narrow no-break space.
                case '\u205F': // Medium mathematical space.
                case '\u3000': // Ideographic space.
                case '\uFEFF': // Byte order mark, or zero-width no-break space.
                    return true;
            }
            // En quad through hair space, zero-width space through right-to-left mark, the bidirectional embedding
            // controls, and the word joiner through the bidirectional isolates.
            return (character >= '\u2000' && character <= '\u200F') ||
                   (character >= '\u202A' && character <= '\u202E') ||
                   (character >= '\u2060' && character <= '\u206F');
        }

        private static bool TryReadUnicodeEscape(ReadOnlySpan<char> value, int start, out char character)
        {
            character = '\0';
            if (start + 4 > value.Length)
            {
                return false;
            }
            int code = 0;
            for (int i = start; i < start + 4; i++)
            {
                int digit = DocumentSyntax.HexValue(value[i]);
                if (digit < 0)
                {
                    return false;
                }
                code = (code << 4) | digit;
            }
            character = (char)code;
            return true;
        }

        private static void AppendUnicodeEscape(char character, StringBuilder builder)
        {
            const string HexDigits = "0123456789ABCDEF";
            builder.Append("\\u")
                .Append(HexDigits[(character >> 12) & 0xF])
                .Append(HexDigits[(character >> 8) & 0xF])
                .Append(HexDigits[(character >> 4) & 0xF])
                .Append(HexDigits[character & 0xF]);
        }
    }
}
