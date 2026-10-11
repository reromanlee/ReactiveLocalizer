using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Formatting;
using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// Translation through any large language model, with no API key or provider: a text bundle to paste into a chat,
    /// and the model's reply read back.
    /// </summary>
    /// <remarks>
    /// The bundle opens with rules (keep the format, ICU syntax, escapes and markup, use the language's plural forms,
    /// respect maximum lengths), then lists the entries in <c>.lang</c> syntax under <c>@table</c> lines, each with its
    /// context, its source text to translate, and the fingerprint tag of that text. The reply keeps the tags, so each
    /// translation is stamped as made from the text the model saw. Reading takes the reply as it is pasted: a code
    /// block is read on its own, and anything around it is left alone.
    /// </remarks>
    public static class LlmHandoff
    {
        private const string CatalogLine = "@catalog";
        private const string LanguageLine = "@language";
        private const string TableLine = "@table";

        /// <summary>Returns <paramref name="book"/>, which must export exactly one translation language, as a bundle to paste into a chat.</summary>
        /// <exception cref="ArgumentException">The book doesn't export exactly one translation language.</exception>
        public static string Write(ExportBook book)
        {
            if (book == null || book.Languages.Count != 1)
            {
                throw new ArgumentException("A handoff asks for one language at a time, so export one book per language.", nameof(book));
            }
            LanguageInfo target = book.Languages[0];
            StringBuilder text = new();
            AppendRules(text, book, target);
            text.Append('\n').Append(CatalogLine).Append(' ').Append(book.Catalog.Key.Name).Append('\n');
            text.Append(LanguageLine).Append(' ').Append(target.Name).Append('\n');
            for (int t = 0; t < book.Tables.Count; t++)
            {
                ExportTable table = book.Tables[t];
                text.Append('\n').Append(TableLine).Append(' ').Append(table.Name).Append('\n');
                bool wasNoted = true;
                for (int r = 0; r < table.Rows.Count; r++)
                {
                    wasNoted = AppendEntry(text, book, target, table.Rows[r], wasNoted);
                }
            }
            return text.ToString();
        }

        /// <summary>Reads a model's reply, named <paramref name="name"/> in problems. Never throws.</summary>
        public static ImportedFile Read(string name, string reply)
        {
            ImportedFile file = new(name);
            string[] lines = ExchangeColumns.NormalizeLineBreaks(reply ?? string.Empty).Split('\n');
            bool[] isRead = FindReadLines(lines);
            string language = null;
            string table = null;
            int sectionStart = 0;
            StringBuilder section = new();
            List<(string Table, int FirstLine, string Text)> sections = new();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = isRead[i] ? lines[i] : string.Empty;
                string trimmed = line.Trim();
                if (TryReadLine(trimmed, CatalogLine, out string catalog))
                {
                    file.CatalogName = catalog;
                    line = string.Empty;
                }
                else if (TryReadLine(trimmed, LanguageLine, out string named))
                {
                    language = named;
                    line = string.Empty;
                }
                else if (TryReadLine(trimmed, TableLine, out string tableName))
                {
                    sections.Add((table, sectionStart, section.ToString()));
                    section.Clear();
                    table = tableName;
                    sectionStart = i + 1;
                    continue;
                }
                section.Append(line).Append('\n');
            }
            sections.Add((table, sectionStart, section.ToString()));
            file.Languages.Add(language != null ? new ImportedLanguage(language, false, language) : new ImportedLanguage("(not named)", false));
            for (int i = 0; i < sections.Count; i++)
            {
                ReadSection(file, sections[i].Table, sections[i].FirstLine, sections[i].Text);
            }
            return file;
        }

        private static void AppendRules(StringBuilder text, ExportBook book, LanguageInfo target)
        {
            int ordinals = 0;
            bool hasOrdinals = false;
            for (int t = 0; t < book.Tables.Count && !hasOrdinals; t++)
            {
                for (int r = 0; r < book.Tables[t].Rows.Count && !hasOrdinals; r++)
                {
                    hasOrdinals = book.Tables[t].Rows[r].SourceText.IndexOf("selectordinal", StringComparison.Ordinal) >= 0;
                }
            }
            if (hasOrdinals)
            {
                ordinals = book.Catalog.GetFormat(target).OrdinalCategories;
            }
            string plurals = PluralCategories.Describe(book.Catalog.GetFormat(target).CardinalCategories);
            text.Append("# Translation request from ReactiveLocalizer: the catalog '").Append(book.Catalog.Key.Name).Append("', from ")
                .Append(Describe(book.SourceLanguage)).Append(" into ").Append(Describe(target)).Append(".\n");
            text.Append("#\n# Rules:\n");
            text.Append("# 1. Reply with everything from the @catalog line down, in the same format, in one code block. Lines starting with # may be left out.\n");
            text.Append("# 2. Keep the @catalog, @language and @table lines, and each entry's key and [tag], exactly as they are. Translate only the text after \" = \".\n");
            text.Append("# 3. Keep the ICU MessageFormat syntax. Arguments such as {name} stay as they are. In {count, plural, one {...} other {...}}, translate only the texts inside the inner braces, ");
            text.Append("and keep the keywords plural, select, selectordinal, number and offset and the selectors such as one, other and =0 unchanged. A quoted '{' is a brace, and '' an apostrophe.\n");
            text.Append("# 4. ").Append(target.Name).Append(" uses the plural forms ").Append(plurals).Append(": give every plural exactly these, keeping any =N forms.");
            if (hasOrdinals)
            {
                text.Append(" Its selectordinal forms are ").Append(PluralCategories.Describe(ordinals)).Append('.');
            }
            text.Append('\n');
            text.Append("# 5. Keep escapes as they are: \\n is a line break, \\t a tab, \\\\ a backslash, \\uXXXX one character. Every entry stays on one line.\n");
            text.Append("# 6. Keep markup such as <b> and </color> unchanged.\n");
            text.Append("# 7. An entry under @maximumLength N must stay within N characters.\n");
            text.Append("# 8. The # lines above an entry describe it.\n");
        }

        /// <summary>
        /// Appends an entry with its notes, set apart by a blank line when it or the entry before has notes, as table
        /// files set entries apart. Returns whether it has notes.
        /// </summary>
        private static bool AppendEntry(StringBuilder text, ExportBook book, LanguageInfo target, ExportRow row, bool wasNoted)
        {
            ExportCell cell = row.Cells[0];
            bool hasNotes = row.Context.Length > 0 || row.MaximumLength > 0 || cell.Text != null;
            if (hasNotes || wasNoted)
            {
                text.Append('\n');
            }
            if (row.Context.Length > 0)
            {
                string[] lines = row.Context.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    text.Append("# ").Append(lines[i]).Append('\n');
                }
            }
            if (cell.Text != null)
            {
                text.Append("# Current ").Append(target.Name).Append(" text");
                if (cell.State == TranslationState.Outdated)
                {
                    text.Append(", made from an older ").Append(book.SourceLanguage.Name).Append(" text");
                }
                else if (cell.State == TranslationState.Unverified)
                {
                    text.Append(", never checked against the ").Append(book.SourceLanguage.Name).Append(" text");
                }
                text.Append(": ");
                TextEscaping.Escape(cell.Text.AsSpan(), text);
                text.Append('\n');
            }
            if (row.MaximumLength > 0)
            {
                text.Append('@').Append(DocumentNames.MaximumLength).Append(' ').Append(row.MaximumLength).Append('\n');
            }
            text.Append(row.Key).Append(" [").Append(Hashing.ComputeFingerprint(row.SourceText).ToString("x6")).Append("] =");
            if (row.SourceText.Length > 0)
            {
                text.Append(' ');
                TextEscaping.Escape(row.SourceText.AsSpan(), text);
            }
            text.Append('\n');
            return hasNotes;
        }

        private static string Describe(LanguageInfo language)
        {
            bool hasCulture = !string.IsNullOrEmpty(language.Culture);
            bool hasDisplayName = !string.IsNullOrEmpty(language.DisplayName) && !string.Equals(language.DisplayName, language.Name, StringComparison.Ordinal);
            if (!hasCulture && !hasDisplayName)
            {
                return language.Name;
            }
            if (hasCulture && hasDisplayName)
            {
                return $"{language.Name} ({language.Culture}, {language.DisplayName})";
            }
            return $"{language.Name} ({(hasCulture ? language.Culture : language.DisplayName)})";
        }

        /// <summary>Marks the lines to read: those inside code blocks when the reply has any, which keeps the words around them out, else every line.</summary>
        private static bool[] FindReadLines(string[] lines)
        {
            bool[] isRead = new bool[lines.Length];
            bool hasBlock = false;
            bool isInside = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    hasBlock = true;
                    isInside = !isInside;
                    continue;
                }
                isRead[i] = isInside;
            }
            if (!hasBlock)
            {
                for (int i = 0; i < isRead.Length; i++)
                {
                    isRead[i] = true;
                }
            }
            return isRead;
        }

        private static bool TryReadLine(string line, string name, out string value)
        {
            if (line.Length > name.Length && line.StartsWith(name, StringComparison.OrdinalIgnoreCase) && char.IsWhiteSpace(line[name.Length]))
            {
                value = line.Substring(name.Length).Trim();
                return value.Length > 0;
            }
            value = null;
            return false;
        }

        private static void ReadSection(ImportedFile file, string table, int firstLine, string text)
        {
            TableDocument document = TableDocument.Parse(text);
            if (table == null)
            {
                // Before the first @table line, only an entry is worth reporting; other words are the model talking.
                for (int i = 0; i < document.Entries.Count; i++)
                {
                    file.Problems.Add($"{file.Name}, line {firstLine + document.Entries[i].Line}: '{document.Entries[i].Key}' comes before any @table line, so its table is unknown and it is skipped.");
                }
                return;
            }
            for (int i = 0; i < document.Issues.Count; i++)
            {
                file.Problems.Add($"{file.Name}, line {firstLine + document.Issues[i].Line}: {document.Issues[i].Message}");
            }
            for (int i = 0; i < document.Entries.Count; i++)
            {
                TableDocumentEntry entry = document.Entries[i];
                ImportedRow row = new($"{file.Name}, line {firstLine + entry.Line}", table, entry.Key, new[] { entry.Value.Length > 0 ? entry.Value : null })
                {
                    HasFingerprint = entry.HasFingerprint,
                    Fingerprint = entry.Fingerprint
                };
                file.Rows.Add(row);
            }
        }
    }
}
