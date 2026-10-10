using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Finds entries by their key or their text, best matches first: a key equal to the query, then keys starting
    /// with it, keys containing it, and last entries whose text contains it. Every word of the query has to match.
    /// </summary>
    public static class EntrySearch
    {
        /// <summary>
        /// Fills <paramref name="results"/> with the items of <paramref name="items"/> that match <paramref name="query"/>,
        /// best first, at most <paramref name="limit"/> of them. An empty query matches everything, in natural order.
        /// </summary>
        public static void Find(IReadOnlyList<EntrySearchItem> items, string query, List<EntrySearchItem> results, int limit)
        {
            results.Clear();
            string[] words = (query ?? string.Empty).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            List<(int Score, EntrySearchItem Item)> scored = new();
            for (int i = 0; i < items.Count; i++)
            {
                int score = Score(items[i], words);
                if (score >= 0)
                {
                    scored.Add((score, items[i]));
                }
            }
            scored.Sort((left, right) =>
            {
                int byScore = left.Score.CompareTo(right.Score);
                return byScore != 0 ? byScore : NaturalOrder.Instance.Compare(left.Item.QualifiedName, right.Item.QualifiedName);
            });
            for (int i = 0; i < scored.Count && results.Count < limit; i++)
            {
                results.Add(scored[i].Item);
            }
        }

        /// <summary>Returns how well an item matches every word, lower being better, or -1 when a word matches nothing.</summary>
        private static int Score(EntrySearchItem item, string[] words)
        {
            int worst = 0;
            for (int i = 0; i < words.Length; i++)
            {
                int score = ScoreWord(item, words[i]);
                if (score < 0)
                {
                    return -1;
                }
                worst = Math.Max(worst, score);
            }
            return worst;
        }

        private static int ScoreWord(EntrySearchItem item, string word)
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
