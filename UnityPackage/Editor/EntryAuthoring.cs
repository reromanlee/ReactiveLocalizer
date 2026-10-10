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

        /// <summary>Field names that only say a field holds an entry, which add nothing to a key.</summary>
        private static readonly string[] GenericFieldWords = { "Entry", "Key", "Reference", "Ref", "Localized", "Content", "Value", "Message", "String" };

        /// <summary>
        /// Suggests a key for text a GameObject shows, from the GameObject's name rather than the text: entries are
        /// named after their role. <c>Title Label (1)</c> suggests <c>Title</c>.
        /// </summary>
        public static string SuggestKey(string gameObjectName)
        {
            List<string> words = SplitWords(gameObjectName);
            StripElementSuffixes(words);
            string key = JoinPascalCase(words);
            return NameRules.IsValid(key) ? key : "Entry";
        }

        /// <summary>
        /// Suggests a key for the field named <paramref name="fieldName"/> of a component on a GameObject: the
        /// GameObject's role, followed by the field's own role when it has one. <c>_description</c> on <c>Sword</c>
        /// suggests <c>SwordDescription</c>, and <c>_entry</c> on <c>Title Label</c> suggests <c>Title</c>.
        /// </summary>
        public static string SuggestKey(string gameObjectName, string fieldName)
        {
            List<string> words = SplitWords(gameObjectName);
            StripElementSuffixes(words);
            string owner = JoinPascalCase(words);
            List<string> fieldWords = SplitWords(fieldName);
            StripElementSuffixes(fieldWords);
            fieldWords.RemoveAll(IsGenericFieldWord);
            string role = JoinPascalCase(fieldWords);
            if (!NameRules.IsValid(owner))
            {
                return NameRules.IsValid(role) ? role : "Entry";
            }
            if (role.Length == 0 || owner.EndsWith(role, StringComparison.OrdinalIgnoreCase) || !NameRules.IsValid(owner + role))
            {
                return owner;
            }
            return owner + role;
        }

        /// <summary>
        /// Suggests the table for text shown by <paramref name="context"/>: the table named after the prefab being
        /// edited or selected, or else the object's scene, matching the convention of one table per owner.
        /// </summary>
        public static string SuggestTable(GameObject context)
        {
            return SuggestOwnerTable(context) ?? "Common";
        }

        /// <summary>Returns the table named after the prefab or scene owning <paramref name="context"/>, or null when it has no usable name, as an unsaved scene.</summary>
        internal static string SuggestOwnerTable(GameObject context)
        {
            if (context == null)
            {
                return null;
            }
            string owner;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.IsPartOfPrefabContents(context))
            {
                owner = Path.GetFileNameWithoutExtension(stage.assetPath);
            }
            else if (PrefabUtility.IsPartOfPrefabAsset(context))
            {
                owner = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(context));
            }
            else
            {
                owner = context.scene.IsValid() ? context.scene.name : null;
            }
            string table = JoinPascalCase(SplitWords(owner));
            return NameRules.IsValid(table) ? table : null;
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

            TableEdit edit = new(catalog, tableName);
            if (edit.Files.Source?.HasErrors == true)
            {
                problem = $"The {catalog.Info.SourceLanguage.Name} file of '{tableName}' has errors. Fix them first, since rewriting the file would drop the lines that have them.";
                return false;
            }
            // The text is shown as it is, so braces and apostrophes are quoted rather than read as a message.
            string quoted = MessageQuoting.Quote(text ?? string.Empty);
            string uniqueKey = key;
            for (int number = 2; !edit.Files.TryAddEntry(uniqueKey, quoted, out problem); number++)
            {
                // A taken key, or a former name of another entry, gets a number; nothing else makes adding fail here.
                if (number > 999)
                {
                    return false;
                }
                uniqueKey = key + number;
            }
            TableEdit.Save($"Create {tableName}.{uniqueKey}", edit);

            reference = new EntryReference(catalog.Name, tableName, uniqueKey);
            problem = null;
            return true;
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

        /// <summary>Removes the words ending a name that describe a UI element, keeping at least one word.</summary>
        private static void StripElementSuffixes(List<string> words)
        {
            while (words.Count > 1 && IsAnyOf(words[words.Count - 1], ElementSuffixes))
            {
                words.RemoveAt(words.Count - 1);
            }
        }

        private static bool IsGenericFieldWord(string word) => IsAnyOf(word, GenericFieldWords) || IsAnyOf(word, ElementSuffixes);

        private static bool IsAnyOf(string word, string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(word, names[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
