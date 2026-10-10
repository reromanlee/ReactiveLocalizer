using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>A table of a catalog, with the settings its source-language file chose for it.</summary>
    public sealed class TableInfo
    {
        /// <summary>Creates a table definition.</summary>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
        public TableInfo(TableKey key, TableLoading loading, TableDelivery delivery) : this(key, loading, delivery, 0)
        {
        }

        /// <param name="key">Identity of the table.</param>
        /// <param name="loading">When the table is in memory.</param>
        /// <param name="delivery">How the table ships in builds.</param>
        /// <param name="keysHash">Identifies the keys of the source-language file; zero when unknown.</param>
        internal TableInfo(TableKey key, TableLoading loading, TableDelivery delivery, ulong keysHash)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A table needs a key.", nameof(key));
            }
            Key = key;
            Loading = loading;
            Delivery = delivery;
            KeysHash = keysHash;
        }

        /// <summary>Identity of the table.</summary>
        public TableKey Key { get; }

        /// <summary>When the table is in memory.</summary>
        public TableLoading Loading { get; }

        /// <summary>How the table ships in builds.</summary>
        public TableDelivery Delivery { get; }

        /// <summary>
        /// Identifies the keys of the source-language file, so a translation compiled with every one of them is known
        /// to need no fallback language. Zero when unknown, which makes every translation load its fallbacks.
        /// </summary>
        internal ulong KeysHash { get; }

        /// <inheritdoc/>
        public override string ToString() => Key.Name;
    }
}
