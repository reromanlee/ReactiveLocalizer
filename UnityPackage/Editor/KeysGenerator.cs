using reromanlee.ReactiveLocalizer.Authoring;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Keeps every writable catalog's generated code current. It runs once after a batch of imports, writes a file
    /// only when its content would change, so translating never costs a recompile, and waits while Play Mode runs,
    /// where a recompile would interrupt it.
    /// </summary>
    internal static class KeysGenerator
    {
        private static readonly UTF8Encoding Utf8WithoutMark = new(false);
        private static bool _isScheduled;
        private static bool _isWaitingForEditMode;

        /// <summary>
        /// Whether generation is held back, as editor tests do: writing a script would recompile and reload the
        /// domain in the middle of them.
        /// </summary>
        public static bool IsSuspended { get; set; }

        /// <summary>Runs generation once the current batch of imports is done.</summary>
        public static void Schedule()
        {
            if (_isScheduled)
            {
                return;
            }
            _isScheduled = true;
            EditorApplication.delayCall += RunScheduled;
        }

        /// <summary>Brings every generated file up to date now. Returns whether any file was written.</summary>
        public static bool Run()
        {
            _isScheduled = false;
            if (IsSuspended)
            {
                return false;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                WaitForEditMode();
                return false;
            }
            bool hasWritten = false;
            foreach (IndexedCatalog catalog in CatalogIndex.Catalogs)
            {
                if (!catalog.IsWritable || catalog.Info == null)
                {
                    continue;
                }
                List<string> problems = new();
                string source = KeysScriptWriter.Write(catalog.CreateKeysScript(), problems);
                for (int i = 0; i < problems.Count; i++)
                {
                    Debug.LogWarning($"[ReactiveLocalizer] {catalog.Path}: {problems[i]}");
                }
                hasWritten |= WriteIfChanged(catalog.GeneratedCodePath, source);
            }
            return hasWritten;
        }

        private static void RunScheduled()
        {
            Run();
        }

        private static bool WriteIfChanged(string path, string source)
        {
            // Line endings may have been converted by git or an editor; only a change of content counts.
            if (File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == source)
            {
                return false;
            }
            File.WriteAllText(path, source, Utf8WithoutMark);
            AssetDatabase.ImportAsset(path);
            return true;
        }

        private static void WaitForEditMode()
        {
            if (_isWaitingForEditMode)
            {
                return;
            }
            _isWaitingForEditMode = true;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            _isWaitingForEditMode = false;
            Schedule();
        }
    }
}
