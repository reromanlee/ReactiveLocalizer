using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class EntrySearchTests
    {
        private static readonly List<EntrySearchItem> Items = CreateItems();

        private static List<EntrySearchItem> CreateItems()
        {
            List<EntrySearchItem> items = new()
            {
                new("Game", "Shop", "Purchase", "Buy"),
                new("Game", "Shop", "PurchaseFailed", "Couldn't buy it"),
                new("Game", "Shop", "Refund", "Return the purchase"),
                new("Game", "Menu", "Line10", "Ten"),
                new("Game", "Menu", "Line9", "Nine"),
                new("Game", "Menu", "Buy", "Purchase now")
            };
            EntrySearch.Sort(items);
            return items;
        }

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

        [Test]
        public void Find_UpToTheLimit_KeepsTheBestMatches()
        {
            Assert.That(Find("purchase", 2), Is.EqualTo(new[] { "Shop.Purchase", "Shop.PurchaseFailed" }));
            Assert.That(Find("line", 1), Is.EqualTo(new[] { "Menu.Line9" }));
        }

        [Test]
        public void Sort_LeavesItemsInOrderAsTheyAre()
        {
            List<EntrySearchItem> sorted = new(Items);

            EntrySearch.Sort(sorted);

            Assert.That(sorted, Is.EqualTo(Items));
            Assert.That(Items.ConvertAll(item => item.QualifiedName), Is.EqualTo(new[] { "Menu.Buy", "Menu.Line9", "Menu.Line10", "Shop.Purchase", "Shop.PurchaseFailed", "Shop.Refund" }));
        }
    }
}
