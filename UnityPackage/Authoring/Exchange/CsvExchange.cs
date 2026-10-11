using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// CSV exchange, one file per table, as RFC 4180 describes it: the key, the context and maximum length for
    /// translators, the source text, then a column per translation language headed by its name.
    /// </summary>
    /// <remarks>
    /// Files are written as UTF-8 with a byte order mark, which Excel needs to show anything beyond Latin text, and
    /// with CRLF between rows. Reading takes comma, semicolon or tab separated files, as spreadsheet applications save
    /// them by region, in UTF-8 or UTF-16. Keys are written <c>Shop.Purchase</c>, so renaming a file changes nothing; a
    /// key without its table belongs to the table the file is named after.
    /// </remarks>
    public static class CsvExchange
    {
        private static readonly UTF8Encoding Utf8WithMark = new(true);
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);
        private static readonly char[] QuotedCharacters = { ',', '"', '\r', '\n' };
        private static readonly string[] SpreadsheetErrors = { "#NAME?", "#VALUE!", "#REF!", "#DIV/0!", "#N/A", "#NUM!", "#NULL!", "#ERROR!" };

        /// <summary>Returns <paramref name="table"/> of <paramref name="book"/> as the bytes of a CSV file.</summary>
        public static byte[] Write(ExportBook book, ExportTable table)
        {
            StringBuilder builder = new();
            AppendField(builder, ExchangeColumns.Key, false);
            AppendField(builder, ExchangeColumns.Context, true);
            AppendField(builder, ExchangeColumns.MaximumLength, true);
            AppendField(builder, book.SourceLanguage.Name, true);
            for (int l = 0; l < book.Languages.Count; l++)
            {
                AppendField(builder, book.Languages[l].Name, true);
            }
            builder.Append("\r\n");
            for (int r = 0; r < table.Rows.Count; r++)
            {
                ExportRow row = table.Rows[r];
                AppendField(builder, $"{table.Name}.{row.Key}", false);
                AppendField(builder, row.Context, true);
                AppendField(builder, row.MaximumLength > 0 ? row.MaximumLength.ToString(CultureInfo.InvariantCulture) : string.Empty, true);
                AppendField(builder, row.SourceText, true);
                for (int l = 0; l < row.Cells.Count; l++)
                {
                    AppendField(builder, row.Cells[l].Text ?? string.Empty, true);
                }
                builder.Append("\r\n");
            }
            byte[] mark = Utf8WithMark.GetPreamble();
            byte[] text = Utf8WithMark.GetBytes(builder.ToString());
            byte[] bytes = new byte[mark.Length + text.Length];
            Buffer.BlockCopy(mark, 0, bytes, 0, mark.Length);
            Buffer.BlockCopy(text, 0, bytes, mark.Length, text.Length);
            return bytes;
        }

        /// <summary>Reads the CSV file <paramref name="fileName"/>, whose bytes are <paramref name="data"/>. Never throws.</summary>
        public static ImportedFile Read(string fileName, byte[] data)
        {
            ImportedFile file = new(fileName);
            if (!TryDecode(data ?? Array.Empty<byte>(), out string text))
            {
                file.Problems.Add($"{fileName}: it isn't UTF-8 or UTF-16 text, so letters beyond ASCII would be lost. In Excel, save it as 'CSV UTF-8'.");
                return file;
            }
            List<List<string>> records = Parse(text, DetectSeparator(text), out bool isCut);
            if (isCut)
            {
                file.Problems.Add($"{fileName}: a quoted cell never ends, so the file is cut short; its last row may be incomplete.");
            }
            int header = records.FindIndex(record => !IsBlank(record));
            if (header < 0)
            {
                return file;
            }
            ReadRows(file, records, header);
            return file;
        }

        private static void ReadRows(ImportedFile file, List<List<string>> records, int header)
        {
            List<string> headers = records[header];
            int keyColumn = -1;
            // The file's language each column holds, or -1 for one the import leaves alone.
            int[] languageOf = new int[headers.Count];
            for (int c = 0; c < headers.Count; c++)
            {
                string label = headers[c].Trim();
                languageOf[c] = -1;
                if (keyColumn < 0 && ExchangeColumns.IsKey(label))
                {
                    keyColumn = c;
                }
                else if (label.Length > 0 && !ExchangeColumns.IsInformation(label))
                {
                    file.Languages.Add(new ImportedLanguage(label, false, label));
                    languageOf[c] = file.Languages.Count - 1;
                }
            }
            if (keyColumn < 0)
            {
                file.Problems.Add($"{file.Name}: it has no '{ExchangeColumns.Key}' column, so its rows can't be matched with entries.");
                return;
            }
            string defaultTable = ExchangeColumns.GetTableFromFileName(file.Name);
            for (int r = header + 1; r < records.Count; r++)
            {
                List<string> record = records[r];
                if (IsBlank(record))
                {
                    continue;
                }
                string location = $"{file.Name}, row {r + 1}";
                string keyCell = keyColumn < record.Count ? record[keyColumn] : string.Empty;
                if (keyCell.Trim().Length == 0)
                {
                    file.Problems.Add($"{location}: it has text but no key, so it is skipped.");
                    continue;
                }
                string[] texts = new string[file.Languages.Count];
                for (int c = 0; c < record.Count && c < languageOf.Length; c++)
                {
                    if (languageOf[c] < 0 || record[c].Length == 0)
                    {
                        continue;
                    }
                    if (IsSpreadsheetError(record[c]))
                    {
                        file.Problems.Add($"{location}: its {file.Languages[languageOf[c]].Label} cell holds {record[c]}, a spreadsheet's error for a text it took for a formula, so it is skipped. Spreadsheets take texts starting with = + - or @ for formulas in CSV files; exchange XLSX instead.");
                        continue;
                    }
                    texts[languageOf[c]] = ExchangeColumns.NormalizeLineBreaks(record[c]);
                }
                ExchangeColumns.ParseKey(keyCell, defaultTable, out string tableName, out string key);
                file.Rows.Add(new ImportedRow(location, tableName, key, texts));
            }
        }

        private static void AppendField(StringBuilder builder, string value, bool isSeparated)
        {
            if (isSeparated)
            {
                builder.Append(',');
            }
            value ??= string.Empty;
            bool isQuoted = value.Length > 0 && (value.IndexOfAny(QuotedCharacters) >= 0 ||
                                                 char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]));
            if (!isQuoted)
            {
                builder.Append(value);
                return;
            }
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '"')
                {
                    builder.Append('"');
                }
                builder.Append(value[i]);
            }
            builder.Append('"');
        }

        private static bool TryDecode(byte[] data, out string text)
        {
            if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            {
                text = Encoding.Unicode.GetString(data, 2, data.Length - 2);
                return true;
            }
            if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            {
                text = Encoding.BigEndianUnicode.GetString(data, 2, data.Length - 2);
                return true;
            }
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            try
            {
                text = StrictUtf8.GetString(data, start, data.Length - start);
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                return false;
            }
        }

        /// <summary>Returns the separator the header row uses most, outside quotes: comma, semicolon or tab, comma when unsure.</summary>
        internal static char DetectSeparator(string text)
        {
            int commas = 0;
            int semicolons = 0;
            int tabs = 0;
            bool isQuoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (character == '"')
                {
                    isQuoted = !isQuoted;
                }
                else if (!isQuoted && (character == '\r' || character == '\n'))
                {
                    break;
                }
                else if (!isQuoted)
                {
                    commas += character == ',' ? 1 : 0;
                    semicolons += character == ';' ? 1 : 0;
                    tabs += character == '\t' ? 1 : 0;
                }
            }
            if (semicolons > commas && semicolons >= tabs)
            {
                return ';';
            }
            return tabs > commas && tabs > semicolons ? '\t' : ',';
        }

        /// <summary>Splits CSV text into records of fields. A quote inside an unquoted field is kept as text.</summary>
        private static List<List<string>> Parse(string text, char separator, out bool isCut)
        {
            List<List<string>> records = new();
            List<string> record = new();
            StringBuilder field = new();
            bool isQuoted = false;
            bool wasQuoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                if (isQuoted)
                {
                    if (character != '"')
                    {
                        field.Append(character);
                    }
                    else if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        isQuoted = false;
                    }
                }
                else if (character == '"' && field.Length == 0 && !wasQuoted)
                {
                    isQuoted = true;
                    wasQuoted = true;
                }
                else if (character == separator)
                {
                    record.Add(field.ToString());
                    field.Clear();
                    wasQuoted = false;
                }
                else if (character == '\r' || character == '\n')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    wasQuoted = false;
                    records.Add(record);
                    record = new List<string>();
                    if (character == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else
                {
                    field.Append(character);
                }
            }
            isCut = isQuoted;
            if (field.Length > 0 || record.Count > 0 || wasQuoted)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }

        /// <summary>Whether a cell holds the error a spreadsheet application shows for a formula it couldn't calculate.</summary>
        private static bool IsSpreadsheetError(string cell)
        {
            if (cell.Length < 4 || cell[0] != '#')
            {
                return false;
            }
            for (int i = 0; i < SpreadsheetErrors.Length; i++)
            {
                if (string.Equals(cell, SpreadsheetErrors[i], StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsBlank(List<string> record)
        {
            for (int i = 0; i < record.Count; i++)
            {
                if (record[i].Trim().Length > 0)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
