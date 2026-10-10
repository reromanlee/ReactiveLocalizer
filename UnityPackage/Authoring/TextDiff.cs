using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>Compares two texts word by word, as tools show what changed in a source text since it was translated.</summary>
    public static class TextDiff
    {
        // Above this many word pairs, comparing costs more than it tells; the texts then show as replaced whole.
        private const int ComparisonLimit = 250_000;

        /// <summary>
        /// Returns the parts of both texts in order: words kept, words only <paramref name="before"/> has, and words only
        /// <paramref name="after"/> has. The kept and removed parts make <paramref name="before"/>; the kept and added
        /// parts make <paramref name="after"/>.
        /// </summary>
        public static List<TextDiffPart> Compare(string before, string after)
        {
            List<string> left = Split(before ?? string.Empty);
            List<string> right = Split(after ?? string.Empty);
            List<TextDiffPart> parts = new();
            if ((long)left.Count * right.Count > ComparisonLimit)
            {
                Add(parts, TextDiffKind.Removed, before);
                Add(parts, TextDiffKind.Added, after);
                return parts;
            }
            // lengths[i, j]: the longest run of words kept between left[i..] and right[j..].
            int[,] lengths = new int[left.Count + 1, right.Count + 1];
            for (int i = left.Count - 1; i >= 0; i--)
            {
                for (int j = right.Count - 1; j >= 0; j--)
                {
                    lengths[i, j] = left[i] == right[j] ? lengths[i + 1, j + 1] + 1 : System.Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }
            int l = 0;
            int r = 0;
            while (l < left.Count && r < right.Count)
            {
                if (left[l] == right[r])
                {
                    Add(parts, TextDiffKind.Kept, left[l++]);
                    r++;
                }
                else if (lengths[l + 1, r] >= lengths[l, r + 1])
                {
                    Add(parts, TextDiffKind.Removed, left[l++]);
                }
                else
                {
                    Add(parts, TextDiffKind.Added, right[r++]);
                }
            }
            while (l < left.Count)
            {
                Add(parts, TextDiffKind.Removed, left[l++]);
            }
            while (r < right.Count)
            {
                Add(parts, TextDiffKind.Added, right[r++]);
            }
            return parts;
        }

        /// <summary>Splits text into words, runs of spaces, and single other characters.</summary>
        private static List<string> Split(string text)
        {
            List<string> words = new();
            int start = 0;
            while (start < text.Length)
            {
                int end = start + 1;
                if (char.IsLetterOrDigit(text[start]))
                {
                    while (end < text.Length && char.IsLetterOrDigit(text[end]))
                    {
                        end++;
                    }
                }
                else if (char.IsWhiteSpace(text[start]))
                {
                    while (end < text.Length && char.IsWhiteSpace(text[end]))
                    {
                        end++;
                    }
                }
                words.Add(text.Substring(start, end - start));
                start = end;
            }
            return words;
        }

        /// <summary>Adds a part, joining it to the last one when they are of the same kind.</summary>
        private static void Add(List<TextDiffPart> parts, TextDiffKind kind, string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (parts.Count > 0 && parts[parts.Count - 1].Kind == kind)
            {
                parts[parts.Count - 1] = new TextDiffPart(kind, parts[parts.Count - 1].Text + text);
                return;
            }
            parts.Add(new TextDiffPart(kind, text));
        }
    }

    /// <summary>A run of words of a <see cref="TextDiff"/>.</summary>
    public readonly struct TextDiffPart
    {
        public TextDiffPart(TextDiffKind kind, string text)
        {
            Kind = kind;
            Text = text;
        }

        public TextDiffKind Kind { get; }

        public string Text { get; }
    }

    /// <summary>Whether a run of words of a <see cref="TextDiff"/> is in both texts, only the first, or only the second.</summary>
    public enum TextDiffKind
    {
        Kept = 0,
        Removed = 1,
        Added = 2
    }
}
