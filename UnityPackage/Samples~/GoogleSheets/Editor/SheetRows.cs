using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>
    /// Turns exported tables into sheets and sheets back into imported files, through the package's CSV format both
    /// ways, so a sheet has exactly the columns, keys and escaping a CSV export has, and pulls are read and checked like
    /// any CSV import.
    /// </summary>
    public static class SheetRows
    {
        /// <summary>Returns every table of <paramref name="book"/> as a sheet named after it.</summary>
        public static List<SheetData> FromBook(ExportBook book)
        {
            List<SheetData> sheets = new(book.Tables.Count);
            for (int t = 0; t < book.Tables.Count; t++)
            {
                ExportTable table = book.Tables[t];
                List<List<string>> records = ParseCsv(Encoding.UTF8.GetString(CsvExchange.Write(book, table)));
                RowData[] rows = new RowData[records.Count];
                for (int r = 0; r < rows.Length; r++)
                {
                    rows[r] = new RowData { cells = records[r].ToArray() };
                }
                sheets.Add(new SheetData { name = table.Name, rows = rows });
            }
            return sheets;
        }

        /// <summary>Returns <paramref name="sheet"/> read as the CSV file of the table it's named after.</summary>
        public static ImportedFile ToImportedFile(SheetData sheet)
        {
            StringBuilder csv = new();
            RowData[] rows = sheet.rows ?? new RowData[0];
            for (int r = 0; r < rows.Length; r++)
            {
                string[] cells = rows[r]?.cells ?? new string[0];
                for (int c = 0; c < cells.Length; c++)
                {
                    if (c > 0)
                    {
                        csv.Append(',');
                    }
                    // Every cell quoted, so separators, quotes and line breaks in it stay text.
                    csv.Append('"').Append((cells[c] ?? string.Empty).Replace("\"", "\"\"")).Append('"');
                }
                csv.Append("\r\n");
            }
            return CsvExchange.Read($"{sheet.name}.csv", Encoding.UTF8.GetBytes(csv.ToString()));
        }

        /// <summary>Reads the CSV the package writes: comma separated, quoted where needed, rows ending with CRLF, after a byte order mark.</summary>
        private static List<List<string>> ParseCsv(string text)
        {
            List<List<string>> records = new();
            List<string> record = new();
            StringBuilder field = new();
            bool isQuoted = false;
            int start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
            for (int i = start; i < text.Length; i++)
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
                    continue;
                }
                switch (character)
                {
                    case '"':
                        isQuoted = true;
                        break;
                    case ',':
                        record.Add(field.ToString());
                        field.Clear();
                        break;
                    case '\r':
                        break;
                    case '\n':
                        record.Add(field.ToString());
                        field.Clear();
                        records.Add(record);
                        record = new List<string>();
                        break;
                    default:
                        field.Append(character);
                        break;
                }
            }
            if (field.Length > 0 || record.Count > 0)
            {
                record.Add(field.ToString());
                records.Add(record);
            }
            return records;
        }
    }
}
