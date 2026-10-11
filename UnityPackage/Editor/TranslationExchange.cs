using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The editor's exchange with translators, for project tools and samples that move translations in their own way,
    /// such as the Google Sheets sample: what an export holds, and the import preview every format goes through.
    /// </summary>
    public static class TranslationExchange
    {
        /// <summary>
        /// Returns what an export of <paramref name="catalog"/> holds with <paramref name="options"/>: its tables,
        /// rows and the state of every translation, which <see cref="CsvExchange"/>, <see cref="XlsxExchange"/> and the
        /// other formats write. Null when the project has no usable catalog of that name.
        /// </summary>
        /// <param name="catalog">The catalog; empty for the default catalog.</param>
        /// <param name="options">What to export; its defaults export every table, language and entry.</param>
        public static ExportBook Collect(CatalogKey catalog, ExportOptions options)
        {
            IndexedCatalog indexed = catalog.IsEmpty ? CatalogIndex.DefaultCatalog : CatalogIndex.Find(catalog);
            return indexed?.Info != null ? LocalizationExchange.Collect(indexed, options ?? new ExportOptions()) : null;
        }

        /// <summary>
        /// Opens the import preview for <paramref name="files"/>, read with any format's reader, such as
        /// <see cref="CsvExchange.Read"/>: it lists every change before anything is written, and applies them as one
        /// step Undo can take back.
        /// </summary>
        public static void PreviewImport(IReadOnlyList<ImportedFile> files)
        {
            if (files != null && files.Count > 0)
            {
                ImportWindow.Show(files);
            }
        }
    }
}
