using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TableFileSetTests
    {
        private static TableFileSet CreateShop()
        {
            TableFileSet shop = new("Shop", "English");
            shop.Set("English", TableFile.Read("@formerly BuyButton\nPurchase = Buy\nTitle = Shop\n"));
            shop.Set("Russian", TableFile.Read("Purchase [9a1248] = Kupit\nTitle = Magazin\nRefund = Vozvrat\n"));
            return shop;
        }

        [Test]
        public void GetState_ComparesEveryLanguageWithItsSourceText()
        {
            TableFileSet shop = CreateShop();

            Assert.That(shop.GetState("English", "Purchase"), Is.EqualTo(TranslationState.Current));
            Assert.That(shop.GetState("Russian", "Purchase"), Is.EqualTo(TranslationState.Current));
            Assert.That(shop.GetState("Russian", "Title"), Is.EqualTo(TranslationState.Unverified));
            Assert.That(shop.GetState("Russian", "Refund"), Is.EqualTo(TranslationState.Orphan));
            Assert.That(shop.GetState("Pirate", "Purchase"), Is.EqualTo(TranslationState.Missing));
        }

        [Test]
        public void TrySetText_OfTheSource_LeavesTranslationsOutdated()
        {
            TableFileSet shop = CreateShop();

            Assert.That(shop.TrySetText("English", "Purchase", "Buy now", out string problem), Is.True, problem);

            Assert.That(shop.GetState("Russian", "Purchase"), Is.EqualTo(TranslationState.Outdated));
            Assert.That(shop.TryMarkCurrent("Russian", "Purchase", out problem), Is.True, problem);
            Assert.That(shop.GetState("Russian", "Purchase"), Is.EqualTo(TranslationState.Current));
        }

        [Test]
        public void TrySetText_OfATranslation_StampsItAndCreatesItsFile()
        {
            TableFileSet shop = CreateShop();

            Assert.That(shop.TrySetText("Pirate", "Title", "Ship's Store", out string problem), Is.True, problem);

            Assert.That(shop.GetState("Pirate", "Title"), Is.EqualTo(TranslationState.Current));
            Assert.That(shop.Get("Pirate").Write(), Is.EqualTo("Title [fe5ea4] = Ship's Store\n".Replace("fe5ea4", Hashing.ComputeFingerprint("Shop").ToString("x6"))));
            Assert.That(shop.TrySetText("Pirate", "Missing", "Text", out problem), Is.False);
            Assert.That(problem, Does.Contain("no entry 'Missing'"));
        }

        [Test]
        public void TryRename_RenamesEveryLanguageAndLeavesAnAlias()
        {
            TableFileSet shop = CreateShop();

            Assert.That(shop.TryRename("Purchase", "Confirm", out string problem), Is.True, problem);

            Assert.That(shop.Source.Write(), Is.EqualTo("@formerly BuyButton\n@formerly Purchase\nConfirm = Buy\n\nTitle = Shop\n"));
            Assert.That(shop.GetState("Russian", "Confirm"), Is.EqualTo(TranslationState.Current));
            Assert.That(shop.TryRename("Confirm", "Purchase", out problem), Is.True, problem);
            Assert.That(shop.Source.Write(), Is.EqualTo("@formerly BuyButton\n@formerly Confirm\nPurchase = Buy\n\nTitle = Shop\n"));
        }

        [Test]
        public void TryRename_RefusesKeysTakenByEntriesAliasesOrOrphans()
        {
            TableFileSet shop = CreateShop();

            Assert.That(shop.TryRename("Title", "purchase", out string entry), Is.False);
            Assert.That(shop.TryRename("Title", "BuyButton", out string alias), Is.False);
            Assert.That(shop.TryRename("Title", "Refund", out string orphan), Is.False);
            Assert.That(shop.TryRename("Title", "Not a key", out string invalid), Is.False);

            Assert.That(entry, Does.Contain("as an entry"));
            Assert.That(alias, Does.Contain("former name of 'Purchase'"));
            Assert.That(orphan, Does.Contain("Russian"));
            Assert.That(invalid, Does.Contain("can't be a key"));
            Assert.That(shop.TryRename("Title", "TITLE", out string caseOnly), Is.True, caseOnly);
            Assert.That(shop.Source.Write(), Does.Not.Contain("@formerly Title"));
        }

        [Test]
        public void TryDelete_RemovesTheEntryFromEveryLanguage()
        {
            TableFileSet shop = CreateShop();

            Assert.That(shop.TryDelete("Title", out string problem), Is.True, problem);

            Assert.That(shop.Source.IndexOf("Title"), Is.EqualTo(-1));
            Assert.That(shop.Get("Russian").IndexOf("Title"), Is.EqualTo(-1));
        }

        [Test]
        public void TryMoveTo_CarriesEveryLanguageAndLeavesQualifiedAliases()
        {
            TableFileSet shop = CreateShop();
            TableFileSet store = new("Store", "English");
            store.Set("English", TableFile.Read("Greeting = Welcome\n"));

            Assert.That(shop.TryMoveTo("Purchase", store, out string problem), Is.True, problem);

            Assert.That(shop.Source.IndexOf("Purchase"), Is.EqualTo(-1));
            Assert.That(shop.Get("Russian").IndexOf("Purchase"), Is.EqualTo(-1));
            Assert.That(store.Source.Write(), Is.EqualTo("Greeting = Welcome\n\n@formerly Shop.BuyButton\n@formerly Shop.Purchase\nPurchase = Buy\n"));
            Assert.That(store.GetState("Russian", "Purchase"), Is.EqualTo(TranslationState.Current));
        }

        [Test]
        public void TryMoveTo_BackToTheFormerTable_TurnsItsAliasesPlainAgain()
        {
            TableFileSet shop = CreateShop();
            TableFileSet store = new("Store", "English");
            shop.TryMoveTo("Purchase", store, out _);

            Assert.That(store.TryMoveTo("Purchase", shop, out string problem), Is.True, problem);

            Assert.That(shop.Source.Write(), Does.StartWith("@formerly BuyButton\n@formerly Store.Purchase\nPurchase = Buy\n"));
        }

        [Test]
        public void Operations_RefuseFilesWithErrorsAndChangeNothing()
        {
            TableFileSet shop = CreateShop();
            shop.Set("Russian", TableFile.Read("Purchase = Kupit\nBroken line\n"));
            string english = shop.Source.Write();

            Assert.That(shop.TryRename("Purchase", "Confirm", out string problem), Is.False);
            Assert.That(problem, Does.Contain("Russian file of 'Shop' has errors"));
            Assert.That(shop.Source.Write(), Is.EqualTo(english));
            Assert.That(shop.TrySetText("English", "Title", "Store", out problem), Is.True, problem);
        }

        [Test]
        public void TrySetContextAndMaximumLength_WriteTheSourceAttributes()
        {
            TableFileSet shop = CreateShop();

            Assert.That(shop.TrySetContext("Title", new[] { "The heading.\nShort." }, out string problem), Is.True, problem);
            Assert.That(shop.TrySetMaximumLength("Title", 12, out problem), Is.True, problem);
            Assert.That(shop.TrySetMaximumLength("Title", 0, out _), Is.False);

            Assert.That(shop.Source.Write(), Does.EndWith("# The heading.\n# Short.\n@maximumLength 12\nTitle = Shop\n"));
        }
    }
}
