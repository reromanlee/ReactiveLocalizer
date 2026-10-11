using System.Text;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// Writes plain text as a message that shows exactly that text, for entries created from text a project already
    /// shows, such as the current text of a UI label, and for messages written back from their parts.
    /// </summary>
    /// <remarks>
    /// Braces go in quotes, together with any apostrophes and braces next to them, since an apostrophe right after
    /// a quote would otherwise merge into it. Other apostrophes stay as they are unless they come before an
    /// apostrophe or a brace, so ordinary text like <c>Don't</c> reads the same in the file.
    /// </remarks>
    internal static class MessageQuoting
    {
        private static readonly char[] Syntax = { '{', '}', '\'' };
        private static readonly char[] PluralFormSyntax = { '{', '}', '\'', '#' };

        /// <summary>Returns a message whose text is <paramref name="text"/>, character for character.</summary>
        public static string Quote(string text) => Quote(text, false, false);

        /// <summary>Returns message text that shows <paramref name="text"/> where a message writes it.</summary>
        /// <param name="text">The text to show.</param>
        /// <param name="isPluralForm">Whether the text goes directly inside a plural form, where <c>#</c> shows the number, so a <c>#</c> meant as text goes in quotes too.</param>
        /// <param name="isFollowedBySyntax">Whether syntax such as a brace comes right after the text, which would join a final apostrophe.</param>
        public static string Quote(string text, bool isPluralForm, bool isFollowedBySyntax)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOfAny(isPluralForm ? PluralFormSyntax : Syntax) < 0)
            {
                return text ?? string.Empty;
            }
            StringBuilder builder = new(text.Length + 8);
            int index = 0;
            while (index < text.Length)
            {
                char character = text[index];
                if (IsQuoted(character, isPluralForm))
                {
                    // One quoted run holds the braces and the apostrophes between them; inside it, an apostrophe is doubled.
                    builder.Append('\'');
                    while (index < text.Length && (IsQuoted(text[index], isPluralForm) || text[index] == '\''))
                    {
                        builder.Append(text[index]);
                        if (text[index] == '\'')
                        {
                            builder.Append('\'');
                        }
                        index++;
                    }
                    builder.Append('\'');
                    continue;
                }
                builder.Append(character);
                bool isBeforeSyntax = index + 1 < text.Length
                    ? text[index + 1] == '\'' || IsQuoted(text[index + 1], isPluralForm)
                    : isFollowedBySyntax;
                if (character == '\'' && isBeforeSyntax)
                {
                    builder.Append('\'');
                }
                index++;
            }
            return builder.ToString();
        }

        private static bool IsQuoted(char character, bool isPluralForm) => character == '{' || character == '}' || (isPluralForm && character == '#');
    }
}
