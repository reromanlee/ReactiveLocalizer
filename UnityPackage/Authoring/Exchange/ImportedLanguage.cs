using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// A language as an exchange file names it: a column of a spreadsheet, or the source or target language of an XLIFF
    /// file. Which catalog language it is gets decided when the catalog is known, and a person can change that.
    /// </summary>
    public sealed class ImportedLanguage
    {
        /// <summary>Creates a language the file names <paramref name="label"/>.</summary>
        /// <param name="label">How the file names it, as the import shows it.</param>
        /// <param name="isSourceOnly">Whether the file holds in it only the text translations were made from.</param>
        /// <param name="candidates">Language names or culture tags that may identify it, the most telling first.</param>
        public ImportedLanguage(string label, bool isSourceOnly, params string[] candidates)
        {
            Label = label ?? string.Empty;
            IsSourceOnly = isSourceOnly;
            Candidates = candidates ?? Array.Empty<string>();
        }

        /// <summary>How the file names the language, such as a column header.</summary>
        public string Label { get; }

        /// <summary>
        /// Whether the file holds in this language only the text its translations were made from, as XLIFF's
        /// <c>&lt;source&gt;</c> does, so its texts are never imported as translations.
        /// </summary>
        public bool IsSourceOnly { get; }

        /// <summary>Language names or culture tags that may identify the language, the most telling first.</summary>
        public IReadOnlyList<string> Candidates { get; }

        /// <summary>
        /// The catalog language the file's texts in this language belong to, or null to skip them.
        /// <see cref="ImportPlanner.ResolveLanguages"/> fills it in, and a person can change it.
        /// </summary>
        public string LanguageName { get; set; }

        /// <inheritdoc/>
        public override string ToString() => Label;
    }
}
