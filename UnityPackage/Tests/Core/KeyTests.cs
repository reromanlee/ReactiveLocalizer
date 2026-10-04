using NUnit.Framework;
using System;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class KeyTests
    {
        [Test]
        public void Keys_AreEqualWhenNamesDifferOnlyInCase()
        {
            Assert.That(new CatalogKey("Localization"), Is.EqualTo(new CatalogKey("localization")));
            Assert.That(new TableKey("Shop"), Is.EqualTo(new TableKey("SHOP")));
            Assert.That(new LanguageKey("English"), Is.EqualTo(new LanguageKey("english")));
            Assert.That(new EntryKey("Shop", "Purchase"), Is.EqualTo(new EntryKey("shop", "purchase")));
        }

        [Test]
        public void EntryKeys_OfDifferentTables_AreNotEqual()
        {
            Assert.That(new EntryKey("Shop", "Title"), Is.Not.EqualTo(new EntryKey("Settings", "Title")));
        }

        [Test]
        public void DefaultKeys_AreEmptyAndEqualOnlyToEachOther()
        {
            Assert.That(default(TableKey).IsEmpty, Is.True);
            Assert.That(default(EntryKey).IsEmpty, Is.True);
            Assert.That(default(EntryKey), Is.EqualTo(default(EntryKey)));
            Assert.That(new EntryKey("Shop", "Purchase"), Is.Not.EqualTo(default(EntryKey)));
        }

        [Test]
        public void EntryKey_ReadsAsTableDotEntry()
        {
            Assert.That(new EntryKey("Shop", "Purchase").ToString(), Is.EqualTo("Shop.Purchase"));
            Assert.That(default(EntryKey).ToString(), Is.Empty);
        }

        [Test]
        public void Keys_KeepTheNameAsWrittenAndHashItIgnoringCase()
        {
            EntryKey key = new("Shop", "Purchase");

            Assert.That(key.Name, Is.EqualTo("Purchase"));
            Assert.That(key.Table.Name, Is.EqualTo("Shop"));
            Assert.That(key.Hash, Is.EqualTo(0x6D289C670845619EUL));
            Assert.That(key.Table.Hash, Is.EqualTo(0xC8FB4EAFC9401791UL));
        }

        [TestCase("")]
        [TestCase("Buy Now")]
        [TestCase("1st")]
        public void Keys_RejectInvalidNames(string name)
        {
            Assert.That(() => { _ = new CatalogKey(name); }, Throws.ArgumentException);
            Assert.That(() => { _ = new TableKey(name); }, Throws.ArgumentException);
            Assert.That(() => { _ = new LanguageKey(name); }, Throws.ArgumentException);
            Assert.That(() => { _ = new EntryKey("Shop", name); }, Throws.ArgumentException);
        }

        [Test]
        public void EntryKey_RejectsAnEmptyTable()
        {
            Assert.That(() => { _ = new EntryKey(default(TableKey), "Purchase"); }, Throws.TypeOf<ArgumentException>());
        }
    }
}
