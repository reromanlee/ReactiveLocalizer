using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// Tracks the keys asked for that exist nowhere in the catalog, and hands out the <c>[Table.Key]</c> marker shown in
    /// their place. Each unique key is new exactly once, which is when its error is reported.
    /// </summary>
    /// <remarks>Safe from any thread. Only the first miss of a key allocates; later misses return the stored marker.</remarks>
    internal sealed class MissingKeys
    {
        private readonly object _lockObject = new();
        private readonly Dictionary<(ulong Table, ulong Entry), string> _markers = new();

        /// <summary>How many unique missing keys were asked for since the localizer was created.</summary>
        public int Count
        {
            get
            {
                lock (_lockObject)
                {
                    return _markers.Count;
                }
            }
        }

        /// <summary>Returns the marker for a missing key; <paramref name="isNew"/> tells whether this is its first miss.</summary>
        public string GetMarker(ReadOnlySpan<char> tableName, ReadOnlySpan<char> entryName, ulong tableHash, ulong entryHash, out bool isNew)
        {
            lock (_lockObject)
            {
                if (_markers.TryGetValue((tableHash, entryHash), out string marker))
                {
                    isNew = false;
                    return marker;
                }
                marker = $"[{tableName.ToString()}.{entryName.ToString()}]";
                _markers.Add((tableHash, entryHash), marker);
                isNew = true;
                return marker;
            }
        }
    }
}
