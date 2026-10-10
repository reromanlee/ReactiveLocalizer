using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Internal;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer
{
    // How the localizer coordinates what it loads, on the host thread: initialization, language switches, tables held
    // on demand and languages registered at runtime.
    public sealed partial class Localizer
    {
        // Host thread work.

        /// <summary>
        /// Runs the work queued from other threads, in the order it was queued, then unloads the tables nothing holds
        /// anymore. Called by the host on its thread.
        /// </summary>
        private void Update()
        {
            Volatile.Write(ref _isUpdateScheduled, 0);
            RunHostActions();
            UnloadReleasedTables();
        }

        private void RunHostActions()
        {
            while (_hostActions.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    Report(ReportSeverity.Error, $"Localizer work for the catalog '{CatalogKey.Name}' failed: {exception}");
                }
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> on the host thread: right away when already there, unless earlier work is
        /// still queued, which then runs first so requests apply in the order they were made.
        /// </summary>
        private void RunOnHost(Action action)
        {
            if (_host.IsHostThread)
            {
                if (_hostActions.IsEmpty)
                {
                    action();
                    return;
                }
                _hostActions.Enqueue(action);
                RunHostActions();
                return;
            }
            _hostActions.Enqueue(action);
            RequestUpdate();
        }

        private void RequestUpdate()
        {
            if (Interlocked.CompareExchange(ref _isUpdateScheduled, 1, 0) != 0)
            {
                return;
            }
            try
            {
                _host.ScheduleUpdate(_update);
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _isUpdateScheduled, 0);
                Report(ReportSeverity.Error, $"The host couldn't schedule the localizer's update: {exception}");
            }
        }

        private void BeginInitialize()
        {
            if (IsDisposed)
            {
                return;
            }
            TableRequest request = TableRequest.ForCatalog(CatalogKey);
            if (!TryLoadCatalog(in request, new TableReceiver(request, _onReceived, null)))
            {
                Report(ReportSeverity.Error, $"No table source has {request}. In Unity, check that the catalog exists and that the build includes it.");
                FailInitialization();
            }
        }

        private void RequestLanguage(LanguageKey key, TaskCompletionSource<bool> completion)
        {
            if (IsDisposed)
            {
                completion.TrySetResult(false);
                return;
            }
            // Before the catalog arrives, a request only chooses the language initialization starts in.
            if (_catalog == null)
            {
                _startingLanguage = key;
                _initialWaiters.Add(completion);
                return;
            }
            if (!_catalog.TryGetLanguage(key, out LanguageInfo target))
            {
                Report(ReportSeverity.Warning, $"'{key.Name}' is not a language of the catalog '{CatalogKey.Name}', so the language stays {CurrentLanguage?.Name ?? "unchanged"}.");
                completion.TrySetResult(false);
                return;
            }
            if (_pendingSwitch != null && _pendingSwitch.Target == target)
            {
                _pendingSwitch.Waiters.Add(completion);
                return;
            }
            if (_pendingSwitch == null && _state.Language == target)
            {
                completion.TrySetResult(true);
                return;
            }
            StartSwitch(target, completion);
        }

        private void OnReceived(TableReceiver receiver)
        {
            if (_host.IsHostThread)
            {
                ProcessReceived(receiver);
                return;
            }
            _hostActions.Enqueue(() => ProcessReceived(receiver));
            RequestUpdate();
        }

        private void ProcessReceived(TableReceiver receiver)
        {
            if (IsDisposed)
            {
                return;
            }
            IReadOnlyList<string> warnings = receiver.TakeWarnings();
            for (int i = 0; i < warnings.Count; i++)
            {
                Report(ReportSeverity.Warning, $"While loading {receiver.Request}: {warnings[i]}");
            }
            if (receiver.Request.IsCatalog)
            {
                ProcessCatalog(receiver);
                return;
            }
            _loader.OnReceived(receiver);
        }

        private void ProcessCatalog(TableReceiver receiver)
        {
            if (!receiver.HasData)
            {
                Report(ReportSeverity.Error, $"Couldn't load {receiver.Request}: {receiver.FailureReason}");
                FailInitialization();
                return;
            }
            if (!CompiledCatalog.TryRead(receiver.Data.Span, out CatalogInfo catalog, out string error))
            {
                Report(ReportSeverity.Error, $"Couldn't read {receiver.Request}: {error}");
                FailInitialization();
                return;
            }
            if (catalog.Key != CatalogKey)
            {
                Report(ReportSeverity.Error, $"A table source delivered the catalog '{catalog.Key.Name}' when asked for '{CatalogKey.Name}'.");
                FailInitialization();
                return;
            }
            for (int i = 0; i < _pendingLanguages.Count; i++)
            {
                TryAddLanguage(catalog, _pendingLanguages[i], out catalog);
            }
            _pendingLanguages.Clear();
            PublishCatalog(catalog);

            LanguageInfo start = catalog.SourceLanguage;
            if (!_startingLanguage.IsEmpty)
            {
                if (catalog.TryGetLanguage(_startingLanguage, out LanguageInfo chosen))
                {
                    start = chosen;
                }
                else
                {
                    Report(ReportSeverity.Warning, $"'{_startingLanguage.Name}' is not a language of the catalog '{CatalogKey.Name}', so it starts in {start.Name}.");
                }
            }
            TaskCompletionSource<bool> initialization;
            lock (_lockObject)
            {
                initialization = _initialization;
            }
            ReportUnknownHeldTables();
            StartSwitch(start, initialization);
        }

        /// <summary>Reports every table a handle held before the catalog arrived that the catalog doesn't have.</summary>
        private void ReportUnknownHeldTables()
        {
            _heldTables.Clear();
            _holds.CopyHeldByHandles(_heldTables);
            for (int i = 0; i < _heldTables.Count; i++)
            {
                if (!_catalog.TryGetTable(_heldTables[i], out _))
                {
                    ReportUnknownTable(_heldTables[i]);
                }
            }
            _heldTables.Clear();
            _holds.CompleteLoaded(_getLoadedResult);
        }

        /// <summary>
        /// Starts loading every table the localizer keeps in <paramref name="target"/>, along with the fallback
        /// languages each table needs, reusing the tables already loaded. A switch still loading is replaced, and its
        /// waiters move to this one.
        /// </summary>
        private void StartSwitch(LanguageInfo target, TaskCompletionSource<bool> waiter)
        {
            LanguageSwitch previous = _pendingSwitch;
            LanguageSwitch languageSwitch = new(target, _catalog.GetFallbackChain(target));
            if (waiter != null)
            {
                languageSwitch.Waiters.Add(waiter);
            }
            languageSwitch.Waiters.AddRange(_initialWaiters);
            _initialWaiters.Clear();
            if (previous != null)
            {
                previous.Supersede();
                languageSwitch.Waiters.AddRange(previous.Waiters);
                previous.Waiters.Clear();
            }
            _pendingSwitch = languageSwitch;
            RaiseLanguageEvent(LanguageChanging, target);

            // One extra outstanding count is held while the loads start, so that sources answering synchronously
            // can't apply the switch before the last load has started.
            languageSwitch.Outstanding = 1;
            IReadOnlyList<TableInfo> tables = _catalog.Tables;
            for (int t = 0; t < tables.Count; t++)
            {
                if (IsKept(tables[t]))
                {
                    AddToSwitch(languageSwitch, tables[t]);
                }
            }
            languageSwitch.Outstanding--;
            TryApply(languageSwitch);
        }

        private void AddToSwitch(LanguageSwitch languageSwitch, TableInfo table)
        {
            // One count for the load, and one held until it is registered, so a load done within the call can't apply
            // the switch without it.
            languageSwitch.Outstanding += 2;
            languageSwitch.Loads[table.Key.Hash] = _loader.Load(table, languageSwitch.Chain, _onSwitchTableLoaded, languageSwitch);
            languageSwitch.Outstanding--;
            TryApply(languageSwitch);
        }

        private void OnSwitchTableLoaded(ChainLoad load)
        {
            LanguageSwitch languageSwitch = (LanguageSwitch)load.Owner;
            if (languageSwitch.IsSuperseded)
            {
                return;
            }
            languageSwitch.Outstanding--;
            TryApply(languageSwitch);
        }

        /// <summary>
        /// Whether the localizer keeps <paramref name="table"/> loaded in the current language: always for a preloaded
        /// table, and while anything holds it for one loaded on demand.
        /// </summary>
        private bool IsKept(TableInfo table) => table.Loading == TableLoading.Preload || _holds.IsHeld(table.Key.Hash);

        /// <summary>
        /// Applies a switch once every load it waits for is done: publishes the new state in one step, then
        /// refreshes every binding, raises <see cref="LanguageChanged"/> and completes the waiting tasks.
        /// </summary>
        private void TryApply(LanguageSwitch languageSwitch)
        {
            if (languageSwitch.Outstanding > 0 || languageSwitch.IsSuperseded || IsDisposed)
            {
                return;
            }
            IReadOnlyList<LanguageInfo> chain = languageSwitch.Chain;
            Dictionary<ulong, TableLayers> stateTables = new(languageSwitch.Loads.Count);
            foreach (ChainLoad load in languageSwitch.Loads.Values)
            {
                if (load.IsDone && IsKept(load.Table))
                {
                    stateTables.Add(load.Table.Key.Hash, load.Result);
                }
            }

            // The one step every lookup observes. Tables of languages the new chain doesn't use are released with the
            // old state, and dropped from the loader right after.
            LanguageFormat[] formats = new LanguageFormat[chain.Count];
            for (int l = 0; l < chain.Count; l++)
            {
                formats[l] = _catalog.GetFormat(chain[l]);
            }
            LocalizerState state = new(_state.Version + 1, languageSwitch.Target, chain, formats, stateTables);
            Volatile.Write(ref _state, state);
            _pendingSwitch = null;
            // On-demand tables still loading in the old language arrived with the switch instead.
            foreach (ChainLoad load in _onDemandLoads.Values)
            {
                load.Cancel();
            }
            _onDemandLoads.Clear();
            _loader.Prune(_isLoadedTableNeeded);

            _bindings.RefreshAll();
            RaiseLanguageEvent(LanguageChanged, languageSwitch.Target);
            _holds.CompleteLoaded(_getLoadedResult);
            for (int i = 0; i < languageSwitch.Waiters.Count; i++)
            {
                languageSwitch.Waiters[i].TrySetResult(true);
            }
            languageSwitch.Waiters.Clear();
        }

        /// <summary>
        /// Whether a table the loader holds in a language is still needed: the localizer keeps the table, and either
        /// the current state looked in that language for it, or the switch in progress may.
        /// </summary>
        private bool IsLoadedTableNeeded(ulong tableHash, ulong languageHash)
        {
            if (_catalog == null || !_catalog.TryGetTable(tableHash, out TableInfo table) || !IsKept(table))
            {
                return false;
            }
            if (_pendingSwitch != null && IndexOf(_pendingSwitch.Chain, languageHash) >= 0)
            {
                return true;
            }
            int index = IndexOf(_state.Chain, languageHash);
            return index >= 0 && _state.TryGetLayers(tableHash, out TableLayers layers) && index < layers.ConsultedLanguageCount;
        }

        private static int IndexOf(IReadOnlyList<LanguageInfo> chain, ulong languageHash)
        {
            for (int i = 0; i < chain.Count; i++)
            {
                if (chain[i].Key.Hash == languageHash)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Asks the sources for the compiled catalog, in order, until one has it.</summary>
        private bool TryLoadCatalog(in TableRequest request, TableReceiver receiver)
        {
            IReadOnlyList<ITableSource> sources = _host.TableSources;
            if (sources == null)
            {
                return false;
            }
            for (int i = 0; i < sources.Count; i++)
            {
                ITableSource source = sources[i];
                if (source == null)
                {
                    continue;
                }
                try
                {
                    if (source.TryLoad(in request, receiver))
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    Report(ReportSeverity.Error, $"The table source {source.GetType().Name} threw while loading {request}: {exception}");
                    // A source that answered before throwing still answered; the next source must not answer again.
                    if (receiver.IsCompleted)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void FailInitialization()
        {
            TaskCompletionSource<bool> initialization;
            lock (_lockObject)
            {
                initialization = _initialization;
                // Clearing it lets the next call to InitializeAsync try again.
                _initialization = null;
            }
            initialization?.TrySetResult(false);
            for (int i = 0; i < _initialWaiters.Count; i++)
            {
                _initialWaiters[i].TrySetResult(false);
            }
            _initialWaiters.Clear();
            _holds.CompleteAllLoaded(false);
        }

        private void ReleaseHostState()
        {
            if (_pendingSwitch != null)
            {
                _pendingSwitch.Supersede();
                for (int i = 0; i < _pendingSwitch.Waiters.Count; i++)
                {
                    _pendingSwitch.Waiters[i].TrySetResult(false);
                }
                _pendingSwitch = null;
            }
            for (int i = 0; i < _initialWaiters.Count; i++)
            {
                _initialWaiters[i].TrySetResult(false);
            }
            _initialWaiters.Clear();
            _loader.Clear();
            _catalog = null;
            Volatile.Write(ref _publishedCatalog, null);
        }

        private void RaiseLanguageEvent(Action<LanguageInfo> handlers, LanguageInfo language)
        {
            if (handlers == null)
            {
                return;
            }
            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                try
                {
                    ((Action<LanguageInfo>)invocationList[i])(language);
                }
                catch (Exception exception)
                {
                    // One failing handler must not keep the others from hearing about the switch.
                    Report(ReportSeverity.Error, $"A language change handler threw: {exception}");
                }
            }
        }

        // Tables.

        /// <inheritdoc/>
        public TableHandle HoldTable(TableKey table)
        {
            if (table.IsEmpty)
            {
                throw new ArgumentException("Holding a table needs the key of a table.", nameof(table));
            }
            if (IsDisposed)
            {
                ReportDisposedUse();
                return default;
            }
            int index = _holds.AcquireHandle(table, out int generation, out bool isFirst);
            CatalogInfo catalog = Catalog;
            if (catalog != null && !catalog.TryGetTable(table, out _))
            {
                ReportUnknownTable(table);
            }
            else if (isFirst)
            {
                OnTableAcquired(table.Hash);
            }
            return new TableHandle(this, index, generation);
        }

        internal bool IsHandleActive(int index, int generation) => _holds.TryGetHandleTable(index, generation, out _);

        internal bool IsHandleLoaded(int index, int generation) =>
            _holds.TryGetHandleTable(index, generation, out ulong tableHash) && GetLoadedResult(tableHash) == true;

        internal Task<bool> GetHandleLoaded(int index, int generation)
        {
            if (!_holds.TryGetHandleTable(index, generation, out ulong tableHash))
            {
                return NotLoaded;
            }
            bool? result = GetLoadedResult(tableHash);
            if (result.HasValue)
            {
                return result.Value ? Loaded : NotLoaded;
            }
            Task<bool> task = _holds.GetLoadedTask(tableHash) ?? NotLoaded;
            // The host may have published the table between the first look and the task's creation.
            _holds.CompleteLoaded(_getLoadedResult);
            return task;
        }

        internal void ReleaseHandle(int index, int generation)
        {
            if (_holds.ReleaseHandle(index, generation, out ulong tableHash, out bool isLast) && isLast)
            {
                OnTableReleased(tableHash);
            }
        }

        /// <summary>Holds the table of a binding's entry, loading it if it loads on demand. Any thread.</summary>
        internal void AcquireTable(TableKey table)
        {
            if (!table.IsEmpty && _holds.Acquire(table))
            {
                OnTableAcquired(table.Hash);
            }
        }

        /// <summary>Releases the table of a binding's entry. Any thread.</summary>
        internal void ReleaseTable(ulong tableHash)
        {
            if (tableHash != 0 && _holds.Release(tableHash))
            {
                OnTableReleased(tableHash);
            }
        }

        /// <summary>
        /// Returns whether a table can be read now (true), can't be loaded at all (false), or is still to load (null).
        /// Safe from any thread.
        /// </summary>
        private bool? GetLoadedResult(ulong tableHash)
        {
            if (IsDisposed)
            {
                return false;
            }
            if (Volatile.Read(ref _state).TryGetLayers(tableHash, out TableLayers layers))
            {
                return layers.Count > 0;
            }
            CatalogInfo catalog = Catalog;
            return catalog != null && !catalog.TryGetTable(tableHash, out _) ? false : null;
        }

        /// <summary>Whether the table with <paramref name="tableHash"/> loads on demand and isn't loaded in <paramref name="state"/>.</summary>
        private bool IsAwaitingTable(LocalizerState state, ulong tableHash)
        {
            CatalogInfo catalog = Catalog;
            return catalog != null && !state.HasTable(tableHash) && catalog.TryGetTable(tableHash, out TableInfo table) &&
                table.Loading == TableLoading.OnDemand;
        }

        private void OnTableAcquired(ulong tableHash)
        {
            CatalogInfo catalog = Catalog;
            // Before the catalog arrives, the first language switch loads whatever is held by then.
            if (catalog == null || !catalog.TryGetTable(tableHash, out TableInfo table) || table.Loading != TableLoading.OnDemand)
            {
                return;
            }
            if (_host.IsHostThread)
            {
                LoadHeldTable(tableHash);
                return;
            }
            LoadHeldTableLater(tableHash);
        }

        // A method of its own, because a lambda in OnTableAcquired would make every first hold allocate its closure.
        private void LoadHeldTableLater(ulong tableHash)
        {
            RunOnHostLater(() => LoadHeldTable(tableHash));
        }

        private void OnTableReleased(ulong tableHash)
        {
            CatalogInfo catalog = Catalog;
            // A table loaded with every language stays loaded, so letting go of it needs no work.
            if (catalog != null && catalog.TryGetTable(tableHash, out TableInfo table) && table.Loading == TableLoading.Preload)
            {
                return;
            }
            _holds.QueueRelease(tableHash);
            RequestUpdate();
        }

        /// <summary>
        /// Loads a held on-demand table in the current language, or as part of the switch in progress, which then
        /// waits for it too. Host thread only.
        /// </summary>
        private void LoadHeldTable(ulong tableHash)
        {
            if (IsDisposed || _catalog == null || !_catalog.TryGetTable(tableHash, out TableInfo table) ||
                table.Loading != TableLoading.OnDemand || !_holds.IsHeld(tableHash))
            {
                return;
            }
            if (_pendingSwitch != null)
            {
                if (!_pendingSwitch.Loads.ContainsKey(tableHash))
                {
                    AddToSwitch(_pendingSwitch, table);
                }
                return;
            }
            if (!_state.IsInitialized || _state.HasTable(tableHash) || _onDemandLoads.ContainsKey(tableHash))
            {
                return;
            }
            ChainLoad load = _loader.Load(table, _state.Chain, _onDemandTableLoaded, null);
            // A source answering within the call has already completed the load.
            if (!load.IsDone)
            {
                _onDemandLoads[tableHash] = load;
            }
        }

        private void OnDemandTableLoaded(ChainLoad load)
        {
            ulong tableHash = load.Table.Key.Hash;
            if (_onDemandLoads.TryGetValue(tableHash, out ChainLoad current) && current == load)
            {
                _onDemandLoads.Remove(tableHash);
            }
            // A switch applied meanwhile loaded the table in its own language.
            if (load.IsCancelled || IsDisposed || !_holds.IsHeld(tableHash) || !ReferenceEquals(load.Chain, _state.Chain))
            {
                return;
            }
            Volatile.Write(ref _state, _state.WithTable(tableHash, load.Result));
            _bindings.RefreshTable(tableHash);
            _holds.CompleteLoaded(_getLoadedResult);
        }

        /// <summary>
        /// Unloads the on-demand tables nothing held at the last update and nothing holds again since, then queues the
        /// ones released since for the next update. The update in between lets a binding disposed and made again
        /// within one frame, as when a panel is toggled, keep its table.
        /// </summary>
        private void UnloadReleasedTables()
        {
            for (int i = 0; i < _releasedTables.Count; i++)
            {
                UnloadIfReleased(_releasedTables[i]);
            }
            _releasedTables.Clear();
            _holds.TakeReleased(_releasedTables);
            if (_releasedTables.Count > 0)
            {
                RequestUpdate();
            }
        }

        private void UnloadIfReleased(ulong tableHash)
        {
            if (IsDisposed || _holds.IsHeld(tableHash))
            {
                return;
            }
            if (_onDemandLoads.TryGetValue(tableHash, out ChainLoad load))
            {
                load.Cancel();
                _onDemandLoads.Remove(tableHash);
            }
            if (_catalog != null && _catalog.TryGetTable(tableHash, out TableInfo table) && table.Loading == TableLoading.OnDemand)
            {
                // A switch in progress no longer waits for it.
                if (_pendingSwitch != null && _pendingSwitch.Loads.TryGetValue(tableHash, out ChainLoad switchLoad))
                {
                    _pendingSwitch.Loads.Remove(tableHash);
                    if (!switchLoad.IsDone)
                    {
                        switchLoad.Cancel();
                        _pendingSwitch.Outstanding--;
                    }
                }
                if (_state.HasTable(tableHash))
                {
                    Volatile.Write(ref _state, _state.WithTable(tableHash, null));
                }
                _loader.Prune(_isLoadedTableNeeded);
            }
            _holds.CompleteLoaded(tableHash, false);
            if (_pendingSwitch != null)
            {
                TryApply(_pendingSwitch);
            }
        }

        // Languages.

        /// <summary>
        /// Adds <paramref name="language"/> to the catalog for as long as the localizer lives, such as a fan translation
        /// a player dropped into a mods folder, so it can be switched to like any other language.
        /// </summary>
        /// <remarks>
        /// Safe from any thread, and applied on the host thread in order with every other request, so switching to the
        /// language right after registering it works. Registered before initialization, it can be the language
        /// initialization starts in. A name the catalog already has, or a fallback it doesn't, is reported and changes
        /// nothing. The language's tables come from the table sources like any other's, such as a
        /// <see cref="FolderTableSource"/> reading the mods folder.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="language"/> is null.</exception>
        public void RegisterLanguage(LanguageInfo language)
        {
            if (language == null)
            {
                throw new ArgumentNullException(nameof(language));
            }
            if (IsDisposed)
            {
                ReportDisposedUse();
                return;
            }
            RunOnHost(() => AddLanguage(language));
        }

        private void AddLanguage(LanguageInfo language)
        {
            if (IsDisposed)
            {
                return;
            }
            if (_catalog == null)
            {
                _pendingLanguages.Add(language);
                return;
            }
            if (TryAddLanguage(_catalog, language, out CatalogInfo catalog))
            {
                PublishCatalog(catalog);
            }
        }

        private bool TryAddLanguage(CatalogInfo catalog, LanguageInfo language, out CatalogInfo extended)
        {
            if (catalog.TryAddLanguage(language, out extended, out string error))
            {
                return true;
            }
            Report(ReportSeverity.Error, $"The language '{language.Name}' wasn't registered: {error}");
            extended = catalog;
            return false;
        }

        private void PublishCatalog(CatalogInfo catalog)
        {
            _catalog = catalog;
            _loader.Catalog = catalog;
            Volatile.Write(ref _publishedCatalog, catalog);
        }
    }
}
