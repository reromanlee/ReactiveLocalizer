namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>A saved reference to an entry, found in a scene, prefab or other asset.</summary>
    public readonly struct EntryUse
    {
        /// <summary>Creates a reference found at <paramref name="location"/>.</summary>
        /// <param name="tableName">The table the reference names.</param>
        /// <param name="entryName">The entry the reference names.</param>
        /// <param name="location">Where the reference is, as problems name it, such as an asset path and a field.</param>
        public EntryUse(string tableName, string entryName, string location)
        {
            TableName = tableName;
            EntryName = entryName;
            Location = location;
        }

        /// <summary>The table the reference names.</summary>
        public string TableName { get; }

        /// <summary>The entry the reference names.</summary>
        public string EntryName { get; }

        /// <summary>Where the reference is, as problems name it.</summary>
        public string Location { get; }
    }
}
