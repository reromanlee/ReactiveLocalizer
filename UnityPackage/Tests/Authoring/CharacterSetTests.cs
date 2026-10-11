using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class CharacterSetTests
    {
        private static readonly CatalogInfo Catalog = CatalogInfo.FromDocument(new CatalogKey("Game"),
            CatalogDocument.Parse("@source English\n\n[English]\nCulture = en\n\n[Russian]\nCulture = ru\n"), null, null);

        private static byte[] Compile(string language, string text, string sourceText)
        {
            List<DocumentIssue> issues = new();
            byte[] data = TableCompiler.Compile(Catalog, new TableKey("Shop"), new LanguageKey(language), TableDocument.Parse(text),
                sourceText != null ? TableDocument.Parse(sourceText) : null, issues);
            Assert.That(issues.FindAll(issue => issue.Severity == IssueSeverity.Error), Is.Empty);
            return data;
        }

        [Test]
        public void AddText_LeavesOutTagsAndControlCharacters()
        {
            CharacterSet set = new();

            set.AddText("Hi <b>there</b>\n\t\u200B".AsSpan());

            Assert.That(set.ToString(), Is.EqualTo(" Hehirt"));
        }

        [Test]
        public void AddText_CountsASurrogatePairAsOneCharacter()
        {
            CharacterSet set = new();

            set.AddText("\uD83D\uDE00a\u00A0\uD800".AsSpan());

            Assert.That(set.Count, Is.EqualTo(3));
            Assert.That(set.Contains(0x1F600), Is.True);
            Assert.That(set.ToCodePoints(), Is.EqualTo(new[] { 'a', 0xA0, 0x1F600 }));
            Assert.That(set.ToString(), Is.EqualTo("a\u00A0\uD83D\uDE00"));
        }

        [Test]
        public void AddText_CountsAnUnclosedTagAsText()
        {
            CharacterSet set = new();

            set.AddText("<color=".AsSpan());

            Assert.That(set.ToString(), Is.EqualTo("<=clor"));
        }

        [Test]
        public void AddTable_CountsEachEntryFromTheFirstLanguageThatHasIt()
        {
            const string english = "Buy = WOW\nCoins = {count, plural, one {# AB} other {# CD}}\nGreet = HI {name}\nPick = {g, select, x {XY} other {ZQ}}\n";
            byte[] source = Compile("English", english, null);
            byte[] russian = Compile("Russian", "Buy = \u041A\u0443\u043F\u0438\u0442\u044C\n", english);
            CharacterSet set = new();

            bool isRead = set.AddTable(new ReadOnlyMemory<byte>[] { russian, ReadOnlyMemory<byte>.Empty, source }, out string problem);

            Assert.That(isRead, Is.True, problem);
            Assert.That(set.ToString(), Is.EqualTo(" ABCDHIQXYZ\u041A\u0438\u043F\u0442\u0443\u044C"));
        }

        [Test]
        public void AddTable_ReportsDamagedData()
        {
            CharacterSet set = new();

            Assert.That(set.AddTable(new ReadOnlyMemory<byte>[] { new byte[] { 1, 2, 3 } }, out string problem), Is.False);
            Assert.That(problem, Is.Not.Null);
        }

        [Test]
        public void AddNumberSymbols_AddsDigitsSeparatorsAndSigns()
        {
            CharacterSet set = new();

            Catalog.TryGetLanguage(new LanguageKey("Russian"), out LanguageInfo russian);
            set.AddNumberSymbols(Catalog, russian);

            Assert.That(set.ToString(), Is.EqualTo("%,-0123456789\u00A0"));
        }
    }
}
