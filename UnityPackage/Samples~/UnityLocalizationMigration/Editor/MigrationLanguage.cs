namespace reromanlee.ReactiveLocalizer.Samples.UnityLocalizationMigration.Editor
{
    /// <summary>A Unity Localization locale as the language it becomes in the migrated catalog.</summary>
    public sealed class MigrationLanguage
    {
        public MigrationLanguage(string name, string displayName, string culture)
        {
            Name = name;
            DisplayName = displayName;
            Culture = culture;
        }

        /// <summary>The language's name, such as <c>English</c>, or <c>PortugueseBrazil</c> beside another Portuguese.</summary>
        public string Name { get; }

        /// <summary>What a language picker shows, such as <c>Portugu&#234;s (Brasil)</c>.</summary>
        public string DisplayName { get; }

        /// <summary>The locale's code, which plural rules and number formats follow, such as <c>pt-BR</c>.</summary>
        public string Culture { get; }

        /// <summary>The language missing entries come from, by name; null for none.</summary>
        public string Fallback { get; set; }
    }
}
