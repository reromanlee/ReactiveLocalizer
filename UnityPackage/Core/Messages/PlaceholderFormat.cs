namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>How a placeholder shows its argument.</summary>
    internal enum PlaceholderFormat : byte
    {
        /// <summary><c>{name}</c>: text as is, numbers in the language's default style.</summary>
        Plain,

        /// <summary><c>{count, number}</c>, optionally with the <c>integer</c> or <c>percent</c> style.</summary>
        Number,

        /// <summary><c>{deadline, date}</c>: through the formatter registered for the type.</summary>
        Custom
    }
}
