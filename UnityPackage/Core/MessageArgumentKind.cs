namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// How a message uses an argument, which decides the parameter type generated code gives it, and which uses
    /// translations may make of it.
    /// </summary>
    public enum MessageArgumentKind : byte
    {
        /// <summary>
        /// Shown as is, like <c>{name}</c>, or through a registered formatter, like <c>{deadline, date}</c>: any
        /// <see cref="MessageValue"/>.
        /// </summary>
        Value,

        /// <summary>
        /// A number: <c>{count, number}</c>, <c>{count, plural, ...}</c> or <c>{place, selectordinal, ...}</c>, as a
        /// <see cref="MessageNumber"/>.
        /// </summary>
        Number,

        /// <summary>A string that picks a branch, like <c>{gender, select, ...}</c>.</summary>
        Keyword
    }
}
