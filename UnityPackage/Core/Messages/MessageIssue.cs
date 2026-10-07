using reromanlee.ReactiveLocalizer.Documents;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>A problem found in a message, at its position within the message text.</summary>
    internal readonly struct MessageIssue
    {
        public MessageIssue(IssueSeverity severity, int index, string message)
        {
            Severity = severity;
            Index = index;
            Message = message;
        }

        public IssueSeverity Severity { get; }

        /// <summary>Position of the problem in the message text, counted from 0.</summary>
        public int Index { get; }

        public string Message { get; }
    }
}
