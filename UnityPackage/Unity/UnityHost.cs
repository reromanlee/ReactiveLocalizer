using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Runs a <see cref="Localizer"/> inside Unity: changes apply on the main thread before UI rebuilds, problems go to
    /// the Console with the object they concern, and tables come from the build, or straight from their files in the
    /// editor.
    /// <code>
    /// Localizer localizer = new Localizer(LocalizationKeys.CatalogKey, new UnityHost());
    /// </code>
    /// </summary>
    public sealed class UnityHost : ILocalizerHost
    {
        private const string LogPrefix = "[ReactiveLocalizer] ";

        private readonly ITableSource[] _sources;

        /// <summary>
        /// Creates a host that asks <paramref name="additionalSources"/> first, in order, then the built-in sources:
        /// the imported files in the editor, and in players the tables packed into the build and into StreamingAssets.
        /// </summary>
        /// <remarks>
        /// A source asked first also patches the languages the game ships, as a mods folder does with a fan
        /// translation: its entries come first, and the game's fill in the rest.
        /// </remarks>
        public UnityHost(params ITableSource[] additionalSources)
        {
            List<ITableSource> sources = new();
            if (additionalSources != null)
            {
                for (int i = 0; i < additionalSources.Length; i++)
                {
                    if (additionalSources[i] != null)
                    {
                        sources.Add(additionalSources[i]);
                    }
                }
            }
            if (EditorBridge.TableSource != null)
            {
                sources.Add(EditorBridge.TableSource);
            }
            sources.Add(new EmbeddedTableSource());
            sources.Add(new StreamingTableSource());
            _sources = sources.ToArray();
        }

        /// <inheritdoc/>
        public IReadOnlyList<ITableSource> TableSources => _sources;

        /// <inheritdoc/>
        /// <remarks>True on Unity's main thread.</remarks>
        public bool IsHostThread => UnityMainThread.IsCurrent;

        /// <inheritdoc/>
        /// <remarks>Runs at the start of the next frame's PreLateUpdate, or at the editor's next update in Edit Mode.</remarks>
        public void ScheduleUpdate(Action update)
        {
            FramePump.Schedule(update);
        }

        /// <inheritdoc/>
        /// <remarks>Logs to the Console; clicking the message selects the object it concerns, when it is a Unity object.</remarks>
        public void Report(in LocalizerReport report)
        {
            UnityEngine.Object context = report.Target as UnityEngine.Object;
            string message = LogPrefix + report.Message;
            if (report.Severity == ReportSeverity.Error)
            {
                Debug.LogError(message, context);
            }
            else
            {
                Debug.LogWarning(message, context);
            }
        }

        /// <inheritdoc/>
        /// <remarks>True for a Unity object after <c>Destroy</c>, which compares equal to null while C# still holds it.</remarks>
        public bool IsDestroyed(object target) => target is UnityEngine.Object unityObject && unityObject == null;
    }
}
