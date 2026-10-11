using System;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>What the Apps Script answers: whether it worked, why not, and the sheets a pull asked for.</summary>
    [Serializable]
    internal sealed class SyncResponse
    {
        public bool ok;
        public string error;
        public SheetData[] sheets;
    }
}
