using reromanlee.ReactiveLocalizer.Authoring;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Samples.UnityLocalizationMigration.Editor
{
    /// <summary>
    /// What a migration writes, made before anything is written: the catalog's languages, every table in every
    /// language, and what needs a person's attention afterwards.
    /// </summary>
    public sealed class MigrationPlan
    {
        public MigrationPlan(string catalogName, MigrationLanguage source)
        {
            CatalogName = catalogName;
            Source = source;
        }

        /// <summary>The catalog's name, which its file and generated classes take, such as <c>Localization</c>.</summary>
        public string CatalogName { get; }

        /// <summary>The language the tables are written in.</summary>
        public MigrationLanguage Source { get; }

        /// <summary>Every language, the source first.</summary>
        public List<MigrationLanguage> Languages { get; } = new();

        /// <summary>Every table, with a file per language that has any of its texts.</summary>
        public List<TableFileSet> Tables { get; } = new();

        /// <summary>Names that changed to follow the naming rule, written <c>old &#8594; new</c>.</summary>
        public List<string> Renamed { get; } = new();

        /// <summary>Entries whose numbered placeholders became named arguments, such as <c>{0}</c> into <c>{arg0}</c>.</summary>
        public List<string> Converted { get; } = new();

        /// <summary>Entries kept as their written text, which need converting to ICU by hand.</summary>
        public List<string> Unconverted { get; } = new();

        /// <summary>How many texts the tables hold, in every language.</summary>
        public int TextCount { get; set; }

        /// <summary>Returns the catalog file's text: the source language, then a section per language.</summary>
        public string WriteCatalog()
        {
            StringBuilder text = new();
            text.Append("# Migrated from Unity Localization. Each language was a locale; its code is the culture.\n\n");
            text.Append("@source ").Append(Source.Name).Append('\n');
            for (int i = 0; i < Languages.Count; i++)
            {
                MigrationLanguage language = Languages[i];
                text.Append('\n').Append('[').Append(language.Name).Append("]\n");
                text.Append("DisplayName = ").Append(Escape(language.DisplayName)).Append('\n');
                if (!string.IsNullOrEmpty(language.Culture))
                {
                    text.Append("Culture = ").Append(language.Culture).Append('\n');
                }
                if (!string.IsNullOrEmpty(language.Fallback))
                {
                    text.Append("Fallback = ").Append(language.Fallback).Append('\n');
                }
            }
            return text.ToString();
        }

        /// <summary>Returns the report the Console shows once the files are written.</summary>
        public string WriteReport(string folder)
        {
            StringBuilder report = new();
            report.Append($"Migrated {Tables.Count} table(s) and {TextCount} text(s) in {Languages.Count} language(s) into {folder}/{CatalogName}.catalog.");
            Append(report, "Renamed to follow the naming rule; update the code that names them", Renamed);
            Append(report, "Numbered placeholders became named arguments; rename them after what they show, in every language", Converted);
            Append(report, "Kept as written, marked with a MIGRATE comment; convert these Smart Format strings to ICU by hand", Unconverted);
            return report.ToString();
        }

        private static void Append(StringBuilder report, string title, List<string> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }
            report.Append("\n\n").Append(title).Append(':');
            for (int i = 0; i < lines.Count; i++)
            {
                report.Append("\n  ").Append(lines[i]);
            }
        }

        /// <summary>Escapes what a catalog value can't hold as is: backslashes, and spaces at its ends.</summary>
        private static string Escape(string value)
        {
            string text = (value ?? string.Empty).Replace("\\", "\\\\").Trim();
            return text;
        }
    }
}
