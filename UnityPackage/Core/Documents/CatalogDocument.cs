using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// A catalog file, <c>Localization.catalog</c>, read into its parts: the attributes at its top, such as
    /// <c>@source</c>, one section per language, and every problem found while reading it.
    /// </summary>
    /// <remarks>
    /// Reading never throws, and checks only the syntax. Whether the source language exists or the fallbacks form a
    /// loop is checked when the catalog is compiled.
    /// </remarks>
    public sealed class CatalogDocument
    {
        internal CatalogDocument(IReadOnlyList<string> headerComments, IReadOnlyList<DocumentProperty> attributes,
            IReadOnlyList<CatalogDocumentLanguage> languages, IReadOnlyList<string> trailingComments,
            IReadOnlyList<DocumentIssue> issues)
        {
            HeaderComments = headerComments;
            Attributes = attributes;
            Languages = languages;
            TrailingComments = trailingComments;
            Issues = issues;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == IssueSeverity.Error)
                {
                    HasErrors = true;
                    break;
                }
            }
        }

        /// <summary>Comments that open the file, separated from what follows by a blank line.</summary>
        public IReadOnlyList<string> HeaderComments { get; }

        /// <summary>The attributes at the top of the file, such as <c>@source</c>, in file order.</summary>
        public IReadOnlyList<DocumentProperty> Attributes { get; }

        /// <summary>The language sections, in file order. Sections with errors are not among them.</summary>
        public IReadOnlyList<CatalogDocumentLanguage> Languages { get; }

        /// <summary>Comments after the last section's fields, kept so that rewriting the file never drops them.</summary>
        public IReadOnlyList<string> TrailingComments { get; }

        /// <summary>Every problem found while reading, in file order.</summary>
        public IReadOnlyList<DocumentIssue> Issues { get; }

        /// <summary>Whether any issue is an <see cref="IssueSeverity.Error"/>.</summary>
        public bool HasErrors { get; }

        /// <summary>Reads a catalog file's text. A null or empty text is a catalog without languages.</summary>
        public static CatalogDocument Parse(string text) => new CatalogDocumentParser(text).Parse();

        /// <summary>Returns the attribute named <paramref name="name"/>, ignoring case.</summary>
        public bool TryGetAttribute(string name, out DocumentProperty attribute)
        {
            for (int i = 0; i < Attributes.Count; i++)
            {
                if (string.Equals(Attributes[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    attribute = Attributes[i];
                    return true;
                }
            }
            attribute = default;
            return false;
        }
    }
}
