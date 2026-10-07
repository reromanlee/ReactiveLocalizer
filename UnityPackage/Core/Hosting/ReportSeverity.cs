namespace reromanlee.ReactiveLocalizer.Hosting
{
    /// <summary>How serious something a localizer reports to its host is.</summary>
    public enum ReportSeverity
    {
        /// <summary>Worth knowing, but every text still shows: a table missing in a translation, a destroyed target.</summary>
        Warning = 0,

        /// <summary>Something can't show its text: a key that doesn't exist, a table that failed to load.</summary>
        Error = 1
    }
}
