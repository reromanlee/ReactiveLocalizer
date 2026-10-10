using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// Loads tables for one localizer. A table is loaded along a fallback chain one language at a time, asking every
    /// source in priority order, and stops at the first table that has every key of the source language: a complete
    /// translation needs no fallback, and a mod that patches part of a language is searched before that language.
    /// </summary>
    /// <remarks>
    /// Host thread only. Loads of the same table in the same language are shared, so rapid language switches never
    /// load anything twice, and tables already loaded are reused until <see cref="Prune"/> drops them. Sources may
    /// answer within their call or later; either way each load advances exactly once per answer.
    /// </remarks>
    internal sealed class TableLoader
    {
        private readonly ILocalizerHost _host;
        private readonly Action<TableReceiver> _onReceived;
        private readonly Action<ReportSeverity, string> _report;
        private readonly Dictionary<(ulong Table, ulong Language), LanguageTables> _cache = new();
        private readonly Dictionary<(ulong Table, ulong Language), LanguageLoad> _inFlight = new();
        private readonly List<(ulong Table, ulong Language)> _removals = new();

        /// <param name="host">Provides the table sources.</param>
        /// <param name="onReceived">Takes every answer of a source, to bring it to the host thread and back to <see cref="OnReceived"/>.</param>
        /// <param name="report">Reports problems with what sources deliver.</param>
        public TableLoader(ILocalizerHost host, Action<TableReceiver> onReceived, Action<ReportSeverity, string> report)
        {
            _host = host;
            _onReceived = onReceived;
            _report = report;
        }

        /// <summary>The catalog tables are loaded for: its key, its source language and its tables' keys hashes.</summary>
        public CatalogInfo Catalog { get; set; }

        /// <summary>
        /// Starts loading <paramref name="table"/> along <paramref name="chain"/>. <paramref name="onCompleted"/> is
        /// called once, possibly before this returns, unless the load is cancelled first.
        /// </summary>
        public ChainLoad Load(TableInfo table, IReadOnlyList<LanguageInfo> chain, Action<ChainLoad> onCompleted, object owner)
        {
            ChainLoad load = new(table, chain, onCompleted, owner);
            Advance(load);
            return load;
        }

        /// <summary>Takes an answer of a source, on the host thread.</summary>
        public void OnReceived(TableReceiver receiver)
        {
            LanguageLoad load = (LanguageLoad)receiver.Context;
            load.IsAwaiting = false;
            if (!load.IsFinished)
            {
                Accept(load, receiver);
                Advance(load);
            }
        }

        /// <summary>Drops every loaded table <paramref name="shouldKeep"/> rejects, given its table hash and language hash.</summary>
        public void Prune(Func<ulong, ulong, bool> shouldKeep)
        {
            foreach ((ulong Table, ulong Language) key in _cache.Keys)
            {
                if (!shouldKeep(key.Table, key.Language))
                {
                    _removals.Add(key);
                }
            }
            for (int i = 0; i < _removals.Count; i++)
            {
                _cache.Remove(_removals[i]);
            }
            _removals.Clear();
        }

        /// <summary>Drops everything, and stops every load in progress from advancing.</summary>
        public void Clear()
        {
            foreach (LanguageLoad load in _inFlight.Values)
            {
                load.IsFinished = true;
            }
            _inFlight.Clear();
            _cache.Clear();
        }

        private void Advance(ChainLoad load)
        {
            if (load.IsAdvancing)
            {
                // A source answered within its call; the outer call carries on from where this answer left the load.
                return;
            }
            load.IsAdvancing = true;
            try
            {
                while (!load.IsAwaiting && !load.IsDone && !load.IsCancelled)
                {
                    if (load.LanguageIndex >= load.Chain.Count)
                    {
                        Complete(load);
                        break;
                    }
                    (ulong Table, ulong Language) key = (load.Table.Key.Hash, load.Chain[load.LanguageIndex].Key.Hash);
                    if (_cache.TryGetValue(key, out LanguageTables loaded))
                    {
                        Append(load, loaded);
                        continue;
                    }
                    load.IsAwaiting = true;
                    if (_inFlight.TryGetValue(key, out LanguageLoad shared))
                    {
                        shared.Waiting.Add(load);
                        break;
                    }
                    LanguageLoad languageLoad = new(TableRequest.ForTable(Catalog.Key, load.Table, load.Chain[load.LanguageIndex].Key),
                        load.Table, load.Chain[load.LanguageIndex] == Catalog.SourceLanguage);
                    languageLoad.Waiting.Add(load);
                    _inFlight.Add(key, languageLoad);
                    Advance(languageLoad);
                }
            }
            finally
            {
                load.IsAdvancing = false;
            }
        }

        private void Advance(LanguageLoad load)
        {
            if (load.IsAdvancing)
            {
                return;
            }
            load.IsAdvancing = true;
            try
            {
                IReadOnlyList<ITableSource> sources = _host.TableSources;
                while (!load.IsAwaiting && !load.IsFinished)
                {
                    int count = sources?.Count ?? 0;
                    if (load.IsComplete || load.NextSource >= count || !load.HasLiveWaiter())
                    {
                        Finish(load);
                        break;
                    }
                    ITableSource source = sources[load.NextSource];
                    load.NextSource++;
                    if (source == null)
                    {
                        continue;
                    }
                    TableReceiver receiver = new(load.Request, _onReceived, load);
                    load.IsAwaiting = true;
                    if (TryStart(source, load.Request, receiver))
                    {
                        load.HasAnySource = true;
                    }
                    else
                    {
                        load.IsAwaiting = false;
                    }
                }
            }
            finally
            {
                load.IsAdvancing = false;
            }
        }

        private bool TryStart(ITableSource source, in TableRequest request, TableReceiver receiver)
        {
            try
            {
                // A source that answered while saying it doesn't have the table still answered.
                return source.TryLoad(in request, receiver) || receiver.IsCompleted;
            }
            catch (Exception exception)
            {
                _report(ReportSeverity.Error, $"The table source {source.GetType().Name} threw while loading {request}: {exception}");
                // A source that answered before throwing still answered; the next source must not answer again.
                return receiver.IsCompleted;
            }
        }

        /// <summary>Checks a delivered table and adds it to the load, reporting whatever is wrong with it.</summary>
        private void Accept(LanguageLoad load, TableReceiver receiver)
        {
            TableRequest request = receiver.Request;
            ReportSeverity severity = load.IsSource ? ReportSeverity.Error : ReportSeverity.Warning;
            if (!receiver.HasData)
            {
                _report(severity, $"Couldn't load {request}: {receiver.FailureReason}");
                return;
            }
            if (!CompiledTable.TryRead(receiver.Data.Span, out CompiledTable table, out string error))
            {
                _report(severity, $"Couldn't read {request}: {error}");
                return;
            }
            if (table.CatalogHash != request.Catalog.Hash || table.TableHash != request.Table.Hash || table.LanguageHash != request.Language.Hash)
            {
                _report(ReportSeverity.Error, $"The data delivered for {request} was compiled for another catalog, table or language; reimport or rebuild it.");
                return;
            }
            load.Tables.Add(table);
            load.IsComplete = table.IsComplete(load.Table.KeysHash);
        }

        private void Finish(LanguageLoad load)
        {
            load.IsFinished = true;
            (ulong Table, ulong Language) key = (load.Request.Table.Hash, load.Request.Language.Hash);
            _inFlight.Remove(key);
            if (!load.HasLiveWaiter())
            {
                return;
            }
            if (load.IsSource && !load.HasAnySource)
            {
                _report(ReportSeverity.Error, $"No table source has {load.Request}, the language every other language falls back to.");
            }
            LanguageTables result = load.Tables.Count == 0 && !load.IsComplete
                ? LanguageTables.None
                : new LanguageTables(load.Tables.ToArray(), load.IsComplete);
            _cache[key] = result;
            for (int i = 0; i < load.Waiting.Count; i++)
            {
                ChainLoad waiter = load.Waiting[i];
                if (waiter.IsCancelled)
                {
                    continue;
                }
                waiter.IsAwaiting = false;
                Append(waiter, result);
                Advance(waiter);
            }
            load.Waiting.Clear();
        }

        private static void Append(ChainLoad load, LanguageTables tables)
        {
            for (int i = 0; i < tables.Tables.Length; i++)
            {
                load.Tables.Add(tables.Tables[i]);
                load.LanguageIndexes.Add(load.LanguageIndex);
            }
            load.LanguageIndex++;
            if (tables.IsComplete)
            {
                Complete(load);
            }
        }

        private static void Complete(ChainLoad load)
        {
            if (load.IsDone)
            {
                return;
            }
            load.IsDone = true;
            load.Result = new TableLayers(load.Tables.ToArray(), load.LanguageIndexes.ToArray(), load.LanguageIndex);
            load.OnCompleted(load);
        }

        /// <summary>The tables found for one table in one language: one per source that has it, in priority order.</summary>
        private sealed class LanguageTables
        {
            public static readonly LanguageTables None = new(Array.Empty<CompiledTable>(), false);

            public LanguageTables(CompiledTable[] tables, bool isComplete)
            {
                Tables = tables;
                IsComplete = isComplete;
            }

            public CompiledTable[] Tables { get; }

            /// <summary>Whether one of the tables has every key of the source language, so no fallback is needed.</summary>
            public bool IsComplete { get; }
        }

        /// <summary>One table in one language while its sources are asked, shared by every chain that needs it.</summary>
        private sealed class LanguageLoad
        {
            public LanguageLoad(TableRequest request, TableInfo table, bool isSource)
            {
                Request = request;
                Table = table;
                IsSource = isSource;
            }

            public TableRequest Request { get; }

            public TableInfo Table { get; }

            /// <summary>Whether this is the source language, whose absence is an error rather than a gap.</summary>
            public bool IsSource { get; }

            public List<CompiledTable> Tables { get; } = new(1);

            public List<ChainLoad> Waiting { get; } = new(1);

            public int NextSource { get; set; }

            public bool IsAwaiting { get; set; }

            public bool IsAdvancing { get; set; }

            public bool IsComplete { get; set; }

            public bool IsFinished { get; set; }

            /// <summary>Whether any source took the request.</summary>
            public bool HasAnySource { get; set; }

            public bool HasLiveWaiter()
            {
                for (int i = 0; i < Waiting.Count; i++)
                {
                    if (!Waiting[i].IsCancelled)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
    }

    /// <summary>One table loading along one fallback chain, for a language switch or for an on-demand table.</summary>
    internal sealed class ChainLoad
    {
        public ChainLoad(TableInfo table, IReadOnlyList<LanguageInfo> chain, Action<ChainLoad> onCompleted, object owner)
        {
            Table = table;
            Chain = chain;
            OnCompleted = onCompleted;
            Owner = owner;
        }

        public TableInfo Table { get; }

        public IReadOnlyList<LanguageInfo> Chain { get; }

        /// <summary>What the load is for, such as the language switch that started it.</summary>
        public object Owner { get; }

        /// <summary>The tables to search once the load is done.</summary>
        public TableLayers Result { get; set; }

        public bool IsDone { get; set; }

        public bool IsCancelled { get; private set; }

        internal Action<ChainLoad> OnCompleted { get; }

        internal List<CompiledTable> Tables { get; } = new(2);

        internal List<int> LanguageIndexes { get; } = new(2);

        internal int LanguageIndex { get; set; }

        internal bool IsAwaiting { get; set; }

        internal bool IsAdvancing { get; set; }

        /// <summary>Stops the load: it never completes, and what it was waiting for is dropped unless another load needs it.</summary>
        public void Cancel()
        {
            IsCancelled = true;
        }
    }
}
