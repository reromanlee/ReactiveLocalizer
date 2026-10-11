using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Exports a catalog's tables to exchange files and reads them back, for the export and import windows and for
    /// project tools: XLSX, CSV, XLIFF and the LLM handoff.
    /// </summary>
    internal static class LocalizationExchange
    {
        /// <summary>The Undo step an import is.</summary>
        public const string UndoName = "Import Translations";

        /// <summary>The extensions of the files imports read, without their dots.</summary>
        public static readonly string[] Extensions = { "xlsx", "csv", "tsv", "xlf", "xliff", "txt" };

        /// <summary>Returns how menus name <paramref name="format"/>.</summary>
        public static string GetDisplayName(ExchangeFormat format)
        {
            return format switch
            {
                ExchangeFormat.Csv => "CSV, a file per table",
                ExchangeFormat.Xliff12 => "XLIFF 1.2, a file per language",
                ExchangeFormat.Xliff20 => "XLIFF 2.0, a file per language",
                ExchangeFormat.LlmHandoff => "LLM handoff, copied to the clipboard",
                _ => "Excel workbook (.xlsx), a sheet per table"
            };
        }

        /// <summary>Whether <paramref name="format"/> writes one file the person names, rather than files into a folder.</summary>
        public static bool IsSingleFile(ExchangeFormat format) => format == ExchangeFormat.Xlsx;

        /// <summary>Lays out the tables of <paramref name="catalog"/> as <paramref name="options"/> asks, reading each file once.</summary>
        public static ExportBook Collect(IndexedCatalog catalog, ExportOptions options)
        {
            List<ValidatedTable> tables = new(catalog.Tables.Count);
            HashSet<string> wanted = options?.Tables != null ? new HashSet<string>(options.Tables, StringComparer.OrdinalIgnoreCase) : null;
            foreach (IndexedTable table in catalog.Tables)
            {
                if (wanted != null && !wanted.Contains(table.Name))
                {
                    continue;
                }
                List<ValidatedFile> files = new();
                foreach (string path in table.FilePaths)
                {
                    if (LocalizationFiles.TryParseTableFileName(path, out _, out string language) && LocalizationFiles.Exists(path))
                    {
                        files.Add(new ValidatedFile(path, language, CatalogIndex.ReadTableDocument(path)));
                    }
                }
                tables.Add(new ValidatedTable(table.Name, files));
            }
            return ExportBook.Collect(catalog.Info, tables, options);
        }

        /// <summary>
        /// Exports <paramref name="catalog"/> as <paramref name="format"/> to <paramref name="path"/>: the workbook's
        /// file, or the folder CSV and XLIFF files go into. Returns the files written; cells and entries that can't be
        /// written are added to <paramref name="problems"/>.
        /// </summary>
        public static List<string> Export(IndexedCatalog catalog, ExchangeFormat format, ExportOptions options, string path, List<string> problems)
        {
            List<string> written = new();
            switch (format)
            {
                case ExchangeFormat.Xlsx:
                    ExportBook book = Collect(catalog, options);
                    Write(path, XlsxExchange.Write(book, problems), written);
                    break;
                case ExchangeFormat.Csv:
                    ExportBook tables = Collect(catalog, options);
                    for (int i = 0; i < tables.Tables.Count; i++)
                    {
                        Write(Path.Combine(path, $"{tables.Tables[i].Name}.csv"), CsvExchange.Write(tables, tables.Tables[i]), written);
                    }
                    break;
                case ExchangeFormat.Xliff12:
                case ExchangeFormat.Xliff20:
                    XliffVersion version = format == ExchangeFormat.Xliff12 ? XliffVersion.Version12 : XliffVersion.Version20;
                    foreach (string language in GetLanguages(catalog, options))
                    {
                        ExportBook languageBook = Collect(catalog, ForLanguage(options, language));
                        if (languageBook.RowCount > 0)
                        {
                            Write(Path.Combine(path, $"{catalog.Name}.{language}.xlf"), XliffExchange.Write(languageBook, version, problems), written);
                        }
                    }
                    break;
                default:
                    throw new ArgumentException("The LLM handoff is text for the clipboard; create it with CreateHandoff.", nameof(format));
            }
            return written;
        }

        /// <summary>Returns the LLM handoff of <paramref name="catalog"/> in <paramref name="language"/>.</summary>
        public static string CreateHandoff(IndexedCatalog catalog, ExportOptions options, string language)
        {
            return LlmHandoff.Write(Collect(catalog, ForLanguage(options, language)));
        }

        /// <summary>Reads an exchange file by its extension. A .txt file is an LLM reply when it has @table lines, and tab-separated values otherwise.</summary>
        public static ImportedFile Read(string path)
        {
            string name = Path.GetFileName(path);
            byte[] data;
            try
            {
                data = File.ReadAllBytes(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                ImportedFile unreadable = new(name);
                unreadable.Problems.Add($"{name}: it can't be read: {exception.Message}");
                return unreadable;
            }
            string extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
            switch (extension)
            {
                case "xlsx":
                    return XlsxExchange.Read(name, data);
                case "xlf":
                case "xliff":
                    return XliffExchange.Read(name, data);
                case "txt":
                    string text = Encoding.UTF8.GetString(data);
                    return HasTableLines(text) ? LlmHandoff.Read(name, text) : CsvExchange.Read(name, data);
                case "csv":
                case "tsv":
                    return CsvExchange.Read(name, data);
                default:
                    ImportedFile unknown = new(name);
                    unknown.Problems.Add($"{name}: imports read {string.Join(", ", Extensions)} files, not .{extension}.");
                    return unknown;
            }
        }

        /// <summary>Reads every exchange file directly inside <paramref name="folder"/>, as an export to a folder wrote them.</summary>
        public static List<ImportedFile> ReadFolder(string folder)
        {
            List<string> paths = new();
            foreach (string path in Directory.GetFiles(folder))
            {
                string extension = Path.GetExtension(path).TrimStart('.');
                if (Array.Exists(Extensions, known => string.Equals(known, extension, StringComparison.OrdinalIgnoreCase)))
                {
                    paths.Add(path);
                }
            }
            paths.Sort(StringComparer.OrdinalIgnoreCase);
            List<ImportedFile> files = new(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                files.Add(Read(paths[i]));
            }
            return files;
        }

        /// <summary>
        /// Returns the catalog <paramref name="files"/> belong to: the one a file names, else the one with most of their
        /// tables, else the default catalog.
        /// </summary>
        public static IndexedCatalog FindCatalog(IReadOnlyList<ImportedFile> files)
        {
            for (int i = 0; i < files.Count; i++)
            {
                if (NameRules.IsValid(files[i].CatalogName) && CatalogIndex.Find(new CatalogKey(files[i].CatalogName)) is { Info: { } } named)
                {
                    return named;
                }
            }
            IndexedCatalog best = null;
            int bestCount = 0;
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.Info == null)
                {
                    continue;
                }
                HashSet<string> found = new(StringComparer.OrdinalIgnoreCase);
                for (int f = 0; f < files.Count; f++)
                {
                    for (int r = 0; r < files[f].Rows.Count; r++)
                    {
                        string table = files[f].Rows[r].TableName;
                        if (NameRules.IsValid(table) && catalog.TryGetTable(new TableKey(table), out _))
                        {
                            found.Add(table);
                        }
                    }
                }
                if (found.Count > bestCount)
                {
                    best = catalog;
                    bestCount = found.Count;
                }
            }
            if (best != null)
            {
                return best;
            }
            IndexedCatalog fallback = CatalogIndex.DefaultCatalog;
            if (fallback?.Info != null)
            {
                return fallback;
            }
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (catalog.Info != null)
                {
                    return catalog;
                }
            }
            return null;
        }

        private static IEnumerable<string> GetLanguages(IndexedCatalog catalog, ExportOptions options)
        {
            HashSet<string> wanted = options?.Languages != null ? new HashSet<string>(options.Languages, StringComparer.OrdinalIgnoreCase) : null;
            for (int i = 0; i < catalog.Info.Languages.Count; i++)
            {
                LanguageInfo language = catalog.Info.Languages[i];
                if (language != catalog.Info.SourceLanguage && (wanted == null || wanted.Contains(language.Name)))
                {
                    yield return language.Name;
                }
            }
        }

        private static ExportOptions ForLanguage(ExportOptions options, string language)
        {
            return new ExportOptions { Tables = options?.Tables, Languages = new[] { language }, Entries = options?.Entries ?? ExportedEntries.All };
        }

        private static void Write(string path, byte[] bytes, List<string> written)
        {
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllBytes(path, bytes);
            written.Add(path);
        }

        private static bool HasTableLines(string text)
        {
            return text.StartsWith("@table", StringComparison.OrdinalIgnoreCase) || text.IndexOf("\n@table", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
