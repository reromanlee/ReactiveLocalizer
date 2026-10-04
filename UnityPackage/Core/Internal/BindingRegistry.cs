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
    /// whenever it is released, which turns a stale or copied handle into a harmless no-op.
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

        public BindingRegistry(Localizer localizer)
        {
            _localizer = localizer;
        }

        /// <summary>How many bindings are active.</summary>
        public int ActiveCount => Volatile.Read(ref _activeCount);

        public void Add(in EntryKey key, object target, Delegate apply, BindingInvoker invoker, out int index, out int generation)
        {
            lock (_lockObject)
            {
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
                slot.NextFree = -1;
                slot.Key = key;
                slot.Target = target;
                slot.Apply = apply;
                slot.Invoker = invoker;
                generation = slot.Generation;
                _activeCount++;
            }
        }

        /// <summary>Releases a binding. Returns false for a handle that is stale, copied after release, or never valid.</summary>
        public bool Release(int index, int generation)
        {
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
                slot.IsActive = false;
                slot.Generation++;
                slot.Key = default;
                slot.Target = null;
                slot.Apply = null;
                slot.Invoker = null;
                slot.NextFree = _freeHead;
                _freeHead = index;
                _activeCount--;
                return true;
            }
        }

        public bool IsActive(int index, int generation)
        {
            lock (_lockObject)
            {
                return (uint)index < (uint)_highWater && _slots[index].IsActive && _slots[index].Generation == generation;
            }
        }

        /// <summary>Applies the current text to one binding. Host thread only.</summary>
        public void Apply(int index, int generation)
        {
            Pending pending;
            lock (_lockObject)
            {
                if ((uint)index >= (uint)_highWater || !_slots[index].IsActive || _slots[index].Generation != generation)
                {
                    return;
                }
                pending = new Pending(index, in _slots[index]);
            }
            Invoke(in pending);
        }

        /// <summary>Applies the current text to every active binding, in the order they were added. Host thread only.</summary>
        public void RefreshAll()
        {
            Pending[] buffer;
            int count = 0;
            lock (_lockObject)
            {
                // A callback that switches the language refreshes again while this refresh is running, so a nested
                // refresh takes a buffer of its own instead of overwriting the one being walked.
                buffer = _isRefreshing ? new Pending[Math.Max(_activeCount, 1)] : EnsureRefreshBuffer(_activeCount);
                for (int i = 0; i < _highWater; i++)
                {
                    if (_slots[i].IsActive)
                    {
                        buffer[count] = new Pending(i, in _slots[i]);
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
                    ref Slot slot = ref _slots[i];
                    if (slot.IsActive)
                    {
                        slot.IsActive = false;
                        slot.Generation++;
                        slot.Key = default;
                        slot.Target = null;
                        slot.Apply = null;
                        slot.Invoker = null;
                        slot.NextFree = _freeHead;
                        _freeHead = i;
                    }
                }
                _activeCount = 0;
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
            string text = _localizer.ResolveBindingText(pending.Key);
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

        private Pending[] EnsureRefreshBuffer(int count)
        {
            if (_refreshBuffer.Length < count)
            {
                _refreshBuffer = new Pending[Math.Max(count, _refreshBuffer.Length * 2)];
            }
            return _refreshBuffer;
        }

        private struct Slot
        {
            public int Generation;
            public bool IsActive;
            public int NextFree;
            public EntryKey Key;
            public object Target;
            public Delegate Apply;
            public BindingInvoker Invoker;
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
            }

            public int Index { get; }

            public int Generation { get; }

            public EntryKey Key { get; }

            public object Target { get; }

            public Delegate Apply { get; }

            public BindingInvoker Invoker { get; }
        }
    }
}
