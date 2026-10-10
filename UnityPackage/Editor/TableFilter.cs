using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Picks the rows of a <see cref="TableSheet"/> the table window lists: those passing a filter, whose key, context
    /// or text in any language contains every word of a search.
    /// </summary>
    internal static class TableFilter
    {
        private static readonly char[] WordSeparators = { ' ', '\t' };

        /// <summary>Fills <paramref name="results"/> with the rows of <paramref name="rows"/> that pass, in their order.</summary>
        /// <param name="rows">The rows to pick from.</param>
        /// <param name="query">Words that must all be found; empty for none.</param>
        /// <param name="kind">The filter.</param>
        /// <param name="language">For <see cref="TableFilterKind.Missing"/>, the index of the language, or -1 for any translation.</param>
        /// <param name="results">Where the rows that pass go.</param>
        public static void Apply(IReadOnlyList<TableSheetRow> rows, string query, TableFilterKind kind, int language, List<TableSheetRow> results)
        {
            results.Clear();
            string[] words = (query ?? string.Empty).Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < rows.Count; i++)
            {
                if (Passes(rows[i], kind, language) && Matches(rows[i], words))
                {
                    results.Add(rows[i]);
                }
            }
        }

        private static bool Passes(TableSheetRow row, TableFilterKind kind, int language)
        {
            switch (kind)
            {
                case TableFilterKind.Missing:
                    if (language > 0 && language < row.Cells.Length)
                    {
                        return row.Source != null && row.Cells[language].State == TranslationState.Missing;
                    }
                    // The source text an orphan lacks is its own problem, not a missing translation.
                    for (int i = 1; i < row.Cells.Length; i++)
                    {
                        if (row.Source != null && row.Cells[i].State == TranslationState.Missing)
                        {
                            return true;
                        }
                    }
                    return false;
                case TableFilterKind.Outdated:
                    for (int i = 1; i < row.Cells.Length; i++)
                    {
                        if (row.Cells[i].State == TranslationState.Outdated || row.Cells[i].State == TranslationState.Unverified)
                        {
                            return true;
                        }
                    }
                    return false;
                case TableFilterKind.Problems:
                    for (int i = 0; i < row.Cells.Length; i++)
                    {
                        if (row.Cells[i].Problems != TableSheetProblem.None || row.Cells[i].State == TranslationState.Orphan)
                        {
                            return true;
                        }
                    }
                    return false;
                default:
                    return true;
            }
        }

        private static bool Matches(TableSheetRow row, string[] words)
        {
            for (int w = 0; w < words.Length; w++)
            {
                if (!Contains(row, words[w]))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool Contains(TableSheetRow row, string word)
        {
            if (row.Key.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0 || row.Context.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            for (int i = 0; i < row.Cells.Length; i++)
            {
                if (row.Cells[i].Text?.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Which rows the table window lists.</summary>
    internal enum TableFilterKind
    {
        /// <summary>Every entry.</summary>
        All = 0,

        /// <summary>Entries missing a translation, in one language or any.</summary>
        Missing = 1,

        /// <summary>Entries with a translation made from an older source text, or not known to follow the current one.</summary>
        Outdated = 2,

        /// <summary>Entries with errors, warnings, texts over their maximum length, or orphans.</summary>
        Problems = 3
    }
}
