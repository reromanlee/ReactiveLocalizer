using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Unity;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class EntryReferenceTests
    {
        [Test]
        public void ToKey_ReturnsTheKeysOfTheReferencedCatalogAndEntry()
        {
            EntryReference reference = new("Localization", "Shop", "Purchase");

            Assert.That(reference.ToKey(), Is.EqualTo(new EntryKey("Shop", "Purchase")));
            Assert.That(reference.ToCatalogKey(), Is.EqualTo(new CatalogKey("Localization")));
            Assert.That(reference.IsEmpty, Is.False);
            Assert.That(reference.ToString(), Is.EqualTo("Shop.Purchase"));
        }

        [TestCase(null, null)]
        [TestCase("Shop", "")]
        [TestCase("Shop", "Buy Now")]
        [TestCase("<<<<<<< HEAD", "Purchase")]
        public void ToKey_TurnsUnusableNamesIntoAnEmptyKey(string table, string entry)
        {
            Assert.That(new EntryReference("Localization", table, entry).ToKey().IsEmpty, Is.True);
        }

        [Test]
        public void ToCatalogKey_IsEmptyWithoutACatalog()
        {
            Assert.That(new EntryReference(null, "Shop", "Purchase").ToCatalogKey().IsEmpty, Is.True);
            Assert.That(new EntryReference("Bad Name", "Shop", "Purchase").ToCatalogKey().IsEmpty, Is.True);
        }

        [Test]
        public void References_CompareLikeKeys()
        {
            EntryReference fromKey = new(new CatalogKey("Localization"), new EntryKey("Shop", "Purchase"));

            Assert.That(fromKey, Is.EqualTo(new EntryReference("localization", "shop", "PURCHASE")));
            Assert.That(fromKey.GetHashCode(), Is.EqualTo(new EntryReference("localization", "shop", "PURCHASE").GetHashCode()));
            Assert.That(fromKey, Is.Not.EqualTo(new EntryReference("Localization", "Shop", "Title")));
            Assert.That(fromKey, Is.Not.EqualTo(new EntryReference("Other", "Shop", "Purchase")));
            Assert.That(default(EntryReference).IsEmpty, Is.True);
        }
    }
}
