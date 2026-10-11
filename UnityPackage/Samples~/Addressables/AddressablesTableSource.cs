using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using reromanlee.ReactiveLocalizer.Unity;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace reromanlee.ReactiveLocalizer.Samples.Addressables
{
    /// <summary>
    /// Delivers the tables a source-language file marks <c>@delivery Custom</c> through Addressables, so they load,
    /// download and update with the project's other Addressables content. Builds don't pack such tables.
    /// </summary>
    /// <remarks>
    /// Each table file is an Addressable at <see cref="GetAddress"/>, which <c>Tools/ReactiveLocalizer/Samples/Mark Custom
    /// Tables Addressable</c> sets up. A table Addressables don't have is passed on to the next source without a problem,
    /// so a language that lacks a file falls back as usual. The source installs itself into Quick Start's
    /// <see cref="GlobalLocalizer"/> before the first scene loads; in the editor, tables nobody marked still load from
    /// their files.
    /// </remarks>
    public sealed class AddressablesTableSource : ITableSource
    {
        /// <summary>The folder every table's address starts with.</summary>
        public const string AddressRoot = "ReactiveLocalizer";

        private static readonly AddressablesTableSource Shared = new();

        /// <summary>Returns the address of one table in one language, such as <c>ReactiveLocalizer/Game/Russian/Dialogue</c>.</summary>
        public static string GetAddress(string catalogName, string languageName, string tableName) => $"{AddressRoot}/{catalogName}/{languageName}/{tableName}";

        /// <inheritdoc/>
        /// <remarks>Main thread only, like Addressables; the answer comes once Addressables found and loaded the table.</remarks>
        public bool TryLoad(in TableRequest request, TableReceiver receiver)
        {
            if (request.IsCatalog || request.Delivery != TableDelivery.Custom || receiver == null)
            {
                return false;
            }
            string address = GetAddress(request.Catalog.Name, request.Language.Name, request.Table.Name);
            // Whether Addressables have the table is known only once they initialized, so the answer waits for them.
            AsyncOperationHandle<IList<IResourceLocation>> locations = UnityEngine.AddressableAssets.Addressables.LoadResourceLocationsAsync(address, typeof(TableAsset));
            locations.Completed += handle => Load(handle, receiver, address);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            GlobalLocalizer.AddTableSources(_ => Shared);
        }

        private static void Load(AsyncOperationHandle<IList<IResourceLocation>> located, TableReceiver receiver, string address)
        {
            IResourceLocation location = located.Status == AsyncOperationStatus.Succeeded && located.Result != null && located.Result.Count > 0
                ? located.Result[0]
                : null;
            UnityEngine.AddressableAssets.Addressables.Release(located);
            if (location == null)
            {
                receiver.Decline();
                return;
            }
            AsyncOperationHandle<TableAsset> loading = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TableAsset>(location);
            loading.Completed += handle => Deliver(handle, receiver, address);
        }

        private static void Deliver(AsyncOperationHandle<TableAsset> loaded, TableReceiver receiver, string address)
        {
            TableAsset asset = loaded.Status == AsyncOperationStatus.Succeeded ? loaded.Result : null;
            if (asset != null && !asset.Data.IsEmpty)
            {
                // Read on the spot, since Addressables answer on the main thread, so the asset can go right after.
                receiver.Receive(asset.Data);
            }
            else
            {
                receiver.Fail($"Addressables couldn't load {address}: {loaded.OperationException?.Message ?? "its table has no data; reimport its file."}");
            }
            UnityEngine.AddressableAssets.Addressables.Release(loaded);
        }
    }
}
