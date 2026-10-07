using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary><c>{gender, select, female {...} male {...} other {...}}</c>.</summary>
    internal sealed class SelectPart : MessagePart
    {
        public SelectPart(int position, string argument, IReadOnlyList<SelectCase> cases) : base(position)
        {
            Argument = argument;
            Cases = cases;
        }

        public string Argument { get; }

        public IReadOnlyList<SelectCase> Cases { get; }
    }
}
