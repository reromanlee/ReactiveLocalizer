namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>What a saved reference to an entry finds, as a <see cref="KeyResolver"/> tells it.</summary>
    public readonly struct KeyResolution
    {
        internal KeyResolution(KeyResolutionKind kind, string tableName, string entryName, string formerName, string suggestion)
        {
            Kind = kind;
            TableName = tableName;
            EntryName = entryName;
            FormerName = formerName;
            Suggestion = suggestion;
        }

        /// <summary>Whether the reference finds its entry, and how.</summary>
        public KeyResolutionKind Kind { get; }

        /// <summary>The table the entry is in now. Null when the reference finds nothing.</summary>
        public string TableName { get; }

        /// <summary>The entry's key now. Null when the reference finds nothing.</summary>
        public string EntryName { get; }

        /// <summary>The <c>@formerly</c> the reference was found through, as written. Null unless renamed or moved.</summary>
        public string FormerName { get; }

        /// <summary>The key, as <c>Table.Entry</c>, that a reference finding nothing most likely meant. Null when none is close.</summary>
        public string Suggestion { get; }

        /// <summary>Whether the reference finds an entry, directly or by a former name.</summary>
        public bool IsFound => Kind == KeyResolutionKind.Found || Kind == KeyResolutionKind.Renamed || Kind == KeyResolutionKind.Moved;
    }

    /// <summary>How a saved reference finds its entry.</summary>
    public enum KeyResolutionKind
    {
        /// <summary>The key names an entry.</summary>
        Found = 0,

        /// <summary>The key is a former name the entry keeps in its table.</summary>
        Renamed = 1,

        /// <summary>The key is the one the entry had before it moved to another table.</summary>
        Moved = 2,

        /// <summary>No entry has or had the key.</summary>
        Missing = 3,

        /// <summary>The key breaks the naming rule, as after a bad merge.</summary>
        Invalid = 4
    }
}
