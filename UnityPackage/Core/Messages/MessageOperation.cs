namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// The operations of a compiled message: a flat array of integers that renders without allocating and is checked
    /// once, when its table loads, so a damaged table can never make rendering read outside it.
    /// </summary>
    /// <remarks>
    /// A message starts with its argument table, then the end of its body, then the body. Each operation is followed
    /// by its operands; texts are a start and a length in the table's character buffer, and doubles take two
    /// integers, low half first. A plural or select stores where it ends, and each of its forms where its body ends.
    /// <code>
    /// int argument count, then per argument: int hash low, int hash high, int name start, int name length
    /// int body end, then the body:
    /// Text        start, length
    /// Plain       argument
    /// Number      argument, style
    /// Custom      argument, type hash low, type hash high, type start, type length, style start, style length
    /// Pound
    /// Plural      argument, is ordinal, offset low, offset high, end, form count,
    ///             then per form: kind (a PluralCategory, or Explicit), value low, value high, body end, body
    /// Select      argument, end, branch count, then per branch: keyword start, keyword length, body end, body
    /// </code>
    /// </remarks>
    internal static class MessageOperation
    {
        public const int Text = 1;
        public const int Plain = 2;
        public const int Number = 3;
        public const int Custom = 4;
        public const int Pound = 5;
        public const int Plural = 6;
        public const int Select = 7;

        /// <summary>The form kind of an exact form such as <c>=0</c>; category forms store their PluralCategory.</summary>
        public const int Explicit = 6;

        /// <summary>Integers per argument in a message's argument table.</summary>
        public const int ArgumentSize = 4;
    }
}
