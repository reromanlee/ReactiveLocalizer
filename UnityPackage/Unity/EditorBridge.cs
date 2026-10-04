using reromanlee.ReactiveLocalizer.Hosting;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Where the editor plugs into the runtime: the Editor assembly sets these when the editor loads, and builds
    /// leave them empty.
    /// </summary>
    internal static class EditorBridge
    {
        /// <summary>
        /// Reads tables straight from their imported files, always current, with no build step. Asked before the
        /// built-in sources; null in builds.
        /// </summary>
        public static ITableSource TableSource { get; set; }
    }
}
