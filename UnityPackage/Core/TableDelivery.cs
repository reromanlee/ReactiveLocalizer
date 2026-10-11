namespace reromanlee.ReactiveLocalizer
{
    /// <summary>How a table ships in builds, as its <c>@delivery</c> setting chooses.</summary>
    public enum TableDelivery
    {
        /// <summary>Packed inside the build, readable synchronously on every platform. The default.</summary>
        Embedded = 0,

        /// <summary>Shipped as separate files read on demand, which keeps huge content out of memory.</summary>
        Streaming = 1,

        /// <summary>
        /// Delivered by a table source of the project's own, such as Addressables or a server, so builds don't pack
        /// it. The editor still reads its files directly.
        /// </summary>
        Custom = 2
    }
}
