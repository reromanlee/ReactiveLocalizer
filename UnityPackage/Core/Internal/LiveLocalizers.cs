using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// Every localizer not yet disposed, held weakly, so the editor can reload them all when a table file changes
    /// without keeping alive one that nothing else uses.
    /// </summary>
    internal static class LiveLocalizers
    {
        private static readonly object LockObject = new();
        private static readonly List<WeakReference<Localizer>> Localizers = new();

        public static void Add(Localizer localizer)
        {
            lock (LockObject)
            {
                Localizers.Add(new WeakReference<Localizer>(localizer));
            }
        }

        public static void Remove(Localizer localizer)
        {
            lock (LockObject)
            {
                Localizers.RemoveAll(reference => !reference.TryGetTarget(out Localizer live) || live == localizer);
            }
        }

        /// <summary>Copies the localizers still alive into <paramref name="localizers"/>, forgetting the collected ones.</summary>
        public static void CopyTo(List<Localizer> localizers)
        {
            lock (LockObject)
            {
                for (int i = Localizers.Count - 1; i >= 0; i--)
                {
                    if (Localizers[i].TryGetTarget(out Localizer live))
                    {
                        localizers.Add(live);
                    }
                    else
                    {
                        Localizers.RemoveAt(i);
                    }
                }
            }
        }
    }
}
