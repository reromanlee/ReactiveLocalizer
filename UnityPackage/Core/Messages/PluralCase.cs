using reromanlee.ReactiveLocalizer.Formatting;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>One form of a plural or selectordinal: a category such as <c>few</c>, or an explicit value such as <c>=0</c>.</summary>
    internal sealed class PluralCase
    {
        public PluralCase(int position, bool isExplicit, double explicitValue, string keyword, bool isCategory, PluralCategory category, MessageBody body)
        {
            Position = position;
            IsExplicit = isExplicit;
            ExplicitValue = explicitValue;
            Keyword = keyword;
            IsCategory = isCategory;
            Category = category;
            Body = body;
        }

        public int Position { get; }

        /// <summary>Whether the form matches one exact number, written <c>=N</c>, before any category is considered.</summary>
        public bool IsExplicit { get; }

        public double ExplicitValue { get; }

        /// <summary>The keyword as written, such as <c>few</c>; for an explicit form, the text after its <c>=</c>.</summary>
        public string Keyword { get; }

        /// <summary>Whether <see cref="Keyword"/> names a CLDR plural category, which only such forms can match.</summary>
        public bool IsCategory { get; }

        public PluralCategory Category { get; }

        public MessageBody Body { get; }
    }
}
