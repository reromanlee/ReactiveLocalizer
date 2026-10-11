using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class MessageTextTests
    {
        [TestCase("Press {0}", "Press '{'0'}'")]
        [TestCase("Don't", "Don't")]
        [TestCase("", "")]
        public void Quote_WritesAMessageThatShowsTheTextAsIs(string text, string expected)
        {
            Assert.That(MessageText.Quote(text), Is.EqualTo(expected));
            Assert.That(MessageText.IsValid(MessageText.Quote(text), out _), Is.True);
        }

        [Test]
        public void Quote_BeforeAnArgument_KeepsAFinalApostropheApart()
        {
            string message = MessageText.Quote("Players'", true) + "{name}";

            Assert.That(message, Is.EqualTo("Players''{name}"));
            Assert.That(MessageText.IsValid(message, out _), Is.True);
        }

        [Test]
        public void IsValid_TellsTheFirstError()
        {
            Assert.That(MessageText.IsValid("You have {coins, plural, one {# coin} other {# coins}}.", out string problem), Is.True);
            Assert.That(problem, Is.Null);
            Assert.That(MessageText.IsValid("Add {0}", out problem), Is.False);
            Assert.That(problem, Does.StartWith("at 5: Arguments have names"));
        }

        [Test]
        public void NameRules_AcceptIdentifierSafeNamesOnly()
        {
            Assert.That(NameRules.IsValid("SettingsPage"), Is.True);
            Assert.That(NameRules.IsValid("Settings Page"), Is.False);
            Assert.That(NameRules.IsValid("1Page"), Is.False);
        }
    }
}
