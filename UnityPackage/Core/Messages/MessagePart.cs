namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>One part of a parsed message: text, a placeholder, a plural, a select, or the number sign of a plural form.</summary>
    internal abstract class MessagePart
    {
        protected MessagePart(int position)
        {
            Position = position;
        }

        /// <summary>Where the part starts in the message text, counted from 0.</summary>
        public int Position { get; }
    }
}
