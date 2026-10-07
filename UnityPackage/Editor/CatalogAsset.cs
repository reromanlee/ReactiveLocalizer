using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// What a <c>.catalog</c> file imports as. The catalog's definition is always read from the file itself, so this
    /// only gives the file a place in the asset database.
    /// </summary>
    internal sealed class CatalogAsset : ScriptableObject
    {
        [SerializeField] private string _catalogName;

        public string CatalogName => _catalogName;

        public void Initialize(string catalogName)
        {
            _catalogName = catalogName;
        }
    }
}
