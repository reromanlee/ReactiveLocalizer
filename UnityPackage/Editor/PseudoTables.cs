using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;
using UnityEditor;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The pseudo language of the project's catalogs: when localizers are offered it, and its tables, compiled from
    /// the source-language files the first time they're asked for and again only after the file changes.
    /// </summary>
    internal static class PseudoTables
    {
        private static readonly Dictionary<string, (ulong Catalog, TableDocument Source, byte[] Data, string Problem)> Compiled = new(StringComparer.Ordinal);

        /// <summary>Whether localizers in the editor are offered the pseudo language: always outside Play Mode, and in Play Mode as the project settings say.</summary>
        public static bool IsOffered => !EditorApplication.isPlayingOrWillChangePlaymode || LocalizationSettings.instance.IsOfferingPseudoLocalization;

        /// <summary>Whether <paramref name="language"/> is the pseudo language of <paramref name="catalog"/> rather than a language its file defines.</summary>
        public static bool IsPseudo(IndexedCatalog catalog, LanguageKey language) =>
            language == PseudoLocalization.Language && PseudoLocalization.IsAvailable(catalog?.Info);

        /// <summary>Returns the catalog as localizers receive it, with the pseudo language when <paramref name="isPseudoIncluded"/>.</summary>
        public static CatalogInfo GetCatalog(IndexedCatalog catalog, bool isPseudoIncluded) =>
            isPseudoIncluded ? PseudoLocalization.AddLanguage(catalog.Info) : catalog.Info;

        /// <summary>
        /// Returns the compiled pseudo version of <paramref name="table"/>, or null when it has no source-language
        /// file. <paramref name="problem"/> tells what pseudo text the table had to leave out, which only a bug can cause.
        /// </summary>
        public static byte[] Compile(IndexedCatalog catalog, IndexedTable table, out string problem)
        {
            problem = null;
            if (!PseudoLocalization.IsAvailable(catalog.Info) || table.SourcePath == null || table.SourceDocument == null)
            {
                return null;
            }
            if (Compiled.TryGetValue(table.SourcePath, out (ulong Catalog, TableDocument Source, byte[] Data, string Problem) cached) &&
                cached.Catalog == catalog.Key.Hash && ReferenceEquals(cached.Source, table.SourceDocument))
            {
                problem = cached.Problem;
                return cached.Data;
            }
            List<DocumentIssue> issues = new();
            byte[] data = PseudoLocalization.Compile(catalog.Info, table.Key, table.SourceDocument, issues);
            // Warnings repeat what the source file's own import reports; an error means pseudo text broke a message.
            int error = issues.FindIndex(issue => issue.Severity == IssueSeverity.Error);
            problem = error >= 0 ? $"Pseudo-localization left out an entry of {table.SourcePath}{issues[error]}" : null;
            Compiled[table.SourcePath] = (catalog.Key.Hash, table.SourceDocument, data, problem);
            return data;
        }
    }
}
