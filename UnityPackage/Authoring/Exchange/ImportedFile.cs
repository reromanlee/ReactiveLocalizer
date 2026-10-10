using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// What an exchange file holds, read without knowing the catalog yet: its languages, its rows, and the problems
    /// reading found, each with its location.
    /// </summary>
    public sealed class ImportedFile
    {
        /// <summary>Creates an empty file named <paramref name="name"/>.</summary>
        public ImportedFile(string name)
        {
            Name = name ?? string.Empty;
        }

        /// <summary>The file's name, as problems name it.</summary>
        public string Name { get; }

        /// <summary>The catalog the file names, as XLIFF and LLM files do; null when it names none.</summary>
        public string CatalogName { get; set; }

        /// <summary>The languages the file holds texts in.</summary>
        public List<ImportedLanguage> Languages { get; } = new();

        /// <summary>The entries the file holds, in file order.</summary>
        public List<ImportedRow> Rows { get; } = new();

        /// <summary>What reading the file found wrong, each with its location.</summary>
        public List<string> Problems { get; } = new();

        /// <summary>Returns the language labeled <paramref name="label"/>, ignoring case, adding it when the file has none yet.</summary>
        internal int GetOrAddLanguage(string label, bool isSourceOnly, params string[] candidates)
        {
            for (int i = 0; i < Languages.Count; i++)
            {
                if (Languages[i].IsSourceOnly == isSourceOnly && string.Equals(Languages[i].Label, label, System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            Languages.Add(new ImportedLanguage(label, isSourceOnly, candidates));
            return Languages.Count - 1;
        }
    }
}
