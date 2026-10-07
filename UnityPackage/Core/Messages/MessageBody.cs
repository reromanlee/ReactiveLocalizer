using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>A run of message parts: the whole message, or the text of one plural or select form.</summary>
    internal sealed class MessageBody
    {
        public MessageBody(IReadOnlyList<MessagePart> parts)
        {
            Parts = parts;
        }

        public IReadOnlyList<MessagePart> Parts { get; }
    }
}
