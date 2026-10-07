using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// Remembers which problems were already reported, so each unique one is reported exactly once per session, however
    /// often a frame runs into it.
    /// </summary>
    /// <remarks>Safe from any thread. Only the first sighting of a problem allocates.</remarks>
    internal sealed class ReportedProblems
    {
        private readonly object _lockObject = new();
        private readonly HashSet<(ulong Table, ulong Entry, ulong Detail)> _reported = new();

        /// <summary>Returns true the first time a problem is seen, and false every time after.</summary>
        /// <param name="table">Hash of the table the problem concerns.</param>
        /// <param name="entry">Hash of the entry the problem concerns.</param>
        /// <param name="detail">What the problem is, such as the hash of an argument combined with its kind of problem.</param>
        public bool TryAdd(ulong table, ulong entry, ulong detail)
        {
            lock (_lockObject)
            {
                return _reported.Add((table, entry, detail));
            }
        }
    }
}
