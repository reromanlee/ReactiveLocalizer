using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Messages;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Pseudo-localization: a virtual language made from the source text, which shows hardcoded text, truncation and
    /// overflow before any translation exists. Its text is accented, about a third longer and bracketed, so
    /// <c>Buy now</c> shows as <c>[B&#251;&#253; &#241;&#246;&#373; one]</c>.
    /// </summary>
    /// <remarks>
    /// Arguments, plural and select syntax, rich text tags and every character other than an ASCII letter stay as they
    /// are. The accented letters all come from Latin-1 and Latin Extended-A, which nearly every font has. The language
    /// is never stored in files; the editor offers it, and development builds pack it.
    /// </remarks>
    public static class PseudoLocalization
    {
        /// <summary>The name of the pseudo language.</summary>
        public const string LanguageName = "Pseudo";

        /// <summary>How much longer pseudo text is than its source text, as translations into German or Finnish often are.</summary>
        public const double Expansion = 0.35;

        // The accented form of each ASCII letter, A to Z, then a to z. Letters without one in Latin-1 or Latin Extended-A stay plain.
        private const string Accents = "\u00C5B\u00C7\u00D0\u00C9F\u011C\u0124\u00CE\u0134\u0136\u013BM\u00D1\u00D6\u00DEQ\u0154\u0160\u0162\u00DBV\u0174X\u00DD\u017D" +
                                       "\u00E5b\u00E7\u00F0\u00E9f\u011D\u0125\u00EE\u0135\u0137\u013Cm\u00F1\u00F6\u00FEq\u0155\u0161\u0163\u00FBv\u0175x\u00FD\u017E";

        // Padding reads as words, so it wraps like text and is easy to tell apart from the accented text before it.
        private static readonly string[] PaddingWords = { "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten" };

        /// <summary>The key of the pseudo language.</summary>
        public static LanguageKey Language { get; } = new(LanguageName);

        /// <summary>
        /// Returns the pseudo text of <paramref name="message"/>: an ICU message with the same arguments, plural and
        /// select forms and rich text tags, whose literal text is accented, padded and bracketed. An empty message stays
        /// empty. A broken message shows as written, so its pseudo text is the written text, accented.
        /// </summary>
        public static string Transform(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }
            ParsedMessage parsed = MessageParser.Parse(message);
            StringBuilder output = new(message.Length * 3 / 2 + 16);
            output.Append('[');
            int shownLength;
            if (parsed.HasErrors)
            {
                bool isInTag = false;
                char[] accented = message.ToCharArray();
                shownLength = Accent(message, accented, ref isInTag);
                output.Append(MessageQuoting.Quote(new string(accented)));
            }
            else
            {
                Accenter accenter = new();
                MessageWriter.Write(parsed.Body, output, accenter.Rewrite);
                shownLength = Measure(parsed.Body);
            }
            int padding = (int)Math.Ceiling(shownLength * Expansion);
            for (int added = 0, word = 0; added < padding; word++)
            {
                string next = PaddingWords[word % PaddingWords.Length];
                output.Append(' ').Append(next);
                added += next.Length + 1;
            }
            output.Append(']');
            return output.ToString();
        }

        /// <summary>
        /// Returns the pseudo language of <paramref name="catalog"/>. It writes and counts like the source language,
        /// whose culture, number symbols and direction it takes, and falls back to it.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        public static LanguageInfo CreateLanguage(CatalogInfo catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            LanguageInfo source = catalog.SourceLanguage;
            return new LanguageInfo(Language, "Pseudo-localization", source.Culture, source.Key, source.Direction, false,
                source.Digits, source.DecimalSeparator, source.GroupSeparator);
        }

        /// <summary>
        /// Whether <paramref name="catalog"/>, as its file defines it, can have the pseudo language: none of its own
        /// languages has the name, since a language of its own with that name stays a language like any other.
        /// </summary>
        public static bool IsAvailable(CatalogInfo catalog) => catalog != null && !catalog.TryGetLanguage(Language, out _);

        /// <summary>Returns <paramref name="catalog"/> with the pseudo language after its languages, or the catalog itself when it isn't <see cref="IsAvailable"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> is null.</exception>
        public static CatalogInfo AddLanguage(CatalogInfo catalog)
        {
            if (!IsAvailable(catalog))
            {
                return catalog ?? throw new ArgumentNullException(nameof(catalog));
            }
            return catalog.TryAddLanguage(CreateLanguage(catalog), out CatalogInfo extended, out _) ? extended : catalog;
        }

        /// <summary>Returns a table document holding the pseudo text of every entry of <paramref name="source"/>, a source-language file.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
        public static TableDocument CreateDocument(TableDocument source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            TableDocumentEntry[] entries = new TableDocumentEntry[source.Entries.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                TableDocumentEntry entry = source.Entries[i];
                entries[i] = new TableDocumentEntry(entry.Key, Transform(entry.Value), false, 0u, null, null, entry.Line);
            }
            return new TableDocument(Array.Empty<string>(), Array.Empty<DocumentProperty>(), entries, Array.Empty<string>(), Array.Empty<DocumentIssue>());
        }

        /// <summary>
        /// Compiles the pseudo language's version of <paramref name="table"/> from <paramref name="source"/>, its
        /// source-language file, checked like any translation, so the result has every key and alias of the source.
        /// </summary>
        /// <param name="catalog">The catalog as its file defines it, which must be <see cref="IsAvailable"/>.</param>
        /// <param name="table">The table being compiled.</param>
        /// <param name="source">The table's source-language file.</param>
        /// <param name="issues">Where problems are appended, which pseudo text never has unless its source text does; null to ignore them.</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> or <paramref name="source"/> is null.</exception>
        /// <exception cref="ArgumentException">The catalog defines a language named <see cref="LanguageName"/> itself, or <paramref name="table"/> is empty.</exception>
        public static byte[] Compile(CatalogInfo catalog, TableKey table, TableDocument source, ICollection<DocumentIssue> issues)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }
            if (!IsAvailable(catalog))
            {
                throw new ArgumentException($"The catalog '{catalog.Key.Name}' defines a language named {LanguageName} itself.", nameof(catalog));
            }
            return TableCompiler.Compile(AddLanguage(catalog), table, Language, CreateDocument(source), source, issues);
        }

        /// <summary>
        /// Writes the accented form of <paramref name="text"/> into <paramref name="output"/>, which starts as a copy of
        /// it, leaving rich text tags as they are, and returns how many characters it shows outside tags.
        /// </summary>
        /// <param name="isInTag">Whether the text starts inside a tag an earlier text left open; updated for the next text.</param>
        private static int Accent(string text, char[] output, ref bool isInTag)
        {
            int index = 0;
            if (isInTag)
            {
                int close = text.IndexOf('>');
                if (close < 0)
                {
                    return 0;
                }
                isInTag = false;
                index = close + 1;
            }
            int shown = 0;
            while (index < text.Length)
            {
                char character = text[index];
                if (character == '<')
                {
                    int tag = RichTextTags.MeasureTag(text.AsSpan(), index, out bool isUnclosed);
                    if (tag > 0)
                    {
                        index += tag;
                        isInTag = isUnclosed;
                        continue;
                    }
                }
                if (output != null)
                {
                    output[index] = Accent(character);
                }
                shown++;
                index++;
            }
            return shown;
        }

        private static char Accent(char character)
        {
            if ((uint)(character - 'A') <= 'Z' - 'A')
            {
                return Accents[character - 'A'];
            }
            return (uint)(character - 'a') <= 'z' - 'a' ? Accents[26 + character - 'a'] : character;
        }

        /// <summary>Returns how many characters of literal text <paramref name="body"/> shows at most, taking its longest form each time.</summary>
        private static int Measure(MessageBody body)
        {
            int shown = 0;
            bool isInTag = false;
            for (int i = 0; i < body.Parts.Count; i++)
            {
                switch (body.Parts[i])
                {
                    case TextPart text:
                        shown += Accent(text.Text, null, ref isInTag);
                        break;
                    case PluralPart plural:
                        int longestForm = 0;
                        for (int c = 0; c < plural.Cases.Count; c++)
                        {
                            longestForm = Math.Max(longestForm, Measure(plural.Cases[c].Body));
                        }
                        shown += longestForm;
                        break;
                    case SelectPart select:
                        int longestBranch = 0;
                        for (int c = 0; c < select.Cases.Count; c++)
                        {
                            longestBranch = Math.Max(longestBranch, Measure(select.Cases[c].Body));
                        }
                        shown += longestBranch;
                        break;
                }
            }
            return shown;
        }

        /// <summary>Accents the literal texts of one message in order, carrying a tag left open by one text into the next text of the same body.</summary>
        private sealed class Accenter
        {
            private MessageBody _body;
            private bool _isInTag;

            public string Rewrite(string text, MessageBody body)
            {
                if (!ReferenceEquals(body, _body))
                {
                    _body = body;
                    _isInTag = false;
                }
                char[] accented = text.ToCharArray();
                Accent(text, accented, ref _isInTag);
                return new string(accented);
            }
        }
    }
}
