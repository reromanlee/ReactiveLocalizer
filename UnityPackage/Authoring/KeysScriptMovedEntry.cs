using System;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// An entry that moved to another table, as the generated code of its former table keeps it: an
    /// <c>[Obsolete]</c> member under its former name, so code written against it still compiles and still finds it.
    /// </summary>
    public sealed class KeysScriptMovedEntry
    {
        /// <summary>Creates a moved entry.</summary>
        /// <param name="formerName">The entry's name in the table it moved from.</param>
        /// <param name="tableName">The table it moved to.</param>
        /// <param name="entry">The entry as it is now, whose name and arguments the member forwards to.</param>
        /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
        public KeysScriptMovedEntry(string formerName, string tableName, KeysScriptEntry entry)
        {
            FormerName = formerName;
            TableName = tableName;
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        }

        /// <summary>The entry's name in the table it moved from.</summary>
        public string FormerName { get; }

        /// <summary>The table it moved to.</summary>
        public string TableName { get; }

        /// <summary>The entry as it is now.</summary>
        public KeysScriptEntry Entry { get; }
    }
}
