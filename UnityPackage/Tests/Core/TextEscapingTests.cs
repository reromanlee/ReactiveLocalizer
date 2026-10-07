using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TextEscapingTests
    {
        [TestCase("Plain text")]
        [TestCase("  Leading and trailing spaces  ")]
        [TestCase(" ")]
        [TestCase("Line one\nLine two\n")]
        [TestCase("Tab\there")]
        [TestCase(@"C:\Games\new\uniform")]
        [TestCase("No-break\u00A0space and zero\u200Bwidth and \u200Fbidi")]
        [TestCase("Carriage\rreturn")]
        [TestCase("Emoji \uD83D\uDE00 and Cyrillic \u0410")]
        [TestCase("= # @ [ ] { } ' \"")]
        public void Escape_ThenUnescape_GivesBackTheOriginal(string value)
        {
            StringBuilder written = new();
            TextEscaping.Escape(value, written);
            List<DocumentIssue> issues = new();

            string read = TextEscaping.Unescape(written.ToString().Trim(' ', '\t'), 1, 1, issues, new StringBuilder());

            Assert.That(read, Is.EqualTo(value));
            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Escape_EscapesOnlyTheSpacesAtTheEnds()
        {
            StringBuilder written = new();

            TextEscaping.Escape(" a b ", written);

            Assert.That(written.ToString(), Is.EqualTo("\\u0020a b\\u0020"));
        }

        [Test]
        public void Escape_WritesInvisibleCharactersAsUnicodeEscapes()
        {
            StringBuilder written = new();

            TextEscaping.Escape("a\u00A0b\u200Dc", written);

            Assert.That(written.ToString(), Is.EqualTo("a\\u00A0b\\u200Dc"));
        }

        [Test]
        public void Escape_WritesTheEscapesTheFormatDefines()
        {
            StringBuilder written = new();

            TextEscaping.Escape("a\nb\tc\\d", written);

            Assert.That(written.ToString(), Is.EqualTo("a\\nb\\tc\\\\d"));
        }
    }
}
