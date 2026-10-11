using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Samples.UIToolkit
{
    /// <summary>
    /// Binds UI Toolkit text elements to entries from code, on every Unity version:
    /// <code>
    /// root.Q&lt;Label&gt;("Title").BindText(QuickStartKeys.Demo.Title, QuickStartKeys.CatalogKey);
    /// </code>
    /// </summary>
    public static class ElementTextBindings
    {
        // An element shows one entry at a time; the table forgets elements nothing else holds.
        private static readonly ConditionalWeakTable<TextElement, ElementTextBinding> Bound = new();

        /// <summary>
        /// Shows <paramref name="key"/> of <paramref name="catalog"/> in <paramref name="element"/> while it's in a
        /// panel, replacing an entry it was bound to before. An empty catalog means Quick Start's default catalog.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="element"/> is null.</exception>
        public static ElementTextBinding BindText(this TextElement element, in EntryKey key, CatalogKey catalog = default)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }
            if (Bound.TryGetValue(element, out ElementTextBinding previous))
            {
                previous.Dispose();
            }
            ElementTextBinding binding = new(element, in key, catalog);
            Bound.AddOrUpdate(element, binding);
            return binding;
        }

        /// <summary>Shows <paramref name="entry"/> in <paramref name="element"/> while it's in a panel, as an Inspector field picked it.</summary>
        public static ElementTextBinding BindText(this TextElement element, EntryReference entry)
        {
            return BindText(element, entry.ToKey(), entry.ToCatalogKey());
        }

        /// <summary>Stops the entry <paramref name="element"/> shows, if any.</summary>
        public static void UnbindText(this TextElement element)
        {
            if (element != null && Bound.TryGetValue(element, out ElementTextBinding binding))
            {
                binding.Dispose();
            }
        }

        internal static void Forget(ElementTextBinding binding)
        {
            if (Bound.TryGetValue(binding.Element, out ElementTextBinding current) && current == binding)
            {
                Bound.Remove(binding.Element);
            }
        }
    }
}
