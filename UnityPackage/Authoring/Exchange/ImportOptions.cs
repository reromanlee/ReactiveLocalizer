namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>How an import treats what it reads.</summary>
    public sealed class ImportOptions
    {
        /// <summary>
        /// Whether source texts the file changed are imported too, as after proofreading in a spreadsheet. Off by
        /// default, since a file exported earlier holds the source texts of then, which would undo every change made
        /// since. Even then, imports never add, rename or delete entries.
        /// </summary>
        public bool IsImportingSourceChanges { get; set; }
    }
}
