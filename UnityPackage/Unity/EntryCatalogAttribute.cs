using System;
using System.Reflection;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Limits the entries an <see cref="EntryReference"/> field offers in the Inspector to one catalog, such as a tool
    /// window's own: <c>[EntryCatalog(typeof(ToolWindowKeys))] [SerializeField] private EntryReference _title;</c>
    /// </summary>
    /// <remarks>Only the Inspector reads it; it changes nothing at runtime.</remarks>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EntryCatalogAttribute : Attribute
    {
        /// <summary>Limits the field to the catalog whose generated keys class is <paramref name="keysClass"/>.</summary>
        public EntryCatalogAttribute(Type keysClass)
        {
            KeysClass = keysClass;
        }

        /// <summary>Limits the field to the catalog named <paramref name="catalogName"/>.</summary>
        public EntryCatalogAttribute(string catalogName)
        {
            GivenCatalogName = catalogName;
        }

        /// <summary>The generated keys class naming the catalog, such as <c>ToolWindowKeys</c>; null when a name was given.</summary>
        public Type KeysClass { get; }

        private string GivenCatalogName { get; }

        /// <summary>
        /// Returns the catalog's name: the one given, or the one the keys class's <c>CatalogKey</c> holds. Null when the
        /// class has no such key.
        /// </summary>
        public string ResolveCatalogName()
        {
            if (GivenCatalogName != null)
            {
                return GivenCatalogName;
            }
            FieldInfo field = KeysClass?.GetField("CatalogKey", BindingFlags.Public | BindingFlags.Static);
            return field?.GetValue(null) is CatalogKey key && !key.IsEmpty ? key.Name : null;
        }
    }
}
