namespace reromanlee.ReactiveLocalizer.Formatting
{
    /// <summary>The ways a message can show a number: <c>{n}</c> or <c>{n, number}</c>, <c>integer</c> and <c>percent</c>.</summary>
    internal enum NumberStyle : byte
    {
        /// <summary>Grouped, with up to three fraction digits: 1,234.5.</summary>
        Default,

        /// <summary>Grouped and rounded to a whole number: 1,235.</summary>
        Integer,

        /// <summary>Multiplied by 100, rounded to a whole number, with the language's percent sign: 12%.</summary>
        Percent
    }
}
