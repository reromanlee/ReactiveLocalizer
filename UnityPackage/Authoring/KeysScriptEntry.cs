using reromanlee.ReactiveLocalizer.Messages;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// One entry as generated code shows it: a key field for plain text, or a method taking the arguments of its
    /// message, in alphabetical order, so rewording the source text never reorders them.
    /// </summary>
    public sealed class KeysScriptEntry
    {
        /// <summary>Creates the entry <paramref name="name"/> with <paramref name="arguments"/>; none makes it a plain key.</summary>
        public KeysScriptEntry(string name, IReadOnlyList<KeysScriptArgument> arguments = null) : this(name, arguments, false)
        {
        }

        private KeysScriptEntry(string name, IReadOnlyList<KeysScriptArgument> arguments, bool hasMessageErrors)
        {
            Name = name;
            Arguments = arguments ?? Array.Empty<KeysScriptArgument>();
            HasMessageErrors = hasMessageErrors;
        }

        /// <summary>Name of the entry.</summary>
        public string Name { get; }

        /// <summary>The arguments of the entry's message, sorted by name ignoring case. Empty for plain text.</summary>
        public IReadOnlyList<KeysScriptArgument> Arguments { get; }

        /// <summary>Whether the entry's message has arguments, which makes it a method.</summary>
        public bool HasArguments => Arguments.Count > 0;

        /// <summary>
        /// Whether the source text has message errors, so its arguments are unknown. The editor keeps the generated
        /// code as it was while any entry has them, so a typo in a text never breaks code that calls it.
        /// </summary>
        public bool HasMessageErrors { get; }

        /// <summary>
        /// Creates the entry <paramref name="name"/> from its source text. A message with errors becomes a plain key
        /// marked with <see cref="HasMessageErrors"/>.
        /// </summary>
        public static KeysScriptEntry FromSource(string name, string sourceText)
        {
            ParsedMessage message = MessageParser.Parse(sourceText);
            if (message.HasErrors || !message.HasArguments)
            {
                return new KeysScriptEntry(name, null, message.HasErrors);
            }
            KeysScriptArgument[] arguments = new KeysScriptArgument[message.Arguments.Count];
            for (int i = 0; i < arguments.Length; i++)
            {
                arguments[i] = new KeysScriptArgument(message.Arguments[i].Name, message.Arguments[i].Kind);
            }
            return new KeysScriptEntry(name, arguments);
        }
    }
}
