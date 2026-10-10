using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// Counts what holds each table of one localizer: bindings of its entries, and <see cref="TableHandle"/>s. A table
    /// loaded on demand stays loaded while anything holds it.
    /// </summary>
    /// <remarks>
    /// Safe from any thread. Handles live in reusable slots like bindings do, so holding and releasing allocate
    /// nothing once each table has been held once. A slot's generation changes on release, which turns a stale or
    /// copied handle into a harmless no-op.
    /// </remarks>
    internal sealed class TableHolds
    {
        private readonly object _lockObject = new();
        private readonly Dictionary<ulong, Hold> _holds = new();
        private readonly List<ulong> _released = new();
        private HandleSlot[] _handles = new HandleSlot[8];
        private int _highWater;
        private int _freeHead = -1;
        private int _waiterCount;

        /// <summary>Holds a table once more. Returns true when nothing held it before.</summary>
        public bool Acquire(TableKey table)
        {
            lock (_lockObject)
            {
                return AcquireLocked(table);
            }
        }

        /// <summary>Holds a table once less. Returns true when nothing holds it anymore.</summary>
        public bool Release(ulong tableHash)
        {
            lock (_lockObject)
            {
                return ReleaseLocked(tableHash);
            }
        }

        public bool IsHeld(ulong tableHash)
        {
            lock (_lockObject)
            {
                return _holds.TryGetValue(tableHash, out Hold hold) && hold.Count > 0;
            }
        }

        /// <summary>Holds a table through a handle, returning its slot; <paramref name="isFirst"/> tells whether nothing held it before.</summary>
        public int AcquireHandle(TableKey table, out int generation, out bool isFirst)
        {
            lock (_lockObject)
            {
                int index;
                if (_freeHead >= 0)
                {
                    index = _freeHead;
                    _freeHead = _handles[index].NextFree;
                }
                else
                {
                    if (_highWater == _handles.Length)
                    {
                        Array.Resize(ref _handles, _handles.Length * 2);
                    }
                    index = _highWater;
                    _highWater++;
                }
                ref HandleSlot slot = ref _handles[index];
                slot.IsActive = true;
                slot.TableHash = table.Hash;
                slot.NextFree = -1;
                generation = slot.Generation;
                isFirst = AcquireLocked(table);
                _holds[table.Hash].HandleCount++;
                return index;
            }
        }

        /// <summary>Releases a handle. Returns false for a stale, copied or default handle; <paramref name="isLast"/> tells whether nothing holds its table anymore.</summary>
        public bool ReleaseHandle(int index, int generation, out ulong tableHash, out bool isLast)
        {
            lock (_lockObject)
            {
                isLast = false;
                if (!IsActiveLocked(index, generation))
                {
                    tableHash = 0;
                    return false;
                }
                ref HandleSlot slot = ref _handles[index];
                tableHash = slot.TableHash;
                slot.IsActive = false;
                slot.Generation++;
                slot.TableHash = 0;
                slot.NextFree = _freeHead;
                _freeHead = index;
                if (_holds.TryGetValue(tableHash, out Hold hold) && hold.HandleCount > 0)
                {
                    hold.HandleCount--;
                }
                isLast = ReleaseLocked(tableHash);
                return true;
            }
        }

        /// <summary>Returns the table a handle holds, when the handle is still active.</summary>
        public bool TryGetHandleTable(int index, int generation, out ulong tableHash)
        {
            lock (_lockObject)
            {
                if (!IsActiveLocked(index, generation))
                {
                    tableHash = 0;
                    return false;
                }
                tableHash = _handles[index].TableHash;
                return true;
            }
        }

        /// <summary>Queues a table nothing holds anymore, for the host to unload unless something holds it again first.</summary>
        public void QueueRelease(ulong tableHash)
        {
            lock (_lockObject)
            {
                if (_holds.TryGetValue(tableHash, out Hold hold) && !hold.IsReleaseQueued)
                {
                    hold.IsReleaseQueued = true;
                    _released.Add(tableHash);
                }
            }
        }

        /// <summary>Moves every queued release into <paramref name="tableHashes"/>.</summary>
        public void TakeReleased(List<ulong> tableHashes)
        {
            lock (_lockObject)
            {
                for (int i = 0; i < _released.Count; i++)
                {
                    if (_holds.TryGetValue(_released[i], out Hold hold))
                    {
                        hold.IsReleaseQueued = false;
                    }
                    tableHashes.Add(_released[i]);
                }
                _released.Clear();
            }
        }

        /// <summary>Copies the key of every table a handle holds into <paramref name="tables"/>.</summary>
        public void CopyHeldByHandles(List<TableKey> tables)
        {
            lock (_lockObject)
            {
                foreach (Hold hold in _holds.Values)
                {
                    if (hold.HandleCount > 0)
                    {
                        tables.Add(hold.Table);
                    }
                }
            }
        }

        /// <summary>
        /// Returns the task that completes once the held table is loaded, or once it can't be; null when nothing has
        /// ever held the table.
        /// </summary>
        public Task<bool> GetLoadedTask(ulong tableHash)
        {
            lock (_lockObject)
            {
                if (!_holds.TryGetValue(tableHash, out Hold hold))
                {
                    return null;
                }
                if (hold.Loaded == null)
                {
                    hold.Loaded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    _waiterCount++;
                }
                return hold.Loaded.Task;
            }
        }

        /// <summary>Completes the loaded task of one table with <paramref name="result"/>, when anyone waits for it.</summary>
        public void CompleteLoaded(ulong tableHash, bool result)
        {
            TaskCompletionSource<bool> completion = null;
            lock (_lockObject)
            {
                if (_holds.TryGetValue(tableHash, out Hold hold) && hold.Loaded != null)
                {
                    completion = hold.Loaded;
                    hold.Loaded = null;
                    _waiterCount--;
                }
            }
            completion?.TrySetResult(result);
        }

        /// <summary>Completes the loaded task of every table with <paramref name="result"/>, as when initialization fails.</summary>
        public void CompleteAllLoaded(bool result)
        {
            CompleteLoaded(result ? (Func<ulong, bool?>)(_ => true) : _ => false);
        }

        /// <summary>
        /// Completes the loaded task of every table <paramref name="getResult"/> has a result for: true when it was
        /// loaded, false when it couldn't be. Returns right away while no one waits.
        /// </summary>
        public void CompleteLoaded(Func<ulong, bool?> getResult)
        {
            List<(TaskCompletionSource<bool> Completion, bool Result)> completed = null;
            lock (_lockObject)
            {
                if (_waiterCount == 0)
                {
                    return;
                }
                foreach (KeyValuePair<ulong, Hold> pair in _holds)
                {
                    if (pair.Value.Loaded == null)
                    {
                        continue;
                    }
                    bool? result = getResult(pair.Key);
                    if (result.HasValue)
                    {
                        completed ??= new List<(TaskCompletionSource<bool>, bool)>();
                        completed.Add((pair.Value.Loaded, result.Value));
                        pair.Value.Loaded = null;
                        _waiterCount--;
                    }
                }
            }
            // Outside the lock, although continuations run asynchronously anyway.
            for (int i = 0; i < (completed?.Count ?? 0); i++)
            {
                completed[i].Completion.TrySetResult(completed[i].Result);
            }
        }

        /// <summary>Releases every handle and completes every loaded task with false, as when the localizer is disposed.</summary>
        public void Clear()
        {
            List<TaskCompletionSource<bool>> waiting = new();
            lock (_lockObject)
            {
                for (int i = 0; i < _highWater; i++)
                {
                    if (_handles[i].IsActive)
                    {
                        _handles[i].IsActive = false;
                        _handles[i].Generation++;
                        _handles[i].NextFree = _freeHead;
                        _freeHead = i;
                    }
                }
                foreach (Hold hold in _holds.Values)
                {
                    if (hold.Loaded != null)
                    {
                        waiting.Add(hold.Loaded);
                    }
                }
                _holds.Clear();
                _released.Clear();
                _waiterCount = 0;
            }
            for (int i = 0; i < waiting.Count; i++)
            {
                waiting[i].TrySetResult(false);
            }
        }

        private bool AcquireLocked(TableKey table)
        {
            Hold hold = GetOrAddLocked(table);
            hold.Count++;
            return hold.Count == 1;
        }

        private bool ReleaseLocked(ulong tableHash)
        {
            if (!_holds.TryGetValue(tableHash, out Hold hold) || hold.Count == 0)
            {
                return false;
            }
            hold.Count--;
            return hold.Count == 0;
        }

        private Hold GetOrAddLocked(TableKey table)
        {
            if (!_holds.TryGetValue(table.Hash, out Hold hold))
            {
                // Records stay once created, so holding a table again allocates nothing.
                hold = new Hold(table);
                _holds.Add(table.Hash, hold);
            }
            return hold;
        }

        private bool IsActiveLocked(int index, int generation) =>
            (uint)index < (uint)_highWater && _handles[index].IsActive && _handles[index].Generation == generation;

        private sealed class Hold
        {
            public readonly TableKey Table;
            public int Count;
            public int HandleCount;
            public bool IsReleaseQueued;
            public TaskCompletionSource<bool> Loaded;

            public Hold(TableKey table)
            {
                Table = table;
            }
        }

        private struct HandleSlot
        {
            public int Generation;
            public bool IsActive;
            public ulong TableHash;
            public int NextFree;
        }
    }
}
