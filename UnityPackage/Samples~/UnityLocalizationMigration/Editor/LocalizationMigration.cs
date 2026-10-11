using reromanlee.ReactiveLocalizer.Authoring;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace reromanlee.ReactiveLocalizer.Samples.UnityLocalizationMigration.Editor
{
    /// <summary>
    /// <c>Tools/ReactiveLocalizer/Samples/Migrate from Unity Localization</c>: turns the project's locales into a
    /// catalog's languages and its string table collections into <c>.lang</c> files, keeping keys, comments and every
    /// translation. Unity Localization's own assets stay as they are.
    /// </summary>
    /// <remarks>
    /// The catalog is named <c>Localization</c>, so its generated class is <c>LocalizationKeys</c>, and call sites such
    /// as <c>LocalizationKeys.SettingsPage.Title</c> keep compiling once a project's own key wrapper is gone. Plain
    /// placeholders carry over (see <see cref="SmartFormatConversion"/>); what needs a person is listed in the Console
    /// and marked in the files with a <c>MIGRATE</c> comment.
    /// </remarks>
    public static class LocalizationMigration
    {
        /// <summary>The name of the catalog the migration creates.</summary>
        public const string CatalogName = "Localization";

        private const string MigrateComment = "MIGRATE: Smart Format text kept as written; convert it to ICU: ";

        [MenuItem("Tools/ReactiveLocalizer/Samples/Migrate from Unity Localization...")]
        private static void Migrate()
        {
            MigrationPlan plan = CreatePlan(out string problem);
            if (plan == null)
            {
                EditorUtility.DisplayDialog("Migrate from Unity Localization", problem, "OK");
                return;
            }
            string chosen = EditorUtility.SaveFolderPanel("Migrate into Folder", Application.dataPath, CatalogName);
            if (string.IsNullOrEmpty(chosen))
            {
                return;
            }
            if (!TryGetAssetFolder(chosen, out string folder, out problem) ||
                !EditorUtility.DisplayDialog("Migrate from Unity Localization",
                    $"Write {plan.Tables.Count} table(s) with {plan.TextCount} text(s) in {plan.Languages.Count} language(s), and {CatalogName}.catalog, into {folder}?",
                    "Migrate", "Cancel"))
            {
                if (problem != null)
                {
                    EditorUtility.DisplayDialog("Migrate from Unity Localization", problem, "OK");
                }
                return;
            }
            Write(plan, folder);
            AssetDatabase.Refresh();
            Debug.Log("[ReactiveLocalizer] " + plan.WriteReport(folder));
        }

        /// <summary>
        /// Plans the migration of the project's locales and string table collections, without writing anything. Null,
        /// with <paramref name="problem"/> saying why, when there's nothing to migrate.
        /// </summary>
        public static MigrationPlan CreatePlan(out string problem)
        {
            IReadOnlyList<Locale> locales = LocalizationEditorSettings.GetLocales();
            IReadOnlyList<StringTableCollection> collections = LocalizationEditorSettings.GetStringTableCollections();
            if (locales.Count == 0 || collections.Count == 0)
            {
                problem = "The project has no locales or no string table collections to migrate.";
                return null;
            }
            Dictionary<string, MigrationLanguage> byCode = CreateLanguages(locales);
            Locale sourceLocale = FindSourceLocale(locales);
            MigrationPlan plan = new(CatalogName, byCode[sourceLocale.Identifier.Code]);
            plan.Languages.Add(plan.Source);
            foreach (Locale locale in locales)
            {
                MigrationLanguage language = byCode[locale.Identifier.Code];
                if (language != plan.Source)
                {
                    plan.Languages.Add(language);
                }
                Locale fallback = locale.Metadata?.GetMetadata<FallbackLocale>()?.Locale;
                if (fallback != null && byCode.TryGetValue(fallback.Identifier.Code, out MigrationLanguage target) && target != language && target != plan.Source)
                {
                    language.Fallback = target.Name;
                }
            }

            HashSet<string> tableNames = MigrationNames.CreateTakenSet();
            foreach (StringTableCollection collection in collections)
            {
                string tableName = MigrationNames.MakeUnique(MigrationNames.ToName(collection.TableCollectionName, "Table"), tableNames);
                Note(plan, collection.TableCollectionName, tableName);
                plan.Tables.Add(CreateTable(plan, collection, tableName, locales, byCode, sourceLocale));
            }
            problem = null;
            return plan;
        }

        /// <summary>Writes the catalog and every table file into <paramref name="folder"/>, a folder of the project.</summary>
        public static void Write(MigrationPlan plan, string folder)
        {
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, plan.CatalogName + ".catalog"), plan.WriteCatalog());
            foreach (TableFileSet table in plan.Tables)
            {
                foreach (string language in table.Languages)
                {
                    File.WriteAllText(Path.Combine(folder, $"{table.TableName}.{language}.lang"), table.Get(language).Write());
                }
            }
        }

        private static TableFileSet CreateTable(MigrationPlan plan, StringTableCollection collection, string tableName, IReadOnlyList<Locale> locales,
            Dictionary<string, MigrationLanguage> byCode, Locale sourceLocale)
        {
            TableFileSet table = new(tableName, plan.Source.Name);
            StringTable sourceTable = collection.GetTable(sourceLocale.Identifier) as StringTable;
            // Generated code names the table and catalog with these two, so no entry can.
            HashSet<string> keys = MigrationNames.CreateTakenSet("TableKey", "CatalogKey");
            foreach (SharedTableData.SharedTableEntry shared in collection.SharedData.Entries)
            {
                string key = MigrationNames.MakeUnique(MigrationNames.ToName(shared.Key, "Entry"), keys);
                Note(plan, $"{collection.TableCollectionName}/{shared.Key}", $"{tableName}.{key}");
                string where = $"{tableName}.{key}";
                StringTableEntry sourceEntry = sourceTable?.GetEntry(shared.Id);
                string sourceText = Convert(plan, where, plan.Source.Name, sourceEntry, out string sourceNote);
                if (!table.TryAddEntry(key, sourceText, out string problem))
                {
                    plan.Unconverted.Add($"{where}: {problem}");
                    continue;
                }
                plan.TextCount++;
                TableFileEntry written = Find(table, plan.Source.Name, key);
                string comment = shared.Metadata?.GetMetadata<Comment>()?.CommentText;
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    written.Comments.AddRange(comment.Replace("\r\n", "\n").Split('\n'));
                }
                if (sourceNote != null)
                {
                    written.Comments.Add(sourceNote);
                }
                foreach (Locale locale in locales)
                {
                    if (locale == sourceLocale || !(collection.GetTable(locale.Identifier) is StringTable translations))
                    {
                        continue;
                    }
                    StringTableEntry entry = translations.GetEntry(shared.Id);
                    // An empty translation is a missing one in Unity Localization, which then shows its fallback.
                    if (entry == null || string.IsNullOrEmpty(entry.Value))
                    {
                        continue;
                    }
                    string language = byCode[locale.Identifier.Code].Name;
                    string text = Convert(plan, where, language, entry, out string note);
                    if (table.TrySetText(language, key, text, out _))
                    {
                        plan.TextCount++;
                        if (note != null)
                        {
                            Find(table, language, key)?.Comments.Add(note);
                        }
                    }
                }
            }
            return table;
        }

        private static string Convert(MigrationPlan plan, string where, string language, StringTableEntry entry, out string note)
        {
            note = null;
            string text = entry?.Value ?? string.Empty;
            bool isSmart = entry != null && entry.IsSmart;
            if (!SmartFormatConversion.TryConvert(text, isSmart, out string message, out bool hasArguments))
            {
                plan.Unconverted.Add($"{where} in {language}: {text}");
                note = MigrateComment + text.Replace("\n", " ");
                return message;
            }
            if (hasArguments && message.Contains("{arg"))
            {
                plan.Converted.Add($"{where} in {language}: {message}");
            }
            return message;
        }

        /// <summary>
        /// Names each locale after its language in English, such as <c>English</c>. Locales sharing a language are
        /// named after their region too, such as <c>PortugueseBrazil</c>; a locale without a known culture after its own name.
        /// </summary>
        private static Dictionary<string, MigrationLanguage> CreateLanguages(IReadOnlyList<Locale> locales)
        {
            Dictionary<string, int> neutralCounts = new(StringComparer.OrdinalIgnoreCase);
            foreach (Locale locale in locales)
            {
                string neutral = GetCulture(locale)?.TwoLetterISOLanguageName ?? locale.Identifier.Code;
                neutralCounts[neutral] = neutralCounts.TryGetValue(neutral, out int count) ? count + 1 : 1;
            }
            HashSet<string> names = MigrationNames.CreateTakenSet();
            Dictionary<string, MigrationLanguage> byCode = new(StringComparer.OrdinalIgnoreCase);
            foreach (Locale locale in locales)
            {
                CultureInfo culture = GetCulture(locale);
                string englishName;
                if (culture == null)
                {
                    englishName = locale.LocaleName;
                }
                else
                {
                    bool isShared = neutralCounts[culture.TwoLetterISOLanguageName] > 1;
                    englishName = isShared || culture.IsNeutralCulture ? culture.EnglishName : culture.Parent.EnglishName;
                }
                string name = MigrationNames.MakeUnique(MigrationNames.ToName(englishName, "Language"), names);
                string displayName = culture != null ? culture.NativeName : locale.LocaleName;
                byCode[locale.Identifier.Code] = new MigrationLanguage(name, displayName, locale.Identifier.Code);
            }
            return byCode;
        }

        private static CultureInfo GetCulture(Locale locale)
        {
            try
            {
                CultureInfo culture = locale.Identifier.CultureInfo;
                // An invented locale gets a culture with no language behind it.
                return culture != null && culture.ThreeLetterISOLanguageName != "ivl" ? culture : null;
            }
            catch (CultureNotFoundException)
            {
                return null;
            }
        }

        private static Locale FindSourceLocale(IReadOnlyList<Locale> locales)
        {
            Locale project = null;
            try
            {
                project = LocalizationSettings.ProjectLocale;
            }
            catch (Exception exception) when (exception is NullReferenceException || exception is InvalidOperationException)
            {
                // Without Localization Settings there is no project locale, and the first locale is the source.
            }
            foreach (Locale locale in locales)
            {
                if (project != null && locale.Identifier == project.Identifier)
                {
                    return locale;
                }
            }
            return locales[0];
        }

        private static TableFileEntry Find(TableFileSet table, string language, string key)
        {
            TableFile file = table.Get(language);
            return file != null && file.TryGetEntry(key, out TableFileEntry entry) ? entry : null;
        }

        private static void Note(MigrationPlan plan, string original, string migrated)
        {
            string originalName = original.Substring(original.LastIndexOf('/') + 1);
            string migratedName = migrated.Substring(migrated.LastIndexOf('.') + 1);
            if (!string.Equals(originalName, migratedName, StringComparison.Ordinal))
            {
                plan.Renamed.Add($"{original} \u2192 {migrated}");
            }
        }

        /// <summary>Turns a chosen folder into a project path, refusing folders outside Assets and ones that already hold localization files.</summary>
        private static bool TryGetAssetFolder(string chosen, out string folder, out string problem)
        {
            string full = Path.GetFullPath(chosen).Replace('\\', '/');
            string assets = Path.GetFullPath(Application.dataPath).Replace('\\', '/');
            folder = null;
            if (!full.Equals(assets, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(assets + "/", StringComparison.OrdinalIgnoreCase))
            {
                problem = "Pick a folder inside the project's Assets folder.";
                return false;
            }
            folder = "Assets" + full.Substring(assets.Length);
            if (Directory.Exists(folder) && (Directory.GetFiles(folder, "*.catalog").Length > 0 || Directory.GetFiles(folder, "*.lang").Length > 0))
            {
                problem = $"{folder} already holds localization files. Pick an empty folder, so nothing is overwritten.";
                return false;
            }
            foreach (string guid in AssetDatabase.FindAssets(CatalogName))
            {
                // Two catalogs with one name would claim the same generated class.
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/" + CatalogName + ".catalog", StringComparison.OrdinalIgnoreCase))
                {
                    problem = $"The project already has {path}, and two catalogs can't share a name. Rename or remove it first.";
                    return false;
                }
            }
            problem = null;
            return true;
        }
    }
}
