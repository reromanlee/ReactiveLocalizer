using System;
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
