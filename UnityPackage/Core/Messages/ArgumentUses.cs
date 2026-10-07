using System;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>Every way a message uses one argument.</summary>
    [Flags]
    internal enum ArgumentUses : byte
    {
        None = 0,

        /// <summary><c>{name}</c>.</summary>
        Plain = 1,

        /// <summary><c>{count, number}</c>.</summary>
        Number = 2,

        /// <summary><c>{count, plural, ...}</c>.</summary>
        Plural = 4,

        /// <summary><c>{place, selectordinal, ...}</c>.</summary>
        Ordinal = 8,

        /// <summary><c>{gender, select, ...}</c>.</summary>
        Select = 16,

        /// <summary><c>{deadline, date}</c> or another registered type.</summary>
        Formatter = 32,

        /// <summary>The uses that need a number.</summary>
        Numeric = Number | Plural | Ordinal
    }
}
