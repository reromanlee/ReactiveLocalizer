namespace reromanlee.ReactiveLocalizer.Hosting
{
    /// <summary>
    /// Delivers compiled catalogs and tables to a localizer: from inside a build, from files, from Addressables, from a
    /// mods folder, or from anywhere else a project keeps them.
    /// </summary>
    public interface ITableSource
    {
        /// <summary>
        /// Starts delivering what <paramref name="request"/> asks for, or returns false at once when this source
        /// doesn't have it, so the next source is asked.
        /// </summary>
        /// <remarks>
        /// After returning true, the source calls <see cref="TableReceiver.Receive"/> or
        /// <see cref="TableReceiver.Fail"/> exactly once, from any thread, either before returning or later.
        /// </remarks>
        bool TryLoad(in TableRequest request, TableReceiver receiver);
    }
}
