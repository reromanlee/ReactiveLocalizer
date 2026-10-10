using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Formatting;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// A catalog: its languages, the source language its tables are written in, and its tables.
    /// </summary>
    /// <remarks>Never changes after it is created, so any number of threads can read it at once.</remarks>
    public sealed class CatalogInfo
    {
        private readonly Dictionary<ulong, LanguageInfo> _languages;
        private readonly Dictionary<ulong, TableInfo> _tables;
        private readonly Dictionary<ulong, LanguageFormat> _formats;

        /// <summary>Creates a catalog definition.</summary>
        /// <exception cref="ArgumentException">
        /// <paramref name="key"/> is empty; there are no languages; two languages or two tables share a name; the
        /// source language or a fallback is not among the languages; or fallbacks form a loop.
        /// </exception>
        public CatalogInfo(CatalogKey key, LanguageKey sourceLanguage, IReadOnlyList<LanguageInfo> languages, IReadOnlyList<TableInfo> tables)
            : this(key, sourceLanguage, languages, tables, null)
        {
        }

        /// <param name="key">Identity of the catalog.</param>
        /// <param name="sourceLanguage">The language the catalog's tables are written in.</param>
        /// <param name="languages">The catalog's languages.</param>
        /// <param name="tables">The catalog's tables.</param>
        /// <param name="movedEntries">The key each entry moved from another table had, with its key now; null for none.</param>
        internal CatalogInfo(CatalogKey key, LanguageKey sourceLanguage, IReadOnlyList<LanguageInfo> languages, IReadOnlyList<TableInfo> tables,
            IReadOnlyDictionary<(ulong Table, ulong Entry), EntryKey> movedEntries)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A catalog needs a key.", nameof(key));
            }
            if (languages == null || languages.Count == 0)
            {
                throw new ArgumentException($"The catalog '{key.Name}' needs at least one language.", nameof(languages));
            }
            _languages = new Dictionary<ulong, LanguageInfo>(languages.Count);
            for (int i = 0; i < languages.Count; i++)
            {
                LanguageInfo language = languages[i] ?? throw new ArgumentException("A catalog's languages can't be null.", nameof(languages));
                if (!_languages.TryAdd(language.Key.Hash, language))
                {
                    throw new ArgumentException($"The catalog '{key.Name}' defines '{language.Name}' twice.", nameof(languages));
                }
            }
            if (!_languages.TryGetValue(sourceLanguage.Hash, out LanguageInfo source) || sourceLanguage.IsEmpty)
            {
                throw new ArgumentException($"The source language '{sourceLanguage.Name}' is not a language of the catalog '{key.Name}'.", nameof(sourceLanguage));
            }
            for (int i = 0; i < languages.Count; i++)
            {
                LanguageInfo language = languages[i];
                if (language.HasFallback && !_languages.ContainsKey(language.Fallback.Hash))
                {
                    throw new ArgumentException($"'{language.Name}' falls back to '{language.Fallback.Name}', which is not a language of the catalog '{key.Name}'.", nameof(languages));
                }
                if (FindLoop(language) != null)
                {
                    throw new ArgumentException($"The fallbacks of '{language.Name}' form a loop.", nameof(languages));
                }
            }
            IReadOnlyList<TableInfo> tableList = tables ?? Array.Empty<TableInfo>();
            _tables = new Dictionary<ulong, TableInfo>(tableList.Count);
            for (int i = 0; i < tableList.Count; i++)
            {
                TableInfo table = tableList[i] ?? throw new ArgumentException("A catalog's tables can't be null.", nameof(tables));
                if (!_tables.TryAdd(table.Key.Hash, table))
                {
                    throw new ArgumentException($"The catalog '{key.Name}' has two tables named '{table.Key.Name}'.", nameof(tables));
                }
            }
            Key = key;
            SourceLanguage = source;
            Languages = languages;
            Tables = tableList;
            MovedEntries = movedEntries ?? global::reromanlee.ReactiveLocalizer.Tables.MovedEntries.None;
            _formats = new Dictionary<ulong, LanguageFormat>(languages.Count);
            for (int i = 0; i < languages.Count; i++)
            {
                ResolveFormat(languages[i]);
            }
        }

        /// <summary>Identity of the catalog.</summary>
        public CatalogKey Key { get; }

        /// <summary>The language the catalog's tables are written in: it defines every key and is the last fallback.</summary>
        public LanguageInfo SourceLanguage { get; }

        /// <summary>The catalog's languages, in the order its file lists them.</summary>
        public IReadOnlyList<LanguageInfo> Languages { get; }

        /// <summary>The catalog's tables.</summary>
        public IReadOnlyList<TableInfo> Tables { get; }

        /// <summary>
        /// The key each entry moved from another table had, by its old table and entry hashes, with its key now, so
        /// saved references to an old key still find the entry.
        /// </summary>
        internal IReadOnlyDictionary<(ulong Table, ulong Entry), EntryKey> MovedEntries { get; }

        /// <summary>Returns the language with <paramref name="key"/>.</summary>
        public bool TryGetLanguage(LanguageKey key, out LanguageInfo language) => _languages.TryGetValue(key.Hash, out language) && !key.IsEmpty;

        /// <summary>Returns the table with <paramref name="key"/>.</summary>
        public bool TryGetTable(TableKey key, out TableInfo table) => _tables.TryGetValue(key.Hash, out table) && !key.IsEmpty;

        /// <summary>Returns the table whose name hashes to <paramref name="tableHash"/>.</summary>
        internal bool TryGetTable(ulong tableHash, out TableInfo table) => _tables.TryGetValue(tableHash, out table);

        /// <summary>
        /// Returns how <paramref name="language"/> formats messages: its plural rules and number symbols, from its
        /// culture, else inherited from its fallback, with its own symbols applied over either.
        /// </summary>
        internal LanguageFormat GetFormat(LanguageInfo language) =>
            language != null && _formats.TryGetValue(language.Key.Hash, out LanguageFormat format) ? format : LanguageFormat.Root;

        /// <summary>
        /// Returns the languages a lookup in <paramref name="language"/> tries, in order: the language itself, its
        /// fallbacks, then the source language. Each language appears once.
        /// </summary>
        public IReadOnlyList<LanguageInfo> GetFallbackChain(LanguageInfo language)
        {
            List<LanguageInfo> chain = new(4);
            LanguageInfo current = language;
            while (current != null && !chain.Contains(current))
            {
                chain.Add(current);
                current = current.HasFallback && _languages.TryGetValue(current.Fallback.Hash, out LanguageInfo fallback) ? fallback : null;
            }
            if (!chain.Contains(SourceLanguage))
            {
                chain.Add(SourceLanguage);
            }
            return chain;
        }

        /// <summary>
        /// Creates a catalog from its file, appending every problem to <paramref name="issues"/>. Returns null when the
        /// catalog can't be used: it names no valid source language, or defines no language at all.
        /// </summary>
        /// <remarks>
        /// A field with a bad value is reported and treated as absent, and a fallback that would form a loop is
        /// dropped, so one mistake costs only that field.
        /// </remarks>
        public static CatalogInfo FromDocument(CatalogKey key, CatalogDocument document, IReadOnlyList<TableInfo> tables, ICollection<DocumentIssue> issues)
        {
            if (document == null || key.IsEmpty)
            {
                return null;
            }
            if (document.Languages.Count == 0)
            {
                Report(issues, IssueSeverity.Error, 1, "The catalog defines no language. Add a section such as '[English]'.");
                return null;
            }

            // Collect the language names first, so fallbacks can point at languages defined further down.
            HashSet<ulong> names = new();
            for (int i = 0; i < document.Languages.Count; i++)
            {
                names.Add(Hashing.ComputeNameHash(document.Languages[i].Name));
            }

            List<LanguageInfo> languages = new(document.Languages.Count);
            for (int i = 0; i < document.Languages.Count; i++)
            {
                languages.Add(ReadLanguage(document.Languages[i], names, issues));
            }
            BreakFallbackLoops(languages, document, issues);

            if (!document.TryGetAttribute(DocumentNames.Source, out DocumentProperty sourceAttribute))
            {
                Report(issues, IssueSeverity.Error, 1, "The catalog names no source language. Add '@source' at its top, such as '@source English'.");
                return null;
            }
            if (!NameRules.IsValid(sourceAttribute.Value) || !names.Contains(Hashing.ComputeNameHash(sourceAttribute.Value)))
            {
                Report(issues, IssueSeverity.Error, sourceAttribute.Line, $"The source language '{sourceAttribute.Value}' is not one of the catalog's language sections.");
                return null;
            }
            return new CatalogInfo(key, new LanguageKey(sourceAttribute.Value), languages, tables);
        }

        /// <summary>
        /// Reads the language sections of <paramref name="document"/> without a catalog around them, as for a fan
        /// translation that adds its language to a game's catalog. Fallbacks may name languages the document doesn't
        /// define; they are checked when the languages are registered.
        /// </summary>
        /// <remarks>
        /// A field with a bad value is reported and treated as absent, as in a catalog file. The document's
        /// attributes, such as <c>@source</c>, are ignored.
        /// </remarks>
        public static IReadOnlyList<LanguageInfo> ReadLanguages(CatalogDocument document, ICollection<DocumentIssue> issues)
        {
            if (document == null)
            {
                return Array.Empty<LanguageInfo>();
            }
            LanguageInfo[] languages = new LanguageInfo[document.Languages.Count];
            for (int i = 0; i < languages.Length; i++)
            {
                languages[i] = ReadLanguage(document.Languages[i], null, issues);
            }
            return languages;
        }

        /// <summary>
        /// Returns this catalog with <paramref name="language"/> added after its languages, as a runtime registration
        /// does. Fails with a reason when the name is taken or the fallback isn't a language of the catalog.
        /// </summary>
        internal bool TryAddLanguage(LanguageInfo language, out CatalogInfo catalog, out string error)
        {
            catalog = null;
            if (_languages.ContainsKey(language.Key.Hash))
            {
                error = $"'{language.Name}' is already a language of the catalog '{Key.Name}'.";
                return false;
            }
            if (language.HasFallback && !_languages.ContainsKey(language.Fallback.Hash))
            {
                error = $"'{language.Name}' falls back to '{language.Fallback.Name}', which is not a language of the catalog '{Key.Name}'.";
                return false;
            }
            LanguageInfo[] languages = new LanguageInfo[Languages.Count + 1];
            for (int i = 0; i < Languages.Count; i++)
            {
                languages[i] = Languages[i];
            }
            languages[Languages.Count] = language;
            catalog = new CatalogInfo(Key, SourceLanguage.Key, languages, Tables, MovedEntries);
            error = null;
            return true;
        }

        /// <summary>Resolves the format of a language after the format of its fallback; the constructor already rejected loops.</summary>
        private LanguageFormat ResolveFormat(LanguageInfo language)
        {
            if (_formats.TryGetValue(language.Key.Hash, out LanguageFormat format))
            {
                return format;
            }
            LanguageFormat inherited = language.HasFallback && _languages.TryGetValue(language.Fallback.Hash, out LanguageInfo fallback)
                ? ResolveFormat(fallback)
                : LanguageFormat.Root;
            format = LanguageFormat.Resolve(language.Culture, inherited, language.Digits, language.DecimalSeparator, language.GroupSeparator, out _);
            _formats[language.Key.Hash] = format;
            return format;
        }

        /// <param name="section">The language's section.</param>
        /// <param name="names">The hashes of every language a fallback may name; null to accept any valid name.</param>
        /// <param name="issues">Where problems are appended; null to ignore them.</param>
        private static LanguageInfo ReadLanguage(CatalogDocumentLanguage section, HashSet<ulong> names, ICollection<DocumentIssue> issues)
        {
            LanguageKey key = new(section.Name);
            string displayName = section.TryGetField(DocumentNames.DisplayName, out DocumentProperty displayField) ? displayField.Value : string.Empty;

            string culture = string.Empty;
            if (section.TryGetField(DocumentNames.Culture, out DocumentProperty cultureField))
            {
                culture = cultureField.Value;
                if (culture.Length > 0 && !LooksLikeLanguageTag(culture))
                {
                    Report(issues, IssueSeverity.Warning, cultureField.Line, $"'{culture}' doesn't look like a language tag such as 'en' or 'pt-BR'; plural rules and number formatting may not recognize it.");
                }
                else if (culture.Length > 0 && !NumberSymbols.TryFind(culture, out _))
                {
                    string inherited = section.TryGetField(DocumentNames.Fallback, out _) ? "the fallback language" : "CLDR's root locale, with only the 'other' plural form";
                    Report(issues, IssueSeverity.Warning, cultureField.Line, $"CLDR doesn't know the culture '{culture}', so plural rules and number formatting come from {inherited}.");
                }
            }

            LanguageKey fallback = default;
            if (section.TryGetField(DocumentNames.Fallback, out DocumentProperty fallbackField) && fallbackField.Value.Length > 0)
            {
                if (!NameRules.IsValid(fallbackField.Value))
                {
                    Report(issues, IssueSeverity.Error, fallbackField.Line, $"'{section.Name}' falls back to '{fallbackField.Value}', which isn't a language name: {NameRules.Description}.");
                }
                else if (names != null && !names.Contains(Hashing.ComputeNameHash(fallbackField.Value)))
                {
                    Report(issues, IssueSeverity.Error, fallbackField.Line, $"'{section.Name}' falls back to '{fallbackField.Value}', which is not one of the catalog's language sections.");
                }
                else if (Hashing.ComputeNameHash(fallbackField.Value) == key.Hash)
                {
                    Report(issues, IssueSeverity.Error, fallbackField.Line, $"'{section.Name}' can't fall back to itself.");
                }
                else
                {
                    fallback = new LanguageKey(fallbackField.Value);
                }
            }

            TextDirection direction = TextDirection.LeftToRight;
            if (section.TryGetField(DocumentNames.Direction, out DocumentProperty directionField))
            {
                if (string.Equals(directionField.Value, nameof(TextDirection.RightToLeft), StringComparison.OrdinalIgnoreCase))
                {
                    direction = TextDirection.RightToLeft;
                }
                else if (!string.Equals(directionField.Value, nameof(TextDirection.LeftToRight), StringComparison.OrdinalIgnoreCase))
                {
                    Report(issues, IssueSeverity.Error, directionField.Line, $"Direction is 'LeftToRight' or 'RightToLeft', not '{directionField.Value}'.");
                }
            }

            bool isRequired = false;
            if (section.TryGetField(DocumentNames.Required, out DocumentProperty requiredField))
            {
                if (string.Equals(requiredField.Value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    isRequired = true;
                }
                else if (!string.Equals(requiredField.Value, "false", StringComparison.OrdinalIgnoreCase))
                {
                    Report(issues, IssueSeverity.Error, requiredField.Line, $"Required is 'true' or 'false', not '{requiredField.Value}'.");
                }
            }
            string digits = null;
            if (section.TryGetField(DocumentNames.Digits, out DocumentProperty digitsField))
            {
                if (NumberSymbols.AreValidDigits(digitsField.Value))
                {
                    digits = digitsField.Value;
                }
                else
                {
                    Report(issues, IssueSeverity.Error, digitsField.Line, "Digits are the ten digits from zero to nine, written in order, such as 0123456789.");
                }
            }
            string decimalSeparator = null;
            if (section.TryGetField(DocumentNames.DecimalSeparator, out DocumentProperty decimalField))
            {
                if (decimalField.Value.Length > 0)
                {
                    decimalSeparator = decimalField.Value;
                }
                else
                {
                    Report(issues, IssueSeverity.Error, decimalField.Line, "A decimal separator can't be empty; write an escape such as \u00A0 for a space.");
                }
            }
            // Present but empty means never grouping, which is why an absent field and an empty one differ.
            string groupSeparator = section.TryGetField(DocumentNames.GroupSeparator, out DocumentProperty groupField) ? groupField.Value : null;
            return new LanguageInfo(key, displayName, culture, fallback, direction, isRequired, digits, decimalSeparator, groupSeparator);
        }

        /// <summary>Drops the fallback of every language whose fallbacks lead back to it, reporting each loop once.</summary>
        private static void BreakFallbackLoops(List<LanguageInfo> languages, CatalogDocument document, ICollection<DocumentIssue> issues)
        {
            Dictionary<ulong, LanguageInfo> byHash = new(languages.Count);
            for (int i = 0; i < languages.Count; i++)
            {
                byHash[languages[i].Key.Hash] = languages[i];
            }
            for (int i = 0; i < languages.Count; i++)
            {
                LanguageInfo language = languages[i];
                if (FindLoop(language, byHash) == null)
                {
                    continue;
                }
                Report(issues, IssueSeverity.Error, document.Languages[i].Line, $"The fallbacks of '{language.Name}' lead back to it; its fallback is ignored.");
                LanguageInfo unlooped = new(language.Key, language.DisplayName, language.Culture, default, language.Direction, language.IsRequired,
                    language.Digits, language.DecimalSeparator, language.GroupSeparator);
                languages[i] = unlooped;
                byHash[language.Key.Hash] = unlooped;
            }
        }

        /// <summary>Returns the first language reached twice when following fallbacks from <paramref name="start"/>, or null.</summary>
        private LanguageInfo FindLoop(LanguageInfo start) => FindLoop(start, _languages);

        private static LanguageInfo FindLoop(LanguageInfo start, Dictionary<ulong, LanguageInfo> languages)
        {
            HashSet<ulong> visited = new();
            LanguageInfo current = start;
            while (current != null)
            {
                if (!visited.Add(current.Key.Hash))
                {
                    return current;
                }
                current = current.HasFallback && languages.TryGetValue(current.Fallback.Hash, out LanguageInfo fallback) ? fallback : null;
            }
            return null;
        }

        /// <summary>Returns whether a culture is shaped like a language tag: a 2 to 8 letter language, then subtags of letters and digits.</summary>
        private static bool LooksLikeLanguageTag(string culture)
        {
            string[] parts = culture.Split('-', '_');
            for (int p = 0; p < parts.Length; p++)
            {
                string part = parts[p];
                int minimum = p == 0 ? 2 : 1;
                if (part.Length < minimum || part.Length > 8)
                {
                    return false;
                }
                for (int i = 0; i < part.Length; i++)
                {
                    char character = part[i];
                    bool isLetter = (uint)((character | 0x20) - 'a') <= 'z' - 'a';
                    bool isDigit = (uint)(character - '0') <= 9;
                    if (!isLetter && (p == 0 || !isDigit))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static void Report(ICollection<DocumentIssue> issues, IssueSeverity severity, int line, string message)
        {
            issues?.Add(new DocumentIssue(severity, line, 1, message));
        }
    }
}
