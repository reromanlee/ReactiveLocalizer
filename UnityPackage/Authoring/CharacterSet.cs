using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Messages;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// A set of characters, such as every character a language can show, for font assets that need to have each of
    /// them. Written out in code point order, it is what TextMeshPro's Font Asset Creator takes as custom characters.
    /// </summary>
    /// <remarks>
    /// Rich text tags, control characters and invisible formatting characters are never added, since no font draws
    /// them, and a surrogate pair counts as the one character it encodes.
    /// </remarks>
    public sealed class CharacterSet
    {
        private readonly HashSet<int> _codePoints = new();
        private readonly List<(int Start, int Length)> _texts = new();
        private readonly HashSet<ulong> _counted = new();

        /// <summary>How many characters the set holds.</summary>
        public int Count => _codePoints.Count;

        /// <summary>Whether the set holds <paramref name="codePoint"/>.</summary>
        public bool Contains(int codePoint) => _codePoints.Contains(codePoint);

        /// <summary>Adds every character <paramref name="text"/> shows, leaving out its rich text tags.</summary>
        public void AddText(ReadOnlySpan<char> text)
        {
            int index = 0;
            while (index < text.Length)
            {
                char character = text[index];
                if (character == '<')
                {
                    // An unclosed tag, as when an argument sits inside it, counts as text: a few extra characters cost less than a missing one.
                    int tag = RichTextTags.MeasureTag(text, index, out bool isUnclosed);
                    if (tag > 0 && !isUnclosed)
                    {
                        index += tag;
                        continue;
                    }
                }
                if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                {
                    Add(char.ConvertToUtf32(character, text[index + 1]));
                    index += 2;
                    continue;
                }
                Add(character);
                index++;
            }
        }

        /// <summary>Adds <paramref name="codePoint"/> unless no font draws it: a control or formatting character, or half a surrogate pair.</summary>
        public void Add(int codePoint)
        {
            if (codePoint < 0 || codePoint > 0x10FFFF)
            {
                return;
            }
            UnicodeCategory category = codePoint < 0x10000
                ? CharUnicodeInfo.GetUnicodeCategory((char)codePoint)
                : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
            if (category == UnicodeCategory.Control || category == UnicodeCategory.Format || category == UnicodeCategory.Surrogate ||
                category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator)
            {
                return;
            }
            _codePoints.Add(codePoint);
        }

        /// <summary>Adds every character of <paramref name="other"/>.</summary>
        public void UnionWith(CharacterSet other)
        {
            if (other != null)
            {
                _codePoints.UnionWith(other._codePoints);
            }
        }

        /// <summary>
        /// Adds every character one table shows in a language: its literal text in every plural and select form, but
        /// not argument names or select keywords. An entry counts from the first table that has it, as lookups find it.
        /// </summary>
        /// <param name="compiledTables">The table's compiled data along the language's fallback chain, from the language itself to the source language; empty data stands for a missing file.</param>
        /// <param name="problem">Why a table couldn't be read; the tables before it still count.</param>
        /// <returns>False when a table couldn't be read.</returns>
        public bool AddTable(IReadOnlyList<ReadOnlyMemory<byte>> compiledTables, out string problem)
        {
            problem = null;
            _counted.Clear();
            for (int t = 0; t < compiledTables.Count; t++)
            {
                if (compiledTables[t].IsEmpty)
                {
                    continue;
                }
                if (!CompiledTable.TryRead(compiledTables[t].Span, out CompiledTable table, out problem))
                {
                    return false;
                }
                for (int i = 0; i < table.EntryCount; i++)
                {
                    if (!_counted.Add(table.GetHash(i)))
                    {
                        continue;
                    }
                    if (!table.TryGetMessage(i, out int start))
                    {
                        AddText(table.GetMemory(i).Span);
                        continue;
                    }
                    _texts.Clear();
                    MessageProgram.CollectTexts(table.Program, start, _texts);
                    for (int s = 0; s < _texts.Count; s++)
                    {
                        AddText(new ReadOnlySpan<char>(table.Characters, _texts[s].Start, _texts[s].Length));
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Adds the symbols <paramref name="language"/> of <paramref name="catalog"/> writes numbers with: its digits,
        /// its separators, and the signs around negative numbers and percentages.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="language"/> is null.</exception>
        public void AddNumberSymbols(CatalogInfo catalog, LanguageInfo language)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }
            NumberSymbols numbers = catalog.GetFormat(language).Numbers;
            AddText(numbers.Digits.AsSpan());
            AddText(numbers.DecimalSeparator.AsSpan());
            AddText(numbers.GroupSeparator.AsSpan());
            AddPattern(numbers.Decimal);
            AddPattern(numbers.Percent);
        }

        /// <summary>Returns the characters in code point order.</summary>
        public int[] ToCodePoints()
        {
            int[] codePoints = new int[_codePoints.Count];
            _codePoints.CopyTo(codePoints);
            Array.Sort(codePoints);
            return codePoints;
        }

        /// <summary>Returns every character in code point order, as one text.</summary>
        public override string ToString()
        {
            int[] codePoints = ToCodePoints();
            StringBuilder text = new(codePoints.Length + 16);
            for (int i = 0; i < codePoints.Length; i++)
            {
                if (codePoints[i] < 0x10000)
                {
                    text.Append((char)codePoints[i]);
                }
                else
                {
                    text.Append(char.ConvertFromUtf32(codePoints[i]));
                }
            }
            return text.ToString();
        }

        private void AddPattern(NumberPattern pattern)
        {
            AddText(pattern.PositivePrefix.AsSpan());
            AddText(pattern.PositiveSuffix.AsSpan());
            AddText(pattern.NegativePrefix.AsSpan());
            AddText(pattern.NegativeSuffix.AsSpan());
        }
    }
}
