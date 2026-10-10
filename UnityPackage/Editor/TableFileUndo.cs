using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Makes edits of localization files undoable. Files aren't Unity objects, so this stand-in holds the text of the
    /// files an edit touched; Unity's Undo records it before each edit, and restoring it writes those texts back.
    /// </summary>
    /// <remarks>
    /// Undoing or redoing a step writes only the files whose recorded text the step changed, so files changed
    /// elsewhere since, as in a text editor or by deleting them, are never overwritten by another step or by an
    /// unrelated Undo. A file an edit created is recorded as absent before the edit, so undoing deletes it again. The
    /// stand-in lives for the editor session only and is never saved.
    /// </remarks>
    internal sealed class TableFileUndo : ScriptableObject
    {
        private static readonly UTF8Encoding Utf8WithoutMark = new(false);
        private static TableFileUndo _instance;
        // The state of the stand-in the files on disk match, and what it recorded then; an Undo or Redo that restores
        // another state changed the files whose recorded text differs.
        private static long _appliedState;
        private static readonly Dictionary<string, FileState> AppliedFiles = new(StringComparer.Ordinal);
        private static long _lastState;

        [SerializeField] private List<FileState> _files = new();
        [SerializeField] private long _state;

        private static TableFileUndo Instance
        {
            get
            {
                if (_instance == null)
                {
                    // The stand-in outlives a domain reload, and Undo's history points at that very object.
                    TableFileUndo[] existing = Resources.FindObjectsOfTypeAll<TableFileUndo>();
                    _instance = existing.Length > 0 ? existing[0] : CreateInstance<TableFileUndo>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return _instance;
            }
        }

        /// <summary>
        /// Writes <paramref name="changes"/>, each a file's new text or null to delete it, as one step Undo can take back,
        /// then imports them.
        /// </summary>
        public static void Write(IReadOnlyList<KeyValuePair<string, string>> changes, string undoName)
        {
            if (changes.Count == 0)
            {
                return;
            }
            TableFileUndo undo = Instance;
            // What the files are now is recorded first, so the step Undo records holds their texts before the edit.
            for (int i = 0; i < changes.Count; i++)
            {
                undo.Remember(changes[i].Key, ReadOrNull(changes[i].Key));
            }
            undo._state = NextState();
            // Each edit is a step of its own, even when several happen within one frame.
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(undo, undoName);
            Undo.SetCurrentGroupName(undoName);
            for (int i = 0; i < changes.Count; i++)
            {
                undo.Remember(changes[i].Key, changes[i].Value);
            }
            undo._state = NextState();
            RememberApplied(undo);
            Apply(changes);
        }

        [InitializeOnLoadMethod]
        private static void ListenForUndo()
        {
            Undo.undoRedoPerformed -= Restore;
            Undo.undoRedoPerformed += Restore;
            // After a domain reload, the files on disk match the stand-in as it was left.
            TableFileUndo[] existing = Resources.FindObjectsOfTypeAll<TableFileUndo>();
            if (existing.Length > 0)
            {
                RememberApplied(existing[0]);
            }
        }

        /// <summary>Returns a state no stand-in had before, even across domain reloads.</summary>
        private static long NextState()
        {
            _lastState = Math.Max(DateTime.UtcNow.Ticks, _lastState + 1);
            return _lastState;
        }

        private static void RememberApplied(TableFileUndo undo)
        {
            _appliedState = undo._state;
            AppliedFiles.Clear();
            for (int i = 0; i < undo._files.Count; i++)
            {
                AppliedFiles[undo._files[i].Path] = undo._files[i];
            }
        }

        /// <summary>
        /// When the Undo or Redo restored another state of the stand-in, writes back the files whose recorded text
        /// differs between the two states: the files of the steps undone or redone.
        /// </summary>
        private static void Restore()
        {
            TableFileUndo undo = Instance;
            if (undo._state == _appliedState)
            {
                return;
            }
            List<KeyValuePair<string, string>> changes = new();
            for (int i = 0; i < undo._files.Count; i++)
            {
                FileState file = undo._files[i];
                // A file the other state didn't record is no part of the steps; a step records every file it changes.
                if (!AppliedFiles.TryGetValue(file.Path, out FileState applied) || applied.IsSame(file))
                {
                    continue;
                }
                string text = file.IsPresent ? file.Text : null;
                if (!string.Equals(ReadOrNull(file.Path), text, StringComparison.Ordinal))
                {
                    changes.Add(new KeyValuePair<string, string>(file.Path, text));
                }
            }
            RememberApplied(undo);
            Apply(changes);
        }

        private static void Apply(IReadOnlyList<KeyValuePair<string, string>> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }
            List<string> deleted = new();
            using KeysGenerator.DeferralScope deferral = KeysGenerator.Defer();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < changes.Count; i++)
                {
                    string path = changes[i].Key;
                    if (changes[i].Value == null)
                    {
                        deleted.Add(path);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, changes[i].Value, Utf8WithoutMark);
                    AssetDatabase.ImportAsset(path);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            for (int i = 0; i < deleted.Count; i++)
            {
                AssetDatabase.DeleteAsset(deleted[i]);
            }
            CatalogIndex.Invalidate();
        }

        private void Remember(string path, string text)
        {
            int index = _files.FindIndex(file => string.Equals(file.Path, path, StringComparison.Ordinal));
            FileState state = new() { Path = path, IsPresent = text != null, Text = text ?? string.Empty };
            if (index >= 0)
            {
                _files[index] = state;
            }
            else
            {
                _files.Add(state);
            }
        }

        private static string ReadOrNull(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

        [Serializable]
        private struct FileState
        {
            public string Path;
            public bool IsPresent;
            public string Text;

            public bool IsSame(FileState other) => IsPresent == other.IsPresent && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }
    }
}
