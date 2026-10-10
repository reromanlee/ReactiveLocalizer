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
    public sealed partial class Localizer : ILocalizer, IDisposable
    {
        private readonly ILocalizerHost _host;
        private readonly BindingRegistry _bindings;
        private readonly MissingKeys _missingKeys = new();
        private readonly ReportedProblems _reportedProblems = new();
        private readonly ConcurrentQueue<Action> _hostActions = new();
        private readonly Action _update;
        private readonly Action<TableReceiver> _onReceived;
        private readonly Action<ChainLoad> _onSwitchTableLoaded;
        private readonly Action<ChainLoad> _onDemandTableLoaded;
        private readonly Func<ulong, ulong, bool> _isLoadedTableNeeded;
        private readonly Func<ulong, bool?> _getLoadedResult;
        private readonly TableHolds _holds = new();
        private readonly object _lockObject = new();

        // Published for every thread: lookups read the state, and the catalog is shown once it is loaded.
        private LocalizerState _state = LocalizerState.Empty;
        private CatalogInfo _publishedCatalog;
        private FormatterTable _formatters = FormatterTable.Empty;

        // Host thread only.
        private readonly List<TaskCompletionSource<bool>> _initialWaiters = new();
        private readonly TableLoader _loader;
        private readonly Dictionary<ulong, ChainLoad> _onDemandLoads = new();
        // Tables nothing held at the last update; they unload at the next one unless something holds them again.
        private readonly List<ulong> _releasedTables = new();
        private readonly List<TableKey> _heldTables = new();
        // Languages registered before the catalog arrived, added as soon as it does.
        private readonly List<LanguageInfo> _pendingLanguages = new();
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
            _onDemandTableLoaded = OnDemandTableLoaded;
            _isLoadedTableNeeded = IsLoadedTableNeeded;
            _getLoadedResult = GetLoadedResult;
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

        /// <summary>The task of a table that can't be loaded or isn't held: already completed, with false.</summary>
        internal static Task<bool> NotLoaded { get; } = Task.FromResult(false);

        private static Task<bool> Loaded { get; } = Task.FromResult(true);

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
            _holds.Clear();
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
            return Format(state, in message, false);
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
        public TextBinding Bind<TTarget>(in EntryKey key, TTarget target, Action<TTarget, string> apply) where TTarget : class =>
            Bind(in key, target, apply, BindingInvokers<TTarget>.Invoker, null);

        /// <inheritdoc/>
        public TextBinding Bind<TTarget>(in EntryMessage message, TTarget target, Action<TTarget, string> apply) where TTarget : class =>
            Bind(in message, target, apply, BindingInvokers<TTarget>.Invoker, null);

        /// <inheritdoc/>
        public TextBinding BindCharacters<TTarget>(in EntryKey key, TTarget target, Action<TTarget, ReadOnlyMemory<char>> apply) where TTarget : class =>
            Bind(in key, target, apply, null, BindingInvokers<TTarget>.CharacterInvoker);

        /// <inheritdoc/>
        public TextBinding BindCharacters<TTarget>(in EntryMessage message, TTarget target, Action<TTarget, ReadOnlyMemory<char>> apply) where TTarget : class =>
            Bind(in message, target, apply, null, BindingInvokers<TTarget>.CharacterInvoker);

        private TextBinding Bind(in EntryKey key, object target, Delegate apply, BindingInvoker invoker, CharacterInvoker characterInvoker)
        {
            if (!CanBind(target, apply))
            {
                return default;
            }
            _bindings.Add(in key, target, apply, invoker, characterInvoker, out int index, out int generation);
            ApplyBinding(index, generation);
            return new TextBinding(_bindings, index, generation);
        }

        private TextBinding Bind(in EntryMessage message, object target, Delegate apply, BindingInvoker invoker, CharacterInvoker characterInvoker)
        {
            if (!CanBind(target, apply))
            {
                return default;
            }
            _bindings.Add(in message, target, apply, invoker, characterInvoker, out int index, out int generation);
            ApplyBinding(index, generation);
            return new TextBinding(_bindings, index, generation);
        }

        private bool CanBind(object target, Delegate apply)
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
                return false;
            }
            return true;
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

        /// <summary>
        /// Returns the text a message binding shows right now, or null while its on-demand table loads, so the binding
        /// keeps its text. Host thread only.
        /// </summary>
        internal string ResolveBindingText(in EntryMessage message)
        {
            LocalizerState state = Volatile.Read(ref _state);
            return state.IsInitialized ? Format(state, in message, true) : string.Empty;
        }

        /// <summary>
        /// Returns the text a binding of <paramref name="key"/> shows right now, or null while its on-demand table
        /// loads, so the binding keeps its text. Host thread only.
        /// </summary>
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
            return IsAwaitingTable(state, key.Table.Hash, key.Hash) ? null : GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash);
        }

        /// <summary>
        /// Gives the characters a character binding of <paramref name="key"/> shows right now, or returns false while
        /// its on-demand table loads, so the binding keeps its text. Host thread only.
        /// </summary>
        internal bool TryResolveBindingCharacters(in EntryKey key, ref char[] buffer, out ReadOnlyMemory<char> characters)
        {
            characters = ReadOnlyMemory<char>.Empty;
            LocalizerState state = Volatile.Read(ref _state);
            if (!state.IsInitialized)
            {
                return true;
            }
            if (key.IsEmpty)
            {
                ReportEmptyKey();
                return true;
            }
            if (state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out _))
            {
                ReportIfMessage(table, index, key.Table.Name, key.Name);
                characters = table.GetMemory(index);
                return true;
            }
            if (IsAwaitingTable(state, key.Table.Hash, key.Hash))
            {
                return false;
            }
            characters = GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash).AsMemory();
            return true;
        }

        /// <summary>
        /// Formats the message a character binding shows into <paramref name="buffer"/>, which grows when the text
        /// outgrows it, or returns false while its on-demand table loads. Host thread only.
        /// </summary>
        internal bool TryResolveBindingCharacters(in EntryMessage message, ref char[] buffer, out ReadOnlyMemory<char> characters)
        {
            characters = ReadOnlyMemory<char>.Empty;
            LocalizerState state = Volatile.Read(ref _state);
            EntryKey key = message.Key;
            if (!state.IsInitialized || key.IsEmpty)
            {
                return true;
            }
            if (!state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out int languageIndex))
            {
                if (IsAwaitingTable(state, key.Table.Hash, key.Hash))
                {
                    return false;
                }
                characters = GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash).AsMemory();
                return true;
            }
            if (!table.TryGetMessage(index, out int start))
            {
                characters = table.GetMemory(index);
                return true;
            }
            TextBuilder output = new(buffer);
            try
            {
                Render(state, table, start, languageIndex, in message, ref output);
                if (output.HasOutgrownInitialBuffer)
                {
                    // The buffer grows once to fit, and every later message of this size formats into it directly.
                    buffer = new char[Math.Max(output.Length, buffer.Length * 2)];
                    output.Text.CopyTo(buffer);
                }
                characters = new ReadOnlyMemory<char>(buffer, 0, output.Length);
                return true;
            }
            finally
            {
                output.Dispose();
            }
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

        // Messages.

        /// <summary>
        /// Returns the text of a message: formatted when its entry has arguments, plain text when it has none. For a
        /// binding, returns null while the message's on-demand table loads, so the binding keeps its text.
        /// </summary>
        private string Format(LocalizerState state, in EntryMessage message, bool isForBinding)
        {
            EntryKey key = message.Key;
            if (key.IsEmpty)
            {
                if (!isForBinding)
                {
                    ReportEmptyKey();
                }
                return string.Empty;
            }
            if (!state.TryResolve(key.Table.Hash, key.Hash, out CompiledTable table, out int index, out int languageIndex))
            {
                return isForBinding && IsAwaitingTable(state, key.Table.Hash, key.Hash)
                    ? null
                    : GetMissingMarker(state, key.Table.Name, key.Name, key.Table.Hash, key.Hash);
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

        /// <summary>
        /// Returns what a read shows for an entry it didn't find: the <c>[Table.Key]</c> marker for a key that exists
        /// nowhere, reported once, or empty text for an on-demand table that isn't loaded, reported once per table.
        /// </summary>
        private string GetMissingMarker(LocalizerState state, ReadOnlySpan<char> tableName, ReadOnlySpan<char> entryName, ulong tableHash, ulong entryHash)
        {
            if (IsAwaitingTable(state, tableHash, entryHash))
            {
                if (_reportedProblems.TryAdd(tableHash, 0, 6))
                {
                    Report(ReportSeverity.Warning, $"'{tableName.ToString()}.{entryName.ToString()}' was read while its table, which loads on demand, wasn't loaded, so the read returned empty text. Hold the table with HoldTable and await WhenLoaded first, or bind the text so it arrives on its own.");
                }
                return string.Empty;
            }
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

        private void ReportUnknownTable(TableKey table)
        {
            if (_reportedProblems.TryAdd(table.Hash, 0, 7))
            {
                Report(ReportSeverity.Error, $"The catalog '{CatalogKey.Name}' has no table '{table.Name}' to hold.");
            }
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
