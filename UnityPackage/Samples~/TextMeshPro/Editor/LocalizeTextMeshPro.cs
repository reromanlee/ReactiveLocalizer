using reromanlee.ReactiveLocalizer.Editor;
using reromanlee.ReactiveLocalizer.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.TextMeshPro.Editor
{
    /// <summary>
    /// Adds <c>Localize</c> to the context menu of every TextMeshPro text: it turns the current text into a new entry
    /// of the default catalog, in the table named after the scene or prefab, keyed after the GameObject's role, and
    /// adds a <see cref="LocalizedTextMeshPro"/> showing it. One click, no file to open.
    /// </summary>
    internal static class LocalizeTextMeshPro
    {
        private const string MenuPath = "CONTEXT/TMP_Text/Localize";

        [MenuItem(MenuPath)]
        private static void Localize(MenuCommand command)
        {
            TMP_Text text = (TMP_Text)command.context;
            string table = EntryAuthoring.SuggestTable(text.gameObject);
            string key = EntryAuthoring.SuggestKey(text.gameObject.name);
            if (!EntryAuthoring.TryCreateEntry(null, table, key, text.text, out EntryReference reference, out string problem))
            {
                Debug.LogWarning($"[ReactiveLocalizer] Couldn't localize '{text.name}': {problem}", text);
                return;
            }
            LocalizedTextMeshPro localized = text.GetComponent<LocalizedTextMeshPro>();
            if (localized == null)
            {
                localized = Undo.AddComponent<LocalizedTextMeshPro>(text.gameObject);
            }
            Undo.RecordObject(localized, "Localize Text");
            localized.Entry = reference;
            EditorUtility.SetDirty(localized);
            Debug.Log($"[ReactiveLocalizer] '{text.name}' now shows {reference}.", text);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanLocalize(MenuCommand command)
        {
            return command.context is TMP_Text text && text.GetComponent<LocalizedTextMeshPro>() == null;
        }
    }
}
