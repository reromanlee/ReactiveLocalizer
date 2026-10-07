using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Hosting
{
    /// <summary>
    /// Everything a <see cref="Localizer"/> needs from the platform it runs on. Unity's is <c>UnityHost</c>; a .NET
    /// server or a test provides its own.
    /// </summary>
    public interface ILocalizerHost
    {
        /// <summary>The table sources, asked in this order; the first that has what is asked for delivers it.</summary>
        IReadOnlyList<ITableSource> TableSources { get; }

        /// <summary>Whether the calling thread is the one the host applies changes and notifies bindings on.</summary>
        bool IsHostThread { get; }

        /// <summary>
        /// Asks the host to call <paramref name="update"/> once on its thread, soon. Called from any thread; a host may
        /// merge calls that arrive before the update runs.
        /// </summary>
        void ScheduleUpdate(Action update);

        /// <summary>Shows a problem the localizer found wherever the platform shows problems.</summary>
        void Report(in LocalizerReport report);

        /// <summary>
        /// Returns whether <paramref name="target"/> is an engine object that was destroyed, such as a Unity object
        /// after <c>Destroy</c>, so bindings to it can be released.
        /// </summary>
        bool IsDestroyed(object target);
    }
}
