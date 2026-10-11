using reromanlee.ReactiveLocalizer.Messages;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// ICU message text for tools that write table files themselves, such as a migration from another localization
    /// system: plain text quoted so it shows as it is, and messages checked the way imports check them.
    /// </summary>
    public static class MessageText
    {
        /// <summary>
        /// Returns a message that shows <paramref name="text"/> exactly: braces, and apostrophes next to them, go in
        /// quotes, so <c>Press {0}</c> stays text instead of becoming an argument.
        /// </summary>
        public static string Quote(string text) => MessageQuoting.Quote(text);

        /// <summary>
        /// Returns message text that shows <paramref name="text"/> exactly where a message continues after it, such as
        /// before an argument: a final apostrophe is doubled too, so it can't join the brace that follows.
        /// </summary>
        public static string Quote(string text, bool isFollowedByArgument) => MessageQuoting.Quote(text, false, isFollowedByArgument);

        /// <summary>Returns whether <paramref name="message"/> is a valid ICU message, and the first error when it isn't.</summary>
        /// <param name="message">The message, with its escapes resolved.</param>
        /// <param name="problem">The first error and the index it's at; null for a valid message.</param>
        public static bool IsValid(string message, out string problem)
        {
            ParsedMessage parsed = MessageParser.Parse(message);
            for (int i = 0; i < parsed.Issues.Count; i++)
            {
                if (parsed.Issues[i].Severity == Documents.IssueSeverity.Error)
                {
                    problem = $"at {parsed.Issues[i].Index}: {parsed.Issues[i].Message}";
                    return false;
                }
            }
            problem = null;
            return true;
        }
    }
}
