using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Tables;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections;
using System.IO;
using UnityEngine.TestTools;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class StreamingTableSourceTests
    {
        private static readonly CatalogKey Catalog = new("UnityTests");
        private static readonly TableKey Shop = new("Shop");
        private static readonly LanguageKey Russian = new("Russian");

        private string _folder;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ReactiveLocalizerStreaming", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        private static TableRequest RequestOf(TableDelivery delivery) =>
            TableRequest.ForTable(Catalog, new TableInfo(Shop, TableLoading.Preload, delivery), Russian);

        private byte[] WritePackedTable()
        {
            byte[] data = TableCompiler.Compile(Catalog, Shop, Russian, TableDocument.Parse("Purchase = Kupit"), null);
            string path = Path.Combine(_folder, StreamingTableSource.GetTablePath(Catalog.Name, Russian.Name, Shop.Name));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
            return data;
        }

        private static IEnumerator WaitFor(TableReceiver receiver)
        {
            for (int frame = 0; frame < 300 && !receiver.IsCompleted; frame++)
            {
                yield return null;
            }
            Assert.That(receiver.IsCompleted, Is.True, "The source never answered.");
        }

        [Test]
        public void OtherDeliveriesAndCatalogs_AreLeftToOtherSources()
        {
            StreamingTableSource source = new(_folder);

            Assert.That(source.TryLoad(RequestOf(TableDelivery.Embedded), new TableReceiver(RequestOf(TableDelivery.Embedded), _ => { }, null)), Is.False);
            Assert.That(source.TryLoad(TableRequest.ForCatalog(Catalog), new TableReceiver(TableRequest.ForCatalog(Catalog), _ => { }, null)), Is.False);
        }

        [UnityTest]
        public IEnumerator FromAFolder_ReadsThePackedTable()
        {
            byte[] data = WritePackedTable();
            TableReceiver receiver = new(RequestOf(TableDelivery.Streaming), _ => { }, null);

            Assert.That(new StreamingTableSource(_folder).TryLoad(RequestOf(TableDelivery.Streaming), receiver), Is.True);
            yield return WaitFor(receiver);

            Assert.That(receiver.HasData, Is.True, receiver.FailureReason);
            Assert.That(receiver.Data.ToArray(), Is.EqualTo(data));
        }

        [UnityTest]
        public IEnumerator FromAUrl_DownloadsThePackedTable()
        {
            byte[] data = WritePackedTable();
            TableReceiver receiver = new(RequestOf(TableDelivery.Streaming), _ => { }, null);

            Assert.That(new StreamingTableSource(new Uri(_folder).AbsoluteUri).TryLoad(RequestOf(TableDelivery.Streaming), receiver), Is.True);
            yield return WaitFor(receiver);

            Assert.That(receiver.HasData, Is.True, receiver.FailureReason);
            Assert.That(receiver.Data.ToArray(), Is.EqualTo(data));
        }

        [UnityTest]
        public IEnumerator AMissingFile_FailsWithItsPath()
        {
            TableReceiver receiver = new(RequestOf(TableDelivery.Streaming), _ => { }, null);

            new StreamingTableSource(_folder).TryLoad(RequestOf(TableDelivery.Streaming), receiver);
            yield return WaitFor(receiver);

            Assert.That(receiver.HasData, Is.False);
            Assert.That(receiver.FailureReason, Does.Contain("Shop.bytes"));
        }
    }
}
