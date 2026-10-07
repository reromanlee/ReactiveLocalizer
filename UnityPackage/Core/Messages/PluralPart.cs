using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary><c>{count, plural, offset:1 =0 {...} one {...} other {...}}</c>, or the same with <c>selectordinal</c>.</summary>
    internal sealed class PluralPart : MessagePart
    {
        public PluralPart(int position, string argument, bool isOrdinal, double offset, IReadOnlyList<PluralCase> cases) : base(position)
        {
            Argument = argument;
            IsOrdinal = isOrdinal;
            Offset = offset;
            Cases = cases;
        }

        public string Argument { get; }

        /// <summary>Whether this is a selectordinal, which chooses by ordinal rules: 1st, 2nd, 3rd.</summary>
        public bool IsOrdinal { get; }

        /// <summary>Subtracted from the number before a category is chosen and before <c>#</c> shows it.</summary>
        public double Offset { get; }

        public IReadOnlyList<PluralCase> Cases { get; }
    }
}
