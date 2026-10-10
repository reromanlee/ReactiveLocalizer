using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class EntrySearchTests
    {
        private static readonly EntrySearchItem[] Items =
        {
            new("Game", "Shop", "Purchase", "Buy"),
            new("Game", "Shop", "PurchaseFailed", "Couldn't buy it"),
            new("Game", "Shop", "Refund", "Return the purchase"),
            new("Game", "Menu", "Line10", "Ten"),
            new("Game", "Menu", "Line9", "Nine"),
            new("Game", "Menu", "Buy", "Purchase now")
        };

        private static List<string> Find(string query, int limit = 10)
        {
            List<EntrySearchItem> results = new();
            EntrySearch.Find(Items, query, results, limit);
            return results.ConvertAll(item => item.QualifiedName);
        }

        [Test]
        public void Find_RanksKeysBeforeTexts()
        {
            Assert.That(Find("purchase"), Is.EqualTo(new[] { "Shop.Purchase", "Shop.PurchaseFailed", "Menu.Buy", "Shop.Refund" }));
        }

        [Test]
        public void Find_MatchesEveryWordAndQualifiedNames()
        {
            Assert.That(Find("shop buy"), Is.EqualTo(new[] { "Shop.Purchase", "Shop.PurchaseFailed" }));
            Assert.That(Find("Menu.Line"), Is.EqualTo(new[] { "Menu.Line9", "Menu.Line10" }));
        }

        [Test]
        public void Find_WithoutAQuery_ListsEverythingInNaturalOrderUpToTheLimit()
        {
            Assert.That(Find(string.Empty, 3), Is.EqualTo(new[] { "Menu.Buy", "Menu.Line9", "Menu.Line10" }));
            Assert.That(Find("nothing like this"), Is.Empty);
        }
    }
}
