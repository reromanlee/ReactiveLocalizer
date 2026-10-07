using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Messages;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Creates entries from editor tools, such as a sample's context action or a project's own tool: it picks the
    /// table, suggests the key, and writes the entry into the table's source-language file.
    /// </summary>
    public static class EntryAuthoring
    {
        /// <summary>Endings that describe a UI element rather than its role, left out of suggested keys.</summary>
        private static readonly string[] ElementSuffixes = { "Label", "Text", "Caption", "Str", "Txt", "TMP", "TextMeshPro" };

        /// <summary>
        /// Suggests a key for text a GameObject shows, from the GameObject's name rather than the text: entries are
        /// named after their role. <c>Title Label (1)</c> suggests <c>Title</c>.
        /// </summary>
        public static string SuggestKey(string gameObjectName)
        {
            List<string> words = SplitWords(gameObjectName);
            while (words.Count > 1 && IsElementSuffix(words[words.Count - 1]))
            {
                words.RemoveAt(words.Count - 1);
            }
            string key = JoinPascalCase(words);
            return NameRules.IsValid(key) ? key : "Entry";
        }

        /// <summary>
        /// Suggests the table for text shown by <paramref name="context"/>: the table named after the prefab being
        /// edited, or else the object's scene, matching the convention of one table per owner.
        /// </summary>
        public static string SuggestTable(GameObject context)
        {
            string owner = null;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && context != null && stage.IsPartOfPrefabContents(context))
            {
                owner = Path.GetFileNameWithoutExtension(stage.assetPath);
            }
            else if (context != null && context.scene.IsValid())
            {
                owner = context.scene.name;
            }
            string table = JoinPascalCase(SplitWords(owner));
            return NameRules.IsValid(table) ? table : "Common";
        }

        /// <summary>
        /// Writes a new entry into the source-language file of <paramref name="tableName"/> in the catalog named
        /// <paramref name="catalogName"/>, or in the default catalog when it is empty, creating the file for a new
        /// table. A key already taken gets a number appended, so creating never replaces an entry.
        /// </summary>
        /// <returns>False, with <paramref name="problem"/> saying why, when the catalog is missing, read-only or unusable, or a name breaks the naming rule.</returns>
        public static bool TryCreateEntry(string catalogName, string tableName, string key, string text, out EntryReference reference, out string problem)
        {
            reference = default;
            IndexedCatalog catalog = string.IsNullOrEmpty(catalogName)
                ? CatalogIndex.DefaultCatalog
                : NameRules.IsValid(catalogName) ? CatalogIndex.Find(new CatalogKey(catalogName)) : null;
            if (catalog?.Info == null)
            {
                problem = string.IsNullOrEmpty(catalogName)
                    ? "There is no default catalog. Create one with Assets > Create > ReactiveLocalizer > Catalog."
                    : $"There is no usable catalog '{catalogName}'.";
                return false;
            }
            if (!catalog.IsWritable)
            {
                problem = $"The catalog '{catalog.Name}' is inside a read-only package.";
                return false;
            }
            if (!NameRules.IsValid(tableName) || !NameRules.IsValid(key))
            {
                problem = $"'{tableName}.{key}' can't be an entry: {NameRules.Description}.";
                return false;
            }

            string sourceLanguage = catalog.Info.SourceLanguage.Name;
            string path = $"{catalog.Folder}/{tableName}.{sourceLanguage}.{LocalizationFiles.TableExtension}";
            if (catalog.TryGetTable(new TableKey(tableName), out IndexedTable table) && table.SourcePath != null)
            {
                path = table.SourcePath;
            }
            string existing = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            string uniqueKey = MakeUnique(TableDocument.Parse(existing), key);

            StringBuilder line = new();
            if (existing.Length > 0 && existing[existing.Length - 1] != '\n')
            {
                line.Append('\n');
            }
            line.Append(uniqueKey).Append(" = ");
            // The text is shown as it is, so braces and apostrophes are quoted rather than read as a message.
            TextEscaping.Escape(MessageQuoting.Quote(text), line);
            line.Append('\n');
            File.AppendAllText(path, line.ToString());
            AssetDatabase.ImportAsset(path);
            CatalogIndex.Invalidate();

            reference = new EntryReference(catalog.Name, tableName, uniqueKey);
            problem = null;
            return true;
        }

        private static string MakeUnique(TableDocument document, string key)
        {
            if (!document.TryGetEntry(key, out _))
            {
                return key;
            }
            for (int number = 2; ; number++)
            {
                string candidate = key + number;
                if (!document.TryGetEntry(candidate, out _))
                {
                    return candidate;
                }
            }
        }

        /// <summary>Splits a name into words at every character that can't be in a name, and where lower case meets upper case.</summary>
        private static List<string> SplitWords(string name)
        {
            List<string> words = new();
            if (string.IsNullOrEmpty(name))
            {
                return words;
            }
            StringBuilder word = new();
            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                bool isAsciiLetterOrDigit = character < 128 && char.IsLetterOrDigit(character);
                bool startsWord = word.Length > 0 && char.IsUpper(character) && char.IsLower(word[word.Length - 1]);
                if (!isAsciiLetterOrDigit || startsWord)
                {
                    Flush(word, words);
                }
                if (isAsciiLetterOrDigit)
                {
                    word.Append(character);
                }
            }
            Flush(word, words);
            // Numbering such as "(1)" only tells copies apart; it is no part of a role.
            words.RemoveAll(part => char.IsDigit(part[0]));
            return words;
        }

        private static void Flush(StringBuilder word, List<string> words)
        {
            if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }

        private static string JoinPascalCase(List<string> words)
        {
            StringBuilder joined = new();
            for (int i = 0; i < words.Count; i++)
            {
                string word = words[i];
                joined.Append(char.ToUpperInvariant(word[0])).Append(word, 1, word.Length - 1);
            }
            return joined.ToString();
        }

        private static bool IsElementSuffix(string word)
        {
            for (int i = 0; i < ElementSuffixes.Length; i++)
            {
                if (string.Equals(word, ElementSuffixes[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
