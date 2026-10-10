using System;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// Keeps a table loaded until disposed, as returned by <see cref="ILocalizer.HoldTable"/>. A table set to
    /// <c>@loading OnDemand</c> is loaded while anything holds it and unloaded once nothing does; holding any other
    /// table changes nothing, so code never needs to know how a table loads.
    /// <code>
    /// using TableHandle dialogue = localizer.HoldTable(LocalizationKeys.Dialogue.TableKey);
    /// await dialogue.WhenLoaded;
    /// </code>
    /// </summary>
    /// <remarks>
    /// A struct, so holding allocates nothing. Disposing it again, or disposing a copy of an already disposed handle,
    /// is a safe no-op. The default value is an inactive handle.
    /// </remarks>
    public readonly struct TableHandle : IDisposable
    {
        private readonly Localizer _localizer;
        private readonly int _index;
        private readonly int _generation;

        internal TableHandle(Localizer localizer, int index, int generation)
        {
            _localizer = localizer;
            _index = index;
            _generation = generation;
        }

        /// <summary>Whether the handle still holds its table: neither disposed nor released with its localizer.</summary>
        public bool IsActive => _localizer != null && _localizer.IsHandleActive(_index, _generation);

        /// <summary>Whether the table's text can be read right now, in the current language or a fallback.</summary>
        public bool IsLoaded => _localizer != null && _localizer.IsHandleLoaded(_index, _generation);

        /// <summary>
        /// Completes once the table can be read: true when it loaded; false when it couldn't be loaded in any language,
        /// isn't a table of the catalog, or stopped being held before it loaded. Never fails.
        /// </summary>
        public Task<bool> WhenLoaded => _localizer != null ? _localizer.GetHandleLoaded(_index, _generation) : Localizer.NotLoaded;

        /// <summary>Stops holding the table. Safe to call any number of times, from any thread.</summary>
        public void Dispose()
        {
            _localizer?.ReleaseHandle(_index, _generation);
        }
    }
}
