namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>One branch of a select, chosen when the argument equals its keyword exactly.</summary>
    internal sealed class SelectCase
    {
        public SelectCase(int position, string keyword, MessageBody body)
        {
            Position = position;
            Keyword = keyword;
            Body = body;
        }

        public int Position { get; }

        public string Keyword { get; }

        public MessageBody Body { get; }
    }
}
