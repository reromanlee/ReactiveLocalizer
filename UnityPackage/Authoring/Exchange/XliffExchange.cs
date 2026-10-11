using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// XLIFF 1.2 and 2.0 exchange, one file per target language with a file element per table: each entry's source text,
    /// its translation and state, the context as a note, and the maximum length.
    /// </summary>
    /// <remarks>
    /// Missing entries have no target, outdated and unverified ones are marked as needing review, and current ones as
    /// translated, so a translation tool shows what needs work. Attributes in this package's own namespace name the
    /// catalog and its languages exactly, as culture tags can't for invented languages. Reading takes either version,
    /// with inline markup a tool added, and treats a translation the tool marks translated, reviewed or final as
    /// checked.
    /// </remarks>
    public static class XliffExchange
    {
        /// <summary>The namespace of the attributes that name the catalog and its languages exactly.</summary>
        public const string ToolNamespace = "https://github.com/reromanlee/ReactiveLocalizer";

        private const string Namespace12 = "urn:oasis:names:tc:xliff:document:1.2";
        private const string Namespace20 = "urn:oasis:names:tc:xliff:document:2.0";
        private static readonly UTF8Encoding Utf8 = new(false);

        /// <summary>
        /// Returns <paramref name="book"/>, which must export exactly one translation language, as the bytes of an XLIFF
        /// file. XLIFF 1.2 can't hold every character XML can't, so entries with one are left out and reported.
        /// </summary>
        /// <param name="book">What to export, in one translation language.</param>
        /// <param name="version">The XLIFF version to write.</param>
        /// <param name="problems">Where entries that can't be written are reported.</param>
        /// <exception cref="ArgumentException">The book doesn't export exactly one translation language.</exception>
        public static byte[] Write(ExportBook book, XliffVersion version, List<string> problems)
        {
            if (book == null || book.Languages.Count != 1)
            {
                throw new ArgumentException("An XLIFF file holds one target language, so export one book per language.", nameof(book));
            }
            LanguageInfo target = book.Languages[0];
            string sourceTag = GetLanguageTag(book.Catalog, book.SourceLanguage);
            string targetTag = GetLanguageTag(book.Catalog, target);
            StringBuilder xml = new("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            if (version == XliffVersion.Version12)
            {
                xml.Append("<xliff version=\"1.2\" xmlns=\"").Append(Namespace12).Append("\" xmlns:rl=\"").Append(ToolNamespace).Append("\">\n");
                for (int t = 0; t < book.Tables.Count; t++)
                {
                    WriteFile12(xml, book, book.Tables[t], sourceTag, targetTag, problems);
                }
            }
            else
            {
                xml.Append("<xliff xmlns=\"").Append(Namespace20).Append("\" xmlns:rl=\"").Append(ToolNamespace).Append("\" version=\"2.0\" srcLang=\"");
                AppendEscaped(xml, sourceTag, true);
                xml.Append("\" trgLang=\"");
                AppendEscaped(xml, targetTag, true);
                xml.Append('"');
                AppendIdentity(xml, book, target);
                xml.Append(">\n");
                for (int t = 0; t < book.Tables.Count; t++)
                {
                    WriteFile20(xml, book.Tables[t]);
                }
            }
            xml.Append("</xliff>\n");
            return Utf8.GetBytes(xml.ToString());
        }

        /// <summary>
        /// Returns the culture tag XLIFF names <paramref name="language"/> by: its culture, else the first culture along
        /// its fallback chain, as its plural rules come from there too, else <c>und</c> for an undetermined language.
        /// </summary>
        public static string GetLanguageTag(CatalogInfo catalog, LanguageInfo language)
        {
            IReadOnlyList<LanguageInfo> chain = catalog.GetFallbackChain(language);
            for (int i = 0; i < chain.Count; i++)
            {
                if (!string.IsNullOrEmpty(chain[i].Culture))
                {
                    return chain[i].Culture;
                }
            }
            return "und";
        }

        /// <summary>Reads the XLIFF file <paramref name="fileName"/>, whose bytes are <paramref name="data"/>. Never throws.</summary>
        public static ImportedFile Read(string fileName, byte[] data)
        {
            ImportedFile file = new(fileName);
            try
            {
                // A DTD is ignored rather than processed, so a crafted file can't expand entities or reach other files.
                XmlReaderSettings settings = new() { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                using XmlReader reader = XmlReader.Create(new MemoryStream(data ?? Array.Empty<byte>(), false), settings);
                XDocument document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                ReadDocument(file, document);
            }
            catch (XmlException exception)
            {
                file.Problems.Add($"{fileName}: it can't be read as XLIFF: {exception.Message}");
            }
            return file;
        }

        private static void WriteFile12(StringBuilder xml, ExportBook book, ExportTable table, string sourceTag, string targetTag, List<string> problems)
        {
            xml.Append("  <file original=\"").Append(table.Name).Append("\" datatype=\"plaintext\" source-language=\"");
            AppendEscaped(xml, sourceTag, true);
            xml.Append("\" target-language=\"");
            AppendEscaped(xml, targetTag, true);
            xml.Append('"');
            AppendIdentity(xml, book, book.Languages[0]);
            xml.Append(">\n    <body>\n");
            for (int r = 0; r < table.Rows.Count; r++)
            {
                ExportRow row = table.Rows[r];
                ExportCell cell = row.Cells[0];
                if (HasCharacterXmlCantHold(row.SourceText) || HasCharacterXmlCantHold(cell.Text) || HasCharacterXmlCantHold(row.Context))
                {
                    problems?.Add($"{table.Name}.{row.Key}: its text holds a control character XLIFF 1.2 can't, so it is left out. Export XLIFF 2.0 or XLSX instead.");
                    continue;
                }
                xml.Append("      <trans-unit id=\"").Append(row.Key).Append("\" resname=\"").Append(row.Key).Append("\" xml:space=\"preserve\"");
                if (row.MaximumLength > 0)
                {
                    xml.Append(" maxwidth=\"").Append(row.MaximumLength.ToString(CultureInfo.InvariantCulture)).Append("\" size-unit=\"char\"");
                }
                xml.Append(">\n        <source>");
                AppendText(xml, row.SourceText, XliffVersion.Version12);
                xml.Append("</source>\n");
                if (cell.Text != null)
                {
                    xml.Append("        <target state=\"").Append(cell.State == TranslationState.Current ? "translated" : "needs-review-translation").Append("\">");
                    AppendText(xml, cell.Text, XliffVersion.Version12);
                    xml.Append("</target>\n");
                }
                if (row.Context.Length > 0)
                {
                    xml.Append("        <note from=\"developer\">");
                    AppendText(xml, row.Context, XliffVersion.Version12);
                    xml.Append("</note>\n");
                }
                xml.Append("      </trans-unit>\n");
            }
            xml.Append("    </body>\n  </file>\n");
        }

        private static void WriteFile20(StringBuilder xml, ExportTable table)
        {
            xml.Append("  <file id=\"").Append(table.Name).Append("\" original=\"").Append(table.Name).Append("\">\n");
            for (int r = 0; r < table.Rows.Count; r++)
            {
                ExportRow row = table.Rows[r];
                ExportCell cell = row.Cells[0];
                xml.Append("    <unit id=\"").Append(row.Key).Append("\" name=\"").Append(row.Key).Append("\" xml:space=\"preserve\">\n");
                if (row.Context.Length > 0 || row.MaximumLength > 0)
                {
                    xml.Append("      <notes>\n");
                    if (row.Context.Length > 0)
                    {
                        xml.Append("        <note category=\"context\">");
                        AppendText(xml, row.Context, XliffVersion.Version20);
                        xml.Append("</note>\n");
                    }
                    if (row.MaximumLength > 0)
                    {
                        xml.Append("        <note category=\"maximumLength\">").Append(row.MaximumLength.ToString(CultureInfo.InvariantCulture)).Append("</note>\n");
                    }
                    xml.Append("      </notes>\n");
                }
                xml.Append("      <segment state=\"").Append(cell.State == TranslationState.Current ? "translated" : "initial").Append("\">\n        <source>");
                AppendText(xml, row.SourceText, XliffVersion.Version20);
                xml.Append("</source>\n");
                if (cell.Text != null)
                {
                    xml.Append("        <target>");
                    AppendText(xml, cell.Text, XliffVersion.Version20);
                    xml.Append("</target>\n");
                }
                xml.Append("      </segment>\n    </unit>\n");
            }
            xml.Append("  </file>\n");
        }

        private static void AppendIdentity(StringBuilder xml, ExportBook book, LanguageInfo target)
        {
            xml.Append(" rl:catalog=\"").Append(book.Catalog.Key.Name)
                .Append("\" rl:sourceLanguage=\"").Append(book.SourceLanguage.Name)
                .Append("\" rl:targetLanguage=\"").Append(target.Name).Append('"');
        }

        /// <summary>
        /// Appends text as element content: XML's escapes, carriage returns as references so readers keep them, and in
        /// XLIFF 2.0 a <c>&lt;cp&gt;</c> element for each character XML can't hold.
        /// </summary>
        private static void AppendText(StringBuilder xml, string text, XliffVersion version)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char character = text[i];
                bool isPaired = char.IsHighSurrogate(character) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                if (isPaired)
                {
                    xml.Append(character).Append(text[++i]);
                }
                else if (!IsXmlCharacter(character))
                {
                    if (version == XliffVersion.Version20)
                    {
                        xml.Append("<cp hex=\"").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture)).Append("\"/>");
                    }
                }
                else
                {
                    AppendEscaped(xml, character, false);
                }
            }
        }

        private static void AppendEscaped(StringBuilder xml, string text, bool isAttribute)
        {
            for (int i = 0; i < text.Length; i++)
            {
                AppendEscaped(xml, text[i], isAttribute);
            }
        }

        private static void AppendEscaped(StringBuilder xml, char character, bool isAttribute)
        {
            switch (character)
            {
                case '&':
                    xml.Append("&amp;");
                    break;
                case '<':
                    xml.Append("&lt;");
                    break;
                case '>':
                    xml.Append("&gt;");
                    break;
                case '"' when isAttribute:
                    xml.Append("&quot;");
                    break;
                case '\r':
                    xml.Append("&#xD;");
                    break;
                default:
                    xml.Append(character);
                    break;
            }
        }

        private static bool HasCharacterXmlCantHold(string text)
        {
            if (text == null)
            {
                return false;
            }
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    i++;
                }
                else if (!IsXmlCharacter(text[i]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsXmlCharacter(char character) =>
            character == '\t' || character == '\n' || character == '\r' || (character >= ' ' && character <= 0xD7FF) || (character >= 0xE000 && character <= 0xFFFD);

        private static void ReadDocument(ImportedFile file, XDocument document)
        {
            XElement root = document.Root;
            if (root == null || root.Name.LocalName != "xliff")
            {
                file.Problems.Add($"{file.Name}: it isn't an XLIFF file, which starts with an <xliff> element.");
                return;
            }
            bool isVersion2 = root.Name.NamespaceName.StartsWith("urn:oasis:names:tc:xliff:document:2", StringComparison.Ordinal) ||
                              ((string)root.Attribute("version") ?? string.Empty).StartsWith("2", StringComparison.Ordinal);
            file.CatalogName = (string)root.Attribute(XName.Get("catalog", ToolNamespace));
            string fileNameLanguage = GetLanguageFromFileName(file.Name);
            XNamespace xliff = root.Name.Namespace;
            foreach (XElement element in root.Elements(xliff + "file"))
            {
                file.CatalogName ??= (string)element.Attribute(XName.Get("catalog", ToolNamespace));
                // Version 2 names the languages on the root, version 1.2 on each file.
                XElement languages = isVersion2 ? root : element;
                int source = AddLanguage(file, languages, isVersion2 ? "srcLang" : "source-language", "sourceLanguage", true, null);
                int target = AddLanguage(file, languages, isVersion2 ? "trgLang" : "target-language", "targetLanguage", false, fileNameLanguage);
                string tableName = (string)element.Attribute("original") ?? (string)element.Attribute("id") ?? string.Empty;
                if (isVersion2)
                {
                    ReadUnits20(file, element, xliff, tableName, source, target);
                }
                else
                {
                    ReadUnits12(file, element, xliff, tableName, source, target);
                }
            }
        }

        private static int AddLanguage(ImportedFile file, XElement element, string tagAttribute, string nameAttribute, bool isSourceOnly, string fileNameLanguage)
        {
            string name = (string)element.Attribute(XName.Get(nameAttribute, ToolNamespace));
            string tag = (string)element.Attribute(tagAttribute);
            List<string> candidates = new(3);
            if (!string.IsNullOrEmpty(name))
            {
                candidates.Add(name);
            }
            if (!string.IsNullOrEmpty(fileNameLanguage) && !string.Equals(fileNameLanguage, name, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(fileNameLanguage);
            }
            if (!string.IsNullOrEmpty(tag))
            {
                candidates.Add(tag);
            }
            string label = !string.IsNullOrEmpty(name) ? name : !string.IsNullOrEmpty(tag) ? tag : isSourceOnly ? "(source)" : "(target)";
            return file.GetOrAddLanguage(label, isSourceOnly, candidates.ToArray());
        }

        private static void ReadUnits12(ImportedFile file, XElement element, XNamespace xliff, string tableName, int source, int target)
        {
            foreach (XElement unit in element.Descendants(xliff + "trans-unit"))
            {
                if ((string)unit.Attribute("translate") == "no")
                {
                    continue;
                }
                XElement targetElement = unit.Element(xliff + "target");
                string state = (string)targetElement?.Attribute("state");
                ImportedRow row = CreateRow(file, unit, tableName, (string)unit.Attribute("resname") ?? (string)unit.Attribute("id"),
                    ReadInline(unit.Element(xliff + "source"), null), ReadInline(targetElement, null), source, target);
                row.IsConfirmed = state == "translated" || state == "final" || state == "signed-off" || (string)unit.Attribute("approved") == "yes";
                file.Rows.Add(row);
            }
        }

        private static void ReadUnits20(ImportedFile file, XElement element, XNamespace xliff, string tableName, int source, int target)
        {
            foreach (XElement unit in element.Descendants(xliff + "unit"))
            {
                if ((string)unit.Attribute("translate") == "no")
                {
                    continue;
                }
                Dictionary<string, string> data = new(StringComparer.Ordinal);
                XElement originalData = unit.Element(xliff + "originalData");
                if (originalData != null)
                {
                    foreach (XElement item in originalData.Elements(xliff + "data"))
                    {
                        data[(string)item.Attribute("id") ?? string.Empty] = ReadInline(item, null);
                    }
                }
                StringBuilder sourceText = new();
                StringBuilder targetText = new();
                bool hasTarget = false;
                bool isComplete = true;
                bool isConfirmed = true;
                foreach (XElement part in unit.Elements())
                {
                    bool isSegment = part.Name == xliff + "segment";
                    if (!isSegment && part.Name != xliff + "ignorable")
                    {
                        continue;
                    }
                    string partSource = ReadInline(part.Element(xliff + "source"), data);
                    string partTarget = ReadInline(part.Element(xliff + "target"), data);
                    sourceText.Append(partSource);
                    // What lies between segments, such as spaces, reads the same in both languages unless a tool changed it.
                    targetText.Append(partTarget ?? (isSegment ? string.Empty : partSource));
                    hasTarget |= isSegment && partTarget != null;
                    isComplete &= !isSegment || partTarget != null;
                    string state = (string)part.Attribute("state") ?? "initial";
                    isConfirmed &= !isSegment || state == "translated" || state == "reviewed" || state == "final";
                }
                string key = (string)unit.Attribute("name") ?? (string)unit.Attribute("id");
                ImportedRow row = CreateRow(file, unit, tableName, key, sourceText.ToString(), hasTarget && isComplete ? targetText.ToString() : null, source, target);
                if (hasTarget && !isComplete)
                {
                    file.Problems.Add($"{row.Location}: '{tableName}.{key}' is translated in only some of its segments, so it is skipped.");
                }
                row.IsConfirmed = isConfirmed && hasTarget && isComplete;
                file.Rows.Add(row);
            }
        }

        private static ImportedRow CreateRow(ImportedFile file, XElement unit, string tableName, string key, string sourceText, string targetText, int source, int target)
        {
            string[] texts = new string[file.Languages.Count];
            texts[source] = string.IsNullOrEmpty(sourceText) ? null : ExchangeColumns.NormalizeLineBreaks(sourceText);
            texts[target] = string.IsNullOrEmpty(targetText) ? null : ExchangeColumns.NormalizeLineBreaks(targetText);
            int line = unit is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 0;
            return new ImportedRow($"{file.Name}, line {line}", tableName, key ?? string.Empty, texts);
        }

        /// <summary>
        /// Returns the text of a source or target element: its text, the text inside markup a tool added, and the codes
        /// placeholders stand for. Null when there is no element.
        /// </summary>
        private static string ReadInline(XElement element, Dictionary<string, string> data)
        {
            if (element == null)
            {
                return null;
            }
            StringBuilder text = new();
            AppendInline(text, element, data);
            return text.ToString();
        }

        private static void AppendInline(StringBuilder text, XElement element, Dictionary<string, string> data)
        {
            foreach (XNode node in element.Nodes())
            {
                if (node is XText part)
                {
                    text.Append(part.Value);
                    continue;
                }
                if (!(node is XElement child))
                {
                    continue;
                }
                switch (child.Name.LocalName)
                {
                    case "g":
                    case "mrk":
                    case "sub":
                        AppendInline(text, child, data);
                        break;
                    case "pc":
                        // A paired code wraps text in the codes it stands for, such as <b> and </b>.
                        AppendData(text, (string)child.Attribute("dataRefStart"), data);
                        AppendInline(text, child, data);
                        AppendData(text, (string)child.Attribute("dataRefEnd"), data);
                        break;
                    case "cp":
                        if (int.TryParse((string)child.Attribute("hex"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code) && code >= 0 && code <= 0xFFFF)
                        {
                            text.Append((char)code);
                        }
                        break;
                    case "ph":
                    case "sc":
                    case "ec":
                    case "bpt":
                    case "ept":
                    case "it":
                        string reference = (string)child.Attribute("dataRef") ?? (string)child.Attribute("dataRefStart") ?? (string)child.Attribute("dataRefEnd");
                        if (reference != null)
                        {
                            AppendData(text, reference, data);
                        }
                        else
                        {
                            // XLIFF 1.2 codes hold the original code as their content.
                            text.Append(child.Value);
                        }
                        break;
                }
            }
        }

        private static void AppendData(StringBuilder text, string reference, Dictionary<string, string> data)
        {
            if (reference != null && data != null && data.TryGetValue(reference, out string original))
            {
                text.Append(original);
            }
        }

        /// <summary>Returns the language an exported file's name holds, as in <c>Localization.Russian.xlf</c>; null otherwise.</summary>
        private static string GetLanguageFromFileName(string fileName)
        {
            string[] parts = Path.GetFileNameWithoutExtension(fileName ?? string.Empty).Split('.');
            return parts.Length >= 2 && NameRules.IsValid(parts[parts.Length - 1]) ? parts[parts.Length - 1] : null;
        }
    }
}
