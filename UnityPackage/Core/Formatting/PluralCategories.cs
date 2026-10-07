using System;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// Names and sets of <see cref="PluralCategory"/> values. A set is a bit mask with one bit per category, which is
    /// how the generated CLDR data describes the forms each language uses.
    /// </summary>
    internal static class PluralCategories
    {
        /// <summary>The set holding only <see cref="PluralCategory.Other"/>, which every language uses.</summary>
        public const int OtherOnly = 1 << (int)PluralCategory.Other;

        private static readonly string[] Names = { "zero", "one", "two", "few", "many", "other" };

        /// <summary>Returns the keyword messages write for <paramref name="category"/>, such as <c>few</c>.</summary>
        public static string GetName(PluralCategory category) => Names[(int)category];

        /// <summary>Returns the category a message keyword names. Keywords are lowercase, as ICU requires.</summary>
        public static bool TryParse(ReadOnlySpan<char> keyword, out PluralCategory category)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (keyword.SequenceEqual(Names[i].AsSpan()))
                {
                    category = (PluralCategory)i;
                    return true;
                }
            }
            category = PluralCategory.Other;
            return false;
        }

        /// <summary>Returns whether the set <paramref name="categories"/> holds <paramref name="category"/>.</summary>
        public static bool Contains(int categories, PluralCategory category) => (categories & (1 << (int)category)) != 0;

        /// <summary>Returns the set with <paramref name="category"/> added.</summary>
        public static int Add(int categories, PluralCategory category) => categories | (1 << (int)category);

        /// <summary>Returns the keywords of a set in CLDR order, such as <c>one, few, many, other</c>.</summary>
        public static string Describe(int categories)
        {
            StringBuilder builder = new();
            for (int i = 0; i < Names.Length; i++)
            {
                if ((categories & (1 << i)) == 0)
                {
                    continue;
                }
                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(Names[i]);
            }
            return builder.ToString();
        }
    }
}
