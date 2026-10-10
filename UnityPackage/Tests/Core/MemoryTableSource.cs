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
        private readonly Dictionary<(ulong Table, ulong Language), string> _texts = new();
        private readonly List<(TableReceiver Receiver, byte[] Data)> _pending = new();
        private readonly bool _isImported;
        private readonly CatalogInfo _catalogInfo;
        private byte[] _catalog;

        /// <summary>Compiles every table on its own, without its catalog, so no translation is known to be complete.</summary>
        public MemoryTableSource(string catalogName, string catalogText, params (string Table, string Language, string Text)[] tables)
            : this(false, catalogName, catalogText, null, tables)
        {
        }

        private MemoryTableSource(bool isImported, string catalogName, string catalogText, TableLoading[] loadings,
            (string Table, string Language, string Text)[] tables)
        {
            _isImported = isImported;
            CatalogKey = new CatalogKey(catalogName);
            CatalogInfo withoutTables = CatalogInfo.FromDocument(CatalogKey, CatalogDocument.Parse(catalogText), null, null);
            for (int i = 0; i < tables.Length; i++)
            {
                _texts[(new TableKey(tables[i].Table).Hash, new LanguageKey(tables[i].Language).Hash)] = tables[i].Text;
            }
            List<TableInfo> tableInfos = new();
            List<string> tableNames = new();
            for (int i = 0; i < tables.Length; i++)
            {
                if (tableNames.Contains(tables[i].Table))
                {
                    continue;
                }
                TableKey key = new(tables[i].Table);
                TableLoading loading = loadings != null && tableNames.Count < loadings.Length ? loadings[tableNames.Count] : TableLoading.Preload;
                tableNames.Add(tables[i].Table);
                ulong keysHash = isImported && _texts.TryGetValue((key.Hash, withoutTables.SourceLanguage.Key.Hash), out string source)
                    ? TableCompiler.ComputeKeysHash(TableDocument.Parse(source))
                    : 0;
                tableInfos.Add(new TableInfo(key, loading, TableDelivery.Embedded, keysHash));
            }
            List<(string TableName, TableDocument Source)> sources = new();
            for (int i = 0; i < tableNames.Count; i++)
            {
                TableKey key = new(tableNames[i]);
                if (_texts.TryGetValue((key.Hash, withoutTables.SourceLanguage.Key.Hash), out string source))
                {
                    sources.Add((tableNames[i], TableDocument.Parse(source)));
                }
            }
            _catalogInfo = new CatalogInfo(CatalogKey, withoutTables.SourceLanguage.Key, withoutTables.Languages, tableInfos,
                isImported ? MovedEntries.Collect(sources) : null);
            _catalog = CompiledCatalog.Write(_catalogInfo);
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

        /// <summary>The requests made so far, as table and language names.</summary>
        public List<string> Requests { get; } = new();

        /// <summary>
        /// Compiles every table the way the importer does: with its catalog, and translations checked against their
        /// source text, so complete translations are marked complete. Tables take <paramref name="loadings"/> in the
        /// order they first appear, and Preload past its end.
        /// </summary>
        public static MemoryTableSource Imported(string catalogName, string catalogText, TableLoading[] loadings,
            params (string Table, string Language, string Text)[] tables)
        {
            return new MemoryTableSource(true, catalogName, catalogText, loadings, tables);
        }

        public void SetTable(string table, string language, string text)
        {
            TableKey tableKey = new(table);
            LanguageKey languageKey = new(language);
            _texts[(tableKey.Hash, languageKey.Hash)] = text;
            TableDocument document = TableDocument.Parse(text);
            if (!_isImported)
            {
                _tables[(tableKey.Hash, languageKey.Hash)] = TableCompiler.Compile(CatalogKey, tableKey, languageKey, document, null);
                return;
            }
            TableDocument source = _texts.TryGetValue((tableKey.Hash, _catalogInfo.SourceLanguage.Key.Hash), out string sourceText)
                ? TableDocument.Parse(sourceText)
                : null;
            _tables[(tableKey.Hash, languageKey.Hash)] = TableCompiler.Compile(_catalogInfo, tableKey, languageKey, document, source, null);
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
            Requests.Add(request.IsCatalog ? "Catalog" : $"{request.Table.Name}.{request.Language.Name}");
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

        /// <summary>Answers held requests, and the requests answering them makes, until none is left.</summary>
        public void DeliverAll()
        {
            while (_pending.Count > 0)
            {
                DeliverPending();
            }
        }
    }
}
