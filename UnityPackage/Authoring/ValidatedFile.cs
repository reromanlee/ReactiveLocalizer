using reromanlee.ReactiveLocalizer.Documents;
using System;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>One file of a table, as the validator reads it.</summary>
    public sealed class ValidatedFile
    {
        /// <summary>Creates a file read from <paramref name="path"/>.</summary>
        /// <param name="path">Where the file is, as problems name it.</param>
        /// <param name="languageName">The language the file is in.</param>
        /// <param name="document">The file, read.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
        public ValidatedFile(string path, string languageName, TableDocument document)
        {
            Path = path ?? string.Empty;
            LanguageName = languageName;
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }

        /// <summary>Where the file is, as problems name it.</summary>
        public string Path { get; }

        /// <summary>The language the file is in.</summary>
        public string LanguageName { get; }

        /// <summary>The file, read.</summary>
        public TableDocument Document { get; }
    }
}
