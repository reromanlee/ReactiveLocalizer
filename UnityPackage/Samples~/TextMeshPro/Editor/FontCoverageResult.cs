using TMPro;

namespace reromanlee.ReactiveLocalizer.Samples.TextMeshPro.Editor
{
    /// <summary>What <see cref="FontCoverage"/> found for one language: its font, and the characters the font can't draw.</summary>
    public sealed class FontCoverageResult
    {
        public FontCoverageResult(string language, TMP_FontAsset font, int characterCount, int missingCount, string missing, string note)
        {
            Language = language;
            Font = font;
            CharacterCount = characterCount;
            MissingCount = missingCount;
            Missing = missing ?? string.Empty;
            Note = note;
        }

        /// <summary>The language checked; null when the check couldn't start.</summary>
        public string Language { get; }

        /// <summary>The font the language shows its text with; null when it has none.</summary>
        public TMP_FontAsset Font { get; }

        /// <summary>How many characters the language can show.</summary>
        public int CharacterCount { get; }

        /// <summary>How many of them the font draws as an empty box.</summary>
        public int MissingCount { get; }

        /// <summary>The characters the font can't draw, in code point order; the first 200 when there are more.</summary>
        public string Missing { get; }

        /// <summary>Why the check couldn't be done, or what it couldn't tell; null when there's nothing to add.</summary>
        public string Note { get; }

        /// <inheritdoc/>
        public override string ToString()
        {
            if (Language == null || Font == null)
            {
                return Language == null ? Note : $"{Language}: {Note}";
            }
            string coverage = MissingCount == 0
                ? $"{Font.name} draws all {CharacterCount} characters."
                : $"{Font.name} can't draw {MissingCount} of {CharacterCount} characters: {Missing}{(MissingCount > FontCoverage.ListedLimit ? "\u2026" : string.Empty)}";
            return Note == null ? $"{Language}: {coverage}" : $"{Language}: {coverage} {Note}";
        }
    }
}
