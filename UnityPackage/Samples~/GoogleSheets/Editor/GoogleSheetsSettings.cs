using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.GoogleSheets.Editor
{
    /// <summary>
    /// Where the Google Sheets sync goes: the web app URL, its token and the catalog synced. Saved in
    /// <c>UserSettings/</c>, which version control leaves out, since the URL and token together let anyone edit the sheet.
    /// </summary>
    [FilePath("UserSettings/ReactiveLocalizerGoogleSheets.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class GoogleSheetsSettings : ScriptableSingleton<GoogleSheetsSettings>
    {
        [SerializeField] private string _webAppUrl = string.Empty;
        [SerializeField] private string _token = string.Empty;
        [SerializeField] private string _catalogName = string.Empty;

        /// <summary>The URL of the deployed Apps Script web app, ending in <c>/exec</c>.</summary>
        public string WebAppUrl
        {
            get => _webAppUrl ?? string.Empty;
            set => Change(ref _webAppUrl, value);
        }

        /// <summary>The secret the script's TOKEN property holds.</summary>
        public string Token
        {
            get => _token ?? string.Empty;
            set => Change(ref _token, value);
        }

        /// <summary>The catalog synced, by name; empty for the default catalog.</summary>
        public string CatalogName
        {
            get => _catalogName ?? string.Empty;
            set => Change(ref _catalogName, value);
        }

        private void Change(ref string field, string value)
        {
            value = value?.Trim() ?? string.Empty;
            if (field == value)
            {
                return;
            }
            field = value;
            Save(true);
        }
    }
}
