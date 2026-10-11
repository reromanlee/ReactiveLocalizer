namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>One text of an import: what happens to it, the entry and language it belongs to, and the text before and after.</summary>
    public sealed class ImportChange
    {
        internal ImportChange(ImportChangeKind kind, string tableName, string key, string languageName, string oldText, string newText, string location)
        {
            Kind = kind;
            TableName = tableName;
            Key = key;
            LanguageName = languageName;
            OldText = oldText;
            NewText = newText;
            Location = location;
        }

        /// <summary>What happens to the text.</summary>
        public ImportChangeKind Kind { get; internal set; }

        /// <summary>The entry's table.</summary>
        public string TableName { get; }

        /// <summary>The entry's key.</summary>
        public string Key { get; }

        /// <summary>The language the text is in; null for a row rejected as a whole.</summary>
        public string LanguageName { get; }

        /// <summary>The language's text before the import; null when it had none.</summary>
        public string OldText { get; }

        /// <summary>The text the file holds.</summary>
        public string NewText { get; }

        /// <summary>Where the text is in the file it came from.</summary>
        public string Location { get; }

        /// <summary>Why a text is rejected or arrives outdated, and the warnings checking it found; null when there is nothing to say.</summary>
        public string Message { get; internal set; }

        internal void AddMessage(string message)
        {
            Message = Message == null ? message : Message + "\n" + message;
        }

        /// <inheritdoc/>
        public override string ToString() => LanguageName == null ? $"{TableName}.{Key}: {Kind}" : $"{TableName}.{Key} ({LanguageName}): {Kind}";
    }
}
