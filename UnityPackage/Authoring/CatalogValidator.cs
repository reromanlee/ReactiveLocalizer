using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Checks a whole catalog: every file the way its import does, and what only the catalog as a whole shows, such as
    /// translations that are missing, outdated or too long, and saved references to keys that don't exist.
    /// </summary>
    /// <remarks>
    /// Errors are what a build must not ship: unreadable lines and merge markers, duplicate keys, orphans, broken
    /// messages, translations whose arguments don't match their source, entries missing in a required language, and
    /// references to keys that don't exist. Warnings are worth fixing but never fail a build: entries missing in other
    /// languages, outdated and unverified translations, texts over their maximum length, references by a former name,
    /// former names nothing uses anymore, and the optional naming convention.
    /// </remarks>
    public static class CatalogValidator
    {
        // Lists of keys in one message stop after this many, so a large table doesn't produce an unreadable line.
        private const int ListedKeys = 5;

        /// <summary>Validates <paramref name="tables"/> of <paramref name="catalog"/>, and <paramref name="uses"/> of their entries.</summary>
        /// <param name="catalog">The catalog: its languages, which of them are required, and its moved entries.</param>
        /// <param name="tables">Every table of the catalog, with its files.</param>
        /// <param name="uses">Saved references to the catalog's entries; null when they weren't looked for, which also skips the check for former names nothing uses.</param>
        /// <param name="options">Which optional checks run; null for the defaults.</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        public static ValidationReport Validate(CatalogInfo catalog, IReadOnlyList<ValidatedTable> tables, IReadOnlyList<EntryUse> uses, ValidationOptions options)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            options ??= ValidationOptions.Default;
            List<ValidationIssue> issues = new();
            List<ValidatedTable> sorted = new(tables ?? Array.Empty<ValidatedTable>());
            sorted.Sort((left, right) => NaturalOrder.Instance.Compare(left.Name, right.Name));
            Dictionary<ulong, KnownTable> known = new();
            for (int i = 0; i < sorted.Count; i++)
            {
                ValidatedTable table = sorted[i];
                if (!NameRules.IsValid(table.Name))
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Error, string.Empty, $"'{table.Name}' can't be a table name: {NameRules.Description}."));
                    continue;
                }
                KnownTable checkedTable = ValidateTable(catalog, table, options, issues);
                known[Hashing.ComputeNameHash(table.Name)] = checkedTable;
            }
            if (uses != null)
            {
                ValidateUses(known, uses, issues);
                ReportUnusedAliases(known, issues);
            }
            return new ValidationReport(issues);
        }

        private static KnownTable ValidateTable(CatalogInfo catalog, ValidatedTable table, ValidationOptions options, List<ValidationIssue> issues)
        {
            ValidatedFile source = null;
            List<ValidatedFile> translations = new();
            for (int i = 0; i < table.Files.Count; i++)
            {
                ValidatedFile file = table.Files[i];
                if (string.Equals(file.LanguageName, catalog.SourceLanguage.Name, StringComparison.OrdinalIgnoreCase))
                {
                    source = file;
                }
                else
                {
                    translations.Add(file);
                }
            }
            translations.Sort((left, right) => NaturalOrder.Instance.Compare(left.LanguageName, right.LanguageName));
            KnownTable known = new(table.Name, source);

            if (source == null)
            {
                string location = translations.Count > 0 ? translations[0].Path : string.Empty;
                issues.Add(new ValidationIssue(IssueSeverity.Error, location,
                    $"'{table.Name}' has no {catalog.SourceLanguage.Name} file, the language that defines its keys, so none of its translations can be shown."));
            }
            else
            {
                ValidateFile(catalog, table.Name, source, null, issues);
                CheckMaximumLengths(source, source, catalog.SourceLanguage.Name, issues);
                if (options.IsCheckingPascalCase)
                {
                    CheckPascalCase(table.Name, source, issues);
                }
            }
            for (int i = 0; i < translations.Count; i++)
            {
                ValidateFile(catalog, table.Name, translations[i], source, issues);
                if (source != null)
                {
                    CheckFingerprints(translations[i], source, issues);
                    CheckMaximumLengths(translations[i], source, translations[i].LanguageName, issues);
                }
            }
            if (source != null)
            {
                CheckMissing(catalog, table.Name, source, translations, issues);
            }
            return known;
        }

        /// <summary>Reports what reading and compiling the file finds, exactly as its import does.</summary>
        private static void ValidateFile(CatalogInfo catalog, string tableName, ValidatedFile file, ValidatedFile source, List<ValidationIssue> issues)
        {
            List<DocumentIssue> found = new(file.Document.Issues);
            if (NameRules.IsValid(file.LanguageName))
            {
                TableCompiler.Compile(catalog, new TableKey(tableName), new LanguageKey(file.LanguageName), file.Document, source?.Document, found);
            }
            else
            {
                found.Add(new DocumentIssue(IssueSeverity.Error, 1, 1, $"'{file.LanguageName}' can't be a language name: {NameRules.Description}."));
            }
            for (int i = 0; i < found.Count; i++)
            {
                issues.Add(new ValidationIssue(found[i].Severity, $"{file.Path}({found[i].Line},{found[i].Column})", found[i].Message));
            }
        }

        private static void CheckMissing(CatalogInfo catalog, string tableName, ValidatedFile source, List<ValidatedFile> translations, List<ValidationIssue> issues)
        {
            IReadOnlyList<LanguageInfo> languages = catalog.Languages;
            for (int l = 0; l < languages.Count; l++)
            {
                LanguageInfo language = languages[l];
                if (language == catalog.SourceLanguage)
                {
                    continue;
                }
                ValidatedFile file = translations.Find(candidate => string.Equals(candidate.LanguageName, language.Name, StringComparison.OrdinalIgnoreCase));
                List<string> missing = new();
                IReadOnlyList<TableDocumentEntry> entries = source.Document.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (file == null || !file.Document.TryGetEntry(entries[i].Key, out _))
                    {
                        missing.Add(entries[i].Key);
                    }
                }
                if (missing.Count == 0)
                {
                    continue;
                }
                string location = file?.Path ?? source.Path;
                string keys = ListKeys(missing);
                issues.Add(language.IsRequired
                    ? new ValidationIssue(IssueSeverity.Error, location, $"{language.Name} is required to have every entry, but '{tableName}' lacks {Count(missing.Count, "entry", "entries")} in it: {keys}.")
                    : new ValidationIssue(IssueSeverity.Warning, location, $"{language.Name} lacks {Count(missing.Count, "entry", "entries")} of '{tableName}', shown in its fallback language: {keys}."));
            }
        }

        /// <summary>Reports, once per file, the translations made from an older source text and those nothing says which source text they follow.</summary>
        private static void CheckFingerprints(ValidatedFile file, ValidatedFile source, List<ValidationIssue> issues)
        {
            List<string> outdated = new();
            List<string> unverified = new();
            int outdatedLine = 0;
            int unverifiedLine = 0;
            IReadOnlyList<TableDocumentEntry> entries = file.Document.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                TableDocumentEntry entry = entries[i];
                if (!source.Document.TryGetEntry(entry.Key, out TableDocumentEntry sourceEntry))
                {
                    continue;
                }
                if (!entry.HasFingerprint)
                {
                    unverified.Add(entry.Key);
                    unverifiedLine = unverifiedLine == 0 ? entry.Line : unverifiedLine;
                }
                else if (entry.Fingerprint != Hashing.ComputeFingerprint(sourceEntry.Value))
                {
                    outdated.Add(entry.Key);
                    outdatedLine = outdatedLine == 0 ? entry.Line : outdatedLine;
                }
            }
            if (outdated.Count > 0)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Warning, $"{file.Path}({outdatedLine},1)",
                    $"{Count(outdated.Count, "translation is", "translations are")} outdated, as their source text changed since: {ListKeys(outdated)}."));
            }
            if (unverified.Count > 0)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Warning, $"{file.Path}({unverifiedLine},1)",
                    $"{Count(unverified.Count, "translation has", "translations have")} no fingerprint, so nothing tells whether they follow the current source text: {ListKeys(unverified)}."));
            }
        }

        private static void CheckMaximumLengths(ValidatedFile file, ValidatedFile source, string languageName, List<ValidationIssue> issues)
        {
            IReadOnlyList<TableDocumentEntry> entries = file.Document.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                TableDocumentEntry entry = entries[i];
                if (!source.Document.TryGetEntry(entry.Key, out TableDocumentEntry sourceEntry) ||
                    !sourceEntry.TryGetAttribute(DocumentNames.MaximumLength, out string limitText))
                {
                    continue;
                }
                if (!int.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out int limit) || limit <= 0)
                {
                    // Reported once, with the source entry that carries it.
                    if (file == source)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, $"{file.Path}({entry.Line},1)",
                            $"'@maximumLength {limitText}' of '{entry.Key}' isn't a number of characters, such as '@maximumLength 16', so it checks nothing."));
                    }
                    continue;
                }
                // A message's length depends on its arguments, so only plain text is measured.
                if (entry.Value.IndexOf('{') < 0 && entry.Value.Length > limit)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, $"{file.Path}({entry.Line},{entry.GetColumn(0)})",
                        $"'{entry.Key}' is {entry.Value.Length} characters long in {languageName}, over its maximum of {limit}."));
                }
            }
        }

        private static void CheckPascalCase(string tableName, ValidatedFile source, List<ValidationIssue> issues)
        {
            if (!IsPascalCase(tableName))
            {
                issues.Add(new ValidationIssue(IssueSeverity.Warning, source.Path, $"The table name '{tableName}' doesn't follow the PascalCase convention, such as 'MainMenu'."));
            }
            IReadOnlyList<TableDocumentEntry> entries = source.Document.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!IsPascalCase(entries[i].Key))
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, $"{source.Path}({entries[i].Line},1)",
                        $"'{entries[i].Key}' doesn't follow the PascalCase convention, such as 'PlayButton'."));
                }
            }
        }

        private static void ValidateUses(Dictionary<ulong, KnownTable> tables, IReadOnlyList<EntryUse> uses, List<ValidationIssue> issues)
        {
            List<(string TableName, TableDocument Source)> sources = new();
            foreach (KnownTable table in tables.Values)
            {
                sources.Add((table.Name, table.Source?.Document));
            }
            KeyResolver resolver = new(sources);
            for (int i = 0; i < uses.Count; i++)
            {
                EntryUse use = uses[i];
                string key = $"{use.TableName}.{use.EntryName}";
                KeyResolution resolution = resolver.Resolve(use.TableName, use.EntryName);
                switch (resolution.Kind)
                {
                    case KeyResolutionKind.Found:
                        break;
                    case KeyResolutionKind.Renamed:
                    case KeyResolutionKind.Moved:
                        MarkUsed(tables, resolution);
                        string change = resolution.Kind == KeyResolutionKind.Renamed ? "former name; it was renamed" : "former key; it moved";
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, use.Location,
                            $"It refers to '{key}' by the entry's {change} to '{resolution.TableName}.{resolution.EntryName}'. Pick the entry again to update it."));
                        break;
                    case KeyResolutionKind.Invalid:
                        issues.Add(new ValidationIssue(IssueSeverity.Error, use.Location, $"It refers to '{key}', which isn't a valid key; pick the entry again."));
                        break;
                    default:
                        issues.Add(new ValidationIssue(IssueSeverity.Error, use.Location, $"It refers to '{key}', which doesn't exist.{DescribeSuggestion(tables, use, resolution)}"));
                        break;
                }
            }
        }

        private static string DescribeSuggestion(Dictionary<ulong, KnownTable> tables, EntryUse use, KeyResolution resolution)
        {
            bool hasTable = tables.TryGetValue(Hashing.ComputeNameHash(use.TableName), out KnownTable table) && table.Source != null;
            if (hasTable)
            {
                return resolution.Suggestion != null ? $" Did you mean '{resolution.Suggestion}'?" : string.Empty;
            }
            return resolution.Suggestion != null
                ? $" There is no table '{use.TableName}'; did you mean '{resolution.Suggestion}'?"
                : $" There is no table '{use.TableName}'.";
        }

        private static void MarkUsed(Dictionary<ulong, KnownTable> tables, KeyResolution resolution)
        {
            if (!tables.TryGetValue(Hashing.ComputeNameHash(resolution.TableName), out KnownTable table))
            {
                return;
            }
            for (int i = 0; i < table.Aliases.Count; i++)
            {
                if (string.Equals(table.Aliases[i].WrittenName, resolution.FormerName, StringComparison.OrdinalIgnoreCase))
                {
                    table.Aliases[i].IsUsed = true;
                }
            }
        }

        private static void ReportUnusedAliases(Dictionary<ulong, KnownTable> tables, List<ValidationIssue> issues)
        {
            List<KnownTable> sorted = new(tables.Values);
            sorted.Sort((left, right) => NaturalOrder.Instance.Compare(left.Name, right.Name));
            for (int t = 0; t < sorted.Count; t++)
            {
                foreach (KnownAlias alias in sorted[t].Aliases)
                {
                    if (!alias.IsUsed)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, $"{sorted[t].Source.Path}({alias.Line},1)",
                            $"No scene, prefab or asset refers to '{alias.WrittenName}', the former name of '{sorted[t].Name}.{alias.EntryName}', anymore. Once no code does either, which its [Obsolete] member shows, remove '@formerly {alias.WrittenName}'."));
                    }
                }
            }
        }

        private static bool IsPascalCase(string name)
        {
            return name.Length > 0 && (uint)(name[0] - 'A') <= 'Z' - 'A' && name.IndexOf('_') < 0;
        }

        private static string ListKeys(List<string> keys)
        {
            StringBuilder builder = new();
            int listed = Math.Min(keys.Count, ListedKeys);
            for (int i = 0; i < listed; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append(keys[i]);
            }
            if (keys.Count > listed)
            {
                builder.Append(" and ").Append(keys.Count - listed).Append(" more");
            }
            return builder.ToString();
        }

        private static string Count(int count, string one, string other) => count == 1 ? $"1 {one}" : $"{count} {other}";

        /// <summary>A table as the checks of references see it: its source file, and the former names its entries keep.</summary>
        private sealed class KnownTable
        {
            public KnownTable(string name, ValidatedFile source)
            {
                Name = name;
                Source = source;
                if (source == null)
                {
                    return;
                }
                IReadOnlyList<TableDocumentEntry> entries = source.Document.Entries;
                for (int e = 0; e < entries.Count; e++)
                {
                    for (int a = 0; a < entries[e].Attributes.Count; a++)
                    {
                        DocumentProperty attribute = entries[e].Attributes[a];
                        if (attribute.Name == DocumentNames.Formerly)
                        {
                            Aliases.Add(new KnownAlias(attribute.Value, entries[e].Key, attribute.Line));
                        }
                    }
                }
            }

            public string Name { get; }

            public ValidatedFile Source { get; }

            /// <summary>Every <c>@formerly</c> of the source file, in file order.</summary>
            public List<KnownAlias> Aliases { get; } = new();
        }

        private sealed class KnownAlias
        {
            public KnownAlias(string writtenName, string entryName, int line)
            {
                WrittenName = writtenName;
                EntryName = entryName;
                Line = line;
            }

            /// <summary>The alias as <c>@formerly</c> writes it.</summary>
            public string WrittenName { get; }

            /// <summary>The entry it is a former name of.</summary>
            public string EntryName { get; }

            public int Line { get; }

            public bool IsUsed { get; set; }
        }
    }
}
