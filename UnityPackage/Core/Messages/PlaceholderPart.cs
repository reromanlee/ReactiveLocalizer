using reromanlee.ReactiveLocalizer.Formatting;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>A placeholder that shows an argument: <c>{name}</c>, <c>{count, number, integer}</c> or <c>{deadline, date, short}</c>.</summary>
    internal sealed class PlaceholderPart : MessagePart
    {
        public PlaceholderPart(int position, string argument, PlaceholderFormat format, NumberStyle numberStyle, string formatterType, string formatterStyle)
            : base(position)
        {
            Argument = argument;
            Format = format;
            NumberStyle = numberStyle;
            FormatterType = formatterType ?? string.Empty;
            FormatterStyle = formatterStyle ?? string.Empty;
        }

        public string Argument { get; }

        public PlaceholderFormat Format { get; }

        /// <summary>The style of a <see cref="PlaceholderFormat.Number"/> placeholder.</summary>
        public NumberStyle NumberStyle { get; }

        /// <summary>The type of a <see cref="PlaceholderFormat.Custom"/> placeholder, such as <c>date</c>. Empty otherwise.</summary>
        public string FormatterType { get; }

        /// <summary>The style text a custom formatter receives, such as <c>short</c>. Empty when there is none.</summary>
        public string FormatterStyle { get; }
    }
}
