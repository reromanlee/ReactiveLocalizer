using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>What the agent guide tells about one catalog of the project.</summary>
    internal readonly struct AgentGuideCatalog
    {
        public AgentGuideCatalog(string path, string sourceLanguage, IReadOnlyList<string> languages, string keysClassName,
            string generatedCodePath, bool isEditorOnly)
        {
            Path = path;
            SourceLanguage = sourceLanguage;
            Languages = languages;
            KeysClassName = keysClassName;
            GeneratedCodePath = generatedCodePath;
            IsEditorOnly = isEditorOnly;
        }

        /// <summary>The catalog file, such as <c>Assets/Localization/Localization.catalog</c>.</summary>
        public string Path { get; }

        public string SourceLanguage { get; }

        /// <summary>Every language of the catalog, the source language included, in the catalog's order.</summary>
        public IReadOnlyList<string> Languages { get; }

        /// <summary>The generated class of the catalog's keys, such as <c>LocalizationKeys</c>.</summary>
        public string KeysClassName { get; }

        public string GeneratedCodePath { get; }

        public bool IsEditorOnly { get; }
    }
}
