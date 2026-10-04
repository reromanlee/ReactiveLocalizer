using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// Everything a lookup reads: the current language, the languages it falls back through, and the loaded tables.
    /// </summary>
    /// <remarks>
    /// Never changes after it is built. A language switch builds a new state and publishes it by replacing one
    /// reference, so a read that takes the state once can never see half of one language and half of another.
    /// </remarks>
    internal sealed class LocalizerState
    {
        /// <summary>The state before initialization: no language, no tables, version 0.</summary>
        public static readonly LocalizerState Empty = new(0, null, Array.Empty<LanguageInfo>(), new Dictionary<ulong, CompiledTable[]>());

        private readonly Dictionary<ulong, CompiledTable[]> _tables;

        /// <param name="version">Counts the states published, starting at 1, so a binding can tell whether its text is stale.</param>
        /// <param name="language">The language lookups are in.</param>
        /// <param name="chain">The language, its fallbacks, then the source language.</param>
        /// <param name="tables">Per table hash, the table in each language of <paramref name="chain"/>, with null where it isn't loaded.</param>
        public LocalizerState(int version, LanguageInfo language, IReadOnlyList<LanguageInfo> chain, Dictionary<ulong, CompiledTable[]> tables)
        {
            Version = version;
            Language = language;
            Chain = chain;
            _tables = tables;
        }

        public int Version { get; }

        public LanguageInfo Language { get; }

        public IReadOnlyList<LanguageInfo> Chain { get; }

        public bool IsInitialized => Version > 0;

        /// <summary>Returns whether the table with <paramref name="tableHash"/> is loaded in this state.</summary>
        public bool HasTable(ulong tableHash) => _tables.ContainsKey(tableHash);

        /// <summary>
        /// Finds an entry in the current language, then in each fallback in order, and returns the table and index it
        /// was found at.
        /// </summary>
        public bool TryResolve(ulong tableHash, ulong entryHash, out CompiledTable table, out int index)
        {
            if (_tables.TryGetValue(tableHash, out CompiledTable[] perLanguage))
            {
                for (int i = 0; i < perLanguage.Length; i++)
                {
                    CompiledTable candidate = perLanguage[i];
                    if (candidate != null && candidate.TryFind(entryHash, out index))
                    {
                        table = candidate;
                        return true;
                    }
                }
            }
            table = null;
            index = -1;
            return false;
        }
    }
}
