using System.Text;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// Writes plain text as a message that shows exactly that text, for entries created from text a project already
    /// shows, such as the current text of a UI label.
    /// </summary>
    /// <remarks>
    /// Braces go in quotes, together with any apostrophes and braces next to them, since an apostrophe right after
    /// a quote would otherwise merge into it. Other apostrophes stay as they are unless they come before an
    /// apostrophe or a brace, so ordinary text like <c>Don't</c> reads the same in the file.
    /// </remarks>
    internal static class MessageQuoting
    {
        /// <summary>Returns a message whose text is <paramref name="text"/>, character for character.</summary>
        public static string Quote(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOfAny(new[] { '{', '}', '\'' }) < 0)
            {
                return text ?? string.Empty;
            }
            StringBuilder builder = new(text.Length + 8);
            int index = 0;
            while (index < text.Length)
            {
                char character = text[index];
                if (character == '{' || character == '}')
                {
                    // One quoted run holds the braces and the apostrophes between them; inside it, an apostrophe is doubled.
                    builder.Append('\'');
                    while (index < text.Length && (text[index] == '{' || text[index] == '}' || text[index] == '\''))
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
                if (character == '\'' && index + 1 < text.Length && (text[index + 1] == '\'' || text[index + 1] == '{' || text[index + 1] == '}'))
                {
                    builder.Append('\'');
                }
                index++;
            }
            return builder.ToString();
        }
    }
}
