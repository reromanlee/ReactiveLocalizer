namespace reromanlee.ReactiveLocalizer.Authoring.Exchange
{
    /// <summary>An entry in one exported language: its text and how it stands against the source text.</summary>
    public readonly struct ExportCell
    {
        internal ExportCell(string text, TranslationState state)
        {
            Text = text;
            State = state;
        }

        /// <summary>The text; null when the language has none.</summary>
        public string Text { get; }

        /// <summary>Missing, current, outdated or unverified.</summary>
        public TranslationState State { get; }
    }
}
