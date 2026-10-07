using NUnit.Framework;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class NameRulesTests
    {
        [TestCase("Purchase")]
        [TestCase("purchase")]
        [TestCase("Item10234")]
        [TestCase("Line_9")]
        [TestCase("A")]
        public void IsValid_AcceptsIdentifierNames(string name)
        {
            Assert.That(NameRules.IsValid(name), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("10234")]
        [TestCase("_Hidden")]
        [TestCase("Shop.Purchase")]
        [TestCase("Buy Now")]
        [TestCase("Main/Menu")]
        [TestCase("Caf\u00E9")]
        [TestCase("\u041A\u0443\u043F\u0438\u0442\u044C")]
        public void IsValid_RejectsEverythingElse(string name)
        {
            Assert.That(NameRules.IsValid(name), Is.False);
        }

        [Test]
        public void IsValid_RejectsNamesLongerThanTheMaximum()
        {
            Assert.That(NameRules.IsValid(new string('a', NameRules.MaximumLength)), Is.True);
            Assert.That(NameRules.IsValid(new string('a', NameRules.MaximumLength + 1)), Is.False);
        }
    }
}
