using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// Entries moved from one table to another: the source file of the table an entry moved to keeps
    /// <c>@formerly OldTable.OldName</c> above it, so every saved reference to the old key still finds it.
    /// </summary>
    internal static class MovedEntries
    {
        /// <summary>No entry ever moved.</summary>
        public static readonly IReadOnlyDictionary<(ulong Table, ulong Entry), EntryKey> None =
            new Dictionary<(ulong Table, ulong Entry), EntryKey>();

        /// <summary>
        /// Collects every <c>@formerly OtherTable.Entry</c> of the tables' source files: the key each moved entry had,
        /// with its key now. When two entries claim the same old key, the first table in order keeps it.
        /// </summary>
        public static Dictionary<(ulong Table, ulong Entry), EntryKey> Collect(IEnumerable<(string TableName, TableDocument Source)> tables)
        {
            Dictionary<(ulong Table, ulong Entry), EntryKey> moved = new();
            foreach ((string TableName, TableDocument Source) table in tables)
            {
                if (table.Source == null || !NameRules.IsValid(table.TableName))
                {
                    continue;
                }
                ulong tableHash = Hashing.ComputeNameHash(table.TableName);
                IReadOnlyList<TableDocumentEntry> entries = table.Source.Entries;
                for (int e = 0; e < entries.Count; e++)
                {
                    for (int a = 0; a < entries[e].Attributes.Count; a++)
                    {
                        DocumentProperty attribute = entries[e].Attributes[a];
                        if (attribute.Name == DocumentNames.Formerly &&
                            TryParseQualified(attribute.Value, out string oldTable, out string oldEntry) &&
                            Hashing.ComputeNameHash(oldTable) != tableHash)
                        {
                            moved.TryAdd((Hashing.ComputeNameHash(oldTable), Hashing.ComputeNameHash(oldEntry)), new EntryKey(table.TableName, entries[e].Key));
                        }
                    }
                }
            }
            return moved;
        }

        /// <summary>Reads an alias written as <c>Table.Entry</c>, both following the naming rule.</summary>
        public static bool TryParseQualified(string alias, out string tableName, out string entryName)
        {
            tableName = null;
            entryName = null;
            int separator = alias?.IndexOf('.') ?? -1;
            if (separator <= 0 || alias.IndexOf('.', separator + 1) >= 0)
            {
                return false;
            }
            string table = alias.Substring(0, separator);
            string entry = alias.Substring(separator + 1);
            if (!NameRules.IsValid(table) || !NameRules.IsValid(entry))
            {
                return false;
            }
            tableName = table;
            entryName = entry;
            return true;
        }
    }
}
