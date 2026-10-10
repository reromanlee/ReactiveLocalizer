using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// Compiles generated keys the way Unity would, as C# 9 against the core assembly, together with code calling
    /// them, so generated code that wouldn't compile fails here instead of in a project. Runs under plain .NET only.
    /// </summary>
    public class GeneratedCodeCompilationTests
    {
        private const string Usage =
            "using reromanlee.ReactiveLocalizer;\n" +
            "using MyGame;\n" +
            "public static class Usage\n" +
            "{\n" +
            "    public static void Use(ILocalizer localizer)\n" +
            "    {\n" +
            "        CatalogKey catalog = LocalizationKeys.CatalogKey;\n" +
            "        TableKey table = LocalizationKeys.Shop.TableKey;\n" +
            "        string purchase = localizer.Get(LocalizationKeys.Shop.Purchase);\n" +
            "        string balance = localizer.Get(LocalizationKeys.Shop.CoinBalance(coins: 5));\n" +
            "        EntryMessage fromFloat = LocalizationKeys.Shop.CoinBalance(2.5f);\n" +
            "        EntryMessage fromDecimal = LocalizationKeys.Shop.CoinBalance(19.99m);\n" +
            "        EntryMessage gift = LocalizationKeys.Shop.Gift(coins: 1L, gender: \"female\", name: \"Ann\");\n" +
            "        EntryMessage due = LocalizationKeys.Shop.Due(deadline: System.DateTime.Now);\n" +
            "        EntryMessage many = LocalizationKeys.Shop.Many(1, 2, 3, 4, 5, 6);\n" +
            "        EntryMessage keyword = LocalizationKeys.Common.@event(@class: 3, @default: \"x\");\n" +
            "        LanguageKey english = LocalizationLanguages.English;\n" +
            "    }\n" +
            "}\n";

        [Test]
        public void GeneratedKeys_CompileWithoutWarnings()
        {
            KeysScript script = new("Localization", "MyGame", new[] { "English", "Russian" }, new[]
            {
                new KeysScriptTable("Shop", new[]
                {
                    new KeysScriptEntry("Purchase"),
                    KeysScriptEntry.FromSource("CoinBalance", "You have {coins, plural, one {# coin} other {# coins}}."),
                    KeysScriptEntry.FromSource("Gift", "{name} sent {gender, select, female {her} other {their}} {coins, number} coins"),
                    KeysScriptEntry.FromSource("Due", "Due {deadline, date, short}"),
                    KeysScriptEntry.FromSource("Many", "{a}{b}{c}{d}{e}{f}")
                }, new[]
                {
                    new KeyValuePair<string, string>("BuyButton", "Purchase"),
                    new KeyValuePair<string, string>("CoinsLabel", "CoinBalance")
                }),
                new KeysScriptTable("Common", new[]
                {
                    KeysScriptEntry.FromSource("event", "{class, plural, other {#}} {default}")
                }, null),
                // Every entry of Hud moved to Shop, so Hud keeps only their former names.
                new KeysScriptTable("Hud", Array.Empty<KeysScriptEntry>(), null, new[]
                {
                    new KeysScriptMovedEntry("Buy", "Shop", new KeysScriptEntry("Purchase")),
                    new KeysScriptMovedEntry("Balance", "Shop", KeysScriptEntry.FromSource("CoinBalance", "You have {coins, plural, one {# coin} other {# coins}}."))
                })
            });
            List<string> problems = new();
            string generated = KeysScriptWriter.Write(script, problems);

            Assert.That(problems, Is.Empty);
            Assert.That(Compile(generated, Usage, MovedUsage), Is.Empty);
            Assert.That(generated, Does.Contain("[global::System.Obsolete(\"Moved to Shop.Purchase.\")]"));
        }

        // Moved entries are obsolete on purpose; code still using them compiles, with the warning silenced here.
        private const string MovedUsage =
            "#pragma warning disable 0618\n" +
            "using reromanlee.ReactiveLocalizer;\n" +
            "using MyGame;\n" +
            "public static class MovedUsage\n" +
            "{\n" +
            "    public static void Use(ILocalizer localizer)\n" +
            "    {\n" +
            "        string buy = localizer.Get(LocalizationKeys.Hud.Buy);\n" +
            "        string balance = localizer.Get(LocalizationKeys.Hud.Balance(coins: 3));\n" +
            "        TableKey table = LocalizationKeys.Hud.TableKey;\n" +
            "    }\n" +
            "}\n";

        private static List<string> Compile(params string[] sources)
        {
            CSharpParseOptions parseOptions = new(LanguageVersion.CSharp9);
            List<MetadataReference> references = new();
            string trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            foreach (string path in trusted.Split(Path.PathSeparator))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
            references.Add(MetadataReference.CreateFromFile(typeof(EntryKey).Assembly.Location));
            CSharpCompilation compilation = CSharpCompilation.Create("GeneratedKeys",
                sources.Select(source => CSharpSyntaxTree.ParseText(source, parseOptions)),
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return compilation.GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
                .Select(diagnostic => diagnostic.ToString())
                .ToList();
        }
    }
}
