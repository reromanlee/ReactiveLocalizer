using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class CompiledTableTests
    {
        private static readonly CatalogKey Catalog = new("Localization");
        private static readonly TableKey Shop = new("Shop");
        private static readonly LanguageKey English = new("English");

        [Test]
        public void Compile_ThenRead_FindsEveryEntryByItsHash()
        {
            CompiledTable table = CompileAndRead("Purchase = Buy\nTitle = Shop\nSuffix =\nDisclaimer = Prices include VAT.\\nRefunds within 14 days.");

            Assert.That(table.EntryCount, Is.EqualTo(4));
            Assert.That(Find(table, "Purchase"), Is.EqualTo("Buy"));
            Assert.That(Find(table, "title"), Is.EqualTo("Shop"));
            Assert.That(Find(table, "Suffix"), Is.Empty);
            Assert.That(Find(table, "Disclaimer"), Is.EqualTo("Prices include VAT.\nRefunds within 14 days."));
            Assert.That(table.TryFind(Hashing.ComputeNameHash("Missing"), out _), Is.False);
        }

        [Test]
        public void Read_KeepsTheHashesOfCatalogTableAndLanguage()
        {
            CompiledTable table = CompileAndRead("Purchase = Buy");

            Assert.That(table.CatalogHash, Is.EqualTo(Catalog.Hash));
            Assert.That(table.TableHash, Is.EqualTo(Shop.Hash));
            Assert.That(table.LanguageHash, Is.EqualTo(English.Hash));
        }

        [Test]
        public void GetString_ReturnsTheSameInstanceEveryTime()
        {
            CompiledTable table = CompileAndRead("Purchase = Buy");
            table.TryFind(Hashing.ComputeNameHash("Purchase"), out int index);

            Assert.That(table.GetString(index), Is.SameAs(table.GetString(index)));
        }

        [Test]
        public void GetMemory_ReadsTheTextWithoutAString()
        {
            CompiledTable table = CompileAndRead("Purchase = Buy\nTitle = Shop");
            table.TryFind(Hashing.ComputeNameHash("Title"), out int index);

            Assert.That(table.GetMemory(index).ToString(), Is.EqualTo("Shop"));
        }

        [Test]
        public void Compile_StoresIdenticalTextsOnce()
        {
            byte[] shared = Compile("Confirm = OK\nDismiss = OK\nAccept = OK", new List<DocumentIssue>());
            byte[] distinct = Compile("Confirm = OK\nDismiss = No\nAccept = Go", new List<DocumentIssue>());

            Assert.That(shared.Length, Is.EqualTo(distinct.Length - 8));
        }

        [Test]
        public void Aliases_FindTheEntryTheyPointTo()
        {
            CompiledTable table = CompileAndRead("@formerly BuyButton\n@formerly Buy\nPurchase = Get it");

            Assert.That(Find(table, "BuyButton"), Is.EqualTo("Get it"));
            Assert.That(Find(table, "buy"), Is.EqualTo("Get it"));
            Assert.That(table.EntryCount, Is.EqualTo(1));
        }

        [Test]
        public void Compile_RejectsAliasesThatReuseANameOfTheTable()
        {
            List<DocumentIssue> issues = new();
            byte[] data = Compile("Title = Shop\n@formerly Title\nPurchase = Buy\n@formerly Not.A.Name\nRefund = Return", issues);

            Assert.That(issues.Count, Is.EqualTo(2));
            Assert.That(issues[0].Severity, Is.EqualTo(IssueSeverity.Error));
            Assert.That(CompiledTable.TryRead(data, out CompiledTable table, out _), Is.True);
            Assert.That(Find(table, "Title"), Is.EqualTo("Shop"));
        }

        [Test]
        public void TryRead_RejectsDataCutShortAtAnyPoint()
        {
            byte[] data = Compile("@formerly Old\nPurchase = Buy\nTitle = Shop", new List<DocumentIssue>());
            for (int length = 0; length < data.Length; length++)
            {
                bool isRead = CompiledTable.TryRead(data.AsSpan(0, length), out CompiledTable table, out string error);

                Assert.That(isRead, Is.False, $"Read {length} of {data.Length} bytes.");
                Assert.That(table, Is.Null);
                Assert.That(error, Is.Not.Empty);
            }
        }

        [Test]
        public void TryRead_RejectsAnotherFormatOrVersion()
        {
            byte[] data = Compile("Purchase = Buy", new List<DocumentIssue>());
            byte[] wrongMagic = (byte[])data.Clone();
            wrongMagic[0] = (byte)'X';
            byte[] wrongVersion = (byte[])data.Clone();
            wrongVersion[4] = 99;

            Assert.That(CompiledTable.TryRead(wrongMagic, out _, out _), Is.False);
            Assert.That(CompiledTable.TryRead(wrongVersion, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("99"));
        }

        [Test]
        public void TryRead_RejectsHashesOutOfOrder()
        {
            byte[] data = Compile("Purchase = Buy\nTitle = Shop", new List<DocumentIssue>());
            // Swap the two eight-byte key hashes that follow the 44-byte header.
            byte[] swapped = (byte[])data.Clone();
            Array.Copy(data, 44, swapped, 52, 8);
            Array.Copy(data, 52, swapped, 44, 8);

            Assert.That(CompiledTable.TryRead(swapped, out _, out _), Is.False);
        }

        [Test]
        public void Compile_ReadsAnEmptyDocumentAsAnEmptyTable()
        {
            CompiledTable table = CompileAndRead(string.Empty);

            Assert.That(table.EntryCount, Is.Zero);
            Assert.That(table.TryFind(Hashing.ComputeNameHash("Anything"), out _), Is.False);
        }

        private static string Find(CompiledTable table, string key)
        {
            Assert.That(table.TryFind(Hashing.ComputeNameHash(key), out int index), Is.True, key);
            return table.GetString(index);
        }

        private static byte[] Compile(string text, List<DocumentIssue> issues)
        {
            return TableCompiler.Compile(Catalog, Shop, English, TableDocument.Parse(text), issues);
        }

        private static CompiledTable CompileAndRead(string text)
        {
            List<DocumentIssue> issues = new();
            byte[] data = Compile(text, issues);
            Assert.That(issues, Is.Empty);
            Assert.That(CompiledTable.TryRead(data, out CompiledTable table, out string error), Is.True, error);
            return table;
        }
    }
}
