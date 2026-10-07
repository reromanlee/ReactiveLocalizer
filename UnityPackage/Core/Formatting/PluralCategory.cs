namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>
    /// The plural forms CLDR defines, which plural and selectordinal messages choose between. Each language uses a
    /// subset of them, and every language has <see cref="Other"/>.
    /// </summary>
    internal enum PluralCategory : byte
    {
        Zero,
        One,
        Two,
        Few,
        Many,
        Other
    }
}
