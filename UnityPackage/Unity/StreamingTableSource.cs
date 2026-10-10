using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
#if REACTIVELOCALIZER_WEB_REQUEST
using UnityEngine.Networking;
#endif

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Delivers the <see cref="TableDelivery.Streaming"/> tables that the build step put in StreamingAssets, each read
    /// when it is asked for, so huge content stays out of memory and, on WebGL, out of the initial download.
    /// </summary>
    /// <remarks>
    /// Reading never blocks the main thread: where StreamingAssets is a folder, a thread pool thread reads the file;
    /// where it is a URL, as on Android and WebGL, UnityWebRequest does, which needs the built-in Unity Web Request
    /// module. The build step lists every file it packs, so a table a language doesn't have is never requested.
    /// </remarks>
    public sealed class StreamingTableSource : ITableSource
    {
        /// <summary>The folder within StreamingAssets the build step packs into.</summary>
        public const string FolderName = "ReactiveLocalizer";

        /// <summary>
        /// Name of the list of packed files, kept with the catalog in Resources, so it can be read synchronously.
        /// Each line is <c>Language/Table</c>.
        /// </summary>
        public const string ManifestFileName = "streaming";

        private readonly string _root;
        // Per catalog hash, the packed tables by table and language hash; null when the catalog has no list.
        private readonly Dictionary<ulong, HashSet<(ulong Table, ulong Language)>> _manifests = new();
        private string _resolvedRoot;

        /// <summary>Reads the tables the build packed into StreamingAssets.</summary>
        public StreamingTableSource() : this(null)
        {
        }

        /// <param name="root">
        /// Where the catalogs' folders are, as a folder path or a URL, such as a server that mirrors the packed files;
        /// null for the build's StreamingAssets.
        /// </param>
        public StreamingTableSource(string root)
        {
            _root = root;
        }

        /// <inheritdoc/>
        /// <remarks>Main thread only, like every Resources load; the answer arrives later, from any thread.</remarks>
        public bool TryLoad(in TableRequest request, TableReceiver receiver)
        {
            if (request.IsCatalog || request.Delivery != TableDelivery.Streaming || !IsPacked(in request))
            {
                return false;
            }
            string root = _resolvedRoot ??= _root ?? Application.streamingAssetsPath + "/" + FolderName;
            string location = $"{root}/{request.Catalog.Name}/{request.Language.Name}/{request.Table.Name}.bytes";
            if (location.Contains("://"))
            {
                LoadFromUrl(location, receiver);
            }
            else if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                // Without threads, a folder can only be read right away.
                ReadFile(location, receiver);
            }
            else
            {
                ThreadPool.QueueUserWorkItem(_ => ReadFile(location, receiver));
            }
            return true;
        }

        /// <summary>Path of a packed table within <see cref="FolderName"/>, the way the build step writes it.</summary>
        public static string GetTablePath(string catalogName, string languageName, string tableName) => $"{catalogName}/{languageName}/{tableName}.bytes";

        /// <summary>Resources path of a catalog's list of packed files, without an extension.</summary>
        public static string GetManifestPath(string catalogName) => $"{EmbeddedTableSource.ResourcesRoot}/{catalogName}/{ManifestFileName}";

        /// <summary>Writes the list of packed files the way <see cref="StreamingTableSource"/> reads it.</summary>
        public static byte[] WriteManifest(IEnumerable<(string Language, string Table)> tables)
        {
            StringBuilder builder = new();
            foreach ((string Language, string Table) table in tables)
            {
                builder.Append(table.Language).Append('/').Append(table.Table).Append('\n');
            }
            return Encoding.UTF8.GetBytes(builder.ToString());
        }

        /// <summary>Whether the build packed the requested table; true for any table of a catalog without a list.</summary>
        private bool IsPacked(in TableRequest request)
        {
            if (!_manifests.TryGetValue(request.Catalog.Hash, out HashSet<(ulong Table, ulong Language)> packed))
            {
                packed = ReadManifest(request.Catalog.Name);
                _manifests.Add(request.Catalog.Hash, packed);
            }
            return packed == null || packed.Contains((request.Table.Hash, request.Language.Hash));
        }

        private static HashSet<(ulong Table, ulong Language)> ReadManifest(string catalogName)
        {
            TextAsset asset = Resources.Load<TextAsset>(GetManifestPath(catalogName));
            if (asset == null)
            {
                return null;
            }
            string text = asset.text;
            Resources.UnloadAsset(asset);
            HashSet<(ulong Table, ulong Language)> packed = new();
            foreach (string line in text.Split('\n'))
            {
                int separator = line.IndexOf('/');
                if (separator > 0)
                {
                    packed.Add((Hashing.ComputeNameHash(line.AsSpan(separator + 1).Trim()), Hashing.ComputeNameHash(line.AsSpan(0, separator))));
                }
            }
            return packed;
        }

        private static void ReadFile(string path, TableReceiver receiver)
        {
            try
            {
                receiver.Receive(File.ReadAllBytes(path));
            }
            catch (Exception exception)
            {
                receiver.Fail($"{path} couldn't be read: {exception.Message}");
            }
        }

        private static void LoadFromUrl(string url, TableReceiver receiver)
        {
#if REACTIVELOCALIZER_WEB_REQUEST
            UnityWebRequest request = UnityWebRequest.Get(url);
            request.SendWebRequest().completed += _ =>
            {
                try
                {
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        receiver.Receive(request.downloadHandler.data);
                    }
                    else
                    {
                        receiver.Fail($"{url} couldn't be downloaded: {request.error}");
                    }
                }
                finally
                {
                    request.Dispose();
                }
            };
#else
            receiver.Fail($"{url} is a URL, which only the built-in Unity Web Request module can read. Enable it in the Package Manager's Built-in packages.");
#endif
        }
    }
}
