using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class KeyResolverTests
    {
        private static readonly KeyResolver Resolver = new(new[]
        {
            ("Shop", TableDocument.Parse("@formerly BuyButton\n@formerly Shop.Checkout\nPurchase = Buy\nTitle = Shop\n")),
            ("Dialogue", TableDocument.Parse("@formerly Hud.Greeting\nWelcome = Hello\n"))
        });

        [Test]
        public void Resolve_FindsEntriesIgnoringCase()
        {
            KeyResolution resolution = Resolver.Resolve("shop", "PURCHASE");

            Assert.That(resolution.Kind, Is.EqualTo(KeyResolutionKind.Found));
            Assert.That(resolution.TableName, Is.EqualTo("Shop"));
            Assert.That(resolution.EntryName, Is.EqualTo("Purchase"));
            Assert.That(resolution.IsFound, Is.True);
        }

        [Test]
        public void Resolve_FindsRenamedAndMovedEntriesByTheirFormerKeys()
        {
            KeyResolution renamed = Resolver.Resolve("Shop", "BuyButton");
            KeyResolution qualified = Resolver.Resolve("Shop", "Checkout");
            KeyResolution moved = Resolver.Resolve("Hud", "Greeting");

            Assert.That(renamed.Kind, Is.EqualTo(KeyResolutionKind.Renamed));
            Assert.That(renamed.EntryName, Is.EqualTo("Purchase"));
            Assert.That(renamed.FormerName, Is.EqualTo("BuyButton"));
            Assert.That(qualified.Kind, Is.EqualTo(KeyResolutionKind.Renamed));
            Assert.That(qualified.FormerName, Is.EqualTo("Shop.Checkout"));
            Assert.That(moved.Kind, Is.EqualTo(KeyResolutionKind.Moved));
            Assert.That($"{moved.TableName}.{moved.EntryName}", Is.EqualTo("Dialogue.Welcome"));
            Assert.That(moved.FormerName, Is.EqualTo("Hud.Greeting"));
        }

        [Test]
        public void Resolve_SuggestsTheKeyAMissingOneMostLikelyMeant()
        {
            Assert.That(Resolver.Resolve("Shop", "Titel").Suggestion, Is.EqualTo("Shop.Title"));
            Assert.That(Resolver.Resolve("Shp", "Title").Suggestion, Is.EqualTo("Shop.Title"));
            Assert.That(Resolver.Resolve("Shop", "Inventory").Suggestion, Is.Null);
            Assert.That(Resolver.Resolve("Shop", "Not a key").Kind, Is.EqualTo(KeyResolutionKind.Invalid));
        }
    }
}
