using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>One table of a catalog with every file it has, read: what validation and exports work from.</summary>
    public sealed class ValidatedTable
    {
        /// <summary>Creates the table named <paramref name="name"/>.</summary>
        /// <param name="name">Name of the table.</param>
        /// <param name="files">The table's files, one per language.</param>
        public ValidatedTable(string name, IReadOnlyList<ValidatedFile> files)
        {
            Name = name;
            Files = files ?? Array.Empty<ValidatedFile>();
        }

        /// <summary>Name of the table.</summary>
        public string Name { get; }

        /// <summary>The table's files, one per language.</summary>
        public IReadOnlyList<ValidatedFile> Files { get; }
    }
}
