using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.Mods.Editor
{
    /// <summary>
    /// <c>Tools/ReactiveLocalizer/Samples/Install Example Mod</c>: copies the sample's example fan translation, an
    /// invented Elvish for the Quick Start scene plus a patch of its English, into the mods folder players use.
    /// </summary>
    internal static class ExampleModInstaller
    {
        private const string ExampleFolder = "ExampleMod~";

        [MenuItem("Tools/ReactiveLocalizer/Samples/Install Example Mod")]
        private static void Install()
        {
            string source = FindExample();
            if (source == null)
            {
                Debug.LogWarning($"[ReactiveLocalizer] The Mods sample's {ExampleFolder} folder wasn't found next to its scripts.");
                return;
            }
            try
            {
                Copy(source, ModTranslations.Root);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[ReactiveLocalizer] The example mod couldn't be copied to {ModTranslations.Root}: {exception.Message}");
                return;
            }
            Debug.Log($"[ReactiveLocalizer] Installed the example mod in {ModTranslations.Root}. Enter Play Mode in the Quick Start scene and pick Elvish.");
            EditorUtility.RevealInFinder(ModTranslations.Root);
        }

        [MenuItem("Tools/ReactiveLocalizer/Samples/Remove Mods")]
        private static void Remove()
        {
            if (Directory.Exists(ModTranslations.Root) &&
                EditorUtility.DisplayDialog("Remove Mods", $"Delete every mod in {ModTranslations.Root}?", "Delete", "Cancel"))
            {
                Directory.Delete(ModTranslations.Root, true);
            }
        }

        /// <summary>Returns the example mod's folder, found next to the sample's own scripts wherever it was imported.</summary>
        private static string FindExample()
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(MonoScript)} {nameof(ModTranslations)}"))
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(scriptPath) != nameof(ModTranslations))
                {
                    continue;
                }
                string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(scriptPath)) ?? string.Empty, ExampleFolder);
                if (Directory.Exists(folder))
                {
                    return folder;
                }
            }
            return null;
        }

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            }
            foreach (string folder in Directory.GetDirectories(source))
            {
                Copy(folder, Path.Combine(target, Path.GetFileName(folder)));
            }
        }
    }
}
