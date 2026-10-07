using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// A host whose thread is the test's thread, unless a test says otherwise, and whose scheduled updates run only
    /// when the test runs them, so every step of the localizer can be observed.
    /// </summary>
    internal sealed class TestHost : ILocalizerHost
    {
        private readonly object _lockObject = new();
        private readonly int _hostThreadId = Thread.CurrentThread.ManagedThreadId;
        private readonly List<Action> _scheduledUpdates = new();

        public TestHost(params ITableSource[] sources)
        {
            Sources.AddRange(sources);
        }

        public List<ITableSource> Sources { get; } = new();

        public List<LocalizerReport> Reports { get; } = new();

        public HashSet<object> DestroyedTargets { get; } = new();

        /// <summary>When false, even the test's thread counts as another thread, so work gets queued.</summary>
        public bool IsTestThreadTheHost { get; set; } = true;

        public int ScheduledUpdateCount
        {
            get
            {
                lock (_lockObject)
                {
                    return _scheduledUpdates.Count;
                }
            }
        }

        public IReadOnlyList<ITableSource> TableSources => Sources;

        public bool IsHostThread => IsTestThreadTheHost && Thread.CurrentThread.ManagedThreadId == _hostThreadId;

        public void ScheduleUpdate(Action update)
        {
            lock (_lockObject)
            {
                _scheduledUpdates.Add(update);
            }
        }

        public void Report(in LocalizerReport report)
        {
            lock (_lockObject)
            {
                Reports.Add(report);
            }
        }

        public bool IsDestroyed(object target) => DestroyedTargets.Contains(target);

        /// <summary>Runs the updates scheduled so far, as the host thread would.</summary>
        public void RunUpdates()
        {
            Action[] updates;
            lock (_lockObject)
            {
                updates = _scheduledUpdates.ToArray();
                _scheduledUpdates.Clear();
            }
            bool wasHost = IsTestThreadTheHost;
            IsTestThreadTheHost = true;
            for (int i = 0; i < updates.Length; i++)
            {
                updates[i]();
            }
            IsTestThreadTheHost = wasHost;
        }

        public int CountReports(ReportSeverity severity)
        {
            lock (_lockObject)
            {
                return Reports.FindAll(report => report.Severity == severity).Count;
            }
        }
    }
}
