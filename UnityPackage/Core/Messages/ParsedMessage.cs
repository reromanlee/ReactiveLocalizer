using reromanlee.ReactiveLocalizer.Documents;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// A message as <see cref="MessageParser"/> read it: its parts, its arguments, and every problem found, each at its
    /// position in the text.
    /// </summary>
    internal sealed class ParsedMessage
    {
        public ParsedMessage(MessageBody body, IReadOnlyList<MessageArgumentInfo> arguments, IReadOnlyList<MessageIssue> issues)
        {
            Body = body;
            Arguments = arguments;
            Issues = issues;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == IssueSeverity.Error)
                {
                    HasErrors = true;
                }
            }
            if (!HasErrors && arguments.Count == 0)
            {
                LiteralText = JoinText(body);
            }
        }

        public MessageBody Body { get; }

        /// <summary>The message's arguments, sorted by name ignoring case, which is the order generated parameters take.</summary>
        public IReadOnlyList<MessageArgumentInfo> Arguments { get; }

        public IReadOnlyList<MessageIssue> Issues { get; }

        /// <summary>Whether any problem is an error, which makes the message unusable as written.</summary>
        public bool HasErrors { get; }

        public bool HasArguments => Arguments.Count > 0;

        /// <summary>The text a valid message without arguments shows, with its quoting resolved; null for any other message.</summary>
        public string LiteralText { get; }

        /// <summary>Returns the argument with <paramref name="hash"/>.</summary>
        public bool TryGetArgument(ulong hash, out MessageArgumentInfo argument)
        {
            for (int i = 0; i < Arguments.Count; i++)
            {
                if (Arguments[i].Hash == hash)
                {
                    argument = Arguments[i];
                    return true;
                }
            }
            argument = null;
            return false;
        }

        private static string JoinText(MessageBody body)
        {
            if (body.Parts.Count == 1 && body.Parts[0] is TextPart single)
            {
                return single.Text;
            }
            StringBuilder builder = new();
            for (int i = 0; i < body.Parts.Count; i++)
            {
                if (body.Parts[i] is TextPart text)
                {
                    builder.Append(text.Text);
                }
            }
            return builder.ToString();
        }
    }
}
