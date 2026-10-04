using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// A named value read from a localization file: an <c>@attribute</c>, a table setting, or a field of a catalog's
    /// language section.
    /// </summary>
    public readonly struct DocumentProperty
    {
        /// <summary>Creates a property read from a 1-based <paramref name="line"/>.</summary>
        public DocumentProperty(string name, string value, int line, IReadOnlyList<string> comments)
        {
            Name = name;
            Value = value;
            Line = line;
            Comments = comments ?? Array.Empty<string>();
        }

        /// <summary>Name of the property, in its canonical spelling when the package knows it.</summary>
        public string Name { get; }

        /// <summary>Value of the property, trimmed. Empty when the line gave none.</summary>
        public string Value { get; }

        /// <summary>Line the property was read from, counted from 1.</summary>
        public int Line { get; }

        /// <summary>The <c>#</c> comments written right above the property, without their <c>#</c>. Never null.</summary>
        public IReadOnlyList<string> Comments { get; }
    }
}
