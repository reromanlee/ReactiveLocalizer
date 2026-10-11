using reromanlee.ReactiveLocalizer.Authoring;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Samples.UnityLocalizationMigration.Editor
{
    /// <summary>
    /// Converts Unity Localization strings to ICU messages that show the same text with the same arguments: plain
    /// placeholders such as <c>{0}</c> and <c>{name}</c> carry over, and anything Smart Format writes differently from
    /// ICU, such as its plural and choose formatters, is left for a person to convert.
    /// </summary>
    /// <remarks>
    /// A numbered placeholder <c>{0}</c> becomes <c>{arg0}</c>, since ICU arguments have names, which generated code
    /// turns into parameters. Plain strings take it from <c>string.Format</c>, which projects often apply to them; Smart
    /// strings also take named placeholders, <c>{{</c> and <c>}}</c> as braces, and character escapes such as <c>\n</c>.
    /// </remarks>
    public static class SmartFormatConversion
    {
        /// <summary>
        /// Converts <paramref name="text"/>. Returns false when it needs converting by hand; <paramref name="message"/>
        /// then shows the text as it is written, braces included, so nothing is lost meanwhile.
        /// </summary>
        /// <param name="text">The string as Unity Localization stores it.</param>
        /// <param name="isSmart">Whether the entry is a Smart string.</param>
        /// <param name="message">The ICU message.</param>
        /// <param name="hasArguments">Whether the message has any argument.</param>
        public static bool TryConvert(string text, bool isSmart, out string message, out bool hasArguments)
        {
            text ??= string.Empty;
            StringBuilder output = new(text.Length + 8);
            StringBuilder literal = new();
            hasArguments = false;
            int index = 0;
            while (index < text.Length)
            {
                char character = text[index];
                char next = index + 1 < text.Length ? text[index + 1] : '\0';
                if (isSmart && (character == '{' || character == '}') && next == character)
                {
                    literal.Append(character);
                    index += 2;
                    continue;
                }
                if (isSmart && character == '\\' && TryUnescape(next, out char unescaped))
                {
                    literal.Append(unescaped);
                    index += 2;
                    continue;
                }
                if (character == '{')
                {
                    int close = text.IndexOf('}', index + 1);
                    string name = close > index ? ToArgument(text.Substring(index + 1, close - index - 1), isSmart) : null;
                    if (name != null)
                    {
                        output.Append(MessageText.Quote(literal.ToString(), true)).Append('{').Append(name).Append('}');
                        literal.Clear();
                        hasArguments = true;
                        index = close + 1;
                        continue;
                    }
                    // Any other placeholder is Smart Format syntax ICU writes another way, or a format string's.
                    if (isSmart || (close > index && IsFormatPlaceholder(text.Substring(index + 1, close - index - 1))))
                    {
                        return Keep(text, out message, out hasArguments);
                    }
                }
                literal.Append(character);
                index++;
            }
            output.Append(MessageText.Quote(literal.ToString()));
            message = output.ToString();
            return MessageText.IsValid(message, out _) || Keep(text, out message, out hasArguments);
        }

        /// <summary>Returns the ICU argument a simple placeholder becomes, or null when it isn't a simple one.</summary>
        private static string ToArgument(string placeholder, bool isSmart)
        {
            if (placeholder.Length > 0 && placeholder.Length <= 3 && IsDigits(placeholder))
            {
                return "arg" + placeholder;
            }
            return isSmart && NameRules.IsValid(placeholder) ? placeholder : null;
        }

        /// <summary>Whether a placeholder of a plain string is one <c>string.Format</c> reads, such as <c>{0:N2}</c> or <c>{1,8}</c>.</summary>
        private static bool IsFormatPlaceholder(string placeholder)
        {
            int end = 0;
            while (end < placeholder.Length && char.IsDigit(placeholder[end]))
            {
                end++;
            }
            return end > 0 && end < placeholder.Length && (placeholder[end] == ':' || placeholder[end] == ',');
        }

        private static bool Keep(string text, out string message, out bool hasArguments)
        {
            message = MessageText.Quote(text);
            hasArguments = false;
            return false;
        }

        private static bool TryUnescape(char escaped, out char character)
        {
            switch (escaped)
            {
                case 'n':
                    character = '\n';
                    return true;
                case 't':
                    character = '\t';
                    return true;
                case 'r':
                    character = '\r';
                    return true;
                case '\\':
                case '\'':
                case '"':
                    character = escaped;
                    return true;
                default:
                    character = '\0';
                    return false;
            }
        }

        private static bool IsDigits(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsDigit(text[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
