using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tables
{
    /// <summary>
    /// Collects the texts of a table being compiled into one character buffer, storing each distinct text once, so a
    /// value repeated across a table, or a word repeated across its messages, costs its characters only once.
    /// </summary>
    internal sealed class CharacterPool
    {
        private readonly Dictionary<string, int> _starts = new(StringComparer.Ordinal);
        private readonly List<string> _texts = new();

        /// <summary>How many characters the buffer holds.</summary>
        public int Length { get; private set; }

        /// <summary>Returns where <paramref name="text"/> starts in the buffer, adding it the first time.</summary>
        public int Add(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }
            if (_starts.TryGetValue(text, out int start))
            {
                return start;
            }
            start = Length;
            _starts.Add(text, start);
            _texts.Add(text);
            Length += text.Length;
            return start;
        }

        /// <summary>Writes the buffer, every text in the order it was added.</summary>
        public void WriteTo(ByteWriter writer)
        {
            for (int i = 0; i < _texts.Count; i++)
            {
                writer.WriteCharacters(_texts[i]);
            }
        }
    }
}
