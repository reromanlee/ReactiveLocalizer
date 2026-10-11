using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Writes the localization section of the project's AGENTS.md or CLAUDE.md, the files coding agents read, after asking
    /// which one. Nothing is written unless asked, and writing again updates the section in place.
    /// </summary>
    internal static class AgentGuideMenu
    {
        private const string Title = "Write Agent Guide";

        [MenuItem("Tools/ReactiveLocalizer/Write Agent Guide", priority = 60)]
        private static void WriteAgentGuide()
        {
            string path = ChooseFile(Path.GetDirectoryName(Application.dataPath));
            if (path == null)
            {
                return;
            }
            string name = Path.GetFileName(path);
            try
            {
                bool hasByteOrderMark = HasByteOrderMark(path);
                string document = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                string section = AgentGuide.Write(CollectCatalogs(), document.Contains("\r\n") ? "\r\n" : "\n");
                if (!AgentGuide.TryMerge(document, section, out string merged))
                {
                    EditorUtility.DisplayDialog(Title,
                        $"{name} has the start of the localization section but not its end, so writing it could erase your text. " +
                        "Fix or remove the section, then write it again.", "OK");
                    return;
                }
                File.WriteAllText(path, merged, new UTF8Encoding(hasByteOrderMark));
                Debug.Log($"[ReactiveLocalizer] Wrote the localization section for coding agents into {name}.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                EditorUtility.DisplayDialog(Title, $"Couldn't write {name}: {exception.Message}", "OK");
            }
        }

        /// <summary>Asks which file gets the section, offering to create AGENTS.md when neither exists. Null when canceled.</summary>
        private static string ChooseFile(string projectFolder)
        {
            string agents = Path.Combine(projectFolder, "AGENTS.md");
            string claude = Path.Combine(projectFolder, "CLAUDE.md");
            bool hasAgents = File.Exists(agents);
            bool hasClaude = File.Exists(claude);
            if (hasAgents && hasClaude)
            {
                int choice = EditorUtility.DisplayDialogComplex(Title,
                    "Which file should get the localization section for coding agents? Writing it again later updates it in place.",
                    "AGENTS.md", "Cancel", "CLAUDE.md");
                return choice == 0 ? agents : choice == 2 ? claude : null;
            }
            if (hasAgents || hasClaude)
            {
                string existing = hasAgents ? agents : claude;
                return EditorUtility.DisplayDialog(Title,
                    $"Add a localization section for coding agents to {Path.GetFileName(existing)}? Writing it again later updates it in place.",
                    "Write", "Cancel") ? existing : null;
            }
            return EditorUtility.DisplayDialog(Title,
                "The project has no AGENTS.md or CLAUDE.md. Create AGENTS.md with a localization section for coding agents?",
                "Create", "Cancel") ? agents : null;
        }

        private static List<AgentGuideCatalog> CollectCatalogs()
        {
            List<AgentGuideCatalog> catalogs = new();
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                List<string> languages = new();
                foreach (CatalogDocumentLanguage language in catalog.Document.Languages)
                {
                    languages.Add(language.Name);
                }
                string sourceLanguage = catalog.Info?.SourceLanguage.Name
                    ?? (catalog.Document.TryGetAttribute(DocumentNames.Source, out DocumentProperty source) ? source.Value : string.Empty);
                catalogs.Add(new AgentGuideCatalog(catalog.Path, sourceLanguage, languages, catalog.Name + "Keys", catalog.GeneratedCodePath,
                    catalog.IsEditorOnly));
            }
            return catalogs;
        }

        private static bool HasByteOrderMark(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }
            byte[] start = new byte[3];
            using FileStream stream = File.OpenRead(path);
            return stream.Read(start, 0, 3) == 3 && start[0] == 0xEF && start[1] == 0xBB && start[2] == 0xBF;
        }
    }
}
