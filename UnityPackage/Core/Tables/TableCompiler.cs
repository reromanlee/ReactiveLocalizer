using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Messages;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// Compiles a table document into the binary form localizers load. The importer compiles every table file with
    /// it, and a game can compile text it receives at runtime the same way, such as a fan translation from a mods
    /// folder.
    /// </summary>
    /// <remarks>
    /// Every value is an ICU message. A value without arguments is stored as its text, with ICU's quoting resolved; a
    /// value with arguments is stored compiled. A message with errors is reported at its line and column: a source
    /// language keeps its text as written so the entry still shows, and a translation leaves the entry out so it shows
    /// in its fallback language instead.
    /// </remarks>
    public static class TableCompiler
    {
        /// <summary>
        /// Compiles <paramref name="document"/> as <paramref name="table"/> in <paramref name="language"/> of
        /// <paramref name="catalog"/>, appending the problems compiling finds to <paramref name="issues"/>.
        /// </summary>
        /// <remarks>
        /// Without the catalog, messages are checked for their syntax only, and broken ones keep their text as a source
        /// language does. Lines the document already rejected are absent from it, so they are simply not compiled.
        /// Compiling itself rejects an entry or alias whose hash collides with another name of the table, and a
        /// <c>@formerly</c> that is not a valid name. Identical texts are stored once.
        /// </remarks>
        /// <exception cref="ArgumentException">A key is empty, or <paramref name="document"/> is null.</exception>
        public static byte[] Compile(CatalogKey catalog, TableKey table, LanguageKey language, TableDocument document, ICollection<DocumentIssue> issues)
        {
            if (catalog.IsEmpty || table.IsEmpty || language.IsEmpty)
            {
                throw new ArgumentException("A compiled table needs the keys of its catalog, table and language.");
            }
            if (document == null)
            {
                throw new ArgumentException("A compiled table needs a document to compile.", nameof(document));
            }
            return new Compilation(catalog, table, language, null, true, document, null, issues).Run();
        }

        /// <summary>
        /// Compiles <paramref name="document"/> with every check the catalog makes possible: plural messages are checked
        /// against the forms their language uses, and a translation's messages against the arguments of the source
        /// text in <paramref name="sourceDocument"/>, leaving out entries that don't match.
        /// </summary>
        /// <param name="catalog">The catalog the table belongs to.</param>
        /// <param name="table">The table being compiled.</param>
        /// <param name="language">The language of <paramref name="document"/>.</param>
        /// <param name="document">The table file in <paramref name="language"/>.</param>
        /// <param name="sourceDocument">The table file in the source language; null when it doesn't exist yet.</param>
        /// <param name="issues">Where problems are appended; null to ignore them.</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        /// <exception cref="ArgumentException">A key is empty, or <paramref name="document"/> is null.</exception>
        public static byte[] Compile(CatalogInfo catalog, TableKey table, LanguageKey language, TableDocument document,
            TableDocument sourceDocument, ICollection<DocumentIssue> issues)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (table.IsEmpty || language.IsEmpty)
            {
                throw new ArgumentException("A compiled table needs the keys of its table and language.");
            }
            if (document == null)
            {
                throw new ArgumentException("A compiled table needs a document to compile.", nameof(document));
            }
            bool isKnown = catalog.TryGetLanguage(language, out LanguageInfo languageInfo);
            if (!isKnown)
            {
                issues?.Add(new DocumentIssue(IssueSeverity.Error, 1, 1,
                    $"'{language.Name}' is not a language of the catalog '{catalog.Key.Name}'. Add a [{language.Name}] section to its catalog file."));
            }
            bool isSource = language == catalog.SourceLanguage.Key;
            return new Compilation(catalog.Key, table, language, isKnown ? catalog.GetFormat(languageInfo) : null,
                isSource, document, isSource ? null : sourceDocument, issues).Run();
        }

        private sealed class Compilation
        {
            private readonly CatalogKey _catalog;
            private readonly TableKey _table;
            private readonly LanguageKey _language;
            private readonly LanguageFormat _format;
            private readonly bool _isSource;
            private readonly TableDocument _document;
            private readonly SourceMessages _source;
            private readonly ICollection<DocumentIssue> _issues;

            public Compilation(CatalogKey catalog, TableKey table, LanguageKey language, LanguageFormat format,
                bool isSource, TableDocument document, TableDocument sourceDocument, ICollection<DocumentIssue> issues)
            {
                _catalog = catalog;
                _table = table;
                _language = language;
                _format = format;
                _isSource = isSource;
                _document = document;
                _source = sourceDocument != null ? new SourceMessages(sourceDocument) : null;
                _issues = issues;
            }

            public byte[] Run()
            {
                // Hash every entry, rejecting the vanishingly rare names whose hashes collide with another name.
                IReadOnlyList<TableDocumentEntry> entries = _document.Entries;
                Dictionary<ulong, string> names = new(entries.Count);
                List<CompiledEntry> compiled = new(entries.Count);
                for (int i = 0; i < entries.Count; i++)
                {
                    TableDocumentEntry entry = entries[i];
                    ulong hash = Hashing.ComputeNameHash(entry.Key);
                    if (names.TryGetValue(hash, out string other))
                    {
                        AddIssue(IssueSeverity.Error, entry.Line, 1, $"'{entry.Key}' has the same hash as '{other}', so it can't be told apart; rename one of them.");
                        continue;
                    }
                    names.Add(hash, entry.Key);
                    if (TryCompileEntry(entry, out string text, out ParsedMessage message))
                    {
                        compiled.Add(new CompiledEntry(hash, entry, text, message));
                    }
                }
                compiled.Sort((left, right) => left.Hash.CompareTo(right.Hash));

                // Lay out every text in one buffer and every message in one program, storing identical ones once.
                int entryCount = compiled.Count;
                ulong[] hashes = new ulong[entryCount];
                int[] starts = new int[entryCount];
                int[] lengths = new int[entryCount];
                CharacterPool characters = new();
                List<int> program = new();
                List<int> messageStarts = new();
                Dictionary<string, int> messageNumbers = new(StringComparer.Ordinal);
                for (int i = 0; i < entryCount; i++)
                {
                    CompiledEntry entry = compiled[i];
                    hashes[i] = entry.Hash;
                    if (entry.Message == null)
                    {
                        starts[i] = characters.Add(entry.Text);
                        lengths[i] = entry.Text.Length;
                        continue;
                    }
                    if (!messageNumbers.TryGetValue(entry.Entry.Value, out int number))
                    {
                        number = messageStarts.Count;
                        messageStarts.Add(MessageCompiler.Compile(entry.Message, program, characters));
                        messageNumbers.Add(entry.Entry.Value, number);
                    }
                    lengths[i] = ~number;
                }

                List<(ulong Hash, int Target)> aliases = CollectAliases(compiled, names);

                // Write the layout described by CompiledTableFormat.
                ByteWriter writer = new(56 + entryCount * 16 + aliases.Count * 12 + (messageStarts.Count + program.Count) * 4 + characters.Length * 2);
                writer.WriteUInt32(CompiledTableFormat.Magic);
                writer.WriteUInt16(CompiledTableFormat.Version);
                writer.WriteUInt16(0);
                writer.WriteUInt64(_catalog.Hash);
                writer.WriteUInt64(_table.Hash);
                writer.WriteUInt64(_language.Hash);
                writer.WriteInt32(entryCount);
                writer.WriteInt32(aliases.Count);
                writer.WriteInt32(messageStarts.Count);
                writer.WriteInt32(program.Count);
                writer.WriteInt32(characters.Length);
                for (int i = 0; i < entryCount; i++)
                {
                    writer.WriteUInt64(hashes[i]);
                }
                for (int i = 0; i < entryCount; i++)
                {
                    writer.WriteInt32(starts[i]);
                }
                for (int i = 0; i < entryCount; i++)
                {
                    writer.WriteInt32(lengths[i]);
                }
                for (int i = 0; i < aliases.Count; i++)
                {
                    writer.WriteUInt64(aliases[i].Hash);
                }
                for (int i = 0; i < aliases.Count; i++)
                {
                    writer.WriteInt32(aliases[i].Target);
                }
                for (int i = 0; i < messageStarts.Count; i++)
                {
                    writer.WriteInt32(messageStarts[i]);
                }
                for (int i = 0; i < program.Count; i++)
                {
                    writer.WriteInt32(program[i]);
                }
                characters.WriteTo(writer);
                return writer.ToArray();
            }

            /// <summary>
            /// Parses and checks one entry's message. Returns false when the entry is left out of the table: a broken
            /// translation, or one whose arguments don't match the source text.
            /// </summary>
            private bool TryCompileEntry(TableDocumentEntry entry, out string text, out ParsedMessage message)
            {
                ParsedMessage parsed = MessageParser.Parse(entry.Value);
                for (int i = 0; i < parsed.Issues.Count; i++)
                {
                    MessageIssue issue = parsed.Issues[i];
                    AddIssue(issue.Severity, entry.Line, entry.GetColumn(issue.Index), issue.Message);
                }
                text = null;
                message = null;
                if (parsed.HasErrors)
                {
                    if (!_isSource)
                    {
                        ReportLeftOut(entry);
                        return false;
                    }
                    // The source keeps its text as written, so the entry still shows while its message gets fixed.
                    text = entry.Value;
                    return true;
                }
                if (_format != null)
                {
                    CheckPluralForms(parsed.Body, entry);
                }
                if (_source != null && !CheckArguments(parsed, entry))
                {
                    ReportLeftOut(entry);
                    return false;
                }
                if (parsed.HasArguments)
                {
                    message = parsed;
                }
                else
                {
                    text = parsed.LiteralText;
                }
                return true;
            }

            private void CheckPluralForms(MessageBody body, TableDocumentEntry entry)
            {
                for (int i = 0; i < body.Parts.Count; i++)
                {
                    if (body.Parts[i] is PluralPart plural)
                    {
                        CheckPlural(plural, entry);
                        for (int c = 0; c < plural.Cases.Count; c++)
                        {
                            CheckPluralForms(plural.Cases[c].Body, entry);
                        }
                    }
                    else if (body.Parts[i] is SelectPart select)
                    {
                        for (int c = 0; c < select.Cases.Count; c++)
                        {
                            CheckPluralForms(select.Cases[c].Body, entry);
                        }
                    }
                }
            }

            private void CheckPlural(PluralPart plural, TableDocumentEntry entry)
            {
                int required = plural.IsOrdinal ? _format.OrdinalCategories : _format.CardinalCategories;
                int present = 0;
                for (int i = 0; i < plural.Cases.Count; i++)
                {
                    if (plural.Cases[i].IsCategory)
                    {
                        present = PluralCategories.Add(present, plural.Cases[i].Category);
                    }
                }
                string kind = plural.IsOrdinal ? "ordinal forms" : "plural forms";
                int missing = PluralCoverage.RemoveCovered(plural, required & ~present, _format);
                if (missing != 0)
                {
                    AddIssue(IssueSeverity.Warning, entry.Line, entry.GetColumn(plural.Position),
                        $"{_language.Name} uses the {kind} {PluralCategories.Describe(required)} for {{{plural.Argument}}}. Without {PluralCategories.Describe(missing)}, those numbers show the 'other' form.");
                }
                int unused = present & ~required;
                if (unused != 0)
                {
                    AddIssue(IssueSeverity.Warning, entry.Line, entry.GetColumn(plural.Position),
                        $"{_language.Name} never uses the {kind} {PluralCategories.Describe(unused)}, so their text is never shown.");
                }
            }

            /// <summary>Checks that a translation uses exactly the source text's arguments, each in a way its value supports.</summary>
            private bool CheckArguments(ParsedMessage translation, TableDocumentEntry entry)
            {
                ParsedMessage source = _source.Find(Hashing.ComputeNameHash(entry.Key));
                // Without a usable source entry there's nothing to compare with; orphans are reported on their own.
                if (source == null || source.HasErrors)
                {
                    return true;
                }
                bool isMatching = true;
                for (int i = 0; i < translation.Arguments.Count; i++)
                {
                    MessageArgumentInfo argument = translation.Arguments[i];
                    if (!source.TryGetArgument(argument.Hash, out MessageArgumentInfo expected))
                    {
                        AddIssue(IssueSeverity.Error, entry.Line, entry.GetColumn(argument.Position),
                            $"{{{argument.Name}}} isn't an argument of the source text, so nothing gives it a value. The source text has {Describe(source)}.");
                        isMatching = false;
                        continue;
                    }
                    ArgumentUses allowed = expected.Kind switch
                    {
                        MessageArgumentKind.Number => ArgumentUses.Plain | ArgumentUses.Numeric,
                        MessageArgumentKind.Keyword => ArgumentUses.Plain | ArgumentUses.Select,
                        _ => ArgumentUses.Plain | ArgumentUses.Formatter
                    };
                    if ((argument.Uses & ~allowed) != 0)
                    {
                        AddIssue(IssueSeverity.Error, entry.Line, entry.GetColumn(argument.Position),
                            $"{{{argument.Name}}} is used as {Describe(argument.Uses & ~allowed)} here, but the source text gives it {Describe(expected.Kind)}.");
                        isMatching = false;
                    }
                }
                for (int i = 0; i < source.Arguments.Count; i++)
                {
                    if (!translation.TryGetArgument(source.Arguments[i].Hash, out _))
                    {
                        AddIssue(IssueSeverity.Error, entry.Line, entry.GetColumn(0),
                            $"The translation leaves out {{{source.Arguments[i].Name}}}, which the source text shows.");
                        isMatching = false;
                    }
                }
                return isMatching;
            }

            private void ReportLeftOut(TableDocumentEntry entry)
            {
                AddIssue(IssueSeverity.Warning, entry.Line, 1,
                    $"'{entry.Key}' is left out of {_language.Name} until its message is fixed, so it shows in the fallback language meanwhile.");
            }

            /// <summary>
            /// Collects every <c>@formerly</c> of the sorted entries as an alias hash and the index of the entry it
            /// points to, sorted by hash. An alias may not reuse the name of an entry or of another alias.
            /// </summary>
            private List<(ulong Hash, int Target)> CollectAliases(List<CompiledEntry> compiled, Dictionary<ulong, string> names)
            {
                List<(ulong Hash, int Target)> aliases = new();
                for (int i = 0; i < compiled.Count; i++)
                {
                    TableDocumentEntry entry = compiled[i].Entry;
                    for (int a = 0; a < entry.Attributes.Count; a++)
                    {
                        DocumentProperty attribute = entry.Attributes[a];
                        if (!string.Equals(attribute.Name, DocumentNames.Formerly, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        string alias = attribute.Value;
                        if (!NameRules.IsValid(alias))
                        {
                            AddIssue(IssueSeverity.Error, attribute.Line, 1, $"'@formerly {alias}' needs the entry's former name: {NameRules.Description}.");
                            continue;
                        }
                        ulong hash = Hashing.ComputeNameHash(alias);
                        if (names.TryGetValue(hash, out string taken))
                        {
                            AddIssue(IssueSeverity.Error, attribute.Line, 1, $"'@formerly {alias}' can't be an alias: '{taken}' already names an entry or alias of this table.");
                            continue;
                        }
                        names.Add(hash, alias);
                        aliases.Add((hash, i));
                    }
                }
                aliases.Sort((left, right) => left.Hash.CompareTo(right.Hash));
                return aliases;
            }

            private void AddIssue(IssueSeverity severity, int line, int column, string message)
            {
                _issues?.Add(new DocumentIssue(severity, line, column, message));
            }

            private static string Describe(ParsedMessage message)
            {
                if (message.Arguments.Count == 0)
                {
                    return "no arguments";
                }
                string[] names = new string[message.Arguments.Count];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = "{" + message.Arguments[i].Name + "}";
                }
                return string.Join(", ", names);
            }

            private static string Describe(ArgumentUses uses)
            {
                if ((uses & ArgumentUses.Numeric) != 0)
                {
                    return "a number";
                }
                if ((uses & ArgumentUses.Select) != 0)
                {
                    return "a select keyword";
                }
                return (uses & ArgumentUses.Formatter) != 0 ? "a formatted value" : "plain text";
            }

            private static string Describe(MessageArgumentKind kind) => kind switch
            {
                MessageArgumentKind.Number => "a number",
                MessageArgumentKind.Keyword => "a select keyword",
                _ => "a value to show as is or through a formatter"
            };
        }

        private readonly struct CompiledEntry
        {
            public CompiledEntry(ulong hash, TableDocumentEntry entry, string text, ParsedMessage message)
            {
                Hash = hash;
                Entry = entry;
                Text = text;
                Message = message;
            }

            public ulong Hash { get; }

            public TableDocumentEntry Entry { get; }

            /// <summary>The text of an entry without arguments; null for a message.</summary>
            public string Text { get; }

            /// <summary>The message of an entry with arguments; null for plain text.</summary>
            public ParsedMessage Message { get; }
        }

        /// <summary>The source-language entries of the table, each parsed the first time a translation asks for it.</summary>
        private sealed class SourceMessages
        {
            private readonly Dictionary<ulong, TableDocumentEntry> _entries = new();
            private readonly Dictionary<ulong, ParsedMessage> _parsed = new();

            public SourceMessages(TableDocument source)
            {
                for (int i = 0; i < source.Entries.Count; i++)
                {
                    _entries[Hashing.ComputeNameHash(source.Entries[i].Key)] = source.Entries[i];
                }
            }

            public ParsedMessage Find(ulong keyHash)
            {
                if (_parsed.TryGetValue(keyHash, out ParsedMessage parsed))
                {
                    return parsed;
                }
                if (!_entries.TryGetValue(keyHash, out TableDocumentEntry entry))
                {
                    return null;
                }
                parsed = MessageParser.Parse(entry.Value);
                _parsed.Add(keyHash, parsed);
                return parsed;
            }
        }
    }
}
