namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>What an import does with one text.</summary>
    public enum ImportChangeKind
    {
        /// <summary>A translation the language didn't have yet.</summary>
        Added = 0,

        /// <summary>A translation that replaces the language's text.</summary>
        Updated = 1,

        /// <summary>A translation added or replaced, but made from another source text than the current one, so it arrives outdated.</summary>
        Outdated = 2,

        /// <summary>An unchanged translation the file marks as checked against the current source text, which makes it current.</summary>
        Confirmed = 3,

        /// <summary>A source text the file changed, imported because source changes were asked for.</summary>
        SourceChanged = 4,

        /// <summary>A text left out, for the reason the change gives.</summary>
        Rejected = 5
    }
}
