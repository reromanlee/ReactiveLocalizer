using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// The formatters a localizer has registered, by type name ignoring case. It never changes after it is created;
    /// registering a formatter creates a new table, which the localizer publishes in one step.
    /// </summary>
    internal sealed class FormatterTable
    {
        public static readonly FormatterTable Empty = new(new Dictionary<ulong, ArgumentFormatter>());

        private readonly Dictionary<ulong, ArgumentFormatter> _formatters;

        private FormatterTable(Dictionary<ulong, ArgumentFormatter> formatters)
        {
            _formatters = formatters;
        }

        public bool TryGet(ulong typeHash, out ArgumentFormatter formatter) => _formatters.TryGetValue(typeHash, out formatter);

        /// <summary>Returns a table with <paramref name="formatter"/> registered for <paramref name="type"/>; null removes it.</summary>
        public FormatterTable With(string type, ArgumentFormatter formatter)
        {
            Dictionary<ulong, ArgumentFormatter> formatters = new(_formatters);
            ulong hash = Hashing.ComputeNameHash(type);
            if (formatter == null)
            {
                formatters.Remove(hash);
            }
            else
            {
                formatters[hash] = formatter;
            }
            return new FormatterTable(formatters);
        }

        /// <summary>Returns whether <paramref name="type"/> names a type the message syntax already defines.</summary>
        public static bool IsBuiltInType(string type)
        {
            return string.Equals(type, "number", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "plural", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "selectordinal", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "select", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "choice", StringComparison.OrdinalIgnoreCase);
        }
    }
}
