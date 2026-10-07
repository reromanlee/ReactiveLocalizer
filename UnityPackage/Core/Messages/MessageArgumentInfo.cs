namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>An argument of a parsed message: its name as first written, and every way the message uses it.</summary>
    internal sealed class MessageArgumentInfo
    {
        public MessageArgumentInfo(string name, ulong hash, int position)
        {
            Name = name;
            Hash = hash;
            Position = position;
        }

        public string Name { get; }

        public ulong Hash { get; }

        /// <summary>Where the argument is first used in the message text.</summary>
        public int Position { get; }

        public ArgumentUses Uses { get; set; }

        /// <summary>The kind generated code gives the argument's parameter.</summary>
        public MessageArgumentKind Kind
        {
            get
            {
                if ((Uses & ArgumentUses.Numeric) != 0)
                {
                    return MessageArgumentKind.Number;
                }
                return (Uses & ArgumentUses.Select) != 0 ? MessageArgumentKind.Keyword : MessageArgumentKind.Value;
            }
        }
    }
}
