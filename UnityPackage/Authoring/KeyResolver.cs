using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Tells what saved references to a catalog's entries find, from the tables' source files: the entry itself, the
    /// entry by a former name it keeps, the entry in the table it moved to, or nothing, with the key most likely meant.
    /// </summary>
    /// <remarks>
    /// Built once and asked many times, as when a build checks every reference of every scene. Never changes after it
    /// is built.
    /// </remarks>
    public sealed class KeyResolver
    {
        private readonly Dictionary<ulong, ResolvedTable> _tables = new();
        private readonly Dictionary<(ulong Table, ulong Entry), (string Table, string Entry, string Former)> _moved = new();

        /// <summary>Builds the resolver of the tables in <paramref name="tables"/>, each with its source-language file.</summary>
        public KeyResolver(IEnumerable<(string TableName, TableDocument Source)> tables)
        {
            foreach ((string TableName, TableDocument Source) table in tables)
            {
                if (!NameRules.IsValid(table.TableName) || table.Source == null)
                {
                    continue;
                }
                ResolvedTable resolved = new(table.TableName, table.Source);
                _tables[Hashing.ComputeNameHash(table.TableName)] = resolved;
                IReadOnlyList<TableDocumentEntry> entries = table.Source.Entries;
                for (int e = 0; e < entries.Count; e++)
                {
                    for (int a = 0; a < entries[e].Attributes.Count; a++)
                    {
                        DocumentProperty attribute = entries[e].Attributes[a];
                        if (attribute.Name != DocumentNames.Formerly)
                        {
                            continue;
                        }
                        string alias = attribute.Value;
                        if (MovedEntries.TryParseQualified(alias, out string formerTable, out string formerEntry) &&
                            !string.Equals(formerTable, table.TableName, StringComparison.OrdinalIgnoreCase))
                        {
                            _moved.TryAdd((Hashing.ComputeNameHash(formerTable), Hashing.ComputeNameHash(formerEntry)), (table.TableName, entries[e].Key, alias));
                            continue;
                        }
                        if (formerEntry != null)
                        {
                            alias = formerEntry;
                        }
                        if (NameRules.IsValid(alias))
                        {
                            resolved.Aliases.TryAdd(Hashing.ComputeNameHash(alias), (entries[e].Key, attribute.Value));
                        }
                    }
                }
            }
        }

        /// <summary>The names of the tables the resolver knows.</summary>
        public IEnumerable<string> TableNames
        {
            get
            {
                foreach (ResolvedTable table in _tables.Values)
                {
                    yield return table.Name;
                }
            }
        }

        /// <summary>Returns what a reference to <paramref name="tableName"/>.<paramref name="entryName"/> finds.</summary>
        public KeyResolution Resolve(string tableName, string entryName)
        {
            if (!NameRules.IsValid(tableName) || !NameRules.IsValid(entryName))
            {
                return new KeyResolution(KeyResolutionKind.Invalid, null, null, null, null);
            }
            ulong tableHash = Hashing.ComputeNameHash(tableName);
            ulong entryHash = Hashing.ComputeNameHash(entryName);
            _tables.TryGetValue(tableHash, out ResolvedTable table);
            if (table != null && table.Source.TryGetEntry(entryName, out TableDocumentEntry entry))
            {
                return new KeyResolution(KeyResolutionKind.Found, table.Name, entry.Key, null, null);
            }
            if (table != null && table.Aliases.TryGetValue(entryHash, out (string Entry, string Former) alias))
            {
                return new KeyResolution(KeyResolutionKind.Renamed, table.Name, alias.Entry, alias.Former, null);
            }
            if (_moved.TryGetValue((tableHash, entryHash), out (string Table, string Entry, string Former) moved))
            {
                return new KeyResolution(KeyResolutionKind.Moved, moved.Table, moved.Entry, moved.Former, null);
            }
            return new KeyResolution(KeyResolutionKind.Missing, null, null, null, Suggest(table, tableName, entryName));
        }

        private string Suggest(ResolvedTable table, string tableName, string entryName)
        {
            if (table != null)
            {
                List<string> keys = new();
                IReadOnlyList<TableDocumentEntry> entries = table.Source.Entries;
                for (int i = 0; i < entries.Count; i++)
                {
                    keys.Add(entries[i].Key);
                }
                string closest = NameDistance.FindClosest(entryName, keys);
                return closest != null ? $"{table.Name}.{closest}" : null;
            }
            string closestTable = NameDistance.FindClosest(tableName, TableNames);
            if (closestTable == null)
            {
                return null;
            }
            // A misspelled table usually comes with a correct key.
            ResolvedTable candidate = _tables[Hashing.ComputeNameHash(closestTable)];
            return candidate.Source.TryGetEntry(entryName, out TableDocumentEntry entry) ? $"{candidate.Name}.{entry.Key}" : $"{candidate.Name}.{entryName}";
        }

        private sealed class ResolvedTable
        {
            public ResolvedTable(string name, TableDocument source)
            {
                Name = name;
                Source = source;
            }

            public string Name { get; }

            public TableDocument Source { get; }

            /// <summary>By the hash of a former name, the entry it belongs to and the alias as written.</summary>
            public Dictionary<ulong, (string Entry, string Former)> Aliases { get; } = new();
        }
    }
}
