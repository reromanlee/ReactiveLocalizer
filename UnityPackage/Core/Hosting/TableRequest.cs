namespace reromanlee.ReactiveLocalizer.Hosting
{
    /// <summary>
    /// What a localizer asks its table sources for: the compiled catalog, or one table in one language.
    /// </summary>
    public readonly struct TableRequest
    {
        private TableRequest(CatalogKey catalog, TableKey table, LanguageKey language, TableDelivery delivery)
        {
            Catalog = catalog;
            Table = table;
            Language = language;
            Delivery = delivery;
        }

        /// <summary>The catalog asked for, or the catalog the asked table belongs to.</summary>
        public CatalogKey Catalog { get; }

        /// <summary>The table asked for. Empty when the request is for the compiled catalog itself.</summary>
        public TableKey Table { get; }

        /// <summary>The language of the table asked for. Empty when the request is for the compiled catalog.</summary>
        public LanguageKey Language { get; }

        /// <summary>How the table ships in builds, which tells a source where to find it.</summary>
        public TableDelivery Delivery { get; }

        /// <summary>Whether the request is for the compiled catalog rather than a table.</summary>
        public bool IsCatalog => Table.IsEmpty;

        /// <summary>Creates a request for the compiled catalog.</summary>
        public static TableRequest ForCatalog(CatalogKey catalog) => new(catalog, default, default, TableDelivery.Embedded);

        /// <summary>Creates a request for <paramref name="table"/> in <paramref name="language"/>.</summary>
        public static TableRequest ForTable(CatalogKey catalog, TableInfo table, LanguageKey language) => new(catalog, table.Key, language, table.Delivery);

        /// <summary>Returns what the request asks for, the way reports name it.</summary>
        public override string ToString() => IsCatalog ? $"the catalog '{Catalog.Name}'" : $"the table '{Table.Name}' in {Language.Name}";
    }
}
