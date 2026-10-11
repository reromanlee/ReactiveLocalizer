using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Messages;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class PseudoLocalizationTests
    {
        private static CatalogInfo CreateCatalog(string languages = "[English]\nCulture = en\n\n[Russian]\nCulture = ru\n")
        {
            return CatalogInfo.FromDocument(new CatalogKey("Game"), CatalogDocument.Parse("@source English\n\n" + languages), null, null);
        }

        [TestCase("Buy now", "[B\u00FB\u00FD \u00F1\u00F6\u0175 one]")]
        [TestCase("ABCDEFGHIJKLMNOPQRSTUVWXYZ abcdefghijklmnopqrstuvwxyz 0123",
            "[\u00C5B\u00C7\u00D0\u00C9F\u011C\u0124\u00CE\u0134\u0136\u013BM\u00D1\u00D6\u00DEQ\u0154\u0160\u0162\u00DBV\u0174X\u00DD\u017D " +
            "\u00E5b\u00E7\u00F0\u00E9f\u011D\u0125\u00EE\u0135\u0137\u013Cm\u00F1\u00F6\u00FEq\u0155\u0161\u0163\u00FBv\u0175x\u00FD\u017E 0123 one two three four five]")]
        [TestCase("\u041A\u0443\u043F\u0438\u0442\u044C", "[\u041A\u0443\u043F\u0438\u0442\u044C one]")]
        [TestCase("{name}", "[{name}]")]
        [TestCase("", "")]
        public void Transform_AccentsPadsAndBracketsText(string message, string expected)
        {
            Assert.That(PseudoLocalization.Transform(message), Is.EqualTo(expected));
        }

        [TestCase("You have {coins, plural, one {# coin} other {# coins}}.",
            "[\u00DD\u00F6\u00FB \u0125\u00E5v\u00E9 {coins, plural, one {# \u00E7\u00F6\u00EE\u00F1} other {# \u00E7\u00F6\u00EE\u00F1\u0161}}. one two]")]
        [TestCase("{gender, select, female {She} other {They}} {name}",
            "[{gender, select, female {\u0160\u0125\u00E9} other {\u0162\u0125\u00E9\u00FD}} {name} one]")]
        [TestCase("Due {deadline, date, short} at {share, number, percent}",
            "[\u00D0\u00FB\u00E9 {deadline, date, short} \u00E5\u0163 {share, number, percent} one]")]
        public void Transform_KeepsMessageSyntax(string message, string expected)
        {
            string pseudo = PseudoLocalization.Transform(message);

            Assert.That(pseudo, Is.EqualTo(expected));
            Assert.That(MessageParser.Parse(pseudo).HasErrors, Is.False);
        }

        [TestCase("<b>Buy</b> <color=#FF0000>now</color>", "[<b>B\u00FB\u00FD</b> <color=#FF0000>\u00F1\u00F6\u0175</color> one]")]
        [TestCase("<color={color}>Hot</color>", "[<color={color}>\u0124\u00F6\u0163</color> one]")]
        [TestCase("<sprite name=\"coin\"> x <3", "[<sprite name=\"coin\"> x <3 one]")]
        public void Transform_LeavesRichTextTags(string message, string expected)
        {
            Assert.That(PseudoLocalization.Transform(message), Is.EqualTo(expected));
        }

        [Test]
        public void Transform_KeepsQuotedTextLiteral()
        {
            string pseudo = PseudoLocalization.Transform("Don't press '{'");

            Assert.That(pseudo, Is.EqualTo("[\u00D0\u00F6\u00F1'\u0163 \u00FE\u0155\u00E9\u0161\u0161 '{' one two]"));
            Assert.That(MessageParser.Parse(pseudo).LiteralText, Is.EqualTo("[\u00D0\u00F6\u00F1'\u0163 \u00FE\u0155\u00E9\u0161\u0161 { one two]"));
        }

        [Test]
        public void Transform_ShowsABrokenMessageAsWritten()
        {
            string pseudo = PseudoLocalization.Transform("Hello {name");

            Assert.That(MessageParser.Parse(pseudo).LiteralText, Is.EqualTo("[\u0124\u00E9\u013C\u013C\u00F6 {\u00F1\u00E5m\u00E9 one]"));
        }

        [Test]
        public void Transform_KeepsTheArgumentsOfEveryValidMessage()
        {
            Random random = new(5);
            string[] pieces = { "a", "Z ", "'", "#", "{", "}", "{n}", "{n, plural, one {", "{g, select, x {", "} other {", "}}", "'{'", "''", "<b>", "</b>", "<i" };
            List<string> failures = new();
            int checkedCount = 0;
            for (int i = 0; i < 20000 && failures.Count < 10; i++)
            {
                StringBuilder message = new();
                int count = random.Next(1, 9);
                for (int p = 0; p < count; p++)
                {
                    message.Append(pieces[random.Next(pieces.Length)]);
                }
                ParsedMessage parsed = MessageParser.Parse(message.ToString());
                if (parsed.HasErrors)
                {
                    continue;
                }
                checkedCount++;
                string pseudo = PseudoLocalization.Transform(message.ToString());
                ParsedMessage pseudoParsed = MessageParser.Parse(pseudo);
                bool isSame = !pseudoParsed.HasErrors && pseudoParsed.Arguments.Count == parsed.Arguments.Count;
                for (int a = 0; isSame && a < parsed.Arguments.Count; a++)
                {
                    isSame = pseudoParsed.Arguments[a].Name == parsed.Arguments[a].Name && pseudoParsed.Arguments[a].Uses == parsed.Arguments[a].Uses;
                }
                if (!isSame)
                {
                    failures.Add($"{message} -> {pseudo}");
                }
            }

            Assert.That(failures, Is.Empty);
            Assert.That(checkedCount, Is.GreaterThan(500));
        }

        [Test]
        public void AddLanguage_AddsALanguageThatWritesLikeTheSource()
        {
            CatalogInfo catalog = CreateCatalog();

            CatalogInfo extended = PseudoLocalization.AddLanguage(catalog);

            Assert.That(PseudoLocalization.IsAvailable(catalog), Is.True);
            Assert.That(extended.Languages.Count, Is.EqualTo(3));
            LanguageInfo pseudo = extended.Languages[2];
            Assert.That(pseudo.Key, Is.EqualTo(PseudoLocalization.Language));
            Assert.That(pseudo.Culture, Is.EqualTo("en"));
            Assert.That(pseudo.Fallback.Name, Is.EqualTo("English"));
            Assert.That(extended.GetFallbackChain(pseudo).Count, Is.EqualTo(2));
            Assert.That(PseudoLocalization.AddLanguage(extended), Is.SameAs(extended));
        }

        [Test]
        public void AddLanguage_LeavesACatalogWithItsOwnPseudoLanguage()
        {
            CatalogInfo catalog = CreateCatalog("[English]\nCulture = en\n\n[Pseudo]\nDisplayName = Pirate\n");
            TableDocument source = TableDocument.Parse("Buy = Buy\n");

            Assert.That(PseudoLocalization.IsAvailable(catalog), Is.False);
            Assert.That(PseudoLocalization.AddLanguage(catalog), Is.SameAs(catalog));
            Assert.That(() => PseudoLocalization.Compile(catalog, new TableKey("Shop"), source, null), Throws.ArgumentException);
        }

        [Test]
        public void Compile_CompilesEveryEntryAndAliasOfTheSource()
        {
            CatalogInfo catalog = CreateCatalog();
            TableDocument source = TableDocument.Parse("Buy = Buy now\n\n@formerly CoinsLabel\nCoins = {n, plural, one {# coin} other {# coins}}\n\nEmpty =\n");
            List<DocumentIssue> issues = new();

            byte[] data = PseudoLocalization.Compile(catalog, new TableKey("Shop"), source, issues);

            Assert.That(issues, Is.Empty);
            Assert.That(CompiledTable.TryRead(data, out CompiledTable table, out string error), Is.True, error);
            Assert.That(table.LanguageHash, Is.EqualTo(PseudoLocalization.Language.Hash));
            Assert.That(table.IsComplete(TableCompiler.ComputeKeysHash(source)), Is.True);
            Assert.That(table.TryFind(Hashing.ComputeNameHash("Buy"), out int buy), Is.True);
            Assert.That(table.GetString(buy), Is.EqualTo("[B\u00FB\u00FD \u00F1\u00F6\u0175 one]"));
            Assert.That(table.TryFind(Hashing.ComputeNameHash("CoinsLabel"), out int coins), Is.True);
            Assert.That(table.TryGetMessage(coins, out _), Is.True);
            Assert.That(table.TryFind(Hashing.ComputeNameHash("Empty"), out int empty), Is.True);
            Assert.That(table.GetString(empty), Is.Empty);
        }
    }
}
