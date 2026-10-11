using System;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>One sheet as the Apps Script sends and takes it: its name and its rows of cells.</summary>
    /// <remarks>Fields are named as the JSON the script reads and writes, which JsonUtility maps by name.</remarks>
    [Serializable]
    public sealed class SheetData
    {
        public string name;
        public RowData[] rows;
    }
}
