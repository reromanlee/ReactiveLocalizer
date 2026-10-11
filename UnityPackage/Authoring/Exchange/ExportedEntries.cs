namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>Which entries an export holds, judged by their state in the exported languages.</summary>
    public enum ExportedEntries
    {
        /// <summary>Every entry of the exported tables.</summary>
        All = 0,

        /// <summary>Entries missing, outdated or unverified in any exported language: what a translator has to do.</summary>
        MissingOrOutdated = 1,

        /// <summary>Entries missing in any exported language.</summary>
        Missing = 2,

        /// <summary>Entries outdated or unverified in any exported language, since both need checking against the source.</summary>
        Outdated = 3
    }
}
