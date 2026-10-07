using System.Text;

namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// Brings language tags into the form CLDR names its locales, so <c>pt_br</c> finds <c>pt-BR</c>, and shortens
    /// them one subtag at a time, which is how a lookup falls back from <c>sr-Latn-BA</c> to <c>sr-Latn</c> to <c>sr</c>.
    /// </summary>
    /// <remarks>Only ASCII is changed, so the result never depends on the culture of the device.</remarks>
    internal static class LanguageTags
    {
        /// <summary>
        /// Returns <paramref name="tag"/> with hyphens between subtags, the language in lowercase, a four-letter
        /// script in title case and a region in uppercase. Empty for a null or blank tag.
        /// </summary>
        public static string Normalize(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return string.Empty;
            }
            string[] parts = tag.Trim().Split('-', '_');
            StringBuilder builder = new(tag.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append('-');
                }
                bool isScript = i > 0 && part.Length == 4 && IsLetters(part);
                bool isRegion = i > 0 && ((part.Length == 2 && IsLetters(part)) || (part.Length == 3 && IsDigits(part)));
                for (int c = 0; c < part.Length; c++)
                {
                    bool isUpper = isRegion || (isScript && c == 0);
                    builder.Append(isUpper ? ToUpper(part[c]) : ToLower(part[c]));
                }
            }
            return builder.ToString();
        }

        /// <summary>Returns <paramref name="tag"/> without its last subtag, or null when it has only one.</summary>
        public static string Truncate(string tag)
        {
            int index = tag.LastIndexOf('-');
            return index <= 0 ? null : tag.Substring(0, index);
        }

        private static bool IsLetters(string part)
        {
            for (int i = 0; i < part.Length; i++)
            {
                if ((uint)((part[i] | 0x20) - 'a') > 'z' - 'a')
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsDigits(string part)
        {
            for (int i = 0; i < part.Length; i++)
            {
                if ((uint)(part[i] - '0') > 9)
                {
                    return false;
                }
            }
            return true;
        }

        private static char ToUpper(char character) => (uint)(character - 'a') <= 'z' - 'a' ? (char)(character & ~0x20) : character;

        private static char ToLower(char character) => (uint)(character - 'A') <= 'Z' - 'A' ? (char)(character | 0x20) : character;
    }
}
