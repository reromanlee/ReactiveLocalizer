namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>Literal text, with its quoting already resolved: <c>'{'</c> is stored as <c>{</c>.</summary>
    internal sealed class TextPart : MessagePart
    {
        public TextPart(int position, string text) : base(position)
        {
            Text = text;
        }

        public string Text { get; }
    }
}
