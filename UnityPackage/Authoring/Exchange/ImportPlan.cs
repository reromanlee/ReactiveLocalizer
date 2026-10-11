using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>
    /// What an import does, for a preview before anything is written: every text it adds, updates, confirms or
    /// rejects, and the problems of the files as a whole. The table files it planned on already hold the changes.
    /// </summary>
    public sealed class ImportPlan
    {
        internal ImportPlan(IReadOnlyList<ImportChange> changes, IReadOnlyList<string> problems, int unchangedCount)
        {
            Changes = changes;
            Problems = problems;
            UnchangedCount = unchangedCount;
        }

        /// <summary>Every text that changes or is rejected, in the order the files hold them.</summary>
        public IReadOnlyList<ImportChange> Changes { get; }

        /// <summary>Problems of the files as a whole, such as a language or table the catalog doesn't have.</summary>
        public IReadOnlyList<string> Problems { get; }

        /// <summary>How many texts equal what the language already has, which the import leaves alone.</summary>
        public int UnchangedCount { get; }

        /// <summary>Whether anything would be written.</summary>
        public bool HasChanges
        {
            get
            {
                for (int i = 0; i < Changes.Count; i++)
                {
                    if (Changes[i].Kind != ImportChangeKind.Rejected)
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>Returns how many changes are of <paramref name="kind"/>.</summary>
        public int Count(ImportChangeKind kind)
        {
            int count = 0;
            for (int i = 0; i < Changes.Count; i++)
            {
                if (Changes[i].Kind == kind)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
