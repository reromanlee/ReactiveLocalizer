using reromanlee.ReactiveLocalizer.Unity;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.QuickStart
{
    /// <summary>
    /// Draws one button per language of a catalog at the left of the screen, for prototypes and debugging. A switch
    /// made here is remembered by <see cref="GlobalLocalizer"/> for the next session.
    /// </summary>
    [AddComponentMenu("ReactiveLocalizer/Language Picker")]
    public sealed class LanguagePicker : MonoBehaviour
    {
        [Tooltip("The catalog whose languages are listed; its switch reaches every catalog that has the language.")]
        [SerializeField] private CatalogReference _catalog;

        private void OnGUI()
        {
            ILocalizer localizer = GlobalLocalizer.For(_catalog.ToKey());
            GUILayout.BeginArea(new Rect(16f, 16f, 200f, Screen.height - 32f));
            for (int i = 0; i < localizer.Languages.Count; i++)
            {
                LanguageInfo language = localizer.Languages[i];
                bool isCurrent = localizer.CurrentLanguage == language;
                if (GUILayout.Toggle(isCurrent, language.DisplayName, GUI.skin.button) && !isCurrent)
                {
                    GlobalLocalizer.SetLanguage(language.Key);
                }
            }
            GUILayout.EndArea();
        }
    }
}
