using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// XLSX exchange: one workbook, one sheet per table, with the columns CSV has. Written and read with no library,
    /// as a zip of SpreadsheetML parts.
    /// </summary>
    /// <remarks>
    /// The sheets keep the header row in view, wrap long texts, gray out the columns translators read but don't write,
    /// and tint outdated and unverified translations. Text cells are formatted as text, so a translation typed as
    /// <c>100</c> stays text. Reading takes what Excel, LibreOffice and Google Sheets save: shared or inline strings,
    /// rich text, and numbers. Keys are written <c>Shop.Purchase</c>, so renaming a sheet changes nothing; a key without
    /// its table belongs to the table the sheet is named after.
    /// </remarks>
    public static class XlsxExchange
    {
        /// <summary>The most characters a cell can hold in Excel.</summary>
        public const int MaximumCellLength = 32767;

        private const string MainNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        // Excel's "Strict Open XML" files name relationships in a namespace of their own.
        private const string StrictRelationshipsNamespace = "http://purl.oclc.org/ooxml/officeDocument/relationships";
        private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string XmlHeader = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n";
        private const int MaximumSheetNameLength = 31;

        // Cell styles, as indices into the stylesheet's cellXfs.
        private const int HeaderStyle = 1;
        private const int ReadOnlyStyle = 2;
        private const int TranslationStyle = 3;
        private const int OutdatedStyle = 4;

        private static readonly UTF8Encoding Utf8 = new(false);

        /// <summary>Returns <paramref name="book"/> as the bytes of an XLSX workbook. Cells too long for Excel are left empty and reported.</summary>
        /// <param name="book">What to export.</param>
        /// <param name="problems">Where cells that can't be written are reported.</param>
        public static byte[] Write(ExportBook book, List<string> problems)
        {
            List<KeyValuePair<string, byte[]>> parts = new();
            List<string> sheetNames = CreateSheetNames(book);
            StringBuilder types = new(XmlHeader);
            types.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
            types.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
            types.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
            types.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
            types.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (int i = 0; i < sheetNames.Count; i++)
            {
                types.Append("<Override PartName=\"/xl/worksheets/sheet").Append(i + 1)
                    .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            types.Append("</Types>");
            parts.Add(Part("[Content_Types].xml", types));

            parts.Add(Part("_rels/.rels", new StringBuilder(XmlHeader)
                .Append("<Relationships xmlns=\"").Append(PackageRelationshipsNamespace).Append("\">")
                .Append("<Relationship Id=\"rId1\" Type=\"").Append(RelationshipsNamespace).Append("/officeDocument\" Target=\"xl/workbook.xml\"/>")
                .Append("</Relationships>")));

            StringBuilder workbook = new(XmlHeader);
            workbook.Append("<workbook xmlns=\"").Append(MainNamespace).Append("\" xmlns:r=\"").Append(RelationshipsNamespace).Append("\"><sheets>");
            StringBuilder relationships = new(XmlHeader);
            relationships.Append("<Relationships xmlns=\"").Append(PackageRelationshipsNamespace).Append("\">");
            for (int i = 0; i < sheetNames.Count; i++)
            {
                workbook.Append("<sheet name=\"").Append(sheetNames[i]).Append("\" sheetId=\"").Append(i + 1).Append("\" r:id=\"rId").Append(i + 1).Append("\"/>");
                relationships.Append("<Relationship Id=\"rId").Append(i + 1).Append("\" Type=\"").Append(RelationshipsNamespace)
                    .Append("/worksheet\" Target=\"worksheets/sheet").Append(i + 1).Append(".xml\"/>");
            }
            workbook.Append("</sheets></workbook>");
            relationships.Append("<Relationship Id=\"rId").Append(sheetNames.Count + 1).Append("\" Type=\"").Append(RelationshipsNamespace)
                .Append("/styles\" Target=\"styles.xml\"/></Relationships>");
            parts.Add(Part("xl/workbook.xml", workbook));
            parts.Add(Part("xl/_rels/workbook.xml.rels", relationships));
            parts.Add(Part("xl/styles.xml", new StringBuilder(XmlHeader).Append(Stylesheet)));
            for (int i = 0; i < book.Tables.Count; i++)
            {
                parts.Add(Part($"xl/worksheets/sheet{i + 1}.xml", WriteSheet(book, book.Tables[i], problems)));
            }
            return ZipContainer.Write(parts);
        }

        /// <summary>Reads the workbook <paramref name="fileName"/>, whose bytes are <paramref name="data"/>. Never throws.</summary>
        public static ImportedFile Read(string fileName, byte[] data)
        {
            ImportedFile file = new(fileName);
            if (!ZipContainer.TryRead(data ?? Array.Empty<byte>(), out Dictionary<string, byte[]> parts, out string problem))
            {
                file.Problems.Add($"{fileName}: it can't be read as a workbook: {problem}.");
                return file;
            }
            try
            {
                ReadWorkbook(file, parts);
            }
            catch (XmlException exception)
            {
                file.Problems.Add($"{fileName}: it can't be read as a workbook: {exception.Message}");
            }
            return file;
        }

        private static StringBuilder WriteSheet(ExportBook book, ExportTable table, List<string> problems)
        {
            int columnCount = 4 + book.Languages.Count;
            StringBuilder sheet = new(XmlHeader);
            sheet.Append("<worksheet xmlns=\"").Append(MainNamespace).Append("\">");
            sheet.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            sheet.Append("<cols>");
            AppendColumn(sheet, 1, 1, 32, ReadOnlyStyle);
            AppendColumn(sheet, 2, 2, 40, ReadOnlyStyle);
            AppendColumn(sheet, 3, 3, 10, ReadOnlyStyle);
            AppendColumn(sheet, 4, 4, 50, ReadOnlyStyle);
            if (book.Languages.Count > 0)
            {
                AppendColumn(sheet, 5, columnCount, 50, TranslationStyle);
            }
            sheet.Append("</cols><sheetData>");
            sheet.Append("<row r=\"1\">");
            AppendText(sheet, 0, 1, ExchangeColumns.Key, HeaderStyle);
            AppendText(sheet, 1, 1, ExchangeColumns.Context, HeaderStyle);
            AppendText(sheet, 2, 1, ExchangeColumns.MaximumLength, HeaderStyle);
            AppendText(sheet, 3, 1, book.SourceLanguage.Name, HeaderStyle);
            for (int l = 0; l < book.Languages.Count; l++)
            {
                AppendText(sheet, 4 + l, 1, book.Languages[l].Name, HeaderStyle);
            }
            sheet.Append("</row>");
            for (int r = 0; r < table.Rows.Count; r++)
            {
                ExportRow row = table.Rows[r];
                int rowNumber = r + 2;
                string qualifiedKey = $"{table.Name}.{row.Key}";
                sheet.Append("<row r=\"").Append(rowNumber).Append("\">");
                AppendText(sheet, 0, rowNumber, qualifiedKey, ReadOnlyStyle);
                AppendChecked(sheet, 1, rowNumber, row.Context, ReadOnlyStyle, qualifiedKey, problems);
                if (row.MaximumLength > 0)
                {
                    AppendCellStart(sheet, 2, rowNumber, ReadOnlyStyle);
                    sheet.Append("><v>").Append(row.MaximumLength.ToString(CultureInfo.InvariantCulture)).Append("</v></c>");
                }
                AppendChecked(sheet, 3, rowNumber, row.SourceText, ReadOnlyStyle, qualifiedKey, problems);
                for (int l = 0; l < row.Cells.Count; l++)
                {
                    ExportCell cell = row.Cells[l];
                    int style = cell.State == TranslationState.Outdated || cell.State == TranslationState.Unverified ? OutdatedStyle : TranslationStyle;
                    AppendChecked(sheet, 4 + l, rowNumber, cell.Text, style, qualifiedKey, problems);
                }
                sheet.Append("</row>");
            }
            sheet.Append("</sheetData></worksheet>");
            return sheet;
        }

        /// <summary>
        /// Appends a text cell, unless the text is empty or too long for Excel. An empty cell is left out, as its
        /// column's style already formats what a translator types into it as text.
        /// </summary>
        private static void AppendChecked(StringBuilder sheet, int column, int row, string text, int style, string location, List<string> problems)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (text.Length > MaximumCellLength)
            {
                problems?.Add($"{location}: its text of {text.Length} characters is longer than an Excel cell holds ({MaximumCellLength}), so it is left out.");
                return;
            }
            AppendText(sheet, column, row, text, style);
        }

        private static void AppendText(StringBuilder sheet, int column, int row, string text, int style)
        {
            AppendCellStart(sheet, column, row, style);
            sheet.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">");
            AppendEscaped(sheet, text);
            sheet.Append("</t></is></c>");
        }

        private static void AppendCellStart(StringBuilder sheet, int column, int row, int style)
        {
            sheet.Append("<c r=\"");
            AppendColumnName(sheet, column);
            sheet.Append(row).Append("\" s=\"").Append(style).Append('"');
        }

        private static void AppendColumn(StringBuilder sheet, int first, int last, int width, int style)
        {
            sheet.Append("<col min=\"").Append(first).Append("\" max=\"").Append(last).Append("\" width=\"").Append(width)
                .Append("\" style=\"").Append(style).Append("\" customWidth=\"1\"/>");
        }

        /// <summary>Appends a column's letters, <c>A</c> for 0 and <c>AA</c> for 26.</summary>
        private static void AppendColumnName(StringBuilder builder, int column)
        {
            int start = builder.Length;
            for (int value = column + 1; value > 0; value = (value - 1) / 26)
            {
                builder.Insert(start, (char)('A' + (value - 1) % 26));
            }
        }

        /// <summary>
        /// Appends text escaped for SpreadsheetML: XML's own escapes, and <c>_xHHHH_</c> for characters XML can't hold
        /// and for carriage returns, which XML readers would drop. A literal <c>_x</c> sequence is protected the same way.
        /// </summary>
        private static void AppendEscaped(StringBuilder builder, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                switch (character)
                {
                    case '&':
                        builder.Append("&amp;");
                        continue;
                    case '<':
                        builder.Append("&lt;");
                        continue;
                    case '>':
                        builder.Append("&gt;");
                        continue;
                    case '_' when IsEscapeAt(text, i):
                        builder.Append("_x005F_");
                        continue;
                }
                bool isPaired = char.IsHighSurrogate(character) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                if (isPaired)
                {
                    builder.Append(character).Append(text[++i]);
                }
                else if (character == '\r' || char.IsSurrogate(character) || !IsXmlCharacter(character))
                {
                    builder.Append("_x").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture)).Append('_');
                }
                else
                {
                    builder.Append(character);
                }
            }
        }

        private static bool IsXmlCharacter(char character) =>
            character == '\t' || character == '\n' || character == '\r' || (character >= ' ' && character <= '\uD7FF') || (character >= '\uE000' && character <= '\uFFFD');

        /// <summary>Whether an <c>_xHHHH_</c> escape starts at <paramref name="index"/>.</summary>
        private static bool IsEscapeAt(string text, int index)
        {
            if (index + 7 > text.Length || text[index] != '_' || text[index + 1] != 'x' || text[index + 6] != '_')
            {
                return false;
            }
            for (int i = index + 2; i < index + 6; i++)
            {
                if (!Uri.IsHexDigit(text[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Resolves the <c>_xHHHH_</c> escapes SpreadsheetML text can hold.</summary>
        internal static string Unescape(string text)
        {
            if (text.IndexOf("_x", StringComparison.Ordinal) < 0)
            {
                return text;
            }
            StringBuilder builder = new(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (IsEscapeAt(text, i))
                {
                    builder.Append((char)int.Parse(text.Substring(i + 2, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                    i += 6;
                }
                else
                {
                    builder.Append(text[i]);
                }
            }
            return builder.ToString();
        }

        /// <summary>Names each table's sheet after it, within Excel's 31 characters and unique ignoring case.</summary>
        private static List<string> CreateSheetNames(ExportBook book)
        {
            List<string> names = new(book.Tables.Count);
            HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < book.Tables.Count; i++)
            {
                string name = book.Tables[i].Name;
                if (name.Length > MaximumSheetNameLength)
                {
                    name = name.Substring(0, MaximumSheetNameLength);
                }
                for (int number = 2; !taken.Add(name); number++)
                {
                    string suffix = number.ToString(CultureInfo.InvariantCulture);
                    string stem = book.Tables[i].Name;
                    name = stem.Substring(0, Math.Min(stem.Length, MaximumSheetNameLength - suffix.Length)) + suffix;
                }
                names.Add(name);
            }
            return names;
        }

        private static KeyValuePair<string, byte[]> Part(string name, StringBuilder content) => new(name, Utf8.GetBytes(content.ToString()));

        private static void ReadWorkbook(ImportedFile file, Dictionary<string, byte[]> parts)
        {
            string workbookPath = FindTarget(parts, "_rels/.rels", string.Empty, "/officeDocument") ?? "xl/workbook.xml";
            if (!parts.TryGetValue(workbookPath, out byte[] workbook))
            {
                file.Problems.Add($"{file.Name}: it has no workbook part, so it isn't a spreadsheet.");
                return;
            }
            string folder = workbookPath.Contains("/") ? workbookPath.Substring(0, workbookPath.LastIndexOf('/') + 1) : string.Empty;
            string relationshipsPath = $"{folder}_rels/{workbookPath.Substring(folder.Length)}.rels";
            Dictionary<string, (string Type, string Target)> relationships = ReadRelationships(parts, relationshipsPath, folder);
            List<string> sharedStrings = new();
            foreach ((string Type, string Target) relationship in relationships.Values)
            {
                if (relationship.Type.EndsWith("/sharedStrings", StringComparison.Ordinal) && parts.TryGetValue(relationship.Target, out byte[] strings))
                {
                    ReadSharedStrings(strings, sharedStrings);
                }
            }
            using XmlReader reader = CreateReader(workbook);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "sheet")
                {
                    continue;
                }
                string name = reader.GetAttribute("name") ?? string.Empty;
                string id = reader.GetAttribute("id", RelationshipsNamespace) ?? reader.GetAttribute("id", StrictRelationshipsNamespace);
                if (id != null && relationships.TryGetValue(id, out (string Type, string Target) sheet) && parts.TryGetValue(sheet.Target, out byte[] content))
                {
                    ReadSheet(file, name, ReadCells(content, sharedStrings, file, name));
                }
            }
        }

        private static void ReadSheet(ImportedFile file, string sheetName, SortedDictionary<int, SortedDictionary<int, string>> rows)
        {
            int keyColumn = -1;
            Dictionary<int, int> languageOf = new();
            int headerRow = -1;
            foreach (KeyValuePair<int, SortedDictionary<int, string>> row in rows)
            {
                foreach (KeyValuePair<int, string> cell in row.Value)
                {
                    if (ExchangeColumns.IsKey(cell.Value.Trim()))
                    {
                        keyColumn = cell.Key;
                        headerRow = row.Key;
                        break;
                    }
                }
                if (headerRow >= 0)
                {
                    break;
                }
            }
            if (headerRow < 0)
            {
                if (rows.Count > 0)
                {
                    file.Problems.Add($"{file.Name}, sheet '{sheetName}': it has no '{ExchangeColumns.Key}' column, so its rows can't be matched with entries.");
                }
                return;
            }
            foreach (KeyValuePair<int, string> cell in rows[headerRow])
            {
                string label = cell.Value.Trim();
                if (cell.Key != keyColumn && label.Length > 0 && !ExchangeColumns.IsInformation(label))
                {
                    languageOf[cell.Key] = file.GetOrAddLanguage(label, false, label);
                }
            }
            foreach (KeyValuePair<int, SortedDictionary<int, string>> row in rows)
            {
                if (row.Key <= headerRow)
                {
                    continue;
                }
                string location = $"{file.Name}, sheet '{sheetName}', row {row.Key}";
                row.Value.TryGetValue(keyColumn, out string keyCell);
                string[] texts = new string[file.Languages.Count];
                bool hasText = false;
                foreach (KeyValuePair<int, string> cell in row.Value)
                {
                    if (languageOf.TryGetValue(cell.Key, out int language) && cell.Value.Length > 0)
                    {
                        texts[language] = ExchangeColumns.NormalizeLineBreaks(cell.Value);
                        hasText = true;
                    }
                }
                if (string.IsNullOrWhiteSpace(keyCell))
                {
                    if (hasText)
                    {
                        file.Problems.Add($"{location}: it has text but no key, so it is skipped.");
                    }
                    continue;
                }
                ExchangeColumns.ParseKey(keyCell, sheetName, out string tableName, out string key);
                file.Rows.Add(new ImportedRow(location, tableName, key, texts));
            }
        }

        /// <summary>Reads a sheet's cells by row and column number, both counted from 1, with every value as text.</summary>
        private static SortedDictionary<int, SortedDictionary<int, string>> ReadCells(byte[] content, List<string> sharedStrings, ImportedFile file, string sheetName)
        {
            SortedDictionary<int, SortedDictionary<int, string>> rows = new();
            SortedDictionary<int, string> cells = null;
            int rowNumber = 0;
            int columnNumber = 0;
            using XmlReader reader = CreateReader(content);
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }
                if (reader.LocalName == "row")
                {
                    rowNumber = int.TryParse(reader.GetAttribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out int number) ? number : rowNumber + 1;
                    columnNumber = 0;
                    cells = new SortedDictionary<int, string>();
                    rows[rowNumber] = cells;
                }
                else if (reader.LocalName == "c" && cells != null)
                {
                    string reference = reader.GetAttribute("r");
                    columnNumber = reference != null ? ParseColumn(reference) : columnNumber + 1;
                    string type = reader.GetAttribute("t");
                    string value = ReadCellValue(reader, type, sharedStrings);
                    if (type == "e")
                    {
                        file.Problems.Add($"{file.Name}, sheet '{sheetName}', row {rowNumber}: a cell holds the error {value}, so it is skipped.");
                        continue;
                    }
                    if (!string.IsNullOrEmpty(value))
                    {
                        cells[columnNumber] = value;
                    }
                }
            }
            return rows;
        }

        /// <summary>Reads the value of the cell the reader is on, leaving the reader on the cell's end.</summary>
        private static string ReadCellValue(XmlReader reader, string type, List<string> sharedStrings)
        {
            if (reader.IsEmptyElement)
            {
                return null;
            }
            string value = null;
            string inline = null;
            int depth = reader.Depth;
            reader.Read();
            while (!reader.EOF && reader.Depth > depth)
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "v")
                {
                    // Reading the content moves past the element's end.
                    value = reader.ReadElementContentAsString();
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "is")
                {
                    inline = ReadStringItem(reader);
                    reader.Read();
                }
                else
                {
                    reader.Read();
                }
            }
            switch (type)
            {
                case "s":
                    return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index < sharedStrings.Count ? sharedStrings[index] : null;
                case "inlineStr":
                    return inline;
                case "str":
                    return value == null ? null : Unescape(value);
                case "b":
                    return value == "1" ? "TRUE" : value == "0" ? "FALSE" : value;
                default:
                    return value;
            }
        }

        private static void ReadSharedStrings(byte[] content, List<string> strings)
        {
            using XmlReader reader = CreateReader(content);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "si")
                {
                    strings.Add(ReadStringItem(reader));
                }
            }
        }

        /// <summary>
        /// Reads a string item, plain or rich, leaving out phonetic hints. The reader starts on the item and ends on its
        /// end, or stays on it when it is empty.
        /// </summary>
        private static string ReadStringItem(XmlReader reader)
        {
            if (reader.IsEmptyElement)
            {
                return string.Empty;
            }
            StringBuilder text = new();
            int depth = reader.Depth;
            reader.Read();
            while (!reader.EOF && reader.Depth > depth)
            {
                bool isElement = reader.NodeType == XmlNodeType.Element;
                if (isElement && (reader.LocalName == "rPh" || reader.LocalName == "phoneticPr"))
                {
                    reader.Skip();
                }
                else if (isElement && reader.LocalName == "t")
                {
                    text.Append(reader.ReadElementContentAsString());
                }
                else
                {
                    reader.Read();
                }
            }
            return Unescape(text.ToString());
        }

        private static Dictionary<string, (string Type, string Target)> ReadRelationships(Dictionary<string, byte[]> parts, string path, string folder)
        {
            Dictionary<string, (string Type, string Target)> relationships = new(StringComparer.Ordinal);
            if (!parts.TryGetValue(path, out byte[] content))
            {
                return relationships;
            }
            using XmlReader reader = CreateReader(content);
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship")
                {
                    string id = reader.GetAttribute("Id");
                    string target = reader.GetAttribute("Target");
                    if (id != null && target != null)
                    {
                        relationships[id] = (reader.GetAttribute("Type") ?? string.Empty, ResolvePath(folder, target));
                    }
                }
            }
            return relationships;
        }

        private static string FindTarget(Dictionary<string, byte[]> parts, string path, string folder, string typeEnding)
        {
            foreach ((string Type, string Target) relationship in ReadRelationships(parts, path, folder).Values)
            {
                if (relationship.Type.EndsWith(typeEnding, StringComparison.Ordinal))
                {
                    return relationship.Target;
                }
            }
            return null;
        }

        /// <summary>Resolves a relationship target, relative to <paramref name="folder"/> or absolute from the package root.</summary>
        private static string ResolvePath(string folder, string target)
        {
            if (target.StartsWith("/", StringComparison.Ordinal))
            {
                return target.Substring(1);
            }
            List<string> segments = new(folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            foreach (string segment in target.Split('/'))
            {
                if (segment == "..")
                {
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }
                }
                else if (segment.Length > 0 && segment != ".")
                {
                    segments.Add(segment);
                }
            }
            return string.Join("/", segments);
        }

        /// <summary>Returns the 1-based column number of a cell reference such as <c>AB12</c>.</summary>
        private static int ParseColumn(string reference)
        {
            int column = 0;
            for (int i = 0; i < reference.Length; i++)
            {
                char letter = char.ToUpperInvariant(reference[i]);
                if (letter < 'A' || letter > 'Z')
                {
                    break;
                }
                column = column * 26 + (letter - 'A' + 1);
            }
            return column;
        }

        private static XmlReader CreateReader(byte[] content)
        {
            // Workbooks never declare a DTD, and refusing one keeps a crafted file from expanding entities.
            XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreComments = true };
            return XmlReader.Create(new MemoryStream(content, false), settings);
        }

        private const string Stylesheet =
            "<styleSheet xmlns=\"" + MainNamespace + "\">" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"4\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFEDEDED\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
            "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"5\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"49\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyFill=\"1\"/>" +
            "<xf numFmtId=\"49\" fontId=\"0\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"49\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
            "<xf numFmtId=\"49\" fontId=\"0\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\" applyFill=\"1\" applyAlignment=\"1\"><alignment vertical=\"top\" wrapText=\"1\"/></xf>" +
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";
    }
}
