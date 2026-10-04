using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The rule every catalog, table, entry and language name follows: ASCII letters, digits and underscores,
    /// starting with a letter, at most <see cref="MaximumLength"/> characters. It keeps every name usable as a C#
    /// identifier and inside a file name on every platform.
    /// </summary>
    internal static class NameRules
    {
        /// <summary>Longest name accepted: far above any real name, and short enough for every file system.</summary>
        public const int MaximumLength = 128;

        /// <summary>The rule in words, for the messages that report a broken name.</summary>
        public const string Description =
            "a name starts with an ASCII letter and continues with ASCII letters, digits and underscores, up to 128 characters";

        /// <summary>Returns whether <paramref name="name"/> follows the rule. An empty name never does.</summary>
        public static bool IsValid(ReadOnlySpan<char> name)
        {
            if (name.IsEmpty || name.Length > MaximumLength || !IsAsciiLetter(name[0]))
            {
                return false;
            }
            for (int i = 1; i < name.Length; i++)
            {
                char character = name[i];
                if (!IsAsciiLetter(character) && !IsAsciiDigit(character) && character != '_')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Throws when <paramref name="name"/> breaks the rule, naming the parameter it came from.</summary>
        /// <exception cref="ArgumentException"><paramref name="name"/> breaks the rule.</exception>
        public static void ThrowIfInvalid(string name, string parameterName)
        {
            if (!IsValid(name))
            {
                throw new ArgumentException($"'{name}' is not a valid name: {Description}.", parameterName);
            }
        }

        private static bool IsAsciiLetter(char character) => (uint)((character | 0x20) - 'a') <= 'z' - 'a';

        private static bool IsAsciiDigit(char character) => (uint)(character - '0') <= 9;
    }
}
