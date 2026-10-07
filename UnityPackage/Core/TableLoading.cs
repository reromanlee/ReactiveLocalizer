namespace reromanlee.ReactiveLocalizer
{
    /// <summary>When a table is in memory, as its <c>@loading</c> setting chooses.</summary>
    public enum TableLoading
    {
        /// <summary>Loaded together with the language, so synchronous reads always find it. The default.</summary>
        Preload = 0,

        /// <summary>Loaded while something uses it, and unloaded once nothing does.</summary>
        OnDemand = 1
    }
}
