using System.Collections.Generic;
using System.Globalization;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>Writes <c>CldrNumberSymbols.cs</c>: the distinct number symbol sets, and which locale uses which.</summary>
    internal static class NumberSymbolsWriter
    {
        public static string Write(NumberData data, string version)
        {
            SourceText source = new("cldr-numbers-full and numberingSystems.json", version);
            source.Line("namespace reromanlee.ReactiveLocalizer.Formatting");
            source.Line("{");
            source.Line("    /// <summary>");
            source.Line("    /// CLDR's number symbols: the distinct sets locales use, and the locales that can't find theirs by shortening");
            source.Line("    /// their tag. Every other CLDR locale has the same symbols as the locale its tag shortens to, such as");
            source.Line("    /// <c>en-GB</c> and <c>en</c>, so a lookup that shortens the tag until it finds one gets exactly CLDR's data.");
            source.Line("    /// </summary>");
            source.Line("    internal static class CldrNumberSymbols");
            source.Line("    {");
            source.Line("        /// <summary>The CLDR release the symbols come from.</summary>");
            source.Line($"        public const string Version = {SourceText.Literal(version)};");
            source.Line();
            source.Line("        /// <summary>How many distinct symbol sets there are.</summary>");
            source.Line($"        public const int SetCount = {data.Sets.Count.ToString(CultureInfo.InvariantCulture)};");
            source.Line();
            source.Line("        /// <summary>The set of the root locale, for languages without a culture or a fallback.</summary>");
            source.Line($"        public const int RootSet = {data.RootSet.ToString(CultureInfo.InvariantCulture)};");
            source.Line();

            List<string> locales = new();
            List<string> sets = new();
            foreach (KeyValuePair<string, int> locale in data.Locales)
            {
                locales.Add(SourceText.Literal(locale.Key));
                sets.Add(locale.Value.ToString(CultureInfo.InvariantCulture));
            }
            source.Line("        /// <summary>The locales a lookup has to find directly, sorted ordinally for binary search.</summary>");
            source.Line("        private static readonly string[] Locales =");
            source.Line("        {");
            source.WrappedList("            ", locales);
            source.Line("        };");
            source.Line();
            source.Line("        /// <summary>The symbol set of each locale in <see cref=\"Locales\"/>.</summary>");
            source.Line("        private static readonly byte[] Sets =");
            source.Line("        {");
            source.WrappedList("            ", sets);
            source.Line("        };");
            source.Line();
            source.Line("        /// <summary>Returns the symbol set of a CLDR locale written exactly as CLDR names it, or -1.</summary>");
            source.Line("        public static int Find(string locale)");
            source.Line("        {");
            source.Line("            int index = global::System.Array.BinarySearch(Locales, locale, global::System.StringComparer.Ordinal);");
            source.Line("            return index >= 0 ? Sets[index] : -1;");
            source.Line("        }");
            source.Line();
            source.Line("        /// <summary>Creates the symbols of the set <paramref name=\"set\"/>.</summary>");
            source.Line("        public static NumberSymbols Create(int set)");
            source.Line("        {");
            source.Line("            switch (set)");
            source.Line("            {");
            for (int i = 0; i < data.Sets.Count; i++)
            {
                source.Line($"                case {i.ToString(CultureInfo.InvariantCulture)}:");
                source.Line($"                    return {data.Sets[i]};");
            }
            source.Line("            }");
            source.Line("            throw new global::System.ArgumentOutOfRangeException(nameof(set));");
            source.Line("        }");
            source.Line("    }");
            source.Line("}");
            return source.ToString();
        }
    }
}
