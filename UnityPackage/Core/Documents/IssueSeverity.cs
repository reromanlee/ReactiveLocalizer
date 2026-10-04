namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>How serious a problem found in a localization file is.</summary>
    public enum IssueSeverity
    {
        /// <summary>Worth fixing, but nothing is lost: an unknown attribute, an escape kept as written.</summary>
        Warning = 0,

        /// <summary>The affected line can't be used. It is left out of the compiled result, and builds fail on it.</summary>
        Error = 1
    }
}
