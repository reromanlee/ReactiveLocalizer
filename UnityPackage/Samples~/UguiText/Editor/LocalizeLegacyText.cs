using reromanlee.ReactiveLocalizer.Editor;
using reromanlee.ReactiveLocalizer.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace reromanlee.ReactiveLocalizer.Samples.UguiText.Editor
{
    /// <summary>
    /// Adds <c>Localize</c> to the context menu of every legacy uGUI text: it turns the current text into a new entry
    /// of the default catalog, in the table named after the scene or prefab, keyed after the GameObject's role, and
    /// adds a <see cref="LocalizedLegacyText"/> showing it.
    /// </summary>
    internal static class LocalizeLegacyText
    {
        private const string MenuPath = "CONTEXT/Text/Localize";

        [MenuItem(MenuPath)]
        private static void Localize(MenuCommand command)
        {
            Text text = (Text)command.context;
            string table = EntryAuthoring.SuggestTable(text.gameObject);
            string key = EntryAuthoring.SuggestKey(text.gameObject.name);
            if (!EntryAuthoring.TryCreateEntry(null, table, key, text.text, out EntryReference reference, out string problem))
            {
                Debug.LogWarning($"[ReactiveLocalizer] Couldn't localize '{text.name}': {problem}", text);
                return;
            }
            LocalizedLegacyText localized = text.GetComponent<LocalizedLegacyText>();
            if (localized == null)
            {
                localized = Undo.AddComponent<LocalizedLegacyText>(text.gameObject);
            }
            Undo.RecordObject(localized, "Localize Text");
            localized.Entry = reference;
            EditorUtility.SetDirty(localized);
            Debug.Log($"[ReactiveLocalizer] '{text.name}' now shows {reference}.", text);
        }

        [MenuItem(MenuPath, true)]
        private static bool CanLocalize(MenuCommand command)
        {
            return command.context is Text text && text.GetComponent<LocalizedLegacyText>() == null;
        }
    }
}
