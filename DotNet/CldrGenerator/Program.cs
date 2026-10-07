using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>
    /// Generates the core's CLDR data from one pinned cldr-json release: the plural rules, the number symbols, and the
    /// plural samples the tests check the rules against. Downloads are cached under this project's obj folder.
    /// </summary>
    /// <remarks>
    /// Moving to another CLDR release is changing <see cref="CldrVersion"/>, running the tool, and reviewing the diff.
    /// </remarks>
    internal static class Program
    {
        private const string CldrVersion = "48.2.3";

        private static readonly UTF8Encoding Utf8WithoutMark = new(false);

        private static async Task<int> Main()
        {
            string repository = FindRepository();
            string cache = Path.Combine(repository, "DotNet", "CldrGenerator", "obj", "cldr-" + CldrVersion);
            using CldrSource source = new(CldrVersion, cache);

            PluralData cardinal = PluralData.Read(await source.ReadAsync("cldr-core/supplemental/plurals.json"), "plurals-type-cardinal");
            PluralData ordinal = PluralData.Read(await source.ReadAsync("cldr-core/supplemental/ordinals.json"), "plurals-type-ordinal");
            NumberData numbers = await NumberData.ReadAsync(source);

            string formatting = Path.Combine(repository, "UnityPackage", "Core", "Formatting");
            Write(Path.Combine(formatting, "CldrPluralRules.cs"), PluralRulesWriter.Write(cardinal, ordinal, CldrVersion));
            Write(Path.Combine(formatting, "CldrNumberSymbols.cs"), NumberSymbolsWriter.Write(numbers, CldrVersion));
            Write(Path.Combine(repository, "UnityPackage", "Tests", "Core", "CldrPluralSamples.cs"), PluralSamplesWriter.Write(cardinal, ordinal, CldrVersion));

            Console.WriteLine($"CLDR {CldrVersion}: {cardinal.Locales.Count} cardinal locales in {cardinal.Sets.Count} rule sets, " +
                              $"{ordinal.Locales.Count} ordinal locales in {ordinal.Sets.Count} rule sets, " +
                              $"{numbers.Locales.Count} number locales in {numbers.Sets.Count} symbol sets.");
            return 0;
        }

        private static void Write(string path, string content)
        {
            File.WriteAllText(path, content, Utf8WithoutMark);
            Console.WriteLine("Wrote " + path);
        }

        /// <summary>Finds the repository root: the first folder up from here that holds UnityPackage/package.json.</summary>
        private static string FindRepository()
        {
            DirectoryInfo folder = new(Directory.GetCurrentDirectory());
            while (folder != null)
            {
                if (File.Exists(Path.Combine(folder.FullName, "UnityPackage", "package.json")))
                {
                    return folder.FullName;
                }
                folder = folder.Parent;
            }
            throw new InvalidOperationException("Run the generator from inside the ReactiveLocalizer repository.");
        }
    }
}
