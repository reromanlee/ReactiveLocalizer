using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>What an export holds: which tables, which translation languages, and which of their entries.</summary>
    public sealed class ExportOptions
    {
        /// <summary>The tables to export, by name; null exports every table.</summary>
        public IReadOnlyCollection<string> Tables { get; set; }

        /// <summary>
        /// The translation languages to export, by name; null exports every language but the source, which every export
        /// holds as the text to translate from.
        /// </summary>
        public IReadOnlyCollection<string> Languages { get; set; }

        /// <summary>Which entries to export.</summary>
        public ExportedEntries Entries { get; set; }
    }
}
