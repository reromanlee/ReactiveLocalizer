using System;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Finds rich text tags such as <c>&lt;b&gt;</c>, <c>&lt;/color&gt;</c>, <c>&lt;#FF0000&gt;</c> and
    /// <c>&lt;sprite name="coin"&gt;</c>, which TextMeshPro, UI Toolkit and uGUI draw as formatting, not as characters.
    /// </summary>
    internal static class RichTextTags
    {
        /// <summary>
        /// Returns the length of the tag starting at <paramref name="index"/>: a <c>&lt;</c> followed by a letter,
        /// <c>/</c> or <c>#</c>, through the next <c>&gt;</c>. Zero when no tag starts there, as when another
        /// <c>&lt;</c> or a line break comes first.
        /// </summary>
        /// <param name="text">The text to look in.</param>
        /// <param name="index">Where the tag would start.</param>
        /// <param name="isUnclosed">
        /// Set when the text ends before the tag closes, which happens when an argument goes inside a tag, as in
        /// <c>&lt;color={color}&gt;</c>; the length then reaches the end of the text.
        /// </param>
        public static int MeasureTag(ReadOnlySpan<char> text, int index, out bool isUnclosed)
        {
            isUnclosed = false;
            if (index + 1 >= text.Length || text[index] != '<' || !IsTagStart(text[index + 1]))
            {
                return 0;
            }
            for (int i = index + 2; i < text.Length; i++)
            {
                char character = text[i];
                if (character == '>')
                {
                    return i + 1 - index;
                }
                if (character == '<' || character == '\n' || character == '\r')
                {
                    return 0;
                }
            }
            isUnclosed = true;
            return text.Length - index;
        }

        private static bool IsTagStart(char character) => (uint)((character | 0x20) - 'a') <= 'z' - 'a' || character == '/' || character == '#';
    }
}
