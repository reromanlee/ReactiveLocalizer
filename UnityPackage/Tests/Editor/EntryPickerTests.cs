using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Editor;
using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>Tests what the Inspector's entry field and its picker list, suggest and store, on real files.</summary>
    public class EntryPickerTests
    {
        private const string Folder = "Assets/ReactiveLocalizerPickerTests";

        private IndexedCatalog _game;
        private IndexedCatalog _tools;

        [SetUp]
        public void CreateFiles()
        {
            KeysGenerator.IsSuspended = true;
            Directory.CreateDirectory($"{Folder}/Game");
            Directory.CreateDirectory($"{Folder}/Tools");
            File.WriteAllText($"{Folder}/Game/Game.catalog", "@source English\n\n[English]\nCulture = en\n");
            File.WriteAllText($"{Folder}/Game/Shop.English.lang", "@formerly BuyButton\nPurchase = Buy\nRefund = Return the purchase\nTitle = Shop\n");
            // Written by hand out of natural order, which the picker lists in order all the same.
            File.WriteAllText($"{Folder}/Game/Menu.English.lang", "Line10 = Ten\nLine9 = Nine\n");
            File.WriteAllText($"{Folder}/Tools/Tools.catalog", "@source English\n\n[English]\nCulture = en\n");
            File.WriteAllText($"{Folder}/Tools/Window.English.lang", "Title = Tools\n");
            CatalogLayout.Refresh();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CatalogIndex.Invalidate();
            _game = CatalogIndex.Find(new CatalogKey("Game"));
            _tools = CatalogIndex.Find(new CatalogKey("Tools"));
        }

        [TearDown]
        public void DeleteFiles()
        {
            AssetDatabase.DeleteAsset(Folder);
            CatalogLayout.Refresh();
            CatalogIndex.Invalidate();
            KeysGenerator.IsSuspended = false;
        }

        [Test]
        public void SearchItems_ListTheCatalogsEntriesInNaturalOrder()
        {
            List<string> names = new();
            foreach (EntrySearchItem item in _game.SearchItems)
            {
                names.Add(item.QualifiedName);
            }

            Assert.That(names, Is.EqualTo(new[] { "Menu.Line9", "Menu.Line10", "Shop.Purchase", "Shop.Refund", "Shop.Title" }));
        }

        [Test]
        public void List_WithoutAQuery_OffersNoneThenRecentPicksThenEverything()
        {
            EntryReference[] recent = { new("Game", "Shop", "Title"), new("Game", "Shop", "Gone"), new("Tools", "Window", "Title") };
            EntryPickerList list = new(new[] { _game }, recent);

            list.Search(string.Empty);

            Assert.That(list.Rows.Count, Is.EqualTo(9));
            Assert.That(list.Rows[0].Kind, Is.EqualTo(EntryPickerRowKind.None));
            Assert.That(list.Rows[1].Label, Is.EqualTo("Recent"));
            Assert.That(list.Rows[2].Item.QualifiedName, Is.EqualTo("Shop.Title"));
            Assert.That(list.Rows[3].Label, Is.EqualTo("All entries"));
            Assert.That(list.Step(2, 1), Is.EqualTo(4));
            Assert.That(list.Step(4, -1), Is.EqualTo(2));
            Assert.That(list.FindRow(new EntryReference("Game", "Shop", "Refund")), Is.EqualTo(7));
            Assert.That(list.FindRow(default), Is.EqualTo(0));
        }

        [Test]
        public void List_WithAQuery_ListsTheMatchesAcrossCatalogs()
        {
            EntryPickerList list = new(new[] { _game, _tools }, new EntryReference[0]);

            list.Search("title");

            Assert.That(list.IsNamingCatalogs, Is.True);
            Assert.That(list.Rows.Count, Is.EqualTo(2));
            Assert.That(list.Rows[0].Item.CatalogName, Is.EqualTo("Game"));
            Assert.That(list.Rows[1].Item.CatalogName, Is.EqualTo("Tools"));
            list.Search("nothing like this");
            Assert.That(list.Rows, Is.Empty);
        }

        [Test]
        public void Draft_SuggestsFromTheFieldAndTakesTheQuery()
        {
            EntryDraft draft = new(new[] { _game, _tools }, "Tools", null, "_title", "Hud");

            Assert.That(draft.Catalog, Is.SameAs(_tools));
            Assert.That(draft.TableName, Is.EqualTo("Hud"));
            Assert.That(draft.Key, Is.EqualTo("Title"));
            draft.TakeQuery("Buy now");
            Assert.That(draft.Text, Is.EqualTo("Buy now"));
            draft.TakeQuery("Shop.Price");
            Assert.That((draft.TableName, draft.Key, draft.Text), Is.EqualTo(("Shop", "Price", string.Empty)));
        }

        [Test]
        public void Draft_NamesItsKeyAfterTheGameObjectAndTheField()
        {
            GameObject sword = new("Sword");
            try
            {
                EntryDraft draft = new(new[] { _game }, null, sword, "_description", "Hud");

                Assert.That(draft.Catalog, Is.SameAs(_game));
                Assert.That(draft.Key, Is.EqualTo("SwordDescription"));
            }
            finally
            {
                Object.DestroyImmediate(sword);
            }
        }

        [Test]
        public void Draft_TellsWhatCreatingWouldDo()
        {
            EntryDraft draft = new(new[] { _game }, "Game", null, "_entry", "Shop") { Key = "Purchase" };

            Assert.That(draft.Describe(out bool canCreate), Does.Contain("'Shop.Purchase2'"));
            Assert.That(canCreate, Is.True);
            draft.Key = "BuyButton";
            Assert.That(draft.Describe(out _), Does.Contain("'Shop.BuyButton2'"));
            draft.TableName = "Hud";
            Assert.That(draft.Describe(out _), Does.StartWith("Creates the table 'Hud'"));
            draft.Key = "Bad Key";
            draft.Describe(out canCreate);
            Assert.That(canCreate, Is.False);
            draft.TableName = "shop";
            draft.MatchTableSpelling();
            Assert.That(draft.TableName, Is.EqualTo("Shop"));
        }

        [TestCase("Sword", "_description", "SwordDescription")]
        [TestCase("Title Label", "_entry", "Title")]
        [TestCase("Title Label", "_text", "Title")]
        [TestCase("Sword Title", "_titleText", "SwordTitle")]
        [TestCase("(1)", "_title", "Title")]
        [TestCase("(1)", "_entry", "Entry")]
        public void SuggestKey_AddsTheFieldsOwnRole(string gameObjectName, string fieldName, string expectedKey)
        {
            Assert.That(EntryAuthoring.SuggestKey(gameObjectName, fieldName), Is.EqualTo(expectedKey));
        }

        [Test]
        public void Field_ShowsTheEntryAndUpdatesAFormerName()
        {
            EntryHolder holder = ScriptableObject.CreateInstance<EntryHolder>();
            holder.Entry = new EntryReference("Game", "Shop", "BuyButton");
            try
            {
                using SerializedObject serializedObject = new(holder);
                SerializedProperty property = serializedObject.FindProperty(nameof(EntryHolder.Entry));
                EntryReferenceField field = new(property, "Entry", null, nameof(EntryHolder.Entry));

                Assert.That(field.value, Is.EqualTo(holder.Entry));
                Assert.That(EntryReferenceDrawer.GetDisplayName(field.value, false), Is.EqualTo("Shop.BuyButton"));
                EntryDescription description = EntryPreview.Describe(field.value);
                Assert.That(description.Kind, Is.EqualTo(EntryPreviewKind.Renamed));

                EntryReferenceDrawer.Assign(new Object[] { holder }, property.propertyPath, description.Fix);

                Assert.That(holder.Entry, Is.EqualTo(new EntryReference("Game", "Shop", "Purchase")));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void CreatingAnEntryForAField_IsOneUndoStep()
        {
            EntryHolder holder = ScriptableObject.CreateInstance<EntryHolder>();
            try
            {
                Assert.That(EntryAuthoring.TryCreateEntry("Game", "Shop", "Fresh", "Brand new", out EntryReference created, out string problem), Is.True, problem);
                EntryReferenceDrawer.Assign(new Object[] { holder }, nameof(EntryHolder.Entry), created);
                Assert.That(holder.Entry, Is.EqualTo(created));

                Undo.PerformUndo();

                Assert.That(holder.Entry.IsEmpty, Is.True);
                Assert.That(File.ReadAllText($"{Folder}/Game/Shop.English.lang"), Does.Not.Contain("Fresh"));
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }
    }
}
