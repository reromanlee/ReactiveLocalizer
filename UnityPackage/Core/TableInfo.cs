using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>A table of a catalog, with the settings its source-language file chose for it.</summary>
    public sealed class TableInfo
    {
        /// <summary>Creates a table definition.</summary>
        /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
        public TableInfo(TableKey key, TableLoading loading, TableDelivery delivery)
        {
            if (key.IsEmpty)
            {
                throw new ArgumentException("A table needs a key.", nameof(key));
            }
            Key = key;
            Loading = loading;
            Delivery = delivery;
        }

        /// <summary>Identity of the table.</summary>
        public TableKey Key { get; }

        /// <summary>When the table is in memory.</summary>
        public TableLoading Loading { get; }

        /// <summary>How the table ships in builds.</summary>
        public TableDelivery Delivery { get; }

        /// <inheritdoc/>
        public override string ToString() => Key.Name;
    }
}
