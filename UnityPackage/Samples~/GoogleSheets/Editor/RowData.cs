using System;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>One row of a sheet: its cells, left to right.</summary>
    [Serializable]
    public sealed class RowData
    {
        public string[] cells;
    }
}
