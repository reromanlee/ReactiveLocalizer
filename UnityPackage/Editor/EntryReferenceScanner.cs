using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Finds the <see cref="EntryReference"/> fields saved in scenes, prefabs and other assets, with the values Unity
    /// actually uses: a prefab instance in a scene is read with its overrides applied.
    /// </summary>
    /// <remarks>
    /// Only files whose text mentions an entry are opened, so scanning a large project stays quick. Scenes not open
    /// already are opened alongside the open ones and closed again, leaving the editor's scenes as they were.
    /// </remarks>
    internal static class EntryReferenceScanner
    {
        private const string EntryField = "_entry";

        // Arrays of these never hold an entry reference, so the scan never walks their elements.
        private static readonly HashSet<string> PlainElementTypes = new(StringComparer.Ordinal)
        {
            "int", "uint", "float", "double", "bool", "char", "byte", "sbyte", "short", "ushort", "long", "ulong", "string",
            "Vector2", "Vector3", "Vector4", "Vector2Int", "Vector3Int", "Quaternion", "Color", "Color32", "Rect", "Bounds", "Matrix4x4"
        };

        /// <summary>A reference found, with where it is.</summary>
        public readonly struct FoundReference
        {
            public FoundReference(EntryReference reference, string location)
            {
                Reference = reference;
                Location = location;
            }

            public EntryReference Reference { get; }

            /// <summary>The asset, the object within it and the field, such as <c>Assets/Main.unity: Canvas/Title (LocalizedText._entry)</c>.</summary>
            public string Location { get; }
        }

        /// <summary>Scans every scene, prefab and asset in Assets.</summary>
        public static List<FoundReference> ScanProject()
        {
            List<FoundReference> found = new();
            string[] guids = AssetDatabase.FindAssets("t:Scene t:Prefab t:ScriptableObject", new[] { "Assets" });
            HashSet<string> paths = new(StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                paths.Add(AssetDatabase.GUIDToAssetPath(guids[i]));
            }
            ScanPaths(paths, found, "Looking for entry references");
            return found;
        }

        /// <summary>Scans the assets in Resources folders, which a build ships whether or not a scene uses them.</summary>
        public static List<FoundReference> ScanResources()
        {
            List<FoundReference> found = new();
            HashSet<string> paths = new(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab t:ScriptableObject", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    paths.Add(path);
                }
            }
            ScanPaths(paths, found, "Looking for entry references in Resources");
            return found;
        }

        /// <summary>Scans the scenes, prefabs and assets at <paramref name="paths"/>.</summary>
        public static List<FoundReference> Scan(IEnumerable<string> paths)
        {
            List<FoundReference> found = new();
            ScanPaths(paths, found, "Looking for entry references");
            return found;
        }

        /// <summary>Scans a scene already loaded, as a build hands each scene it processes.</summary>
        public static List<FoundReference> ScanLoadedScene(Scene scene)
        {
            List<FoundReference> found = new();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                ScanGameObject(root, scene.path, found);
            }
            return found;
        }

        private static void ScanPaths(IEnumerable<string> paths, List<FoundReference> found, string title)
        {
            List<string> candidates = new();
            foreach (string path in paths)
            {
                if (MentionsEntries(path))
                {
                    candidates.Add(path);
                }
            }
            candidates.Sort(StringComparer.Ordinal);
            try
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("ReactiveLocalizer", $"{title}: {candidates[i]}", (float)i / Math.Max(1, candidates.Count));
                    if (candidates[i].EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    {
                        ScanScene(candidates[i], found);
                    }
                    else
                    {
                        ScanAsset(candidates[i], found);
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Whether the file's text names the entry field at all, as every serialized reference and override does.</summary>
        private static bool MentionsEntries(string path)
        {
            try
            {
                return File.ReadAllText(path).IndexOf(EntryField, StringComparison.Ordinal) >= 0;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static void ScanAsset(string path, List<FoundReference> found)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is GameObject gameObject)
                {
                    // Every GameObject of a prefab is loaded as an asset of its own; components are read from each once.
                    foreach (Component component in gameObject.GetComponents<Component>())
                    {
                        ScanObject(component, $"{path}: {GetHierarchyPath(gameObject.transform)}", found);
                    }
                }
                else if (asset is ScriptableObject)
                {
                    ScanObject(asset, path, found);
                }
            }
        }

        private static void ScanScene(string path, List<FoundReference> found)
        {
            if (Application.isPlaying)
            {
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
            try
            {
                found.AddRange(ScanLoadedScene(scene));
            }
            finally
            {
                if (!wasLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ScanGameObject(GameObject gameObject, string assetPath, List<FoundReference> found)
        {
            string location = $"{assetPath}: {GetHierarchyPath(gameObject.transform)}";
            foreach (Component component in gameObject.GetComponents<Component>())
            {
                ScanObject(component, location, found);
            }
            foreach (Transform child in gameObject.transform)
            {
                ScanGameObject(child.gameObject, assetPath, found);
            }
        }

        private static void ScanObject(Object target, string location, List<FoundReference> found)
        {
            // A missing script leaves a null component.
            if (target == null)
            {
                return;
            }
            using SerializedObject serialized = new(target);
            SerializedProperty property = serialized.GetIterator();
            bool isEntering = true;
            while (property.Next(isEntering))
            {
                isEntering = property.propertyType == SerializedPropertyType.Generic &&
                             !(property.isArray && PlainElementTypes.Contains(property.arrayElementType));
                // A list of references reports its elements' type as its own, so only the elements count.
                if (property.propertyType != SerializedPropertyType.Generic || property.isArray || property.type != nameof(EntryReference))
                {
                    continue;
                }
                string catalog = property.FindPropertyRelative("_catalog")?.stringValue;
                string table = property.FindPropertyRelative("_table")?.stringValue;
                string entry = property.FindPropertyRelative(EntryField)?.stringValue;
                // An empty reference picks nothing, which is a choice, not a broken key.
                if (!string.IsNullOrEmpty(entry) || !string.IsNullOrEmpty(table))
                {
                    found.Add(new FoundReference(new EntryReference(catalog, table, entry), $"{location} ({target.GetType().Name}.{property.propertyPath})"));
                }
                isEntering = false;
            }
        }

        private static string GetHierarchyPath(Transform transform)
        {
            StringBuilder path = new(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path.Insert(0, '/').Insert(0, parent.name);
            }
            return path.ToString();
        }
    }
}
