namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>An entry as a search shows it: where it is, its key, and its source text.</summary>
    public sealed class EntrySearchItem
    {
        /// <summary>Creates a searchable entry.</summary>
        /// <param name="catalogName">The catalog the entry is in.</param>
        /// <param name="tableName">The table the entry is in.</param>
        /// <param name="entryName">The entry's key.</param>
        /// <param name="text">The entry's source text.</param>
        public EntrySearchItem(string catalogName, string tableName, string entryName, string text)
        {
            CatalogName = catalogName;
            TableName = tableName;
            EntryName = entryName;
            Text = text ?? string.Empty;
            QualifiedName = $"{tableName}.{entryName}";
        }

        /// <summary>The catalog the entry is in.</summary>
        public string CatalogName { get; }

        /// <summary>The table the entry is in.</summary>
        public string TableName { get; }

        /// <summary>The entry's key.</summary>
        public string EntryName { get; }

        /// <summary>The entry's source text.</summary>
        public string Text { get; }

        /// <summary><c>Table.Entry</c>, as searches match and lists show it.</summary>
        public string QualifiedName { get; }
    }
}
