using System;
using System.Collections.Generic;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Hosting
{
    /// <summary>
    /// Takes the answer to one <see cref="TableRequest"/>: a table source calls <see cref="Receive"/> with the compiled
    /// data, or <see cref="Fail"/> with the reason it couldn't deliver it.
    /// </summary>
    /// <remarks>
    /// The first call counts and every later one is ignored. Either can come from any thread, before
    /// <see cref="ITableSource.TryLoad"/> returns or long after.
    /// </remarks>
    public sealed class TableReceiver
    {
        private readonly Action<TableReceiver> _onCompleted;
        private readonly object _warningsLock = new();
        private List<string> _warnings;
        private int _isCompleted;

        internal TableReceiver(TableRequest request, Action<TableReceiver> onCompleted, object context)
        {
            Request = request;
            _onCompleted = onCompleted;
            Context = context;
        }

        /// <summary>What was asked for.</summary>
        public TableRequest Request { get; }

        /// <summary>Whether the request is already answered, so later calls would be ignored.</summary>
        public bool IsCompleted => Volatile.Read(ref _isCompleted) != 0;

        internal object Context { get; }

        internal ReadOnlyMemory<byte> Data { get; private set; }

        internal bool HasData { get; private set; }

        internal string FailureReason { get; private set; }

        /// <summary>
        /// Delivers the compiled data asked for. Empty data counts as a failure. The data must stay unchanged until it
        /// is read, which happens on the host thread: right away when delivered there, otherwise at the host's next
        /// update. Reading copies what is needed, so no reference to the data is kept afterwards.
        /// </summary>
        public void Receive(ReadOnlyMemory<byte> compiledData)
        {
            if (compiledData.IsEmpty)
            {
                Fail("The source delivered no data.");
                return;
            }
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }
            Data = compiledData;
            HasData = true;
            _onCompleted(this);
        }

        /// <summary>
        /// Reports a problem with what is about to be delivered that doesn't stop it, such as a line of a file that
        /// couldn't be read. Warnings reach the localizer's host along with the answer; ones given after it are ignored.
        /// </summary>
        public void Warn(string message)
        {
            if (string.IsNullOrEmpty(message) || IsCompleted)
            {
                return;
            }
            lock (_warningsLock)
            {
                (_warnings ??= new List<string>()).Add(message);
            }
        }

        /// <summary>The warnings given before the answer. Read on the host thread once the request is answered.</summary>
        internal IReadOnlyList<string> TakeWarnings()
        {
            lock (_warningsLock)
            {
                IReadOnlyList<string> warnings = (IReadOnlyList<string>)_warnings ?? Array.Empty<string>();
                _warnings = null;
                return warnings;
            }
        }

        /// <summary>Reports that the data can't be delivered, and why.</summary>
        public void Fail(string reason)
        {
            if (Interlocked.Exchange(ref _isCompleted, 1) != 0)
            {
                return;
            }
            FailureReason = string.IsNullOrEmpty(reason) ? "The source gave no reason." : reason;
            _onCompleted(this);
        }
    }
}
