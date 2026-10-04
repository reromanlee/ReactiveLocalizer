using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>One <c>[Language]</c> section of a catalog file, with the fields written under it.</summary>
    public sealed class CatalogDocumentLanguage
    {
        /// <summary>Creates a section read from a 1-based <paramref name="line"/>.</summary>
        public CatalogDocumentLanguage(string name, IReadOnlyList<DocumentProperty> fields, IReadOnlyList<string> comments, int line)
        {
            Name = name;
            Fields = fields ?? Array.Empty<DocumentProperty>();
            Comments = comments ?? Array.Empty<string>();
            Line = line;
        }

        /// <summary>Name of the language, as written between the brackets.</summary>
        public string Name { get; }

        /// <summary>The section's <c>Field = Value</c> lines, such as <c>DisplayName</c>, in file order. Never null.</summary>
        public IReadOnlyList<DocumentProperty> Fields { get; }

        /// <summary>The <c>#</c> comments written right above the section header. Never null.</summary>
        public IReadOnlyList<string> Comments { get; }

        /// <summary>Line of the section header, counted from 1.</summary>
        public int Line { get; }

        /// <summary>Returns the field named <paramref name="name"/>, ignoring case.</summary>
        public bool TryGetField(string name, out DocumentProperty field)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                if (string.Equals(Fields[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    field = Fields[i];
                    return true;
                }
            }
            field = default;
            return false;
        }
    }
}
