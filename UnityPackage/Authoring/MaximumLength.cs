using System.Globalization;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// The <c>@maximumLength</c> attribute of a source entry: how many characters its text may have in any language,
    /// as the space the UI gives it allows.
    /// </summary>
    public static class MaximumLength
    {
        /// <summary>Reads the attribute's value. False when it isn't a positive number of characters.</summary>
        public static bool TryParse(string value, out int limit)
        {
            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) && limit > 0;
        }

        /// <summary>Whether <paramref name="text"/> is longer than <paramref name="limit"/>. A message never is, as its length depends on its arguments.</summary>
        public static bool IsExceededBy(string text, int limit)
        {
            return text != null && text.IndexOf('{') < 0 && text.Length > limit;
        }
    }
}
