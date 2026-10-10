using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>One table of an <see cref="ExportBook"/>: its rows in natural key order.</summary>
    public sealed class ExportTable
    {
        internal ExportTable(string name, IReadOnlyList<ExportRow> rows)
        {
            Name = name;
            Rows = rows;
        }

        /// <summary>Name of the table.</summary>
        public string Name { get; }

        /// <summary>The exported entries, in natural key order.</summary>
        public IReadOnlyList<ExportRow> Rows { get; }
    }
}
