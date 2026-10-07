using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>
    /// Writes <c>CldrPluralSamples.cs</c> for the core's tests: every sample number CLDR lists for every form of every
    /// locale, so the tests can check that each one chooses its form.
    /// </summary>
    internal static class PluralSamplesWriter
    {
        public static string Write(PluralData cardinal, PluralData ordinal, string version)
        {
            SourceText source = new("the samples of plurals.json and ordinals.json", version);
            source.Line("namespace reromanlee.ReactiveLocalizer.Tests");
            source.Line("{");
            source.Line("    /// <summary>");
            source.Line("    /// The sample numbers CLDR lists for each plural form of each locale, written as CLDR writes them: ranges such as");
            source.Line("    /// <c>2~16</c>, compact numbers such as <c>1c6</c>, and an ellipsis where the list goes on.");
            source.Line("    /// </summary>");
            source.Line("    internal static class CldrPluralSamples");
            source.Line("    {");
            source.Line("        public static readonly (string Locale, bool IsOrdinal, string Category, string Samples)[] Rows =");
            source.Line("        {");
            List<string> rows = new();
            AddRows(rows, cardinal, false);
            AddRows(rows, ordinal, true);
            for (int i = 0; i < rows.Count; i++)
            {
                source.Line("            " + rows[i] + (i < rows.Count - 1 ? "," : string.Empty));
            }
            source.Line("        };");
            source.Line("    }");
            source.Line("}");
            return source.ToString();
        }

        private static void AddRows(List<string> rows, PluralData data, bool isOrdinal)
        {
            foreach (PluralSample sample in data.Samples)
            {
                rows.Add($"({SourceText.Literal(sample.Locale)}, {(isOrdinal ? "true" : "false")}, {SourceText.Literal(sample.Category)}, {SourceText.Literal(sample.Samples)})");
            }
        }
    }
}
