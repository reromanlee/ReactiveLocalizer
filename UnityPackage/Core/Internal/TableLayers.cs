using reromanlee.ReactiveLocalizer.Tables;
using System;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// What a lookup searches for one table, in order: the table from each source that has it in the current language,
    /// then in each fallback language, until one of them has every key of the source language.
    /// </summary>
    /// <remarks>
    /// A translation that has every key therefore makes it the only layer, and a mod that patches a few entries of a
    /// language comes first, with the language it patches right behind it. Never changes after it is built.
    /// </remarks>
    internal sealed class TableLayers
    {
        private readonly CompiledTable[] _tables;
        private readonly int[] _languageIndexes;

        /// <param name="tables">The tables to search, in order.</param>
        /// <param name="languageIndexes">For each table, the position of its language in the fallback chain.</param>
        /// <param name="consultedLanguageCount">How many languages of the chain, from its start, were looked in.</param>
        public TableLayers(CompiledTable[] tables, int[] languageIndexes, int consultedLanguageCount)
        {
            _tables = tables;
            _languageIndexes = languageIndexes;
            ConsultedLanguageCount = consultedLanguageCount;
        }

        /// <summary>How many tables are searched; zero for a table that couldn't be loaded in any language.</summary>
        public int Count => _tables.Length;

        /// <summary>
        /// How many languages of the fallback chain, from its start, were looked in. The ones after them were never
        /// needed, because a table before them has every key.
        /// </summary>
        public int ConsultedLanguageCount { get; }

        /// <summary>
        /// Finds an entry in the first layer that has it, and returns that table, the entry's index in it, and the
        /// position in the fallback chain of the language it was found in.
        /// </summary>
        public bool TryFind(ulong entryHash, out CompiledTable table, out int index, out int languageIndex)
        {
            for (int i = 0; i < _tables.Length; i++)
            {
                if (_tables[i].TryFind(entryHash, out index))
                {
                    table = _tables[i];
                    languageIndex = _languageIndexes[i];
                    return true;
                }
            }
            table = null;
            index = -1;
            languageIndex = -1;
            return false;
        }
    }
}
