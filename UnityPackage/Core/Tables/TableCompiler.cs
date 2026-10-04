using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// Compiles a table document into the binary form localizers load. The importer compiles every table file with
    /// it, and a game can compile text it receives at runtime the same way, such as a fan translation from a mods
    /// folder.
    /// </summary>
    public static class TableCompiler
    {
        /// <summary>
        /// Compiles <paramref name="document"/> as <paramref name="table"/> in <paramref name="language"/> of
        /// <paramref name="catalog"/>, appending the problems compiling finds to <paramref name="issues"/>.
        /// </summary>
        /// <remarks>
        /// Lines the document already rejected are absent from it, so they are simply not compiled. Compiling itself
        /// rejects an entry or alias whose hash collides with another name of the table, and a <c>@formerly</c> that
        /// is not a valid name. Identical texts are stored once.
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

            // Hash every entry, rejecting the vanishingly rare names whose hashes collide with another name.
            IReadOnlyList<TableDocumentEntry> entries = document.Entries;
            Dictionary<ulong, string> names = new(entries.Count);
            List<(ulong Hash, TableDocumentEntry Entry)> hashed = new(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                TableDocumentEntry entry = entries[i];
                ulong hash = Hashing.ComputeNameHash(entry.Key);
                if (names.TryGetValue(hash, out string other))
                {
                    AddIssue(issues, IssueSeverity.Error, entry.Line, $"'{entry.Key}' has the same hash as '{other}', so it can't be told apart; rename one of them.");
                    continue;
                }
                names.Add(hash, entry.Key);
                hashed.Add((hash, entry));
            }
            hashed.Sort((left, right) => left.Hash.CompareTo(right.Hash));

            // Lay out every text in one buffer, storing identical texts once.
            int entryCount = hashed.Count;
            ulong[] hashes = new ulong[entryCount];
            int[] starts = new int[entryCount];
            int[] lengths = new int[entryCount];
            Dictionary<string, int> sharedStarts = new(StringComparer.Ordinal);
            List<string> texts = new();
            int characterCount = 0;
            for (int i = 0; i < entryCount; i++)
            {
                string value = hashed[i].Entry.Value;
                hashes[i] = hashed[i].Hash;
                lengths[i] = value.Length;
                if (!sharedStarts.TryGetValue(value, out int start))
                {
                    start = characterCount;
                    sharedStarts.Add(value, start);
                    texts.Add(value);
                    characterCount += value.Length;
                }
                starts[i] = start;
            }

            List<(ulong Hash, int Target)> aliases = CollectAliases(hashed, names, issues);

            // Write the layout described by CompiledTableFormat.
            ByteWriter writer = new(48 + entryCount * 16 + aliases.Count * 12 + characterCount * 2);
            writer.WriteUInt32(CompiledTableFormat.Magic);
            writer.WriteUInt16(CompiledTableFormat.Version);
            writer.WriteUInt16(0);
            writer.WriteUInt64(catalog.Hash);
            writer.WriteUInt64(table.Hash);
            writer.WriteUInt64(language.Hash);
            writer.WriteInt32(entryCount);
            writer.WriteInt32(aliases.Count);
            writer.WriteInt32(characterCount);
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
            for (int i = 0; i < texts.Count; i++)
            {
                writer.WriteCharacters(texts[i]);
            }
            return writer.ToArray();
        }

        /// <summary>
        /// Collects every <c>@formerly</c> of the sorted entries as an alias hash and the index of the entry it
        /// points to, sorted by hash. An alias may not reuse the name of an entry or of another alias.
        /// </summary>
        private static List<(ulong Hash, int Target)> CollectAliases(List<(ulong Hash, TableDocumentEntry Entry)> hashed,
            Dictionary<ulong, string> names, ICollection<DocumentIssue> issues)
        {
            List<(ulong Hash, int Target)> aliases = new();
            for (int i = 0; i < hashed.Count; i++)
            {
                TableDocumentEntry entry = hashed[i].Entry;
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
                        AddIssue(issues, IssueSeverity.Error, attribute.Line, $"'@formerly {alias}' needs the entry's former name: {NameRules.Description}.");
                        continue;
                    }
                    ulong hash = Hashing.ComputeNameHash(alias);
                    if (names.TryGetValue(hash, out string taken))
                    {
                        AddIssue(issues, IssueSeverity.Error, attribute.Line, $"'@formerly {alias}' can't be an alias: '{taken}' already names an entry or alias of this table.");
                        continue;
                    }
                    names.Add(hash, alias);
                    aliases.Add((hash, i));
                }
            }
            aliases.Sort((left, right) => left.Hash.CompareTo(right.Hash));
            return aliases;
        }

        private static void AddIssue(ICollection<DocumentIssue> issues, IssueSeverity severity, int line, string message)
        {
            issues?.Add(new DocumentIssue(severity, line, 1, message));
        }
    }
}
