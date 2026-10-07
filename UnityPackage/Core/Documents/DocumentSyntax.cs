using System;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// The small pieces of syntax every localization file shares. Blanks are spaces and tabs only: every other
    /// whitespace character, a non-breaking space included, is content, so it is never trimmed away unnoticed.
    /// </summary>
    internal static class DocumentSyntax
    {
        /// <summary>Length of the markers git writes around a merge conflict.</summary>
        private const int MergeMarkerLength = 7;

        public static bool IsBlank(char character) => character == ' ' || character == '\t';

        public static bool IsIdentifierCharacter(char character)
        {
            return (uint)((character | 0x20) - 'a') <= 'z' - 'a' ||
                   (uint)(character - '0') <= 9 ||
                   character == '_';
        }

        /// <summary>Returns the index of the first non-blank character at or after <paramref name="start"/>.</summary>
        public static int SkipBlanks(ReadOnlySpan<char> line, int start)
        {
            int position = start;
            while (position < line.Length && IsBlank(line[position]))
            {
                position++;
            }
            return position;
        }

        /// <summary>Returns the index right after the identifier characters starting at <paramref name="start"/>.</summary>
        public static int SkipIdentifier(ReadOnlySpan<char> line, int start)
        {
            int position = start;
            while (position < line.Length && IsIdentifierCharacter(line[position]))
            {
                position++;
            }
            return position;
        }

        /// <summary>Returns the index right after the run of characters starting at <paramref name="start"/> that ends at a blank or at <paramref name="stop"/>.</summary>
        public static int SkipToken(ReadOnlySpan<char> line, int start, char stop)
        {
            int position = start;
            while (position < line.Length && !IsBlank(line[position]) && line[position] != stop)
            {
                position++;
            }
            return position;
        }

        /// <summary>Returns the range of <paramref name="line"/> from <paramref name="start"/> with trailing blanks removed.</summary>
        public static int TrimEnd(ReadOnlySpan<char> line, int start)
        {
            int end = line.Length;
            while (end > start && IsBlank(line[end - 1]))
            {
                end--;
            }
            return end;
        }

        /// <summary>
        /// Returns whether the line is a marker git leaves around a merge conflict: seven <c>&lt;</c>, <c>=</c>,
        /// <c>&gt;</c> or <c>|</c> characters at its very start, followed by nothing or a blank.
        /// </summary>
        public static bool IsMergeMarker(ReadOnlySpan<char> line)
        {
            if (line.Length < MergeMarkerLength)
            {
                return false;
            }
            char marker = line[0];
            if (marker != '<' && marker != '=' && marker != '>' && marker != '|')
            {
                return false;
            }
            for (int i = 1; i < MergeMarkerLength; i++)
            {
                if (line[i] != marker)
                {
                    return false;
                }
            }
            return line.Length == MergeMarkerLength || IsBlank(line[MergeMarkerLength]);
        }

        /// <summary>
        /// Returns the text of a <c>#</c> comment whose <c>#</c> is at <paramref name="hashIndex"/>, without the
        /// <c>#</c>, the blank that usually follows it, or trailing blanks.
        /// </summary>
        public static string ReadComment(ReadOnlySpan<char> line, int hashIndex)
        {
            int start = hashIndex + 1;
            if (start < line.Length && IsBlank(line[start]))
            {
                start++;
            }
            int end = TrimEnd(line, start);
            return line.Slice(start, end - start).ToString();
        }

        /// <summary>Parses exactly six hexadecimal digits, in either case, into a fingerprint.</summary>
        public static bool TryParseFingerprint(ReadOnlySpan<char> digits, out uint fingerprint)
        {
            fingerprint = 0;
            if (digits.Length != 6)
            {
                return false;
            }
            for (int i = 0; i < digits.Length; i++)
            {
                int value = HexValue(digits[i]);
                if (value < 0)
                {
                    fingerprint = 0;
                    return false;
                }
                fingerprint = (fingerprint << 4) | (uint)value;
            }
            return true;
        }

        /// <summary>Returns the value of a hexadecimal digit in either case, or -1 for any other character.</summary>
        public static int HexValue(char character)
        {
            if ((uint)(character - '0') <= 9)
            {
                return character - '0';
            }
            int lowered = character | 0x20;
            if ((uint)(lowered - 'a') <= 'f' - 'a')
            {
                return lowered - 'a' + 10;
            }
            return -1;
        }
    }
}
