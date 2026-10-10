using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// Plans an import of exchange files into a catalog's tables, the same way for every format, so a preview can show
    /// exactly what will change before anything is written.
    /// </summary>
    /// <remarks>
    /// Imports change translations only: they never add, rename or delete entries, a row for a key the source language
    /// doesn't have is rejected as an orphan, and an empty cell never erases a translation. The source text the file
    /// holds is what the translator saw, so a translation is stamped with its fingerprint, and arrives outdated when
    /// the source changed since the export; a fingerprint tag the file carries, as an LLM reply does, wins. A text
    /// equal to the current one changes nothing, so an outdated translation a translator left as it was stays
    /// outdated, unless the file marks it as checked. Every changed file is compiled as its import compiles it, and
    /// texts that fail are rejected with the reason.
    /// </remarks>
    public static class ImportPlanner
    {
        /// <summary>
        /// Decides which catalog language each language of <paramref name="file"/> is, from its candidates: a language
        /// name, else a culture tag only one language has, else a tag whose base language only one has, as <c>ru</c>
        /// for <c>ru-RU</c>. A language nothing identifies is left null, to be skipped or chosen by a person.
        /// </summary>
        public static void ResolveLanguages(CatalogInfo catalog, ImportedFile file)
        {
            for (int i = 0; i < file.Languages.Count; i++)
            {
                file.Languages[i].LanguageName = Resolve(catalog, file.Languages[i].Candidates);
            }
        }

        /// <summary>
        /// Plans importing <paramref name="files"/>, whose languages are resolved, into <paramref name="catalog"/>. The
        /// files of every table the import touches are changed in memory; writing them applies the plan.
        /// </summary>
        /// <param name="catalog">The catalog imported into.</param>
        /// <param name="files">The files to import, in order; a text imported twice keeps its first.</param>
        /// <param name="openTable">Opens a table's files by name, once per table; null for a table the catalog doesn't have.</param>
        /// <param name="options">How to import; null for the defaults.</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="openTable"/> is null.</exception>
        public static ImportPlan Plan(CatalogInfo catalog, IReadOnlyList<ImportedFile> files, Func<string, TableFileSet> openTable, ImportOptions options)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (openTable == null)
            {
                throw new ArgumentNullException(nameof(openTable));
            }
            Planning planning = new(catalog, openTable, options ?? new ImportOptions());
            for (int i = 0; files != null && i < files.Count; i++)
            {
                planning.Add(files[i]);
            }
            return planning.Finish();
        }

        private static string Resolve(CatalogInfo catalog, IReadOnlyList<string> candidates)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    string candidate = candidates[i];
                    if (string.IsNullOrEmpty(candidate))
                    {
                        continue;
                    }
                    if (pass == 0)
                    {
                        for (int l = 0; l < catalog.Languages.Count; l++)
                        {
                            if (string.Equals(catalog.Languages[l].Name, candidate, StringComparison.OrdinalIgnoreCase))
                            {
                                return catalog.Languages[l].Name;
                            }
                        }
                    }
                    string tag = NormalizeTag(candidate, pass == 1);
                    LanguageInfo match = null;
                    int matches = 0;
                    for (int l = 0; l < catalog.Languages.Count; l++)
                    {
                        string culture = catalog.Languages[l].Culture;
                        if (!string.IsNullOrEmpty(culture) && string.Equals(NormalizeTag(culture, pass == 1), tag, StringComparison.OrdinalIgnoreCase))
                        {
                            match ??= catalog.Languages[l];
                            matches++;
                        }
                    }
                    if (matches == 1)
                    {
                        return match.Name;
                    }
                }
            }
            return null;
        }

        /// <summary>Returns a culture tag with hyphens, cut to its base language when <paramref name="isBaseOnly"/>.</summary>
        private static string NormalizeTag(string tag, bool isBaseOnly)
        {
            string normalized = tag.Trim().Replace('_', '-');
            int separator = normalized.IndexOf('-');
            return isBaseOnly && separator > 0 ? normalized.Substring(0, separator) : normalized;
        }

        /// <summary>The state of one planning: the tables opened, the changes made to them, and what to report.</summary>
        private sealed class Planning
        {
            private readonly CatalogInfo _catalog;
            private readonly Func<string, TableFileSet> _openTable;
            private readonly ImportOptions _options;
            private readonly Dictionary<string, TableFileSet> _tables = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, int> _unknownTableRows = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, string> _claimed = new(StringComparer.OrdinalIgnoreCase);
            private readonly List<Pending> _pending = new();
            private readonly List<ImportChange> _changes = new();
            private readonly List<string> _problems = new();
            private int _unchanged;
            private int _changedSources;

            public Planning(CatalogInfo catalog, Func<string, TableFileSet> openTable, ImportOptions options)
            {
                _catalog = catalog;
                _openTable = openTable;
                _options = options;
            }

            private string SourceName => _catalog.SourceLanguage.Name;

            public void Add(ImportedFile file)
            {
                _problems.AddRange(file.Problems);
                int sourceIndex = -1;
                List<(int Index, string Name)> translations = new();
                for (int i = 0; i < file.Languages.Count; i++)
                {
                    ImportedLanguage language = file.Languages[i];
                    string name = FindLanguage(language.LanguageName);
                    if (name == null)
                    {
                        _problems.Add(language.IsSourceOnly
                            ? $"{file.Name}: its source language '{language.Label}' isn't {SourceName}, so its translations can't be checked against the texts they were made from and arrive unverified."
                            : $"{file.Name}: '{language.Label}' isn't a language of the catalog '{_catalog.Key.Name}', so its texts are skipped.");
                        continue;
                    }
                    if (string.Equals(name, SourceName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (sourceIndex >= 0)
                        {
                            _problems.Add($"{file.Name}: '{file.Languages[sourceIndex].Label}' and '{language.Label}' both hold {SourceName}, so the second is skipped.");
                            continue;
                        }
                        sourceIndex = i;
                        continue;
                    }
                    if (language.IsSourceOnly)
                    {
                        _problems.Add($"{file.Name}: its source language '{language.Label}' is {name}, not {SourceName}, so its translations can't be checked against the texts they were made from and arrive unverified.");
                        continue;
                    }
                    if (translations.Exists(taken => string.Equals(taken.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _problems.Add($"{file.Name}: two of its languages are {name}, so '{language.Label}' is skipped.");
                        continue;
                    }
                    translations.Add((i, name));
                }
                for (int i = 0; i < file.Rows.Count; i++)
                {
                    AddRow(file.Rows[i], sourceIndex, translations);
                }
            }

            private void AddRow(ImportedRow row, int sourceIndex, List<(int Index, string Name)> translations)
            {
                string sourceText = row.GetText(sourceIndex);
                bool hasText = sourceText != null && _options.IsImportingSourceChanges;
                for (int t = 0; t < translations.Count && !hasText; t++)
                {
                    hasText = !string.IsNullOrEmpty(row.GetText(translations[t].Index));
                }
                if (!hasText)
                {
                    return;
                }
                if (!NameRules.IsValid(row.TableName) || !NameRules.IsValid(row.Key))
                {
                    Reject(row, null, null, $"'{row}' isn't a key written as Table.Key: {NameRules.Description}.");
                    return;
                }
                TableFileSet set = Open(row.TableName);
                if (set == null)
                {
                    _unknownTableRows.TryGetValue(row.TableName, out int count);
                    _unknownTableRows[row.TableName] = count + 1;
                    return;
                }
                if (set.Source == null || !set.Source.TryGetEntry(row.Key, out TableFileEntry source))
                {
                    string suggestion = set.Source != null ? NameDistance.FindClosest(row.Key, EnumerateKeys(set.Source)) : null;
                    Reject(row, null, null, $"{SourceName} has no entry '{row}', and imports never add entries.{(suggestion != null ? $" Did you mean '{suggestion}'?" : string.Empty)}");
                    return;
                }
                string key = source.Key;
                if (!string.IsNullOrEmpty(sourceText) && !string.Equals(sourceText, source.Value, StringComparison.Ordinal))
                {
                    if (_options.IsImportingSourceChanges)
                    {
                        ChangeSource(row, set, source, sourceText);
                    }
                    else
                    {
                        _changedSources++;
                    }
                }
                bool hasFingerprint = row.HasFingerprint || sourceText != null;
                uint fingerprint = row.HasFingerprint ? row.Fingerprint : sourceText != null ? Hashing.ComputeFingerprint(sourceText) : 0;
                for (int t = 0; t < translations.Count; t++)
                {
                    string text = row.GetText(translations[t].Index);
                    if (!string.IsNullOrEmpty(text))
                    {
                        AddTranslation(row, set, key, translations[t].Name, text, hasFingerprint, fingerprint);
                    }
                }
            }

            private void ChangeSource(ImportedRow row, TableFileSet set, TableFileEntry source, string text)
            {
                if (!TryClaim(row, set.TableName, source.Key, SourceName))
                {
                    return;
                }
                Pending pending = new(set, SourceName, source.Key, true, source);
                pending.Change = new ImportChange(ImportChangeKind.SourceChanged, set.TableName, source.Key, SourceName, source.Value, text, row.Location);
                if (!set.TrySetText(SourceName, source.Key, text, out string problem))
                {
                    pending.Reject(problem);
                }
                Record(pending);
            }

            private void AddTranslation(ImportedRow row, TableFileSet set, string key, string language, string text, bool hasFingerprint, uint fingerprint)
            {
                if (!TryClaim(row, set.TableName, key, language))
                {
                    return;
                }
                TableFileEntry existing = null;
                set.Get(language)?.TryGetEntry(key, out existing);
                Pending pending = new(set, language, key, existing != null, existing) { HasFingerprint = hasFingerprint, Fingerprint = fingerprint };
                string problem;
                if (existing != null && string.Equals(existing.Value, text, StringComparison.Ordinal))
                {
                    set.Source.TryGetEntry(key, out TableFileEntry source);
                    bool isConfirmed = row.IsConfirmed && hasFingerprint && fingerprint == Hashing.ComputeFingerprint(source.Value) &&
                                       !(existing.HasFingerprint && existing.Fingerprint == fingerprint);
                    if (!isConfirmed)
                    {
                        _unchanged++;
                        return;
                    }
                    pending.Change = new ImportChange(ImportChangeKind.Confirmed, set.TableName, key, language, existing.Value, text, row.Location);
                    if (!set.TryMarkCurrent(language, key, out problem))
                    {
                        pending.Reject(problem);
                    }
                    Record(pending);
                    return;
                }
                ImportChangeKind kind = existing == null ? ImportChangeKind.Added : ImportChangeKind.Updated;
                pending.Change = new ImportChange(kind, set.TableName, key, language, existing?.Value, text, row.Location);
                if (!set.TrySetText(language, key, text, out problem))
                {
                    pending.Reject(problem);
                }
                else
                {
                    set.Get(language).TryGetEntry(key, out TableFileEntry entry);
                    entry.HasFingerprint = hasFingerprint;
                    entry.Fingerprint = hasFingerprint ? fingerprint : 0;
                    if (!hasFingerprint)
                    {
                        pending.Change.AddMessage($"The file holds no {SourceName} text it was made from, so it arrives unverified.");
                    }
                }
                Record(pending);
            }

            /// <summary>Compiles every changed file, rejects the texts that fail, and returns the plan.</summary>
            public ImportPlan Finish()
            {
                Dictionary<TableFileSet, List<Pending>> byTable = new();
                for (int i = 0; i < _pending.Count; i++)
                {
                    if (_pending[i].Change.Kind == ImportChangeKind.Rejected)
                    {
                        continue;
                    }
                    if (!byTable.TryGetValue(_pending[i].Set, out List<Pending> pending))
                    {
                        pending = new List<Pending>();
                        byTable.Add(_pending[i].Set, pending);
                    }
                    pending.Add(_pending[i]);
                }
                foreach (KeyValuePair<TableFileSet, List<Pending>> table in byTable)
                {
                    Check(table.Key, table.Value);
                }
                foreach (KeyValuePair<string, int> table in _unknownTableRows)
                {
                    _problems.Add($"The catalog '{_catalog.Key.Name}' has no table '{table.Key}', so {table.Value} of the rows naming it {(table.Value == 1 ? "is" : "are")} skipped.");
                }
                if (_changedSources > 0)
                {
                    _problems.Add($"{_changedSources} {SourceName} {(_changedSources == 1 ? "text differs" : "texts differ")} from the current ones, so translations made from them arrive outdated. " +
                                  "When the file's source texts were edited on purpose, import source changes too.");
                }
                return new ImportPlan(_changes, _problems, _unchanged);
            }

            private void Check(TableFileSet set, List<Pending> pending)
            {
                Dictionary<string, List<DocumentIssue>> byKey = new(StringComparer.OrdinalIgnoreCase);
                List<DocumentIssue> fileIssues = new();
                TableDocument source = TableDocument.Parse(set.Source.Write());
                if (pending.Exists(change => change.IsSource))
                {
                    EntryIssues.Collect(_catalog, set.TableName, SourceName, source, null, byKey, fileIssues);
                    if (RejectFailed(pending, SourceName, byKey))
                    {
                        source = TableDocument.Parse(set.Source.Write());
                    }
                }
                HashSet<string> languages = new(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < pending.Count; i++)
                {
                    if (!pending[i].IsSource && languages.Add(pending[i].Language))
                    {
                        byKey.Clear();
                        TableDocument document = TableDocument.Parse(set.Get(pending[i].Language).Write());
                        EntryIssues.Collect(_catalog, set.TableName, pending[i].Language, document, source, byKey, fileIssues);
                        RejectFailed(pending, pending[i].Language, byKey);
                    }
                }
                for (int i = 0; i < pending.Count; i++)
                {
                    Pending change = pending[i];
                    bool isTranslated = change.Change.Kind == ImportChangeKind.Added || change.Change.Kind == ImportChangeKind.Updated;
                    if (isTranslated && change.HasFingerprint && set.Source.TryGetEntry(change.Key, out TableFileEntry sourceEntry) &&
                        change.Fingerprint != Hashing.ComputeFingerprint(sourceEntry.Value))
                    {
                        change.Change.Kind = ImportChangeKind.Outdated;
                        change.Change.AddMessage($"It was made from another {SourceName} text than the current one, so it arrives outdated.");
                    }
                }
            }

            /// <summary>Rejects the changes in <paramref name="language"/> whose entry compiling fails, attaching warnings to the rest. Returns whether any was rejected.</summary>
            private static bool RejectFailed(List<Pending> pending, string language, Dictionary<string, List<DocumentIssue>> byKey)
            {
                bool hasRejected = false;
                for (int i = 0; i < pending.Count; i++)
                {
                    Pending change = pending[i];
                    if (change.Change.Kind == ImportChangeKind.Rejected || !string.Equals(change.Language, language, StringComparison.OrdinalIgnoreCase) ||
                        !byKey.TryGetValue(change.Key, out List<DocumentIssue> issues))
                    {
                        continue;
                    }
                    string errors = null;
                    for (int e = 0; e < issues.Count; e++)
                    {
                        if (issues[e].Severity == IssueSeverity.Error)
                        {
                            errors = errors == null ? issues[e].Message : errors + "\n" + issues[e].Message;
                        }
                        else
                        {
                            change.Change.AddMessage(issues[e].Message);
                        }
                    }
                    if (errors != null)
                    {
                        change.Revert();
                        change.Reject(errors);
                        hasRejected = true;
                    }
                }
                return hasRejected;
            }

            private void Record(Pending pending)
            {
                _pending.Add(pending);
                _changes.Add(pending.Change);
            }

            private void Reject(ImportedRow row, string tableName, string language, string message)
            {
                ImportChange change = new(ImportChangeKind.Rejected, tableName ?? row.TableName, row.Key, language, null, null, row.Location);
                change.AddMessage(message);
                _changes.Add(change);
            }

            /// <summary>Claims a text of an entry in a language for <paramref name="row"/>; false, rejecting it, when an earlier row already did.</summary>
            private bool TryClaim(ImportedRow row, string tableName, string key, string language)
            {
                string claim = $"{tableName}.{key}.{language}";
                if (_claimed.TryGetValue(claim, out string location))
                {
                    Reject(row, tableName, language, $"The file holds it twice; the text from {location} is the one imported.");
                    return false;
                }
                _claimed.Add(claim, row.Location);
                return true;
            }

            private TableFileSet Open(string tableName)
            {
                if (!_tables.TryGetValue(tableName, out TableFileSet set))
                {
                    set = _openTable(tableName);
                    _tables.Add(tableName, set);
                }
                return set;
            }

            private string FindLanguage(string name)
            {
                return name != null && NameRules.IsValid(name) && _catalog.TryGetLanguage(new LanguageKey(name), out LanguageInfo language) ? language.Name : null;
            }

            private static IEnumerable<string> EnumerateKeys(TableFile file)
            {
                for (int i = 0; i < file.Entries.Count; i++)
                {
                    yield return file.Entries[i].Key;
                }
            }
        }

        /// <summary>A change made to a table's files, with what the entry was before, so a check that fails can take it back.</summary>
        private sealed class Pending
        {
            private readonly bool _hadEntry;
            private readonly string _oldValue;
            private readonly bool _oldHasFingerprint;
            private readonly uint _oldFingerprint;

            public Pending(TableFileSet set, string language, string key, bool hadEntry, TableFileEntry existing)
            {
                Set = set;
                Language = language;
                Key = key;
                _hadEntry = hadEntry;
                IsSource = string.Equals(language, set.SourceLanguage, StringComparison.OrdinalIgnoreCase);
                if (existing != null)
                {
                    _oldValue = existing.Value;
                    _oldHasFingerprint = existing.HasFingerprint;
                    _oldFingerprint = existing.Fingerprint;
                }
            }

            public TableFileSet Set { get; }

            public string Language { get; }

            public string Key { get; }

            public bool IsSource { get; }

            public bool HasFingerprint { get; set; }

            public uint Fingerprint { get; set; }

            public ImportChange Change { get; set; }

            public void Reject(string message)
            {
                Change.Kind = ImportChangeKind.Rejected;
                Change.AddMessage(message);
            }

            /// <summary>Puts the entry back as it was.</summary>
            public void Revert()
            {
                TableFile file = Set.Get(Language);
                if (file == null)
                {
                    return;
                }
                if (!_hadEntry)
                {
                    file.RemoveEntry(Key);
                    return;
                }
                if (file.TryGetEntry(Key, out TableFileEntry entry))
                {
                    entry.Value = _oldValue;
                    entry.HasFingerprint = _oldHasFingerprint;
                    entry.Fingerprint = _oldFingerprint;
                }
            }
        }
    }
}
