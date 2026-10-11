namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>The kinds of files the editor exports translations to.</summary>
    internal enum ExchangeFormat
    {
        /// <summary>One XLSX workbook, a sheet per table.</summary>
        Xlsx = 0,

        /// <summary>A CSV file per table.</summary>
        Csv = 1,

        /// <summary>An XLIFF 1.2 file per language.</summary>
        Xliff12 = 2,

        /// <summary>An XLIFF 2.0 file per language.</summary>
        Xliff20 = 3,

        /// <summary>A text to paste into a chat with a large language model, one language at a time.</summary>
        LlmHandoff = 4
    }
}
