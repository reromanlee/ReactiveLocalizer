using reromanlee.ReactiveLocalizer.Hosting;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Delivers the catalogs and <see cref="TableDelivery.Embedded"/> tables that the build step packed inside the
    /// build. Loading is synchronous on every platform, WebGL included, so initialization finishes within its call.
    /// </summary>
    /// <remarks>
    /// The packed data sits in Resources under <see cref="ResourcesRoot"/>. Each file is unloaded as soon as it is
    /// read, since the localizer keeps its own compact copy.
    /// </remarks>
    public sealed class EmbeddedTableSource : ITableSource
    {
        /// <summary>The Resources folder the build step packs into.</summary>
        public const string ResourcesRoot = "ReactiveLocalizer";

        /// <summary>Name of the packed catalog file within its catalog's folder.</summary>
        public const string CatalogFileName = "catalog";

        /// <inheritdoc/>
        /// <remarks>Main thread only, like every Resources load.</remarks>
        public bool TryLoad(in TableRequest request, TableReceiver receiver)
        {
            if (!request.IsCatalog && request.Delivery != TableDelivery.Embedded)
            {
                return false;
            }
            string path = request.IsCatalog
                ? GetCatalogPath(request.Catalog.Name)
                : GetTablePath(request.Catalog.Name, request.Language.Name, request.Table.Name);
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null)
            {
                return false;
            }
            byte[] data = asset.bytes;
            Resources.UnloadAsset(asset);
            receiver.Receive(data);
            return true;
        }

        /// <summary>Resources path of a packed catalog, without an extension.</summary>
        public static string GetCatalogPath(string catalogName) => $"{ResourcesRoot}/{catalogName}/{CatalogFileName}";

        /// <summary>Resources path of a packed table, without an extension.</summary>
        public static string GetTablePath(string catalogName, string languageName, string tableName) => $"{ResourcesRoot}/{catalogName}/{languageName}/{tableName}";
    }
}
