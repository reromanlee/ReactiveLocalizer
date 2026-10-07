using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>One table as generated code shows it: its entries, and the aliases its renamed entries left behind.</summary>
    public sealed class KeysScriptTable
    {
        /// <summary>Creates the table named <paramref name="name"/>.</summary>
        /// <param name="name">Name of the table.</param>
        /// <param name="entries">The table's entries, in any order.</param>
        /// <param name="aliases">Each alias with the key of the entry it now points to, in any order.</param>
        public KeysScriptTable(string name, IReadOnlyList<KeysScriptEntry> entries, IReadOnlyList<KeyValuePair<string, string>> aliases)
        {
            Name = name;
            Entries = entries ?? Array.Empty<KeysScriptEntry>();
            Aliases = aliases ?? Array.Empty<KeyValuePair<string, string>>();
        }

        /// <summary>Creates the table named <paramref name="name"/> whose entries are all plain text.</summary>
        /// <param name="name">Name of the table.</param>
        /// <param name="entries">Keys of the table's entries, in any order.</param>
        /// <param name="aliases">Each alias with the key of the entry it now points to, in any order.</param>
        public KeysScriptTable(string name, IReadOnlyList<string> entries, IReadOnlyList<KeyValuePair<string, string>> aliases)
            : this(name, ToEntries(entries), aliases)
        {
        }

        /// <summary>Name of the table.</summary>
        public string Name { get; }

        /// <summary>The table's entries.</summary>
        public IReadOnlyList<KeysScriptEntry> Entries { get; }

        /// <summary>Each alias, as the key, with the key of the entry it points to, as the value.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Aliases { get; }

        private static KeysScriptEntry[] ToEntries(IReadOnlyList<string> names)
        {
            if (names == null)
            {
                return Array.Empty<KeysScriptEntry>();
            }
            KeysScriptEntry[] entries = new KeysScriptEntry[names.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                entries[i] = new KeysScriptEntry(names[i]);
            }
            return entries;
        }
    }
}
