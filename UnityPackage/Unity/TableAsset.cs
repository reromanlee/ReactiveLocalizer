using System;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// What a <c>.lang</c> file imports as: one table in one language, compiled. The editor reads tables straight
    /// from these, and table sources such as Addressables can load them like any other asset.
    /// </summary>
    public sealed class TableAsset : ScriptableObject
    {
        [SerializeField] private string _catalogName;
        [SerializeField] private string _tableName;
        [SerializeField] private string _languageName;
        [SerializeField] private byte[] _data = Array.Empty<byte>();

        /// <summary>Name of the catalog the table belongs to.</summary>
        public string CatalogName => _catalogName;

        /// <summary>Name of the table.</summary>
        public string TableName => _tableName;

        /// <summary>Name of the language the table is in.</summary>
        public string LanguageName => _languageName;

        /// <summary>The compiled table, ready to hand to a <see cref="Hosting.TableReceiver"/>. Empty when the file couldn't be compiled.</summary>
        public ReadOnlyMemory<byte> Data => _data;

        internal void Initialize(string catalogName, string tableName, string languageName, byte[] data)
        {
            _catalogName = catalogName;
            _tableName = tableName;
            _languageName = languageName;
            _data = data ?? Array.Empty<byte>();
        }
    }
}
