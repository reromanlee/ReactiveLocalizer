using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Tables;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>A table source compiling a catalog and its tables from text, for tests that run inside Unity.</summary>
    internal sealed class TextTableSource : ITableSource
    {
        private readonly Dictionary<(ulong Table, ulong Language), byte[]> _tables = new();
        private readonly byte[] _catalog;

        public TextTableSource(string catalogText, params (string Table, string Language, string Text)[] tables)
        {
            CatalogKey = new CatalogKey("UnityTests");
            List<TableInfo> tableInfos = new();
            for (int i = 0; i < tables.Length; i++)
            {
                TableKey table = new(tables[i].Table);
                LanguageKey language = new(tables[i].Language);
                if (!tableInfos.Exists(info => info.Key == table))
                {
                    tableInfos.Add(new TableInfo(table, TableLoading.Preload, TableDelivery.Embedded));
                }
                _tables[(table.Hash, language.Hash)] = TableCompiler.Compile(CatalogKey, table, language, TableDocument.Parse(tables[i].Text), null);
            }
            _catalog = CompiledCatalog.Write(CatalogInfo.FromDocument(CatalogKey, CatalogDocument.Parse(catalogText), tableInfos, null));
        }

        public CatalogKey CatalogKey { get; }

        public bool TryLoad(in TableRequest request, TableReceiver receiver)
        {
            if (request.IsCatalog)
            {
                if (request.Catalog != CatalogKey)
                {
                    return false;
                }
                receiver.Receive(_catalog);
                return true;
            }
            if (!_tables.TryGetValue((request.Table.Hash, request.Language.Hash), out byte[] data))
            {
                return false;
            }
            receiver.Receive(data);
            return true;
        }
    }
}
