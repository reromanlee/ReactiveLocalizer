using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// Everything a lookup reads: the current language, the languages it falls back through with how each formats
    /// messages, and the loaded tables.
    /// </summary>
    /// <remarks>
    /// Never changes after it is built. A language switch, or a table loading or unloading, builds a new state and
    /// publishes it by replacing one reference, so a read that takes the state once can never see half of one language
    /// and half of another.
    /// </remarks>
    internal sealed class LocalizerState
    {
        /// <summary>The state before initialization: no language, no tables, version 0.</summary>
        public static readonly LocalizerState Empty = new(0, null, Array.Empty<LanguageInfo>(), Array.Empty<LanguageFormat>(),
            new Dictionary<ulong, TableLayers>(), MovedEntries.None);

        private readonly Dictionary<ulong, TableLayers> _tables;
        private readonly IReadOnlyDictionary<(ulong Table, ulong Entry), EntryKey> _movedEntries;

        /// <param name="version">Counts the states published, starting at 1, so a binding can tell whether its text is stale.</param>
        /// <param name="language">The language lookups are in.</param>
        /// <param name="chain">The language, its fallbacks, then the source language.</param>
        /// <param name="formats">How each language of <paramref name="chain"/> formats messages.</param>
        /// <param name="tables">Per table hash, the tables a lookup searches. Never changed afterwards.</param>
        /// <param name="movedEntries">The key each entry moved from another table had, with its key now.</param>
        public LocalizerState(int version, LanguageInfo language, IReadOnlyList<LanguageInfo> chain, IReadOnlyList<LanguageFormat> formats,
            Dictionary<ulong, TableLayers> tables, IReadOnlyDictionary<(ulong Table, ulong Entry), EntryKey> movedEntries)
        {
            Version = version;
            Language = language;
            Chain = chain;
            Formats = formats;
            _tables = tables;
            _movedEntries = movedEntries;
        }

        public int Version { get; }

        public LanguageInfo Language { get; }

        public IReadOnlyList<LanguageInfo> Chain { get; }

        /// <summary>How each language of <see cref="Chain"/> formats messages, at the same positions.</summary>
        public IReadOnlyList<LanguageFormat> Formats { get; }

        public bool IsInitialized => Version > 0;

        /// <summary>The loaded tables, by table hash.</summary>
        public IReadOnlyDictionary<ulong, TableLayers> Tables => _tables;

        /// <summary>Returns whether the table with <paramref name="tableHash"/> is loaded in this state.</summary>
        public bool HasTable(ulong tableHash) => _tables.ContainsKey(tableHash);

        /// <summary>Returns the layers of a loaded table.</summary>
        public bool TryGetLayers(ulong tableHash, out TableLayers layers) => _tables.TryGetValue(tableHash, out layers);

        /// <summary>
        /// Finds an entry in the current language, then in each fallback in order, and returns the table and index it
        /// was found at, and the position in <see cref="Chain"/> of the language it was found in. A key an entry had
        /// before it moved to another table finds it there.
        /// </summary>
        public bool TryResolve(ulong tableHash, ulong entryHash, out CompiledTable table, out int index, out int languageIndex)
        {
            if (_tables.TryGetValue(tableHash, out TableLayers layers) && layers.TryFind(entryHash, out table, out index, out languageIndex))
            {
                return true;
            }
            if (_movedEntries.Count > 0 && _movedEntries.TryGetValue((tableHash, entryHash), out EntryKey moved) &&
                _tables.TryGetValue(moved.Table.Hash, out layers) && layers.TryFind(moved.Hash, out table, out index, out languageIndex))
            {
                return true;
            }
            table = null;
            index = -1;
            languageIndex = -1;
            return false;
        }

        /// <summary>Returns the key an entry has now: the one it moved to, or <paramref name="key"/> itself.</summary>
        public EntryKey ResolveMoved(in EntryKey key) =>
            _movedEntries.Count > 0 && _movedEntries.TryGetValue((key.Table.Hash, key.Hash), out EntryKey moved) ? moved : key;

        /// <summary>Returns the next state: the same language with <paramref name="tableHash"/> loaded as <paramref name="layers"/>, or unloaded when null.</summary>
        public LocalizerState WithTable(ulong tableHash, TableLayers layers)
        {
            Dictionary<ulong, TableLayers> tables = new(_tables);
            if (layers != null)
            {
                tables[tableHash] = layers;
            }
            else
            {
                tables.Remove(tableHash);
            }
            return new LocalizerState(Version + 1, Language, Chain, Formats, tables, _movedEntries);
        }
    }
}
