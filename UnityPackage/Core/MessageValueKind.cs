namespace reromanlee.ReactiveLocalizer
{
    /// <summary>What a <see cref="MessageValue"/> holds.</summary>
    public enum MessageValueKind : byte
    {
        /// <summary>Nothing: the default value, or a null string.</summary>
        Empty,

        /// <summary>A string.</summary>
        Text,

        /// <summary>A number of any numeric type, as a <see cref="MessageNumber"/>.</summary>
        Number,

        /// <summary>A <see cref="System.DateTime"/>.</summary>
        DateTime,

        /// <summary>A <see cref="System.TimeSpan"/>.</summary>
        TimeSpan,

        /// <summary>Any other object, for formatters the project registers.</summary>
        Object
    }
}
