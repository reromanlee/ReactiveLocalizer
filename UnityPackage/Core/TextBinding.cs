using reromanlee.ReactiveLocalizer.Internal;
using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The handle of a binding made by <see cref="ILocalizer.Bind{TTarget}"/>. Disposing it stops the binding; until
    /// then, its callback receives the entry's text every time the text changes.
    /// </summary>
    /// <remarks>
    /// A struct, so binding allocates nothing. Disposing it again, or disposing a copy of an already disposed
    /// handle, is a safe no-op. The default value is an inactive handle.
    /// </remarks>
    public readonly struct TextBinding : IDisposable
    {
        private readonly BindingRegistry _registry;
        private readonly int _index;
        private readonly int _generation;

        internal TextBinding(BindingRegistry registry, int index, int generation)
        {
            _registry = registry;
            _index = index;
            _generation = generation;
        }

        /// <summary>Whether the binding still receives text: neither disposed nor released with its localizer or target.</summary>
        public bool IsActive => _registry != null && _registry.IsActive(_index, _generation);

        /// <summary>
        /// Shows <paramref name="message"/> from now on: formats it right away when it differs from what the binding
        /// shows, and does nothing when it is equal, so calling it every frame with the same arguments costs nothing.
        /// </summary>
        /// <remarks>
        /// Works on bindings of keys too, which then show the message. From a thread other than the host's, the change
        /// is applied on the host's next update. A stale or default handle ignores it.
        /// </remarks>
        public void SetMessage(in EntryMessage message)
        {
            _registry?.SetMessage(_index, _generation, in message);
        }

        /// <summary>Stops the binding. Safe to call any number of times, from any thread.</summary>
        public void Dispose()
        {
            _registry?.Release(_index, _generation);
        }
    }
}
