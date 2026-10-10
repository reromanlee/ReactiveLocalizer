using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Tells what an entry field shows under its picked entry: the entry's text in the preview language, or what is
    /// wrong with the reference, with the reference that fixes it when the entry was renamed or moved.
    /// </summary>
    /// <remarks>
    /// Descriptions are cached until a localization file or the preview language changes, so an Inspector redrawing
    /// many fields every frame reads no file and allocates nothing.
    /// </remarks>
    internal static class EntryPreview
    {
        private const int CacheLimit = 1024;
        private const int TextLimit = 300;
        private static readonly Dictionary<EntryReference, EntryDescription> Cache = new();
        private static int _cacheVersion = -1;
        private static string _cacheLanguage;

        /// <summary>
        /// Describes <paramref name="reference"/>. A field limited to one catalog passes its name as
        /// <paramref name="limitedCatalog"/>, which turns a reference into another catalog into a problem.
        /// </summary>
        public static EntryDescription Describe(EntryReference reference, string limitedCatalog = null)
        {
            if (reference.IsEmpty)
            {
                return new EntryDescription(EntryPreviewKind.Unpicked, "Pick an entry.");
            }
            if (!string.IsNullOrEmpty(limitedCatalog) && !IsInCatalog(reference, limitedCatalog))
            {
                return new EntryDescription(EntryPreviewKind.Broken, $"This field takes entries of the catalog '{limitedCatalog}' only.");
            }
            string language = LocalizationUserSettings.instance.PreviewLanguage;
            if (_cacheVersion != CatalogIndex.Version || !string.Equals(_cacheLanguage, language, StringComparison.Ordinal) || Cache.Count >= CacheLimit)
            {
                Cache.Clear();
                _cacheVersion = CatalogIndex.Version;
                _cacheLanguage = language;
            }
            if (!Cache.TryGetValue(reference, out EntryDescription description))
            {
                description = Create(reference, language);
                Cache[reference] = description;
            }
            return description;
        }

        /// <summary>Returns the catalog <paramref name="catalogName"/> names, or the default catalog when it is empty; null when there is none.</summary>
        public static IndexedCatalog FindCatalog(string catalogName)
        {
            if (string.IsNullOrEmpty(catalogName))
            {
                return CatalogIndex.DefaultCatalog;
            }
            return NameRules.IsValid(catalogName) ? CatalogIndex.Find(new CatalogKey(catalogName)) : null;
        }

        /// <summary>
        /// Returns the text of <paramref name="entryName"/> in <paramref name="language"/>, or in the source language when
        /// the entry has no translation there, as one line. Null when the table has no such entry.
        /// </summary>
        public static string GetText(IndexedCatalog catalog, string tableName, string entryName, string language, out bool isTranslated)
        {
            isTranslated = false;
            if (!NameRules.IsValid(tableName) || !catalog.TryGetTable(new TableKey(tableName), out IndexedTable table) ||
                table.SourceDocument == null || !table.SourceDocument.TryGetEntry(entryName, out TableDocumentEntry source))
            {
                return null;
            }
            string text = source.Value;
            if (NameRules.IsValid(language) && !string.Equals(language, catalog.Info?.SourceLanguage.Name, StringComparison.OrdinalIgnoreCase) &&
                table.TryGetFile(new LanguageKey(language), out string path) &&
                CatalogIndex.ReadTableDocument(path).TryGetEntry(entryName, out TableDocumentEntry translation))
            {
                text = translation.Value;
                isTranslated = true;
            }
            return ToOneLine(text);
        }

        private static EntryDescription Create(EntryReference reference, string language)
        {
            IndexedCatalog catalog = FindCatalog(reference.CatalogName);
            if (catalog == null)
            {
                return new EntryDescription(EntryPreviewKind.Broken, string.IsNullOrEmpty(reference.CatalogName)
                    ? "The project has no default catalog. Create one with Assets > Create > ReactiveLocalizer > Catalog."
                    : $"The project has no catalog '{reference.CatalogName}'.");
            }
            if (catalog.Info == null)
            {
                return new EntryDescription(EntryPreviewKind.Broken, $"The catalog '{catalog.Name}' can't be used; its file's import reports why.");
            }
            KeyResolution resolution = catalog.Resolver.Resolve(reference.TableName, reference.EntryName);
            switch (resolution.Kind)
            {
                case KeyResolutionKind.Found:
                    string text = GetText(catalog, resolution.TableName, resolution.EntryName, language, out bool isTranslated);
                    string tooltip = isTranslated || !NameRules.IsValid(language) || string.Equals(language, catalog.Info.SourceLanguage.Name, StringComparison.OrdinalIgnoreCase)
                        ? null
                        : $"Not translated to {language}; this is the {catalog.Info.SourceLanguage.Name} text.";
                    return new EntryDescription(EntryPreviewKind.Found, text, default, tooltip);
                case KeyResolutionKind.Renamed:
                case KeyResolutionKind.Moved:
                    EntryReference fix = new(catalog.Name, resolution.TableName, resolution.EntryName);
                    string change = resolution.Kind == KeyResolutionKind.Moved ? "Moved" : "Renamed";
                    return new EntryDescription(EntryPreviewKind.Renamed, $"{change} to {fix}. It still works by its former name.", fix);
                case KeyResolutionKind.Invalid:
                    return new EntryDescription(EntryPreviewKind.Broken, $"'{reference.TableName}.{reference.EntryName}' isn't a valid key: {NameRules.Description}.");
                default:
                    string suggestion = resolution.Suggestion != null ? $" Did you mean '{resolution.Suggestion}'?" : string.Empty;
                    return new EntryDescription(EntryPreviewKind.Broken, $"'{reference}' doesn't exist in the catalog '{catalog.Name}'.{suggestion}");
            }
        }

        private static bool IsInCatalog(EntryReference reference, string catalogName)
        {
            string name = reference.CatalogName;
            if (string.IsNullOrEmpty(name))
            {
                name = CatalogIndex.DefaultCatalog?.Name;
            }
            return string.Equals(name, catalogName, StringComparison.OrdinalIgnoreCase);
        }

        private static string ToOneLine(string text)
        {
            if (text.Length == 0)
            {
                return "(intentionally empty)";
            }
            if (text.Length > TextLimit)
            {
                text = text.Substring(0, TextLimit) + "\u2026";
            }
            return text.IndexOf('\n') >= 0 ? text.Replace("\r\n", " ").Replace('\n', ' ') : text;
        }
    }

    /// <summary>What an entry field shows under its picked entry, as <see cref="EntryPreview"/> describes it.</summary>
    internal readonly struct EntryDescription
    {
        public EntryDescription(EntryPreviewKind kind, string text, EntryReference fix = default, string tooltip = null)
        {
            Kind = kind;
            Text = text;
            Fix = fix;
            Tooltip = tooltip;
        }

        public EntryPreviewKind Kind { get; }

        /// <summary>The entry's text, or what is wrong with the reference.</summary>
        public string Text { get; }

        /// <summary>The reference that names the entry now; empty unless it was renamed or moved.</summary>
        public EntryReference Fix { get; }

        /// <summary>More about the text, such as a missing translation; null when there is nothing more.</summary>
        public string Tooltip { get; }
    }

    /// <summary>What an entry field's reference finds.</summary>
    internal enum EntryPreviewKind
    {
        /// <summary>No entry is picked.</summary>
        Unpicked = 0,

        /// <summary>The entry exists; the text is its preview.</summary>
        Found = 1,

        /// <summary>The entry is found by a former name; the fix names it as it is now.</summary>
        Renamed = 2,

        /// <summary>The reference finds nothing; the text says why.</summary>
        Broken = 3
    }
}
