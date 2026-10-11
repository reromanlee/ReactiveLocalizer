using System;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>What the editor asks the Apps Script: to pull every sheet, or to push the sheets it sends.</summary>
    [Serializable]
    internal sealed class SyncRequest
    {
        public string token;
        public string action;
        public SheetData[] sheets;
    }
}
