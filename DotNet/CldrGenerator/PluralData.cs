using System;
using System.Collections.Generic;
using System.Text.Json;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>
    /// One kind of CLDR plural rules, cardinal or ordinal: every locale's rules, grouped into the distinct rule sets
    /// they share, and the sample numbers CLDR lists for each form.
    /// </summary>
    internal sealed class PluralData
    {
        /// <summary>The forms in CLDR's order, which is also the order of the core's PluralCategory values.</summary>
        public static readonly string[] Categories = { "zero", "one", "two", "few", "many", "other" };

        private PluralData(List<PluralRuleSet> sets, SortedDictionary<string, PluralRuleSet> locales, List<PluralSample> samples)
        {
            Sets = sets;
            Locales = locales;
            Samples = samples;
        }

        /// <summary>The distinct rule sets, ordered by their number: the root's rules first, then by first locale.</summary>
        public IReadOnlyList<PluralRuleSet> Sets { get; }

        /// <summary>Every locale with its rule set, sorted ordinally.</summary>
        public SortedDictionary<string, PluralRuleSet> Locales { get; }

        public IReadOnlyList<PluralSample> Samples { get; }

        /// <summary>Reads <c>plurals.json</c> or <c>ordinals.json</c>, whose rules sit under <paramref name="typeName"/>.</summary>
        public static PluralData Read(JsonDocument document, string typeName)
        {
            JsonElement rules = document.RootElement.GetProperty("supplemental").GetProperty(typeName);
            Dictionary<string, PluralRuleSet> byKey = new(StringComparer.Ordinal);
            SortedDictionary<string, PluralRuleSet> locales = new(StringComparer.Ordinal);
            List<PluralSample> samples = new();
            foreach (JsonProperty locale in rules.EnumerateObject())
            {
                SortedDictionary<int, string> conditions = new();
                foreach (JsonProperty rule in locale.Value.EnumerateObject())
                {
                    const string prefix = "pluralRule-count-";
                    if (!rule.Name.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        throw new FormatException($"Unexpected plural rule '{rule.Name}' for '{locale.Name}'.");
                    }
                    string category = rule.Name.Substring(prefix.Length);
                    int categoryIndex = Array.IndexOf(Categories, category);
                    if (categoryIndex < 0)
                    {
                        throw new FormatException($"Unknown plural category '{category}' for '{locale.Name}'.");
                    }
                    string text = rule.Value.GetString() ?? string.Empty;
                    int samplesStart = text.IndexOf('@');
                    string condition = (samplesStart < 0 ? text : text.Substring(0, samplesStart)).Trim();
                    string sampleText = samplesStart < 0 ? string.Empty : text.Substring(samplesStart).Trim();
                    if (category != "other")
                    {
                        conditions.Add(categoryIndex, condition);
                    }
                    if (sampleText.Length > 0)
                    {
                        samples.Add(new PluralSample(locale.Name, category, sampleText));
                    }
                }
                string key = string.Join("|", conditions);
                if (!byKey.TryGetValue(key, out PluralRuleSet set))
                {
                    set = new PluralRuleSet(conditions);
                    byKey.Add(key, set);
                }
                set.Locales.Add(locale.Name);
                locales.Add(locale.Name, set);
            }

            // The root locale's set, which has only 'other', becomes number 0; the rest follow their first locale.
            List<PluralRuleSet> sets = new(byKey.Values);
            foreach (PluralRuleSet set in sets)
            {
                set.Locales.Sort(StringComparer.Ordinal);
            }
            sets.Sort((left, right) =>
            {
                bool isLeftRoot = left.Locales.Contains("und");
                bool isRightRoot = right.Locales.Contains("und");
                if (isLeftRoot != isRightRoot)
                {
                    return isLeftRoot ? -1 : 1;
                }
                return string.CompareOrdinal(left.Locales[0], right.Locales[0]);
            });
            if (sets.Count == 0 || !sets[0].Locales.Contains("und") || sets[0].Conditions.Count != 0)
            {
                throw new FormatException($"The root locale of '{typeName}' is expected to have only the 'other' form.");
            }
            for (int i = 0; i < sets.Count; i++)
            {
                sets[i].Id = i;
            }
            samples.Sort((left, right) =>
            {
                int byLocale = string.CompareOrdinal(left.Locale, right.Locale);
                return byLocale != 0 ? byLocale : Array.IndexOf(Categories, left.Category).CompareTo(Array.IndexOf(Categories, right.Category));
            });
            return new PluralData(sets, locales, samples);
        }
    }

    /// <summary>A distinct set of plural rules and the locales that share it.</summary>
    internal sealed class PluralRuleSet
    {
        public PluralRuleSet(SortedDictionary<int, string> conditions)
        {
            Conditions = conditions;
        }

        public int Id { get; set; }

        /// <summary>The condition of each form but <c>other</c>, keyed by the form's index in <see cref="PluralData.Categories"/>.</summary>
        public SortedDictionary<int, string> Conditions { get; }

        public List<string> Locales { get; } = new();

        /// <summary>The forms the set uses as a bit mask, <c>other</c> included.</summary>
        public int CategoryMask
        {
            get
            {
                int mask = 1 << Array.IndexOf(PluralData.Categories, "other");
                foreach (int category in Conditions.Keys)
                {
                    mask |= 1 << category;
                }
                return mask;
            }
        }
    }

    /// <summary>The sample numbers CLDR lists for one form of one locale, as written in the data.</summary>
    internal sealed class PluralSample
    {
        public PluralSample(string locale, string category, string samples)
        {
            Locale = locale;
            Category = category;
            Samples = samples;
        }

        public string Locale { get; }

        public string Category { get; }

        public string Samples { get; }
    }
}
