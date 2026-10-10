namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>How an entry stands in one language, compared with its source text.</summary>
    public enum TranslationState
    {
        /// <summary>The language has no text for the entry, so it shows the fallback language's.</summary>
        Missing = 0,

        /// <summary>The text was made from the current source text, or is the source text itself.</summary>
        Current = 1,

        /// <summary>The source text changed since the text was made from it.</summary>
        Outdated = 2,

        /// <summary>The text carries no fingerprint, as when written by hand, so nothing says which source text it follows.</summary>
        Unverified = 3,

        /// <summary>The language has text for a key the source language doesn't have, so it is never shown.</summary>
        Orphan = 4
    }
}
