using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Tables;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// A table source that compiles a catalog and its tables from text, and answers right away, or only when the
    /// test calls <see cref="DeliverPending"/> to simulate a platform that loads asynchronously.
    /// </summary>
    internal sealed class MemoryTableSource : ITableSource
    {
        private readonly Dictionary<(ulong Table, ulong Language), byte[]> _tables = new();
        private readonly List<(TableReceiver Receiver, byte[] Data)> _pending = new();
        private byte[] _catalog;

        public MemoryTableSource(string catalogName, string catalogText, params (string Table, string Language, string Text)[] tables)
        {
            CatalogKey = new CatalogKey(catalogName);
            List<TableInfo> tableInfos = new();
            List<string> tableNames = new();
            for (int i = 0; i < tables.Length; i++)
            {
                if (!tableNames.Contains(tables[i].Table))
                {
                    tableNames.Add(tables[i].Table);
                    tableInfos.Add(new TableInfo(new TableKey(tables[i].Table), TableLoading.Preload, TableDelivery.Embedded));
                }
            }
            CatalogInfo catalog = CatalogInfo.FromDocument(CatalogKey, CatalogDocument.Parse(catalogText), tableInfos, null);
            _catalog = CompiledCatalog.Write(catalog);
            for (int i = 0; i < tables.Length; i++)
            {
                SetTable(tables[i].Table, tables[i].Language, tables[i].Text);
            }
        }

        public CatalogKey CatalogKey { get; }

        /// <summary>When true, requests are held until <see cref="DeliverPending"/>.</summary>
        public bool IsDeferred { get; set; }

        public int RequestCount { get; private set; }

        public int PendingCount => _pending.Count;

        public void SetTable(string table, string language, string text)
        {
            TableKey tableKey = new(table);
            LanguageKey languageKey = new(language);
            _tables[(tableKey.Hash, languageKey.Hash)] = TableCompiler.Compile(CatalogKey, tableKey, languageKey, TableDocument.Parse(text), null);
        }

        public void SetRawTable(string table, string language, byte[] data)
        {
            _tables[(new TableKey(table).Hash, new LanguageKey(language).Hash)] = data;
        }

        public void RemoveCatalog()
        {
            _catalog = null;
        }

        public bool TryLoad(in TableRequest request, TableReceiver receiver)
        {
            byte[] data;
            if (request.IsCatalog)
            {
                if (_catalog == null || request.Catalog != CatalogKey)
                {
                    return false;
                }
                data = _catalog;
            }
            else if (!_tables.TryGetValue((request.Table.Hash, request.Language.Hash), out data))
            {
                return false;
            }
            RequestCount++;
            if (IsDeferred)
            {
                _pending.Add((receiver, data));
            }
            else
            {
                receiver.Receive(data);
            }
            return true;
        }

        /// <summary>Answers every held request, in the order they were made.</summary>
        public void DeliverPending()
        {
            (TableReceiver Receiver, byte[] Data)[] pending = _pending.ToArray();
            _pending.Clear();
            for (int i = 0; i < pending.Length; i++)
            {
                pending[i].Receiver.Receive(pending[i].Data);
            }
        }
    }
}
