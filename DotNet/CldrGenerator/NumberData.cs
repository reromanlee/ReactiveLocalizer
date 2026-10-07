using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>
    /// The number symbols of every CLDR locale, reduced to what the runtime needs: the distinct symbol sets, and only
    /// the locales whose set differs from the locale their tag shortens to, which the runtime falls back to on its own.
    /// </summary>
    internal sealed class NumberData
    {
        private NumberData(List<string> sets, SortedDictionary<string, int> locales, int rootSet)
        {
            Sets = sets;
            Locales = locales;
            RootSet = rootSet;
        }

        /// <summary>Each distinct set as the C# expression that creates it, ordered by first locale.</summary>
        public IReadOnlyList<string> Sets { get; }

        /// <summary>The locales the runtime has to find directly, with their set, sorted ordinally.</summary>
        public SortedDictionary<string, int> Locales { get; }

        /// <summary>The set of the root locale, <c>und</c>.</summary>
        public int RootSet { get; }

        public static async Task<NumberData> ReadAsync(CldrSource source)
        {
            JsonDocument available = await source.ReadAsync("cldr-core/availableLocales.json");
            List<string> locales = new();
            foreach (JsonElement locale in available.RootElement.GetProperty("availableLocales").GetProperty("full").EnumerateArray())
            {
                locales.Add(locale.GetString());
            }
            locales.Sort(StringComparer.Ordinal);
            JsonDocument systems = await source.ReadAsync("cldr-core/supplemental/numberingSystems.json");
            JsonElement numberingSystems = systems.RootElement.GetProperty("supplemental").GetProperty("numberingSystems");

            Dictionary<string, string> setOf = new(StringComparer.Ordinal);
            object lockObject = new();
            await Parallel.ForEachAsync(locales, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (locale, _) =>
            {
                JsonDocument document = await source.ReadAsync($"cldr-numbers-full/main/{locale}/numbers.json");
                string set = ReadSet(locale, document.RootElement.GetProperty("main").GetProperty(locale).GetProperty("numbers"), numberingSystems);
                lock (lockObject)
                {
                    setOf.Add(locale, set);
                }
            });

            // Sets are numbered in order of their first locale, so the numbering only changes when the data does.
            List<string> sets = new();
            Dictionary<string, int> setNumbers = new(StringComparer.Ordinal);
            foreach (string locale in locales)
            {
                string set = setOf[locale];
                if (!setNumbers.ContainsKey(set))
                {
                    setNumbers.Add(set, sets.Count);
                    sets.Add(set);
                }
            }
            if (!setOf.ContainsKey("und"))
            {
                throw new FormatException("CLDR has no numbers for the root locale 'und'.");
            }

            SortedDictionary<string, int> kept = new(StringComparer.Ordinal);
            foreach (string locale in locales)
            {
                string parent = Truncate(locale);
                while (parent != null && !setOf.ContainsKey(parent))
                {
                    parent = Truncate(parent);
                }
                // A locale is kept when shortening its tag would reach different symbols, or no symbols at all.
                if (parent == null || setOf[parent] != setOf[locale])
                {
                    kept.Add(locale, setNumbers[setOf[locale]]);
                }
            }
            return new NumberData(sets, kept, setNumbers[setOf["und"]]);
        }

        /// <summary>Shortens a tag the way the runtime does: <c>sr-Latn-BA</c> to <c>sr-Latn</c>; a single subtag has none.</summary>
        private static string Truncate(string locale)
        {
            int index = locale.LastIndexOf('-');
            return index <= 0 ? null : locale.Substring(0, index);
        }

        private static string ReadSet(string locale, JsonElement numbers, JsonElement numberingSystems)
        {
            string system = numbers.GetProperty("defaultNumberingSystem").GetString();
            JsonElement numberingSystem = numberingSystems.GetProperty(system);
            if (numberingSystem.GetProperty("_type").GetString() != "numeric")
            {
                throw new FormatException($"'{locale}' uses the algorithmic numbering system '{system}'.");
            }
            string digits = numberingSystem.GetProperty("_digits").GetString();
            if (new StringInfo(digits).LengthInTextElements != 10)
            {
                throw new FormatException($"The numbering system '{system}' doesn't have ten digits.");
            }
            JsonElement symbols = numbers.GetProperty("symbols-numberSystem-" + system);
            string decimalSeparator = symbols.GetProperty("decimal").GetString();
            string groupSeparator = symbols.GetProperty("group").GetString();
            string minusSign = symbols.GetProperty("minusSign").GetString();
            string percentSign = symbols.GetProperty("percentSign").GetString();
            int minimumGrouping = int.Parse(numbers.GetProperty("minimumGroupingDigits").GetString(), CultureInfo.InvariantCulture);
            string decimalPattern = numbers.GetProperty("decimalFormats-numberSystem-" + system).GetProperty("standard").GetString();
            string percentPattern = numbers.GetProperty("percentFormats-numberSystem-" + system).GetProperty("standard").GetString();

            PatternParts decimalParts = PatternParts.Read(locale, decimalPattern, minusSign, percentSign);
            PatternParts percentParts = PatternParts.Read(locale, percentPattern, minusSign, percentSign);
            // The runtime formats plain numbers without affixes and with three fraction digits at most, and
            // percentages without fraction digits, as every CLDR locale does; anything else must fail loudly here.
            if (decimalParts.PositivePrefix.Length > 0 || decimalParts.PositiveSuffix.Length > 0 || decimalParts.MaximumFractionDigits != 3)
            {
                throw new FormatException($"The decimal pattern '{decimalPattern}' of '{locale}' isn't supported by the runtime.");
            }
            if (percentParts.MaximumFractionDigits != 0)
            {
                throw new FormatException($"The percent pattern '{percentPattern}' of '{locale}' isn't supported by the runtime.");
            }
            return $"new NumberSymbols({SourceText.Literal(decimalSeparator)}, {SourceText.Literal(groupSeparator)}, " +
                   $"{SourceText.Literal(digits)}, {minimumGrouping},\n" +
                   $"                        {decimalParts.ToConstructor()},\n" +
                   $"                        {percentParts.ToConstructor()})";
        }

        /// <summary>A CLDR number pattern taken apart, with its symbols already replaced by the locale's.</summary>
        private sealed class PatternParts
        {
            public string PositivePrefix { get; private set; }

            public string PositiveSuffix { get; private set; }

            public string NegativePrefix { get; private set; }

            public string NegativeSuffix { get; private set; }

            public int PrimaryGrouping { get; private set; }

            public int SecondaryGrouping { get; private set; }

            public int MaximumFractionDigits { get; private set; }

            public static PatternParts Read(string locale, string pattern, string minusSign, string percentSign)
            {
                string[] subpatterns = pattern.Split(';');
                if (subpatterns.Length > 2)
                {
                    throw new FormatException($"The pattern '{pattern}' of '{locale}' has more than two parts.");
                }
                Split(subpatterns[0], out string prefix, out string number, out string suffix);
                PatternParts parts = new()
                {
                    PositivePrefix = ReplaceSymbols(prefix, minusSign, percentSign),
                    PositiveSuffix = ReplaceSymbols(suffix, minusSign, percentSign)
                };
                if (subpatterns.Length == 2)
                {
                    Split(subpatterns[1], out string negativePrefix, out _, out string negativeSuffix);
                    parts.NegativePrefix = ReplaceSymbols(negativePrefix, minusSign, percentSign);
                    parts.NegativeSuffix = ReplaceSymbols(negativeSuffix, minusSign, percentSign);
                }
                else
                {
                    parts.NegativePrefix = minusSign + parts.PositivePrefix;
                    parts.NegativeSuffix = parts.PositiveSuffix;
                }

                int decimalPoint = number.IndexOf('.');
                string integerPart = decimalPoint < 0 ? number : number.Substring(0, decimalPoint);
                string fractionPart = decimalPoint < 0 ? string.Empty : number.Substring(decimalPoint + 1);
                if (fractionPart.Contains('0') || integerPart.Replace("#", string.Empty).Replace(",", string.Empty) != "0")
                {
                    throw new FormatException($"The pattern '{pattern}' of '{locale}' needs minimum digits the runtime doesn't support.");
                }
                parts.MaximumFractionDigits = fractionPart.Length;
                string[] groups = integerPart.Split(',');
                parts.PrimaryGrouping = groups.Length > 1 ? groups[^1].Length : 0;
                parts.SecondaryGrouping = groups.Length > 2 ? groups[^2].Length : parts.PrimaryGrouping;
                return parts;
            }

            public string ToConstructor() =>
                $"new NumberPattern({SourceText.Literal(PositivePrefix)}, {SourceText.Literal(PositiveSuffix)}, " +
                $"{SourceText.Literal(NegativePrefix)}, {SourceText.Literal(NegativeSuffix)}, " +
                $"{PrimaryGrouping}, {SecondaryGrouping}, {MaximumFractionDigits})";

            /// <summary>Splits a subpattern into the text before its number, the number and the text after it.</summary>
            private static void Split(string subpattern, out string prefix, out string number, out string suffix)
            {
                int start = subpattern.IndexOfAny(new[] { '#', '0', ',', '.' });
                if (start < 0)
                {
                    throw new FormatException($"The subpattern '{subpattern}' has no number.");
                }
                int end = start;
                while (end < subpattern.Length && "#0,.".IndexOf(subpattern[end]) >= 0)
                {
                    end++;
                }
                prefix = subpattern.Substring(0, start);
                number = subpattern.Substring(start, end - start);
                suffix = subpattern.Substring(end);
            }

            /// <summary>Turns a pattern's affix into text: <c>%</c> and <c>-</c> become the locale's symbols, quotes are removed.</summary>
            private static string ReplaceSymbols(string affix, string minusSign, string percentSign)
            {
                StringBuilder text = new();
                bool isQuoted = false;
                for (int i = 0; i < affix.Length; i++)
                {
                    char character = affix[i];
                    if (character == '\'')
                    {
                        if (i + 1 < affix.Length && affix[i + 1] == '\'')
                        {
                            text.Append('\'');
                            i++;
                        }
                        else
                        {
                            isQuoted = !isQuoted;
                        }
                    }
                    else if (!isQuoted && character == '%')
                    {
                        text.Append(percentSign);
                    }
                    else if (!isQuoted && character == '-')
                    {
                        text.Append(minusSign);
                    }
                    else
                    {
                        text.Append(character);
                    }
                }
                return text.ToString();
            }
        }
    }
}
