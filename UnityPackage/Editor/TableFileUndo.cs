using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Makes edits of localization files undoable. Files aren't Unity objects, so this stand-in holds the text of every
    /// file an edit touched; Unity's Undo records it before each edit, and restoring it writes those texts back.
    /// </summary>
    /// <remarks>
    /// A file an edit created is recorded as absent before the edit, so undoing deletes it again. The stand-in lives
    /// for the editor session only and is never saved.
    /// </remarks>
    internal sealed class TableFileUndo : ScriptableObject
    {
        private static readonly UTF8Encoding Utf8WithoutMark = new(false);
        private static TableFileUndo _instance;

        [SerializeField] private List<FileState> _files = new();

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
            // Each edit is a step of its own, even when several happen within one frame.
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(undo, undoName);
            Undo.SetCurrentGroupName(undoName);
            for (int i = 0; i < changes.Count; i++)
            {
                undo.Remember(changes[i].Key, changes[i].Value);
            }
            Apply(changes);
        }

        [InitializeOnLoadMethod]
        private static void ListenForUndo()
        {
            Undo.undoRedoPerformed -= Restore;
            Undo.undoRedoPerformed += Restore;
        }

        /// <summary>Writes back every file whose text on disk differs from what the restored stand-in holds.</summary>
        private static void Restore()
        {
            TableFileUndo undo = Instance;
            List<KeyValuePair<string, string>> changes = new();
            for (int i = 0; i < undo._files.Count; i++)
            {
                FileState file = undo._files[i];
                string text = file.IsPresent ? file.Text : null;
                if (!string.Equals(ReadOrNull(file.Path), text, StringComparison.Ordinal))
                {
                    changes.Add(new KeyValuePair<string, string>(file.Path, text));
                }
            }
            Apply(changes);
        }

        private static void Apply(IReadOnlyList<KeyValuePair<string, string>> changes)
        {
            if (changes.Count == 0)
            {
                return;
            }
            List<string> deleted = new();
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
        }
    }
}
