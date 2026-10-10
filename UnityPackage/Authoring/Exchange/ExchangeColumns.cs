using System;
using System.IO;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// The columns spreadsheet formats share: the key, written <c>Table.Key</c> as everywhere else, the context and the
    /// maximum length for translators, then one column per language headed by its name.
    /// </summary>
    internal static class ExchangeColumns
    {
        public const string Key = "Key";
        public const string Context = "Context";
        public const string MaximumLength = "Maximum length";

        /// <summary>Whether a header names the key column.</summary>
        public static bool IsKey(string header) => string.Equals(header, Key, StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether a header names a column that only informs translators, which imports leave alone.</summary>
        public static bool IsInformation(string header) =>
            string.Equals(header, Context, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header, MaximumLength, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(header, "MaximumLength", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reads a key cell. <c>Shop.Purchase</c> names its table; a plain key belongs to <paramref name="defaultTable"/>,
        /// the table the file or sheet is named after.
        /// </summary>
        public static void ParseKey(string cell, string defaultTable, out string tableName, out string key)
        {
            string trimmed = cell.Trim();
            int separator = trimmed.IndexOf('.');
            if (separator < 0)
            {
                tableName = defaultTable ?? string.Empty;
                key = trimmed;
                return;
            }
            tableName = trimmed.Substring(0, separator);
            key = trimmed.Substring(separator + 1);
        }

        /// <summary>Returns the table a file is named after: its name up to the first dot, as in <c>Shop.csv</c>.</summary>
        public static string GetTableFromFileName(string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName ?? string.Empty);
            int separator = name.IndexOf('.');
            return separator < 0 ? name : name.Substring(0, separator);
        }

        /// <summary>Returns <paramref name="text"/> with every line break as <c>\n</c>, as table files hold them.</summary>
        public static string NormalizeLineBreaks(string text)
        {
            return text.IndexOf('\r') < 0 ? text : text.Replace("\r\n", "\n").Replace('\r', '\n');
        }
    }
}
