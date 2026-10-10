using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// How far apart two names are, counting the edits a typo makes: a character added, removed, replaced, or two
    /// neighbors swapped. Suggestions for misspelled keys use it.
    /// </summary>
    internal static class NameDistance
    {
        /// <summary>
        /// Returns the edits between two names, ignoring ASCII case, or a number above <paramref name="limit"/> as soon as
        /// it is clear the distance exceeds it.
        /// </summary>
        public static int Compute(string left, string right, int limit)
        {
            if (Math.Abs(left.Length - right.Length) > limit)
            {
                return limit + 1;
            }
            // Three rows of the optimal string alignment table: two back for swaps, one back, and the current one.
            int[] twoBack = new int[right.Length + 1];
            int[] previous = new int[right.Length + 1];
            int[] current = new int[right.Length + 1];
            for (int j = 0; j <= right.Length; j++)
            {
                previous[j] = j;
            }
            for (int i = 1; i <= left.Length; i++)
            {
                current[0] = i;
                int rowMinimum = i;
                for (int j = 1; j <= right.Length; j++)
                {
                    int cost = ToLower(left[i - 1]) == ToLower(right[j - 1]) ? 0 : 1;
                    int value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                    if (i > 1 && j > 1 && ToLower(left[i - 1]) == ToLower(right[j - 2]) && ToLower(left[i - 2]) == ToLower(right[j - 1]))
                    {
                        value = Math.Min(value, twoBack[j - 2] + 1);
                    }
                    current[j] = value;
                    rowMinimum = Math.Min(rowMinimum, value);
                }
                if (rowMinimum > limit)
                {
                    return limit + 1;
                }
                int[] recycled = twoBack;
                twoBack = previous;
                previous = current;
                current = recycled;
            }
            return previous[right.Length];
        }

        /// <summary>
        /// Returns the candidate closest to <paramref name="name"/> that a typo could have turned into it: at most one
        /// edit for short names, and one per four characters for longer ones. Null when none is that close.
        /// </summary>
        public static string FindClosest(string name, IEnumerable<string> candidates)
        {
            int limit = Math.Max(1, name.Length / 4);
            string closest = null;
            int closestDistance = limit + 1;
            foreach (string candidate in candidates)
            {
                int distance = Compute(name, candidate, limit);
                if (distance < closestDistance)
                {
                    closest = candidate;
                    closestDistance = distance;
                }
            }
            return closest;
        }

        private static char ToLower(char character) => (uint)(character - 'A') <= 'Z' - 'A' ? (char)(character | 0x20) : character;
    }
}
