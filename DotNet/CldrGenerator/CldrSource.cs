using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>
    /// Reads files of one cldr-json release, downloading each once into a cache folder, so later runs work offline and
    /// always see the same data.
    /// </summary>
    internal sealed class CldrSource : IDisposable
    {
        private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(60) };
        private readonly string _baseAddress;
        private readonly string _cacheFolder;

        public CldrSource(string version, string cacheFolder)
        {
            Version = version;
            _baseAddress = $"https://raw.githubusercontent.com/unicode-org/cldr-json/{version}/cldr-json/";
            _cacheFolder = cacheFolder;
        }

        public string Version { get; }

        /// <summary>Returns the JSON file at <paramref name="relativePath"/> of the release, such as <c>cldr-core/supplemental/plurals.json</c>.</summary>
        public async Task<JsonDocument> ReadAsync(string relativePath)
        {
            string cachePath = Path.Combine(_cacheFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(cachePath))
            {
                byte[] downloaded = await _client.GetByteArrayAsync(_baseAddress + relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(cachePath));
                string partialPath = cachePath + ".partial";
                await File.WriteAllBytesAsync(partialPath, downloaded);
                File.Move(partialPath, cachePath, true);
            }
            return JsonDocument.Parse(await File.ReadAllBytesAsync(cachePath));
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
