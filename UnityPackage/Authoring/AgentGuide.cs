using System;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Writes the section of a project's AGENTS.md or CLAUDE.md that tells coding agents how the project localizes its
    /// text, since agents read the project rather than the package cache. Writing it again replaces the section in place.
    /// </summary>
    internal static class AgentGuide
    {
        public const string StartMarker = "<!-- ReactiveLocalizer agent guide: start -->";
        public const string EndMarker = "<!-- ReactiveLocalizer agent guide: end -->";

        /// <summary>Returns the section for <paramref name="catalogs"/>, its lines ended by <paramref name="newLine"/>.</summary>
        public static string Write(IReadOnlyList<AgentGuideCatalog> catalogs, string newLine)
        {
            string keysClass = "LocalizationKeys";
            for (int i = 0; i < catalogs.Count; i++)
            {
                if (!catalogs[i].IsEditorOnly)
                {
                    keysClass = catalogs[i].KeysClassName;
                    break;
                }
            }

            StringBuilder builder = new();
            builder.Append(StartMarker).Append(newLine);
            builder.Append("### Localization").Append(newLine).Append(newLine);
            builder.Append("This project localizes its text with the ReactiveLocalizer package: text that players see lives in `.lang` files, ")
                .Append("never in code, scenes or prefabs. *Tools > ReactiveLocalizer > Write Agent Guide* in the Unity editor rewrites this section.")
                .Append(newLine).Append(newLine);
            if (catalogs.Count == 0)
            {
                builder.Append("The project has no catalog yet: *Assets > Create > ReactiveLocalizer > Catalog* creates one.").Append(newLine);
            }
            else
            {
                builder.Append("Catalogs, each with its own languages, tables and generated keys:").Append(newLine).Append(newLine);
                for (int i = 0; i < catalogs.Count; i++)
                {
                    AppendCatalog(builder, catalogs[i], newLine);
                }
            }
            builder.Append(newLine);
            builder.Append("1. **Tables** are files named `<Table>.<Language>.lang` anywhere under their catalog's folder. The source language's file ")
                .Append("defines the keys, and a translation file holds only the entries translated so far; missing ones fall back.").Append(newLine);
            builder.Append("2. **Add an entry** to the source-language file as `Key = Text`, with a `#` comment above it telling translators where ")
                .Append("the text shows. Names use ASCII letters, digits and underscores, start with a letter, and are unique ignoring case. ")
                .Append("Name an entry after its role, such as `PlayButton`, never after its text.").Append(newLine);
            builder.Append("3. **Arguments** use ICU MessageFormat and are named: `Coins = You have {coins, plural, one {# coin} other {# coins}}.` ")
                .Append("A literal brace is written `'{'`. Translations keep the same arguments, with the plural forms of their own language.").Append(newLine);
            builder.Append("4. **Code names entries through the generated keys,** such as `").Append(keysClass).Append(".Shop.Purchase`, ")
                .Append("and an entry with arguments is a method: `").Append(keysClass).Append(".Shop.Coins(coins: 5)`.").Append(newLine);
            builder.Append("5. **Never edit the generated `*Keys.cs` files,** which the editor rewrites as keys change, and never write the ")
                .Append("`[1a2b3c]` fingerprint tags of translation lines yourself: the editor writes them, and a translation without one ")
                .Append("counts as unverified.").Append(newLine);
            builder.Append("6. **To rename an entry,** rename it in every language file of its table and put `@formerly OldName` above it in the ")
                .Append("source file, so references saved in scenes and data keep working.").Append(newLine);
            builder.Append("7. **Unity checks each file as it imports it** and reports problems with their file and line in the Console. ")
                .Append("*Tools > ReactiveLocalizer > Validate* checks the whole project, and player builds fail on errors.").Append(newLine);
            builder.Append(EndMarker).Append(newLine);
            return builder.ToString();
        }

        /// <summary>
        /// Puts <paramref name="section"/> in <paramref name="document"/>: in place of the section it has, or after a blank
        /// line at its end. False when the document has the section's start without its end, which a person has to fix,
        /// since replacing everything after the start could erase their text.
        /// </summary>
        public static bool TryMerge(string document, string section, out string merged)
        {
            if (string.IsNullOrEmpty(document))
            {
                merged = section;
                return true;
            }
            int start = document.IndexOf(StartMarker, StringComparison.Ordinal);
            if (start < 0)
            {
                string newLine = document.Contains("\r\n") ? "\r\n" : "\n";
                merged = document.TrimEnd() + newLine + newLine + section;
                return true;
            }
            int end = document.IndexOf(EndMarker, start, StringComparison.Ordinal);
            if (end < 0)
            {
                merged = null;
                return false;
            }
            merged = document.Substring(0, start) + section.TrimEnd() + document.Substring(end + EndMarker.Length);
            return true;
        }

        private static void AppendCatalog(StringBuilder builder, AgentGuideCatalog catalog, string newLine)
        {
            builder.Append("- `").Append(catalog.Path).Append('`');
            if (catalog.IsEditorOnly)
            {
                builder.Append(", editor only");
            }
            builder.Append(": ");
            if (catalog.Languages.Count == 0)
            {
                builder.Append("no languages yet");
            }
            for (int i = 0; i < catalog.Languages.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(i == catalog.Languages.Count - 1 ? " and " : ", ");
                }
                builder.Append(catalog.Languages[i]);
                if (string.Equals(catalog.Languages[i], catalog.SourceLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Append(" (source)");
                }
            }
            builder.Append(". Keys: `").Append(catalog.KeysClassName).Append("` in `").Append(catalog.GeneratedCodePath).Append("`.").Append(newLine);
        }
    }
}
