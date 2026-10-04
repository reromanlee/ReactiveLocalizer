using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>One table as generated code shows it: its entries, and the aliases its renamed entries left behind.</summary>
    public sealed class KeysScriptTable
    {
        /// <summary>Creates the table named <paramref name="name"/>.</summary>
        /// <param name="name">Name of the table.</param>
        /// <param name="entries">Keys of the table's entries, in any order.</param>
        /// <param name="aliases">Each alias with the key of the entry it now points to, in any order.</param>
        public KeysScriptTable(string name, IReadOnlyList<string> entries, IReadOnlyList<KeyValuePair<string, string>> aliases)
        {
            Name = name;
            Entries = entries ?? Array.Empty<string>();
            Aliases = aliases ?? Array.Empty<KeyValuePair<string, string>>();
        }

        /// <summary>Name of the table.</summary>
        public string Name { get; }

        /// <summary>Keys of the table's entries.</summary>
        public IReadOnlyList<string> Entries { get; }

        /// <summary>Each alias, as the key, with the key of the entry it points to, as the value.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Aliases { get; }
    }
}
