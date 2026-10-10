using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// Renders one message text on its own, exactly as a table would show it in a language, with no table compiled:
    /// editors preview text while it is typed, with sample arguments.
    /// </summary>
    internal static class MessagePreview
    {
        /// <summary>
        /// Renders <paramref name="text"/> as <paramref name="language"/> of <paramref name="catalog"/> shows it with
        /// <paramref name="arguments"/>. Returns null, with <paramref name="problem"/> saying why, when the text has errors.
        /// </summary>
        public static string Render(CatalogInfo catalog, LanguageInfo language, string text, MessageArgument[] arguments, out string problem)
        {
            ParsedMessage parsed = MessageParser.Parse(text ?? string.Empty);
            if (parsed.HasErrors)
            {
                problem = FindError(parsed);
                return null;
            }
            problem = null;
            if (!parsed.HasArguments)
            {
                return parsed.LiteralText;
            }
            List<int> program = new();
            CharacterPool characters = new();
            int start = MessageCompiler.Compile(parsed, program, characters);
            EntryMessage message = new(default, arguments);
            TextBuilder output = new(new char[256]);
            MessageProblems problems = default;
            try
            {
                MessageRenderer.Render(program.ToArray(), characters.ToCharacters(), start, message, catalog.GetFormat(language),
                    FormatterTable.Empty, language, ref output, ref problems);
                return output.ToString();
            }
            finally
            {
                output.Dispose();
            }
        }

        /// <summary>
        /// Returns the arguments <paramref name="text"/> takes, with the kind of value each needs, in the order they
        /// first appear; empty for plain text or text with errors.
        /// </summary>
        public static IReadOnlyList<(string Name, MessageArgumentKind Kind)> GetArguments(string text)
        {
            ParsedMessage parsed = MessageParser.Parse(text ?? string.Empty);
            if (parsed.HasErrors || !parsed.HasArguments)
            {
                return Array.Empty<(string, MessageArgumentKind)>();
            }
            List<MessageArgumentInfo> arguments = new(parsed.Arguments);
            arguments.Sort((left, right) => left.Position.CompareTo(right.Position));
            (string Name, MessageArgumentKind Kind)[] found = new (string, MessageArgumentKind)[arguments.Count];
            for (int i = 0; i < found.Length; i++)
            {
                found[i] = (arguments[i].Name, arguments[i].Kind);
            }
            return found;
        }

        /// <summary>Makes an argument of a sample value someone typed: a number when it reads as one, written the invariant way, else text.</summary>
        public static MessageArgument CreateArgument(string name, string sample)
        {
            sample = sample?.Trim() ?? string.Empty;
            if (decimal.TryParse(sample, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal number))
            {
                return new MessageArgument(name, (MessageNumber)number);
            }
            return new MessageArgument(name, sample);
        }

        private static string FindError(ParsedMessage parsed)
        {
            for (int i = 0; i < parsed.Issues.Count; i++)
            {
                if (parsed.Issues[i].Severity == IssueSeverity.Error)
                {
                    return parsed.Issues[i].Message;
                }
            }
            return "The message has errors.";
        }
    }
}
