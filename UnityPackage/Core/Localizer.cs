using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Internal;
using reromanlee.ReactiveLocalizer.Messages;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The localizer of one catalog, and the one implementation of <see cref="ILocalizer"/> on every platform. What
    /// changes per platform is its <see cref="ILocalizerHost"/>:
    /// <code>
    /// Localizer localizer = new Localizer(LocalizationKeys.CatalogKey, new UnityHost());
    /// await localizer.InitializeAsync();
    /// </code>
    /// </summary>
    /// <remarks>
    /// Lookups read one published state object that never changes, so they need no lock and see either the old
    /// language or the new one, never a mix. Everything that changes the state runs on the host thread, in the order
    /// it was requested; work requested from other threads is queued for the host's next update.
    /// </remarks>
    public sealed class Localizer : ILocalizer, IDisposable
    {
        private readonly ILocalizerHost _host;
        private readonly BindingRegistry _bindings;
        private readonly MissingKeys _missingKeys = new();
        private readonly ReportedProblems _reportedProblems = new();
        private readonly ConcurrentQueue<Action> _hostActions = new();
        private readonly Action _update;
        private readonly Action<TableReceiver> _onReceived;
        private readonly Action<ChainLoad> _onSwitchTableLoaded;
        private readonly Func<ulong, ulong, bool> _isLoadedTableNeeded;
        private readonly object _lockObject = new();

        // Published for every thread: lookups read the state, and the catalog is shown once it is loaded.
        private LocalizerState _state = LocalizerState.Empty;
        private CatalogInfo _publishedCatalog;
        private FormatterTable _formatters = FormatterTable.Empty;

        // Host thread only.
        private readonly List<TaskCompletionSource<bool>> _initialWaiters = new();
        private readonly TableLoader _loader;
        private CatalogInfo _catalog;
        private LanguageSwitch _pendingSwitch;
        private LanguageKey _startingLanguage;

        // Any thread, guarded by the lock or by interlocked access.
        private TaskCompletionSource<bool> _initialization;
        private int _isUpdateScheduled;
        private int _isDisposed;
        private int _hasReportedEarlyRead;
        private int _hasReportedEmptyKey;
        private int _hasReportedDisposedUse;

        /// <summary>Creates the localizer of <paramref name="catalog"/>. Nothing loads until <see cref="InitializeAsync"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="catalog"/> is empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="host"/> is null.</exception>
        public Localizer(CatalogKey catalog, ILocalizerHost host)
        {
            if (catalog.IsEmpty)
            {
                throw new ArgumentException("A localizer needs the key of its catalog.", nameof(catalog));
            }
            _host = host ?? throw new ArgumentNullException(nameof(host));
            CatalogKey = catalog;
            _bindings = new BindingRegistry(this);
            _update = Update;
            _onReceived = OnReceived;
            _onSwitchTableLoaded = OnSwitchTableLoaded;
            _isLoadedTableNeeded = IsLoadedTableNeeded;
            _loader = new TableLoader(host, _onReceived, (severity, message) => Report(severity, message));
        }

        /// <inheritdoc/>
        public event Action<LanguageInfo> LanguageChanging;

        /// <inheritdoc/>
        public event Action<LanguageInfo> LanguageChanged;

        /// <summary>Key of the catalog this localizer was created for.</summary>
        public CatalogKey CatalogKey { get; }

        /// <inheritdoc/>
        public CatalogInfo Catalog => Volatile.Read(ref _publishedCatalog);

        /// <inheritdoc/>
        public bool IsInitialized => Volatile.Read(ref _state).IsInitialized;

        /// <inheritdoc/>
        public LanguageInfo CurrentLanguage => Volatile.Read(ref _state).Language;

        /// <inheritdoc/>
        public IReadOnlyList<LanguageInfo> Languages => Catalog?.Languages ?? Array.Empty<LanguageInfo>();

        /// <inheritdoc/>
        public int MissingKeyCount => _missingKeys.Count;

        /// <summary>How many bindings are active.</summary>
        public int BindingCount => _bindings.ActiveCount;

        private bool IsDisposed => Volatile.Read(ref _isDisposed) != 0;

        // Lifecycle.

        /// <inheritdoc/>
        /// <remarks>After a failed initialization, calling it again tries again.</remarks>
        public Task InitializeAsync()
        {
            if (IsDisposed)
            {
                ReportDisposedUse();
                return Task.CompletedTask;
            }
            TaskCompletionSource<bool> initialization;
            lock (_lockObject)
            {
                if (_initialization != null)
                {
                    return _initialization.Task;
                }
                initialization = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _initialization = initialization;
            }
            RunOnHost(BeginInitialize);
            return initialization.Task;
        }

        /// <inheritdoc/>
        public Task SetLanguageAsync(LanguageKey language)
        {
            if (language.IsEmpty)
            {
                throw new ArgumentException("Switching the language needs the key of a language.", nameof(language));
            }
            if (IsDisposed)
            {
                ReportDisposedUse();
                return Task.CompletedTask;
            }
            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            RunOnHost(() => RequestLanguage(language, completion));
            return completion.Task;
        }

        /// <summary>
        /// Releases every binding and every loaded table. Lookups return empty text afterwards, and every pending task
        /// completes. Safe to call any number of times, from any thread.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }
            _bindings.Clear();
            Volatile.Write(ref _state, LocalizerState.Empty);
            TaskCompletionSource<bool> initialization;
            lock (_lockObject)
            {
                initialization = _initialization;
            }
            initialization?.TrySetResult(false);
            // The rest belongs to the host thread: pending switches complete their waiters there.
            RunOnHost(ReleaseHostState);
        }

        // Lookups.

        /// <inheritdoc/>
        public string Get(in EntryKey key)
        {
            LocalizerState state = Volatile.Read(ref _state);
            if (!state.IsInitialized)
            {
                ReportEarlyRead();
                return string.Empty;
            }
            if (key.IsEmpty)
            {
                ReportEmptyKey();
                return string.Empty;
            }
            if (state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out _))
            {
                ReportIfMessage(table, index, key.Table.Name, key.Name);
                return table.GetString(index);
            }
            return GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash);
        }

        /// <inheritdoc/>
        public string Get(in EntryMessage message)
        {
            LocalizerState state = Volatile.Read(ref _state);
            if (!state.IsInitialized)
            {
                ReportEarlyRead();
                return string.Empty;
            }
            return Format(state, in message, true);
        }

        /// <inheritdoc/>
        public bool TryFormat(in EntryMessage message, Span<char> destination, out int written)
        {
            written = 0;
            LocalizerState state = Volatile.Read(ref _state);
            if (!state.IsInitialized)
            {
                ReportEarlyRead();
                return true;
            }
            EntryKey key = message.Key;
            if (key.IsEmpty)
            {
                ReportEmptyKey();
                return true;
            }
            if (!state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out int languageIndex))
            {
                return TryCopy(GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash).AsSpan(), destination, out written);
            }
            if (!table.TryGetMessage(index, out int start))
            {
                return TryCopy(table.GetMemory(index).Span, destination, out written);
            }
            // The message is written straight into the caller's buffer; it fits when it never had to grow out of it.
            TextBuilder output = new(destination);
            try
            {
                Render(state, table, start, languageIndex, in message, ref output);
                if (output.HasOutgrownInitialBuffer)
                {
                    return false;
                }
                written = output.Length;
                return true;
            }
            finally
            {
                output.Dispose();
            }
        }

        /// <inheritdoc/>
        public string Get(ReadOnlySpan<char> tableName, ReadOnlySpan<char> entryName)
        {
            LocalizerState state = Volatile.Read(ref _state);
            if (!state.IsInitialized)
            {
                ReportEarlyRead();
                return string.Empty;
            }
            ulong tableHash = Hashing.ComputeNameHash(tableName);
            ulong entryHash = Hashing.ComputeNameHash(entryName);
            if (state.TryResolve(tableHash, entryHash, out CompiledTable table, out int index, out _))
            {
                ReportIfMessage(table, index, tableName, entryName);
                return table.GetString(index);
            }
            return GetMissingMarker(state, tableName, entryName, tableHash, entryHash);
        }

        /// <inheritdoc/>
        public bool TryGet(in EntryKey key, out string text)
        {
            LocalizerState state = Volatile.Read(ref _state);
            if (state.IsInitialized && !key.IsEmpty && state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out _))
            {
                text = table.GetString(index);
                return true;
            }
            text = null;
            return false;
        }

        /// <inheritdoc/>
        public ReadOnlyMemory<char> GetMemory(in EntryKey key)
        {
            LocalizerState state = Volatile.Read(ref _state);
            if (state.IsInitialized && !key.IsEmpty && state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out _))
            {
                ReportIfMessage(table, index, key.Table.Name, key.Name);
                return table.GetMemory(index);
            }
            return Get(in key).AsMemory();
        }

        // Bindings.

        /// <inheritdoc/>
        public TextBinding Bind<TTarget>(in EntryKey key, TTarget target, Action<TTarget, string> apply) where TTarget : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }
            if (IsDisposed)
            {
                ReportDisposedUse();
                return default;
            }
            _bindings.Add(in key, target, apply, BindingInvokers<TTarget>.Invoker, out int index, out int generation);
            ApplyBinding(index, generation);
            return new TextBinding(_bindings, index, generation);
        }

        /// <inheritdoc/>
        public TextBinding Bind<TTarget>(in EntryMessage message, TTarget target, Action<TTarget, string> apply) where TTarget : class
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (apply == null)
            {
                throw new ArgumentNullException(nameof(apply));
            }
            if (IsDisposed)
            {
                ReportDisposedUse();
                return default;
            }
            _bindings.Add(in message, target, apply, BindingInvokers<TTarget>.Invoker, out int index, out int generation);
            ApplyBinding(index, generation);
            return new TextBinding(_bindings, index, generation);
        }

        /// <summary>
        /// Registers <paramref name="formatter"/> for arguments of <paramref name="type"/>, such as <c>date</c> in
        /// <c>{deadline, date}</c>, replacing any formatter registered for it before; null removes it. Types ignore case.
        /// </summary>
        /// <remarks>
        /// Safe from any thread; messages formatted afterwards use it. Bound text formatted before keeps its text until
        /// it is formatted again, so register formatters before binding, as part of setting the localizer up.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// <paramref name="type"/> breaks the naming rule, or is a type messages already define, such as <c>number</c>.
        /// </exception>
        public void SetFormatter(string type, ArgumentFormatter formatter)
        {
            NameRules.ThrowIfInvalid(type, nameof(type));
            if (FormatterTable.IsBuiltInType(type))
            {
                throw new ArgumentException($"'{type}' is a type messages already define; formatters are for types such as date.", nameof(type));
            }
            lock (_lockObject)
            {
                Volatile.Write(ref _formatters, Volatile.Read(ref _formatters).With(type, formatter));
            }
        }

        /// <summary>Applies a new binding's first text: right away on the host thread, else at the host's next update.</summary>
        private void ApplyBinding(int index, int generation)
        {
            if (_host.IsHostThread)
            {
                _bindings.Apply(index, generation);
                return;
            }
            ApplyBindingLater(index, generation);
        }

        // A method of its own, because a lambda in ApplyBinding would make every binding allocate its closure.
        private void ApplyBindingLater(int index, int generation)
        {
            _hostActions.Enqueue(() => _bindings.Apply(index, generation));
            RequestUpdate();
        }

        /// <summary>Whether the calling thread is the host's, where bindings may be changed right away.</summary>
        internal bool IsOnHostThread => _host.IsHostThread;

        /// <summary>Queues <paramref name="action"/> for the host thread's next update.</summary>
        internal void RunOnHostLater(Action action)
        {
            _hostActions.Enqueue(action);
            RequestUpdate();
        }

        /// <summary>Returns the text a message binding shows right now. Host thread only.</summary>
        internal string ResolveBindingText(in EntryMessage message)
        {
            LocalizerState state = Volatile.Read(ref _state);
            return state.IsInitialized ? Format(state, in message, false) : string.Empty;
        }

        /// <summary>Returns the text a binding of <paramref name="key"/> shows right now. Host thread only.</summary>
        internal string ResolveBindingText(in EntryKey key)
        {
            LocalizerState state = Volatile.Read(ref _state);
            if (!state.IsInitialized)
            {
                return string.Empty;
            }
            if (key.IsEmpty)
            {
                ReportEmptyKey();
                return string.Empty;
            }
            if (state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out _))
            {
                ReportIfMessage(table, index, key.Table.Name, key.Name);
                return table.GetString(index);
            }
            return GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash);
        }

        internal bool IsTargetDestroyed(object target)
        {
            try
            {
                return _host.IsDestroyed(target);
            }
            catch (Exception)
            {
                // A host that can't tell is treated as saying no: the binding keeps working.
                return false;
            }
        }

        internal void ReportDestroyedTarget(in EntryKey key)
        {
            Report(ReportSeverity.Warning, $"A binding of '{key}' was released because its target was destroyed while still bound. Dispose bindings when their target goes away, such as in OnDisable.");
        }

        internal void ReportBindingFailure(in EntryKey key, object target, Exception exception)
        {
            Report(ReportSeverity.Error, $"The binding callback of '{key}' threw, so it didn't receive its text: {exception}", target);
        }

        // Host thread work.

        /// <summary>Runs the work queued from other threads, in the order it was queued. Called by the host on its thread.</summary>
        private void Update()
        {
            Volatile.Write(ref _isUpdateScheduled, 0);
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
                Update();
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
            _catalog = catalog;
            _loader.Catalog = catalog;
            Volatile.Write(ref _publishedCatalog, catalog);

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
            StartSwitch(start, initialization);
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

        /// <summary>Whether the localizer keeps <paramref name="table"/> loaded in the current language.</summary>
        private bool IsKept(TableInfo table) => table.Loading == TableLoading.Preload;

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
            _loader.Prune(_isLoadedTableNeeded);

            _bindings.RefreshAll();
            RaiseLanguageEvent(LanguageChanged, languageSwitch.Target);
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

        // Messages.

        /// <summary>Returns the text of a message: formatted when its entry has arguments, plain text when it has none.</summary>
        private string Format(LocalizerState state, in EntryMessage message, bool isReportingEmptyKey)
        {
            EntryKey key = message.Key;
            if (key.IsEmpty)
            {
                if (isReportingEmptyKey)
                {
                    ReportEmptyKey();
                }
                return string.Empty;
            }
            if (!state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out int languageIndex))
            {
                return GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash);
            }
            if (!table.TryGetMessage(index, out int start))
            {
                return table.GetString(index);
            }
            Span<char> buffer = stackalloc char[256];
            TextBuilder output = new(buffer);
            try
            {
                Render(state, table, start, languageIndex, in message, ref output);
                return output.ToString();
            }
            finally
            {
                output.Dispose();
            }
        }

        private void Render(LocalizerState state, CompiledTable table, int start, int languageIndex, in EntryMessage message, ref TextBuilder output)
        {
            MessageProblems problems = default;
            try
            {
                MessageRenderer.Render(table.Program, table.Characters, start, in message, state.Formats[languageIndex],
                    Volatile.Read(ref _formatters), state.Chain[languageIndex], ref output, ref problems);
            }
            catch (Exception exception)
            {
                // A verified table can't make rendering fail; this only guards against a bug turning into a crash.
                if (_reportedProblems.TryAdd(message.Key.Table.Hash, message.Key.Hash, 0))
                {
                    Report(ReportSeverity.Error, $"Formatting the message of '{message.Key}' failed: {exception}");
                }
            }
            if (problems.HasAny)
            {
                ReportMessageProblems(table, start, in message, in problems);
            }
        }

        /// <summary>Reports each problem of a formatted message once: arguments missing, of the wrong kind, or without a working formatter.</summary>
        private void ReportMessageProblems(CompiledTable table, int start, in EntryMessage message, in MessageProblems problems)
        {
            EntryKey key = message.Key;
            int count = MessageProgram.GetArgumentCount(table.Program, start);
            for (int argument = 0; argument < count; argument++)
            {
                uint bit = 1u << argument;
                int entry = MessageProgram.GetArgumentEntry(start, argument);
                ulong nameHash = MessageProgram.ReadUInt64(table.Program, entry);
                string name = MessageRenderer.GetArgumentName(table.Program, table.Characters, start, argument).ToString();
                if ((problems.MissingArguments & bit) != 0 && _reportedProblems.TryAdd(key.Table.Hash, key.Hash, nameHash ^ 1))
                {
                    Report(ReportSeverity.Error, $"The message of '{key}' was formatted without its argument {{{name}}}, so it shows as {{{name}}}. Each problem is reported once.");
                }
                if ((problems.MistypedArguments & bit) != 0 && _reportedProblems.TryAdd(key.Table.Hash, key.Hash, nameHash ^ 2))
                {
                    Report(ReportSeverity.Error, $"The message of '{key}' was given a value of the wrong kind for {{{name}}}, such as text where it needs a number, so it shows the value as it is or its 'other' form. Each problem is reported once.");
                }
                if ((problems.UnformattedArguments & bit) != 0 && _reportedProblems.TryAdd(key.Table.Hash, key.Hash, nameHash ^ 3))
                {
                    string type = new(table.Characters, problems.UnformattedTypeStart, problems.UnformattedTypeLength);
                    Report(ReportSeverity.Error, $"The message of '{key}' formats {{{name}}} as '{type}', but no formatter is registered for it, so it shows the value as it is. Register one with SetFormatter(\"{type}\", ...). Each problem is reported once.");
                }
                if ((problems.FailedArguments & bit) != 0 && _reportedProblems.TryAdd(key.Table.Hash, key.Hash, nameHash ^ 4))
                {
                    string reason = problems.FormatterException != null ? $" It threw: {problems.FormatterException}" : " It kept asking for more room.";
                    Report(ReportSeverity.Error, $"The formatter of {{{name}}} in the message of '{key}' failed, so it shows as {{{name}}}.{reason}");
                }
            }
        }

        /// <summary>Reports, once per entry, that a message with arguments was read as plain text, where its arguments show as {name}.</summary>
        private void ReportIfMessage(CompiledTable table, int index, ReadOnlySpan<char> tableName, ReadOnlySpan<char> entryName)
        {
            if (!table.TryGetMessage(index, out _) || !_reportedProblems.TryAdd(table.TableHash, Hashing.ComputeNameHash(entryName), 5))
            {
                return;
            }
            Report(ReportSeverity.Warning, $"'{tableName.ToString()}.{entryName.ToString()}' takes arguments, so reading it as plain text shows them as {{name}}. Read it as a message, such as {CatalogKey.Name}Keys.{tableName.ToString()}.{entryName.ToString()}(...).");
        }

        private static bool TryCopy(ReadOnlySpan<char> text, Span<char> destination, out int written)
        {
            if (text.Length > destination.Length)
            {
                written = 0;
                return false;
            }
            text.CopyTo(destination);
            written = text.Length;
            return true;
        }

        // Reports.

        private string GetMissingMarker(LocalizerState state, ReadOnlySpan<char> tableName, ReadOnlySpan<char> entryName, ulong tableHash, ulong entryHash)
        {
            string marker = _missingKeys.GetMarker(tableName, entryName, tableHash, entryHash, out bool isNew);
            if (isNew)
            {
                string reason = !state.TryGetLayers(tableHash, out TableLayers layers)
                    ? $"the catalog '{CatalogKey.Name}' has no table '{tableName.ToString()}'"
                    : layers.Count == 0
                        ? $"the table '{tableName.ToString()}' couldn't be loaded in any language"
                        : $"the table '{tableName.ToString()}' has no entry '{entryName.ToString()}' in any of its languages";
                Report(ReportSeverity.Error, $"{marker} is shown because {reason}. Each missing key is reported once.");
            }
            return marker;
        }

        private void ReportEarlyRead()
        {
            if (IsDisposed)
            {
                ReportDisposedUse();
                return;
            }
            if (Interlocked.Exchange(ref _hasReportedEarlyRead, 1) == 0)
            {
                Report(ReportSeverity.Warning, $"Text of the catalog '{CatalogKey.Name}' was read before its localizer finished initializing, so the read returned empty text. Await InitializeAsync first, or bind the text so it arrives on its own.");
            }
        }

        private void ReportEmptyKey()
        {
            if (Interlocked.Exchange(ref _hasReportedEmptyKey, 1) == 0)
            {
                Report(ReportSeverity.Warning, "Text was asked for with an empty key, such as a field no entry was picked for, so it shows empty text.");
            }
        }

        private void ReportDisposedUse()
        {
            if (Interlocked.Exchange(ref _hasReportedDisposedUse, 1) == 0)
            {
                Report(ReportSeverity.Warning, $"The localizer of the catalog '{CatalogKey.Name}' was used after it was disposed; it returns empty text and does nothing else.");
            }
        }

        private void Report(ReportSeverity severity, string message, object target = null)
        {
            try
            {
                _host.Report(new LocalizerReport(severity, message, target));
            }
            catch (Exception)
            {
                // A host that fails to report can't be told about it; the localizer keeps working regardless.
            }
        }
    }
}
