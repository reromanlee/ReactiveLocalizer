using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Everything the generated code of one catalog is made from: its name, namespace, languages and tables.
    /// Translations play no part, so translating never changes the generated code.
    /// </summary>
    public sealed class KeysScript
    {
        /// <summary>Creates the description of a catalog's generated code.</summary>
        /// <param name="catalogName">Name of the catalog, which also names the generated classes.</param>
        /// <param name="namespaceName">Namespace of the generated classes; null or empty for the global namespace.</param>
        /// <param name="languages">Names of the catalog's languages, in any order.</param>
        /// <param name="tables">The tables that generate code, in any order.</param>
        public KeysScript(string catalogName, string namespaceName, IReadOnlyList<string> languages, IReadOnlyList<KeysScriptTable> tables)
        {
            CatalogName = catalogName;
            NamespaceName = namespaceName ?? string.Empty;
            Languages = languages ?? Array.Empty<string>();
            Tables = tables ?? Array.Empty<KeysScriptTable>();
        }

        /// <summary>Name of the catalog.</summary>
        public string CatalogName { get; }

        /// <summary>Namespace of the generated classes. Empty for the global namespace.</summary>
        public string NamespaceName { get; }

        /// <summary>Names of the catalog's languages.</summary>
        public IReadOnlyList<string> Languages { get; }

        /// <summary>The tables that generate code.</summary>
        public IReadOnlyList<KeysScriptTable> Tables { get; }

        /// <summary>Name of the generated class of keys, such as <c>LocalizationKeys</c>.</summary>
        public string KeysClassName => CatalogName + "Keys";

        /// <summary>Name of the generated class of languages, such as <c>LocalizationLanguages</c>.</summary>
        public string LanguagesClassName => CatalogName + "Languages";

        /// <summary>Name of the generated file, which holds both classes: <c>LocalizationKeys.cs</c>.</summary>
        public string FileName => KeysClassName + ".cs";
    }
}
