using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Messages;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class MessageFormattingTests
    {
        private static readonly CatalogKey Catalog = new("Localization");
        private static readonly TableKey Table = new("Shop");

        private const string CatalogText =
            "@source English\n" +
            "[English]\nCulture = en\n" +
            "[Russian]\nCulture = ru\n";

        [TestCase(1, "You have 1 coin.")]
        [TestCase(5, "You have 5 coins.")]
        [TestCase(1234, "You have 1,234 coins.")]
        [TestCase(1.5, "You have 1.5 coins.")]
        public void Plural_ChoosesTheEnglishForm(double coins, string expected)
        {
            Assert.That(Format("Balance = You have {coins, plural, one {# coin} other {# coins}}.", "Balance", "en", ("coins", coins)), Is.EqualTo(expected));
        }

        [TestCase(1, "1 \u043C\u043E\u043D\u0435\u0442\u0430")]
        [TestCase(3, "3 \u043C\u043E\u043D\u0435\u0442\u044B")]
        [TestCase(5, "5 \u043C\u043E\u043D\u0435\u0442")]
        [TestCase(21, "21 \u043C\u043E\u043D\u0435\u0442\u0430")]
        [TestCase(1.5, "1,5 \u043C\u043E\u043D\u0435\u0442\u044B")]
        public void Plural_ChoosesTheRussianForm(double coins, string expected)
        {
            const string text = "Balance = {coins, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430} few {# \u043C\u043E\u043D\u0435\u0442\u044B} many {# \u043C\u043E\u043D\u0435\u0442} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}";

            Assert.That(Format(text, "Balance", "ru", ("coins", coins)), Is.EqualTo(expected));
        }

        [TestCase(0, "Nobody")]
        [TestCase(1, "Only you")]
        [TestCase(2, "You and 1 other")]
        [TestCase(5, "You and 4 others")]
        public void Plural_MatchesExactFormsBeforeTheOffset(int count, string expected)
        {
            const string text = "Party = {count, plural, offset:1 =0 {Nobody} =1 {Only you} one {You and # other} other {You and # others}}";

            Assert.That(Format(text, "Party", "en", ("count", count)), Is.EqualTo(expected));
        }

        [TestCase(1, "1st")]
        [TestCase(2, "2nd")]
        [TestCase(3, "3rd")]
        [TestCase(4, "4th")]
        [TestCase(11, "11th")]
        [TestCase(21, "21st")]
        [TestCase(112, "112th")]
        public void SelectOrdinal_ChoosesTheEnglishSuffix(int place, string expected)
        {
            const string text = "Place = {place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}";

            Assert.That(Format(text, "Place", "en", ("place", place)), Is.EqualTo(expected));
        }

        [TestCase("female", 1, "She has 1 coin")]
        [TestCase("male", 2, "He has 2 coins")]
        [TestCase("robot", 3, "They have 3 coins")]
        public void Select_ChoosesTheBranchOfItsKeyword(string gender, int coins, string expected)
        {
            const string text = "Owner = {gender, select, female {She has} male {He has} other {They have}} " +
                                "{coins, plural, one {# coin} other {# coins}}";

            Assert.That(Format(text, "Owner", "en", ("gender", gender), ("coins", coins)), Is.EqualTo(expected));
        }

        [Test]
        public void Number_WritesItsStyles()
        {
            Assert.That(Format("Progress = {done, number, percent} done", "Progress", "en", ("done", 0.25)), Is.EqualTo("25% done"));
            Assert.That(Format("Score = {score, number, integer} points", "Score", "en", ("score", 1234.5)), Is.EqualTo("1,234 points"));
            Assert.That(Format("Score = {score} points", "Score", "ru", ("score", 1234.5)), Is.EqualTo("1\u00A0234,5 points"));
        }

        [Test]
        public void Arguments_MatchByNameIgnoringCaseAndOrder()
        {
            const string text = "Trade = {Buyer} pays {seller}";

            Assert.That(Format(text, "Trade", "en", ("SELLER", "Bob"), ("buyer", "Alice")), Is.EqualTo("Alice pays Bob"));
        }

        [Test]
        public void MissingArgument_ShowsItsNameAndIsRecorded()
        {
            string text = Format("Balance = You have {coins, plural, one {# coin} other {# coins}} and {gems} gems", "Balance", "en",
                out MessageProblems problems, ("gems", 3));

            Assert.That(text, Is.EqualTo("You have {coins} and 3 gems"));
            Assert.That(problems.MissingArguments, Is.EqualTo(1u));
        }

        [Test]
        public void MistypedArgument_ChoosesOtherAndIsRecorded()
        {
            string text = Format("Balance = {coins, plural, one {# coin} other {# coins}}", "Balance", "en", out MessageProblems problems, ("coins", "many"));

            Assert.That(text, Is.EqualTo("many coins"));
            Assert.That(problems.MistypedArguments, Is.EqualTo(1u));
        }

        [Test]
        public void CustomType_UsesTheRegisteredFormatter()
        {
            FormatterTable formatters = FormatterTable.Empty.With("date", FormatShortDate);
            DateTime deadline = new(2026, 10, 7);

            string text = Format("Due = Due {deadline, date, short}", "Due", "en", formatters, out MessageProblems problems, ("deadline", deadline));

            Assert.That(text, Is.EqualTo("Due 2026/10/07 (short, en)"));
            Assert.That(problems.HasAny, Is.False);
        }

        [Test]
        public void CustomType_WithoutAFormatter_ShowsTheValueAndIsRecorded()
        {
            string text = Format("Due = Due {deadline, date}", "Due", "en", FormatterTable.Empty, out MessageProblems problems,
                ("deadline", new DateTime(2026, 10, 7, 9, 30, 0)));

            Assert.That(text, Is.EqualTo("Due 2026-10-07 09:30:00"));
            Assert.That(problems.UnformattedArguments, Is.EqualTo(1u));
        }

        [Test]
        public void CustomType_GetsMoreRoomWhenItAsksForIt()
        {
            FormatterTable formatters = FormatterTable.Empty.With("long", FormatLongText);

            string text = Format("Story = [{story, long}]", "Story", "en", formatters, out MessageProblems problems, ("story", 300));

            Assert.That(text, Is.EqualTo("[" + new string('x', 300) + "]"));
            Assert.That(problems.HasAny, Is.False);
        }

        [Test]
        public void CustomType_ThatThrows_ShowsItsNameAndIsRecorded()
        {
            FormatterTable formatters = FormatterTable.Empty.With("broken", FormatByThrowing);

            string text = Format("Broken = <{value, broken}>", "Broken", "en", formatters, out MessageProblems problems, ("value", 1));

            Assert.That(text, Is.EqualTo("<{value}>"));
            Assert.That(problems.FailedArguments, Is.EqualTo(1u));
            Assert.That(problems.FormatterException, Is.InstanceOf<InvalidOperationException>());
        }

        [Test]
        public void GetString_ResolvesQuotingAndShowsMessagesWithoutArguments()
        {
            CompiledTable table = Compile("Hint = Press '{'Enter'}', don''t wait\nBalance = You have {coins, plural, one {# coin} other {# coins}}.", null);

            Assert.That(table.TryFind(Hashing.ComputeNameHash("Hint"), out int hint), Is.True);
            Assert.That(table.TryFind(Hashing.ComputeNameHash("Balance"), out int balance), Is.True);
            Assert.That(table.GetString(hint), Is.EqualTo("Press {Enter}, don't wait"));
            Assert.That(table.TryGetMessage(hint, out _), Is.False);
            Assert.That(table.TryGetMessage(balance, out _), Is.True);
            Assert.That(table.GetString(balance), Is.EqualTo("You have {coins}."));
            Assert.That(table.GetString(balance), Is.SameAs(table.GetString(balance)));
        }

        [Test]
        public void Compile_StoresIdenticalMessagesOnce()
        {
            CompiledTable table = Compile("First = {n} items\nSecond = {n} items\nThird = {n} things", null);

            Assert.That(table.MessageCount, Is.EqualTo(2));
        }

        [Test]
        public void Compile_BrokenSourceMessage_KeepsItsTextAndReportsTheColumn()
        {
            List<DocumentIssue> issues = new();
            CompiledTable table = Compile("Broken = Line\\nbreak {count, plural, one {#}}", issues);

            Assert.That(table.TryFind(Hashing.ComputeNameHash("Broken"), out int broken), Is.True);
            Assert.That(table.GetString(broken), Is.EqualTo("Line\nbreak {count, plural, one {#}}"));
            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Severity, Is.EqualTo(IssueSeverity.Error));
            Assert.That(issues[0].Column, Is.EqualTo(22));
        }

        [Test]
        public void Compile_BrokenTranslation_LeavesTheEntryOut()
        {
            List<DocumentIssue> issues = new();
            CompiledTable table = CompileTranslation("Russian", "Balance = {coins, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430}}", "Balance = {coins, plural, one {# coin} other {# coins}}", issues);

            Assert.That(table.TryFind(Hashing.ComputeNameHash("Balance"), out _), Is.False);
            Assert.That(issues.Exists(issue => issue.Severity == IssueSeverity.Error), Is.True);
        }

        [TestCase("Balance = {money, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430} few {# \u043C\u043E\u043D\u0435\u0442\u044B} many {# \u043C\u043E\u043D\u0435\u0442} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}")]
        [TestCase("Balance = {coins, select, a {x} other {y}}")]
        [TestCase("Balance = \u043C\u043E\u043D\u0435\u0442\u044B")]
        public void Compile_TranslationWithOtherArguments_LeavesTheEntryOut(string translation)
        {
            List<DocumentIssue> issues = new();
            CompiledTable table = CompileTranslation("Russian", translation, "Balance = {coins, plural, one {# coin} other {# coins}}", issues);

            Assert.That(table.TryFind(Hashing.ComputeNameHash("Balance"), out _), Is.False);
            Assert.That(issues.Exists(issue => issue.Severity == IssueSeverity.Error), Is.True);
        }

        [Test]
        public void Compile_TranslationMayShowANumberPlainly()
        {
            List<DocumentIssue> issues = new();
            CompiledTable table = CompileTranslation("Russian", "Balance = \u041C\u043E\u043D\u0435\u0442: {coins}", "Balance = {coins, plural, one {# coin} other {# coins}}", issues);

            Assert.That(table.TryFind(Hashing.ComputeNameHash("Balance"), out _), Is.True);
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Compile_WarnsAboutFormsTheLanguageNeedsOrNeverUses()
        {
            List<DocumentIssue> russianIssues = new();
            List<DocumentIssue> englishIssues = new();
            CompileTranslation("Russian", "Balance = {coins, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}", "Balance = {coins, plural, one {# coin} other {# coins}}", russianIssues);
            CompileTranslation("English", "Balance = {coins, plural, one {# coin} few {# coins} other {# coins}}", null, englishIssues);

            Assert.That(russianIssues.Count, Is.EqualTo(1));
            Assert.That(russianIssues[0].Message, Does.Contain("few, many"));
            Assert.That(englishIssues.Count, Is.EqualTo(1));
            Assert.That(englishIssues[0].Message, Does.Contain("never uses"));
        }

        [Test]
        public void Compile_LetsExactFormsStandInForAForm()
        {
            List<DocumentIssue> english = new();
            List<DocumentIssue> russian = new();
            CompileTranslation("English", "Balance = {coins, plural, =1 {a coin} other {# coins}}", null, english);
            CompileTranslation("Russian", "Balance = {coins, plural, =1 {\u043C\u043E\u043D\u0435\u0442\u0430} few {# \u043C\u043E\u043D\u0435\u0442\u044B} many {# \u043C\u043E\u043D\u0435\u0442} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}", "Balance = {coins, plural, one {# coin} other {# coins}}", russian);

            Assert.That(english, Is.Empty);
            Assert.That(russian.Count, Is.EqualTo(1));
        }

        [Test]
        public void TryRead_RejectsDamagedMessages()
        {
            byte[] data = TableCompiler.Compile(Catalog, Table, new LanguageKey("English"),
                TableDocument.Parse("Balance = {coins, plural, one {# coin} other {# coins}}"), null);
            int damaged = 0;
            for (int offset = 64; offset < data.Length; offset += 4)
            {
                byte[] copy = (byte[])data.Clone();
                copy[offset] ^= 0x5A;
                copy[offset + 1] ^= 0x5A;
                if (!CompiledTable.TryRead(copy, out CompiledTable table, out _))
                {
                    damaged++;
                    continue;
                }
                // Whatever a damaged table still accepts must render without throwing.
                for (int entry = 0; entry < table.EntryCount; entry++)
                {
                    Assert.That(() => { table.GetString(entry); }, Throws.Nothing);
                }
            }

            Assert.That(damaged, Is.GreaterThan(0));
        }

        private static string Format(string tableText, string key, string culture, params (string Name, MessageValue Value)[] arguments)
        {
            return Format(tableText, key, culture, FormatterTable.Empty, out _, arguments);
        }

        private static string Format(string tableText, string key, string culture, out MessageProblems problems, params (string Name, MessageValue Value)[] arguments)
        {
            return Format(tableText, key, culture, FormatterTable.Empty, out problems, arguments);
        }

        private static string Format(string tableText, string key, string culture, FormatterTable formatters, out MessageProblems problems,
            params (string Name, MessageValue Value)[] arguments)
        {
            CompiledTable table = Compile(tableText, null);
            Assert.That(table.TryFind(Hashing.ComputeNameHash(key), out int index), Is.True, key);
            Assert.That(table.TryGetMessage(index, out int start), Is.True, key);
            MessageArgument[] messageArguments = new MessageArgument[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                messageArguments[i] = new MessageArgument(arguments[i].Name, arguments[i].Value);
            }
            EntryMessage message = new(new EntryKey(Table, key), messageArguments);
            LanguageFormat format = LanguageFormat.Resolve(culture, LanguageFormat.Root, null, null, null, out _);
            LanguageInfo language = new(new LanguageKey("Language"), null, culture, default, TextDirection.LeftToRight, false);

            Span<char> buffer = stackalloc char[16];
            TextBuilder output = new(buffer);
            problems = default;
            MessageRenderer.Render(table.Program, table.Characters, start, in message, format, formatters, language, ref output, ref problems);
            string text = output.ToString();
            output.Dispose();
            return text;
        }

        private static CompiledTable Compile(string text, List<DocumentIssue> issues)
        {
            byte[] data = TableCompiler.Compile(Catalog, Table, new LanguageKey("English"), TableDocument.Parse(text), issues);
            Assert.That(CompiledTable.TryRead(data, out CompiledTable table, out string error), Is.True, error);
            return table;
        }

        private static CompiledTable CompileTranslation(string language, string text, string sourceText, List<DocumentIssue> issues)
        {
            CatalogInfo catalog = CatalogInfo.FromDocument(Catalog, CatalogDocument.Parse(CatalogText), null, null);
            TableDocument source = sourceText == null ? null : TableDocument.Parse(sourceText);
            byte[] data = TableCompiler.Compile(catalog, Table, new LanguageKey(language), TableDocument.Parse(text), source, issues);
            Assert.That(CompiledTable.TryRead(data, out CompiledTable table, out string error), Is.True, error);
            return table;
        }

        private static bool FormatShortDate(in MessageValue value, ReadOnlySpan<char> style, LanguageInfo language, Span<char> destination, out int written)
        {
            value.TryGetDateTime(out DateTime date);
            string text = $"{date.Year:D4}/{date.Month:D2}/{date.Day:D2} ({style.ToString()}, {language.Culture})";
            written = 0;
            if (text.Length > destination.Length)
            {
                return false;
            }
            text.AsSpan().CopyTo(destination);
            written = text.Length;
            return true;
        }

        private static bool FormatLongText(in MessageValue value, ReadOnlySpan<char> style, LanguageInfo language, Span<char> destination, out int written)
        {
            value.TryGetNumber(out MessageNumber number);
            int length = (int)number.ToDouble();
            written = 0;
            if (length > destination.Length)
            {
                return false;
            }
            destination.Slice(0, length).Fill('x');
            written = length;
            return true;
        }

        private static bool FormatByThrowing(in MessageValue value, ReadOnlySpan<char> style, LanguageInfo language, Span<char> destination, out int written)
        {
            throw new InvalidOperationException("The formatter failed on purpose.");
        }
    }
}
