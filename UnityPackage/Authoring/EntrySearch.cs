using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Finds entries by their key or their text, best matches first: a key equal to the query, then keys starting
    /// with it, keys containing it, and last entries whose text contains it. Every word of the query has to match.
    /// </summary>
    /// <remarks>
    /// Matches of equal rank keep the order of the items searched, so a search never sorts: items put in natural order
    /// once with <see cref="Sort"/> list in natural order in every search after.
    /// </remarks>
    public static class EntrySearch
    {
        private const int Ranks = 4;
        private static readonly char[] WordSeparators = { ' ', '\t' };

        /// <summary>
        /// Fills <paramref name="results"/> with the items of <paramref name="items"/> that match <paramref name="query"/>,
        /// best first, at most <paramref name="limit"/> of them. An empty query matches everything, in the items' order.
        /// </summary>
        public static void Find(IReadOnlyList<EntrySearchItem> items, string query, List<EntrySearchItem> results, int limit)
        {
            results.Clear();
            string[] words = (query ?? string.Empty).Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                for (int i = 0; i < items.Count && results.Count < limit; i++)
                {
                    results.Add(items[i]);
                }
                return;
            }
            // The best rank fills the results directly; the others wait in lists of their own until it is known.
            List<EntrySearchItem>[] byRank = { results, null, null, null };
            for (int i = 0; i < items.Count && results.Count < limit; i++)
            {
                int rank = Rank(items[i], words);
                if (rank < 0)
                {
                    continue;
                }
                List<EntrySearchItem> matches = byRank[rank] ??= new List<EntrySearchItem>();
                if (matches.Count < limit)
                {
                    matches.Add(items[i]);
                }
            }
            for (int rank = 1; rank < Ranks; rank++)
            {
                List<EntrySearchItem> matches = byRank[rank];
                for (int i = 0; matches != null && i < matches.Count && results.Count < limit; i++)
                {
                    results.Add(matches[i]);
                }
            }
        }

        /// <summary>Puts <paramref name="items"/> in natural order of their <c>Table.Entry</c>. Items already in order are only checked.</summary>
        public static void Sort(List<EntrySearchItem> items)
        {
            for (int i = 1; i < items.Count; i++)
            {
                if (Compare(items[i - 1], items[i]) > 0)
                {
                    items.Sort(Compare);
                    return;
                }
            }
        }

        private static int Compare(EntrySearchItem left, EntrySearchItem right) => NaturalOrder.Instance.Compare(left.QualifiedName, right.QualifiedName);

        /// <summary>Returns how well an item matches every word, lower being better, or -1 when a word matches nothing.</summary>
        private static int Rank(EntrySearchItem item, string[] words)
        {
            int worst = 0;
            for (int i = 0; i < words.Length; i++)
            {
                int rank = RankWord(item, words[i]);
                if (rank < 0)
                {
                    return -1;
                }
                worst = Math.Max(worst, rank);
            }
            return worst;
        }

        private static int RankWord(EntrySearchItem item, string word)
        {
            if (string.Equals(item.EntryName, word, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.QualifiedName, word, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            if (item.EntryName.StartsWith(word, StringComparison.OrdinalIgnoreCase) ||
                item.QualifiedName.StartsWith(word, StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }
            if (item.QualifiedName.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 2;
            }
            if (item.Text.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 3;
            }
            return -1;
        }
    }
}
