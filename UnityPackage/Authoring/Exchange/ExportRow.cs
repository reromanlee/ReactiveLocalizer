using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>One entry of an <see cref="ExportTable"/>, as every format shows it.</summary>
    public sealed class ExportRow
    {
        internal ExportRow(string key, string context, int maximumLength, string sourceText, ExportCell[] cells)
        {
            Key = key;
            Context = context;
            MaximumLength = maximumLength;
            SourceText = sourceText;
            Cells = cells;
        }

        /// <summary>The entry's key, without its table.</summary>
        public string Key { get; }

        /// <summary>The comments above the source entry, one per line: the context translators see. Empty without any.</summary>
        public string Context { get; }

        /// <summary>The entry's <c>@maximumLength</c>, or 0 without one.</summary>
        public int MaximumLength { get; }

        /// <summary>The text in the source language, which translations are made from.</summary>
        public string SourceText { get; }

        /// <summary>The entry in each of the book's <see cref="ExportBook.Languages"/>, in their order.</summary>
        public IReadOnlyList<ExportCell> Cells { get; }
    }
}
