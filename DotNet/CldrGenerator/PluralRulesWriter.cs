using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>Writes <c>CldrPluralRules.cs</c>: every cardinal and ordinal rule set as code, and which locale uses which.</summary>
    internal static class PluralRulesWriter
    {
        private static readonly string[] CategoryNames = { "Zero", "One", "Two", "Few", "Many", "Other" };

        public static string Write(PluralData cardinal, PluralData ordinal, string version)
        {
            SourceText source = new("plurals.json and ordinals.json", version);
            source.Line("namespace reromanlee.ReactiveLocalizer.Formatting");
            source.Line("{");
            source.Line("    /// <summary>");
            source.Line("    /// CLDR's plural rules as code: which rule set each locale uses, the forms each set has, and the conditions");
            source.Line("    /// that choose a form for a number. Rule set 0 is the root locale's, which chooses <c>other</c> for every number.");
            source.Line("    /// </summary>");
            source.Line("    internal static class CldrPluralRules");
            source.Line("    {");
            source.Line("        /// <summary>The CLDR release the rules come from.</summary>");
            source.Line($"        public const string Version = {SourceText.Literal(version)};");
            source.Line();
            WriteLocaleTables(source, "Cardinal", cardinal);
            WriteLocaleTables(source, "Ordinal", ordinal);
            source.Line("        /// <summary>Returns the cardinal rule set of a CLDR locale, or -1 when CLDR has none for it.</summary>");
            source.Line("        public static int FindCardinal(string locale) => Find(CardinalLocales, CardinalSets, locale);");
            source.Line();
            source.Line("        /// <summary>Returns the ordinal rule set of a CLDR locale, or -1 when CLDR has none for it.</summary>");
            source.Line("        public static int FindOrdinal(string locale) => Find(OrdinalLocales, OrdinalSets, locale);");
            source.Line();
            WriteCategories(source, "Cardinal", cardinal);
            WriteCategories(source, "Ordinal", ordinal);
            WriteSelect(source, "Cardinal", cardinal);
            WriteSelect(source, "Ordinal", ordinal);
            source.Line("        private static int Find(string[] locales, byte[] sets, string locale)");
            source.Line("        {");
            source.Line("            int index = global::System.Array.BinarySearch(locales, locale, global::System.StringComparer.Ordinal);");
            source.Line("            return index >= 0 ? sets[index] : -1;");
            source.Line("        }");
            source.Line("    }");
            source.Line("}");
            return source.ToString();
        }

        private static void WriteLocaleTables(SourceText source, string kind, PluralData data)
        {
            List<string> locales = new();
            List<string> sets = new();
            foreach (KeyValuePair<string, PluralRuleSet> locale in data.Locales)
            {
                locales.Add(SourceText.Literal(locale.Key));
                sets.Add(locale.Value.Id.ToString(CultureInfo.InvariantCulture));
            }
            source.Line($"        /// <summary>Every locale with {kind.ToLowerInvariant()} rules, sorted ordinally for binary search.</summary>");
            source.Line($"        private static readonly string[] {kind}Locales =");
            source.Line("        {");
            source.WrappedList("            ", locales);
            source.Line("        };");
            source.Line();
            source.Line($"        /// <summary>The rule set of each locale in <see cref=\"{kind}Locales\"/>.</summary>");
            source.Line($"        private static readonly byte[] {kind}Sets =");
            source.Line("        {");
            source.WrappedList("            ", sets);
            source.Line("        };");
            source.Line();
        }

        private static void WriteCategories(SourceText source, string kind, PluralData data)
        {
            source.Line($"        /// <summary>Returns the forms {Article(kind)} {kind.ToLowerInvariant()} rule set uses, one bit per <see cref=\"PluralCategory\"/>.</summary>");
            source.Line($"        public static int Get{kind}Categories(int rules)");
            source.Line("        {");
            source.Line("            switch (rules)");
            source.Line("            {");
            foreach (PluralRuleSet set in data.Sets)
            {
                if (set.Id == 0)
                {
                    continue;
                }
                source.Line($"                case {set.Id}:");
                source.Line($"                    return 0b{Convert.ToString(set.CategoryMask, 2).PadLeft(6, '0')}; // {DescribeMask(set.CategoryMask)}");
            }
            source.Line("            }");
            source.Line("            return PluralCategories.OtherOnly;");
            source.Line("        }");
            source.Line();
        }

        private static void WriteSelect(SourceText source, string kind, PluralData data)
        {
            source.Line($"        /// <summary>Returns the form the {kind.ToLowerInvariant()} rule set <paramref name=\"rules\"/> chooses for a number.</summary>");
            source.Line($"        public static PluralCategory Select{kind}(int rules, in PluralOperands o)");
            source.Line("        {");
            source.Line("            switch (rules)");
            source.Line("            {");
            foreach (PluralRuleSet set in data.Sets)
            {
                if (set.Id == 0)
                {
                    continue;
                }
                WriteLocaleComment(source, set.Locales);
                source.Line($"                case {set.Id}:");
                foreach (KeyValuePair<int, string> condition in set.Conditions)
                {
                    source.Line($"                    if ({PluralConditionTranslator.Translate(condition.Value)})");
                    source.Line("                    {");
                    source.Line($"                        return PluralCategory.{CategoryNames[condition.Key]};");
                    source.Line("                    }");
                }
                source.Line("                    break;");
            }
            source.Line("            }");
            source.Line("            return PluralCategory.Other;");
            source.Line("        }");
            source.Line();
        }

        private static void WriteLocaleComment(SourceText source, IReadOnlyList<string> locales)
        {
            const string prefix = "                // ";
            StringBuilder line = new();
            for (int i = 0; i < locales.Count; i++)
            {
                string item = i < locales.Count - 1 ? locales[i] + "," : locales[i];
                if (line.Length > 0 && prefix.Length + line.Length + 1 + item.Length > 120)
                {
                    source.Line(prefix + line);
                    line.Clear();
                }
                if (line.Length > 0)
                {
                    line.Append(' ');
                }
                line.Append(item);
            }
            source.Line(prefix + line);
        }

        private static string Article(string kind) => kind == "Ordinal" ? "an" : "a";

        private static string DescribeMask(int mask)
        {
            List<string> names = new();
            for (int i = 0; i < PluralData.Categories.Length; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    names.Add(PluralData.Categories[i]);
                }
            }
            return string.Join(", ", names);
        }
    }
}
