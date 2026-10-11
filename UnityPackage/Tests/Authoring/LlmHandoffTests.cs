using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring.Exchange;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class LlmHandoffTests
    {
        private static readonly string Expected =
            "# Translation request from ReactiveLocalizer: the catalog 'Localization', from English (en) into Russian (ru, Russkij).\n" +
            "#\n" +
            "# Rules:\n" +
            "# 1. Reply with everything from the @catalog line down, in the same format, in one code block. Lines starting with # may be left out.\n" +
            "# 2. Keep the @catalog, @language and @table lines, and each entry's key and [tag], exactly as they are. Translate only the text after \" = \".\n" +
            "# 3. Keep the ICU MessageFormat syntax. Arguments such as {name} stay as they are. In {count, plural, one {...} other {...}}, translate only the texts inside the inner braces, " +
            "and keep the keywords plural, select, selectordinal, number and offset and the selectors such as one, other and =0 unchanged. A quoted '{' is a brace, and '' an apostrophe.\n" +
            "# 4. Russian uses the plural forms one, few, many, other: give every plural exactly these, keeping any =N forms.\n" +
            "# 5. Keep escapes as they are: \\n is a line break, \\t a tab, \\\\ a backslash, \\uXXXX one character. Every entry stays on one line.\n" +
            "# 6. Keep markup such as <b> and </color> unchanged.\n" +
            "# 7. An entry under @maximumLength N must stay within N characters.\n" +
            "# 8. The # lines above an entry describe it.\n" +
            "\n" +
            "@catalog Localization\n" +
            "@language Russian\n" +
            "\n" +
            "@table Common\n" +
            "\n" +
            "# Current Russian text, never checked against the English text: Da\n" +
            $"Confirm [{ExchangeFixture.Tag("OK")}] = OK\n" +
            "\n" +
            "@table Shop\n" +
            "\n" +
            $"Balance [{ExchangeFixture.Tag("You have {coins, plural, one {# coin} other {# coins}}.")}] = You have {{coins, plural, one {{# coin}} other {{# coins}}}}.\n" +
            "\n" +
            "# Current Russian text, made from an older English text: Magazin\n" +
            $"Title [{ExchangeFixture.Tag("Shop")}] = Shop\n";

        [Test]
        public void Write_GivesRulesThenTheEntriesToTranslate()
        {
            string bundle = LlmHandoff.Write(ExchangeFixture.ExportRussian(ExportedEntries.MissingOrOutdated));

            Assert.That(bundle, Is.EqualTo(Expected));
        }

        [Test]
        public void Write_NamesTheContextAndMaximumLength()
        {
            string bundle = LlmHandoff.Write(ExchangeFixture.ExportRussian());

            Assert.That(bundle, Does.Contain($"\n# Button that confirms buying the selected item.\n# Current Russian text: Kupit\n@maximumLength 16\nPurchase [{ExchangeFixture.Tag("Buy")}] = Buy\n"));
        }

        [Test]
        public void Read_TakesTheCodeBlockOfAReply()
        {
            string bundle = LlmHandoff.Write(ExchangeFixture.ExportRussian(ExportedEntries.MissingOrOutdated));
            string entries = bundle.Substring(bundle.IndexOf("\n@catalog ", System.StringComparison.Ordinal) + 1)
                .Replace("] = OK", "] = Da")
                .Replace("] = Shop", "] = Magazin")
                .Replace("] = You have", "] = U vas");
            string reply = "Here is the translation:\n\n```\n" + entries + "```\n\nLet me know if you need anything else.";

            ImportedFile file = LlmHandoff.Read("Reply", reply);

            Assert.That(file.Problems, Is.Empty);
            Assert.That(file.CatalogName, Is.EqualTo("Localization"));
            Assert.That(file.Languages.Count, Is.EqualTo(1));
            Assert.That(file.Languages[0].Label, Is.EqualTo("Russian"));
            Assert.That(file.Rows.Count, Is.EqualTo(3));
            Assert.That(file.Rows[0].ToString(), Is.EqualTo("Common.Confirm"));
            Assert.That(file.Rows[0].GetText(0), Is.EqualTo("Da"));
            Assert.That(file.Rows[0].HasFingerprint, Is.True);
            Assert.That(file.Rows[0].Fingerprint, Is.EqualTo(Hashing.ComputeFingerprint("OK")));
            Assert.That(file.Rows[2].ToString(), Is.EqualTo("Shop.Title"));
            Assert.That(file.Rows[2].GetText(0), Is.EqualTo("Magazin"));
            Assert.That(file.Rows[2].Location, Is.EqualTo("Reply, line 17"));
        }

        [Test]
        public void Read_ReportsWhatItCantPlace()
        {
            string reply = "Purchase = Kupit\n@table Shop\nTitle = Magazin\nthis line is no entry\nEmpty [123abc] =\n";

            ImportedFile file = LlmHandoff.Read("Reply", reply);

            Assert.That(file.Languages[0].Label, Is.EqualTo("(not named)"));
            Assert.That(file.Languages[0].Candidates.Count, Is.EqualTo(0));
            Assert.That(file.Rows.Count, Is.EqualTo(2));
            Assert.That(file.Rows[0].HasFingerprint, Is.False);
            Assert.That(file.Rows[1].GetText(0), Is.Null, "An empty text never erases a translation.");
            Assert.That(file.Problems.Count, Is.EqualTo(2));
            Assert.That(file.Problems[0], Does.StartWith("Reply, line 1: 'Purchase' comes before any @table line"));
            Assert.That(file.Problems[1], Does.StartWith("Reply, line 4: "));
        }
    }
}
