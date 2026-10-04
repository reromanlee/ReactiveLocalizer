using System;

namespace reromanlee.ReactiveLocalizer.Documents
{
    /// <summary>
    /// The attribute and field names localization files use, in their canonical spelling. Files may write them in any
    /// case; the writer always uses these.
    /// </summary>
    public static class DocumentNames
    {
        // Entry attributes, written right above the entry they belong to.

        /// <summary><c>@formerly OldName</c>: the entry used to be called OldName, which still resolves to it.</summary>
        public const string Formerly = "formerly";

        /// <summary><c>@maximumLength 16</c>: translations longer than this many characters are reported.</summary>
        public const string MaximumLength = "maximumLength";

        // Table settings, written at the top of a table's source-language file.

        /// <summary><c>@loading Preload</c> or <c>OnDemand</c>: when the table is in memory.</summary>
        public const string Loading = "loading";

        /// <summary><c>@delivery Embedded</c> or <c>Streaming</c>: how the table ships in builds.</summary>
        public const string Delivery = "delivery";

        /// <summary><c>@generateCode false</c>: the table gets no generated keys.</summary>
        public const string GenerateCode = "generateCode";

        // Catalog attributes, written at the top of a catalog file.

        /// <summary><c>@source English</c>: the language the catalog is written in.</summary>
        public const string Source = "source";

        /// <summary><c>@namespace MyGame</c>: the namespace of the catalog's generated code.</summary>
        public const string Namespace = "namespace";

        // Fields of a catalog's language section.

        /// <summary>What a language picker shows for the language.</summary>
        public const string DisplayName = "DisplayName";

        /// <summary>The language tag that plural rules, number formatting and exports follow.</summary>
        public const string Culture = "Culture";

        /// <summary>The language missing entries come from.</summary>
        public const string Fallback = "Fallback";

        /// <summary><c>LeftToRight</c> or <c>RightToLeft</c>.</summary>
        public const string Direction = "Direction";

        /// <summary><c>true</c> when builds fail while an entry is missing in the language.</summary>
        public const string Required = "Required";

        private static readonly string[] EntryAttributes = { Formerly, MaximumLength };
        private static readonly string[] TableSettings = { Loading, Delivery, GenerateCode };
        private static readonly string[] CatalogAttributes = { Source, Namespace };
        private static readonly string[] LanguageFields = { DisplayName, Culture, Fallback, Direction, Required };

        /// <summary>Returns the canonical spelling of a known entry attribute, or null for an unknown one.</summary>
        internal static string FindEntryAttribute(ReadOnlySpan<char> name) => Find(EntryAttributes, name);

        /// <summary>Returns the canonical spelling of a known table setting, or null for an unknown one.</summary>
        internal static string FindTableSetting(ReadOnlySpan<char> name) => Find(TableSettings, name);

        /// <summary>Returns the canonical spelling of a known catalog attribute, or null for an unknown one.</summary>
        internal static string FindCatalogAttribute(ReadOnlySpan<char> name) => Find(CatalogAttributes, name);

        /// <summary>Returns the canonical spelling of a known language field, or null for an unknown one.</summary>
        internal static string FindLanguageField(ReadOnlySpan<char> name) => Find(LanguageFields, name);

        private static string Find(string[] names, ReadOnlySpan<char> name)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (name.Equals(names[i].AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return names[i];
                }
            }
            return null;
        }
    }
}
