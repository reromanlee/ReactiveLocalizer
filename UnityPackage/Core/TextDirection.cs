namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The direction a language is written in. It is a hint for whatever renders the text: the core never reorders
    /// or shapes text itself.
    /// </summary>
    public enum TextDirection
    {
        /// <summary>Written from left to right, like English or Russian.</summary>
        LeftToRight = 0,

        /// <summary>Written from right to left, like Arabic or Hebrew.</summary>
        RightToLeft = 1
    }
}
