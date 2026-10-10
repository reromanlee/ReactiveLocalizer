using System;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// The active bindings of one localizer, kept in reusable slots so binding and releasing allocate nothing once the
    /// slot array has grown to the project's peak.
    /// </summary>
    /// <remarks>
    /// Adding and releasing are safe from any thread. Applying text happens on the host thread only, outside the lock,
    /// so a callback may bind, release or even switch the language without deadlocking. A slot's generation changes
    /// whenever it is released, which turns a stale or copied handle into a harmless no-op. The arguments of message
    /// bindings live in pooled holders that only the host thread recycles, so a binding released from another thread
    /// can never change the message the host thread is formatting. Every binding holds the table of its entry, so a
    /// table loaded on demand stays loaded while anything shows its text.
    /// </remarks>
    internal sealed class BindingRegistry
    {
        private readonly Localizer _localizer;
        private readonly object _lockObject = new();
        private Slot[] _slots = new Slot[16];
        private Pending[] _refreshBuffer = new Pending[16];
        private int _highWater;
        private int _freeHead = -1;
        private int _activeCount;
        private bool _isRefreshing;

        // Host thread only: the buffers character bindings' messages are formatted into, one per callback nesting level.
        private char[][] _characterBuffers = new char[2][];
        private int _characterDepth;

        // Holders ready to take a message, and holders released since the host thread last recycled them.
        private BoundMessage _freeMessages;
        private BoundMessage _retiredMessages;

        public BindingRegistry(Localizer localizer)
        {
            _localizer = localizer;
        }

        /// <summary>How many bindings are active.</summary>
        public int ActiveCount => Volatile.Read(ref _activeCount);

        /// <summary>Adds a binding of a key, which receives text through <paramref name="invoker"/>, or characters through <paramref name="characterInvoker"/>.</summary>
        public void Add(in EntryKey key, object target, Delegate apply, BindingInvoker invoker, CharacterInvoker characterInvoker, out int index, out int generation)
        {
            // Held before the binding exists, so a table loading within the call can't refresh it before its first text.
            _localizer.AcquireTable(key.Table);
            lock (_lockObject)
            {
                index = AddSlot(in key, target, apply, invoker, characterInvoker, null);
                generation = _slots[index].Generation;
            }
        }

        /// <summary>Adds a binding of a message, which receives text through <paramref name="invoker"/>, or characters through <paramref name="characterInvoker"/>.</summary>
        public void Add(in EntryMessage message, object target, Delegate apply, BindingInvoker invoker, CharacterInvoker characterInvoker, out int index, out int generation)
        {
            _localizer.AcquireTable(message.Key.Table);
            lock (_lockObject)
            {
                BoundMessage bound = RentMessage();
                bound.Value = message;
                index = AddSlot(message.Key, target, apply, invoker, characterInvoker, bound);
                generation = _slots[index].Generation;
            }
        }

        /// <summary>Releases a binding. Returns false for a handle that is stale, copied after release, or never valid.</summary>
        public bool Release(int index, int generation)
        {
            ulong tableHash;
            lock (_lockObject)
            {
                if ((uint)index >= (uint)_highWater)
                {
                    return false;
                }
                ref Slot slot = ref _slots[index];
                if (!slot.IsActive || slot.Generation != generation)
                {
                    return false;
                }
                tableHash = slot.Key.Table.Hash;
                ReleaseSlot(index);
            }
            _localizer.ReleaseTable(tableHash);
            return true;
        }

        public bool IsActive(int index, int generation)
        {
            lock (_lockObject)
            {
                return (uint)index < (uint)_highWater && _slots[index].IsActive && _slots[index].Generation == generation;
            }
        }

        /// <summary>
        /// Makes a binding show <paramref name="message"/>, formatting it right away when it differs from what the
        /// binding shows. From another thread, the change is queued for the host thread.
        /// </summary>
        public void SetMessage(int index, int generation, in EntryMessage message)
        {
            if (!_localizer.IsOnHostThread)
            {
                QueueSetMessage(index, generation, message);
                return;
            }
            SetMessageOnHost(index, generation, in message);
        }

        // A method of its own, because a lambda in SetMessage would make every call allocate its closure, even on the host thread.
        private void QueueSetMessage(int index, int generation, EntryMessage message)
        {
            _localizer.RunOnHostLater(() => SetMessageOnHost(index, generation, in message));
        }

        /// <summary>Applies the current text to one binding. Host thread only.</summary>
        public void Apply(int index, int generation)
        {
            Pending pending;
            lock (_lockObject)
            {
                RecycleMessages();
                if ((uint)index >= (uint)_highWater || !_slots[index].IsActive || _slots[index].Generation != generation)
                {
                    return;
                }
                pending = new Pending(index, in _slots[index]);
                _slots[index].HasText = true;
            }
            Invoke(in pending);
        }

        /// <summary>Applies the current text to every active binding, in the order they were added. Host thread only.</summary>
        public void RefreshAll()
        {
            Refresh(0, true);
        }

        /// <summary>Applies the current text to every active binding of one table, as when it arrives. Host thread only.</summary>
        public void RefreshTable(ulong tableHash)
        {
            Refresh(tableHash, false);
        }

        private void Refresh(ulong tableHash, bool isEveryTable)
        {
            Pending[] buffer;
            int count = 0;
            lock (_lockObject)
            {
                RecycleMessages();
                // A callback that switches the language refreshes again while this refresh is running, so a nested
                // refresh takes a buffer of its own instead of overwriting the one being walked.
                buffer = _isRefreshing ? new Pending[Math.Max(_activeCount, 1)] : EnsureRefreshBuffer(_activeCount);
                for (int i = 0; i < _highWater; i++)
                {
                    ref Slot slot = ref _slots[i];
                    if (slot.IsActive && (isEveryTable || slot.Key.Table.Hash == tableHash))
                    {
                        buffer[count] = new Pending(i, in slot);
                        slot.HasText = true;
                        count++;
                    }
                }
            }
            bool isOutermost = !_isRefreshing;
            _isRefreshing = true;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Pending pending = buffer[i];
                    // Drop the copied references right away, so the buffer never keeps a target alive.
                    buffer[i] = default;
                    if (IsActive(pending.Index, pending.Generation))
                    {
                        Invoke(in pending);
                    }
                }
            }
            finally
            {
                if (isOutermost)
                {
                    _isRefreshing = false;
                }
            }
        }

        /// <summary>Releases every binding, as when the localizer is disposed.</summary>
        public void Clear()
        {
            lock (_lockObject)
            {
                for (int i = 0; i < _highWater; i++)
                {
                    if (_slots[i].IsActive)
                    {
                        ReleaseSlot(i);
                    }
                }
                _activeCount = 0;
            }
        }

        private int AddSlot(in EntryKey key, object target, Delegate apply, BindingInvoker invoker, CharacterInvoker characterInvoker, BoundMessage message)
        {
            int index;
            if (_freeHead >= 0)
            {
                index = _freeHead;
                _freeHead = _slots[index].NextFree;
            }
            else
            {
                if (_highWater == _slots.Length)
                {
                    Array.Resize(ref _slots, _slots.Length * 2);
                }
                index = _highWater;
                _highWater++;
            }
            ref Slot slot = ref _slots[index];
            slot.IsActive = true;
            slot.HasText = false;
            slot.NextFree = -1;
            slot.Key = key;
            slot.Target = target;
            slot.Apply = apply;
            slot.Invoker = invoker;
            slot.CharacterInvoker = characterInvoker;
            slot.Message = message;
            _activeCount++;
            return index;
        }

        private void ReleaseSlot(int index)
        {
            ref Slot slot = ref _slots[index];
            slot.IsActive = false;
            slot.Generation++;
            slot.Key = default;
            slot.Target = null;
            slot.Apply = null;
            slot.Invoker = null;
            slot.CharacterInvoker = null;
            if (slot.Message != null)
            {
                // The host thread may be formatting this message right now; it recycles the holder once it isn't.
                slot.Message.NextFree = _retiredMessages;
                _retiredMessages = slot.Message;
                slot.Message = null;
            }
            slot.NextFree = _freeHead;
            _freeHead = index;
            _activeCount--;
        }

        private void SetMessageOnHost(int index, int generation, in EntryMessage message)
        {
            ulong previousTable;
            lock (_lockObject)
            {
                if (!IsActiveLocked(index, generation))
                {
                    return;
                }
                ref Slot slot = ref _slots[index];
                if (slot.Message != null && slot.Message.Value.Equals(message))
                {
                    return;
                }
                previousTable = slot.Key.Table.Hash;
            }
            // A message from another table holds that table instead, outside the lock, as holding may load it.
            bool isOtherTable = previousTable != message.Key.Table.Hash;
            if (isOtherTable)
            {
                _localizer.AcquireTable(message.Key.Table);
            }
            lock (_lockObject)
            {
                if (!IsActiveLocked(index, generation))
                {
                    // Released meanwhile, along with what it held.
                    if (isOtherTable)
                    {
                        _localizer.ReleaseTable(message.Key.Table.Hash);
                    }
                    return;
                }
                ref Slot slot = ref _slots[index];
                slot.Message ??= RentMessage();
                slot.Message.Value = message;
                slot.Key = message.Key;
            }
            if (isOtherTable)
            {
                _localizer.ReleaseTable(previousTable);
            }
            Apply(index, generation);
        }

        private bool IsActiveLocked(int index, int generation) =>
            (uint)index < (uint)_highWater && _slots[index].IsActive && _slots[index].Generation == generation;

        private BoundMessage RentMessage()
        {
            BoundMessage message = _freeMessages;
            if (message == null)
            {
                return new BoundMessage();
            }
            _freeMessages = message.NextFree;
            message.NextFree = null;
            return message;
        }

        /// <summary>Moves released holders to the free list. Host thread only, and never while a message is formatted.</summary>
        private void RecycleMessages()
        {
            while (_retiredMessages != null)
            {
                BoundMessage message = _retiredMessages;
                _retiredMessages = message.NextFree;
                message.Value = default;
                message.NextFree = _freeMessages;
                _freeMessages = message;
            }
        }

        private void Invoke(in Pending pending)
        {
            if (_localizer.IsTargetDestroyed(pending.Target))
            {
                if (Release(pending.Index, pending.Generation))
                {
                    _localizer.ReportDestroyedTarget(pending.Key);
                }
                return;
            }
            if (pending.CharacterInvoker != null)
            {
                InvokeWithCharacters(in pending);
                return;
            }
            string text = pending.Message != null
                ? _localizer.ResolveBindingText(in pending.Message.Value)
                : _localizer.ResolveBindingText(pending.Key);
            if (text == null)
            {
                // Its table is still loading: the binding keeps what it shows, which is nothing before its first text.
                if (pending.HadText)
                {
                    return;
                }
                text = string.Empty;
            }
            try
            {
                pending.Invoker(pending.Target, pending.Apply, text);
            }
            catch (Exception exception)
            {
                // One failing callback must not stop the others from receiving their text.
                _localizer.ReportBindingFailure(pending.Key, pending.Target, exception);
            }
        }

        private void InvokeWithCharacters(in Pending pending)
        {
            // A callback that makes other bindings update, as by switching the language, formats theirs one level deeper,
            // so the characters it was given stay intact until it returns.
            int depth = _characterDepth;
            if (depth == _characterBuffers.Length)
            {
                Array.Resize(ref _characterBuffers, depth * 2);
            }
            char[] buffer = _characterBuffers[depth] ??= new char[256];
            bool isResolved = pending.Message != null
                ? _localizer.TryResolveBindingCharacters(in pending.Message.Value, ref buffer, out ReadOnlyMemory<char> characters)
                : _localizer.TryResolveBindingCharacters(pending.Key, ref buffer, out characters);
            _characterBuffers[depth] = buffer;
            if (!isResolved)
            {
                if (pending.HadText)
                {
                    return;
                }
                characters = ReadOnlyMemory<char>.Empty;
            }
            _characterDepth++;
            try
            {
                pending.CharacterInvoker(pending.Target, pending.Apply, characters);
            }
            catch (Exception exception)
            {
                _localizer.ReportBindingFailure(pending.Key, pending.Target, exception);
            }
            finally
            {
                _characterDepth--;
            }
        }

        private Pending[] EnsureRefreshBuffer(int count)
        {
            if (_refreshBuffer.Length < count)
            {
                _refreshBuffer = new Pending[Math.Max(count, _refreshBuffer.Length * 2)];
            }
            return _refreshBuffer;
        }

        /// <summary>Holds the arguments of a message binding, outside the slots, so plain bindings stay small.</summary>
        private sealed class BoundMessage
        {
            public EntryMessage Value;
            public BoundMessage NextFree;
        }

        private struct Slot
        {
            public int Generation;
            public bool IsActive;
            // Whether the binding received a text, which it then keeps while its table loads.
            public bool HasText;
            public int NextFree;
            public EntryKey Key;
            public object Target;
            public Delegate Apply;
            public BindingInvoker Invoker;
            public CharacterInvoker CharacterInvoker;
            public BoundMessage Message;
        }

        private readonly struct Pending
        {
            public Pending(int index, in Slot slot)
            {
                Index = index;
                Generation = slot.Generation;
                Key = slot.Key;
                Target = slot.Target;
                Apply = slot.Apply;
                Invoker = slot.Invoker;
                CharacterInvoker = slot.CharacterInvoker;
                Message = slot.Message;
                HadText = slot.HasText;
            }

            public int Index { get; }

            public int Generation { get; }

            public EntryKey Key { get; }

            public object Target { get; }

            public Delegate Apply { get; }

            public BindingInvoker Invoker { get; }

            public CharacterInvoker CharacterInvoker { get; }

            public BoundMessage Message { get; }

            public bool HadText { get; }
        }
    }
}
