using reromanlee.ReactiveLocalizer.Authoring;
using System;
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
    /// <remarks>
    /// Edits the editor's own tools make, such as creating an entry from the Inspector, leave the code to be written
    /// when the editor loses focus: code only needs new keys once someone switches to write it, and the recompile
    /// doesn't interrupt them in the editor.
    /// </remarks>
    internal static class KeysGenerator
    {
        private const string PendingState = "ReactiveLocalizer.KeysGenerator.IsPending";
        private const string StartupCheckState = "ReactiveLocalizer.KeysGenerator.HasCheckedAtStartup";
        private static readonly UTF8Encoding Utf8WithoutMark = new(false);
        private static bool _isScheduled;
        private static bool _isWaitingForEditMode;
        private static int _deferrals;

        /// <summary>
        /// Whether generation is held back, as editor tests do: writing a script would recompile and reload the
        /// domain in the middle of them.
        /// </summary>
        public static bool IsSuspended { get; set; }

        /// <summary>Whether code waits to be written when the editor loses focus. Kept across domain reloads.</summary>
        public static bool IsPending
        {
            get => SessionState.GetBool(PendingState, false);
            private set => SessionState.SetBool(PendingState, value);
        }

        /// <summary>Until the returned scope is disposed, imports leave generation for when the editor loses focus.</summary>
        public static DeferralScope Defer()
        {
            _deferrals++;
            return new DeferralScope(true);
        }

        /// <summary>Runs generation once the current batch of imports is done, or, while deferred, when the editor loses focus.</summary>
        public static void Schedule()
        {
            if (_deferrals > 0)
            {
                IsPending = true;
                return;
            }
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
            IsPending = false;
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
                KeysScript script = catalog.CreateKeysScript(out string brokenMessage);
                // A broken message has no known arguments; keeping the last generated code means a typo in a text
                // never breaks the code calling it. A catalog without generated code yet gets it anyway.
                if (brokenMessage != null && File.Exists(catalog.GeneratedCodePath))
                {
                    Debug.LogWarning($"[ReactiveLocalizer] {catalog.GeneratedCodePath} keeps its current members until the message of {brokenMessage} is fixed.");
                    continue;
                }
                string source = KeysScriptWriter.Write(script, problems);
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

        [InitializeOnLoadMethod]
        private static void ListenForFocusLoss()
        {
            EditorApplication.focusChanged -= OnFocusChanged;
            EditorApplication.focusChanged += OnFocusChanged;
            // Code left pending when the last session ended is caught up once, as the editor starts.
            if (!SessionState.GetBool(StartupCheckState, false))
            {
                SessionState.SetBool(StartupCheckState, true);
                Schedule();
            }
        }

        private static void OnFocusChanged(bool hasFocus)
        {
            if (!hasFocus && IsPending)
            {
                Run();
            }
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

        /// <summary>Ends a deferral of <see cref="Defer"/> when disposed.</summary>
        public readonly struct DeferralScope : IDisposable
        {
            private readonly bool _isActive;

            internal DeferralScope(bool isActive)
            {
                _isActive = isActive;
            }

            public void Dispose()
            {
                if (_isActive && _deferrals > 0)
                {
                    _deferrals--;
                }
            }
        }
    }
}
