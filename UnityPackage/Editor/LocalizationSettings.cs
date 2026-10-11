using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// The project's ReactiveLocalizer settings, saved in <c>ProjectSettings/ReactiveLocalizer.asset</c> so the whole
    /// team shares them, and edited under Project Settings.
    /// </summary>
    [FilePath("ProjectSettings/ReactiveLocalizer.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class LocalizationSettings : ScriptableSingleton<LocalizationSettings>
    {
        [SerializeField] private bool _isFailingBuildsOnErrors = true;
        [SerializeField] private bool _isCheckingPascalCase;
        [SerializeField] private bool _isOfferingPseudoLocalization = true;

        /// <summary>Whether validation errors fail player builds. On by default; turning it off ships whatever the errors break.</summary>
        public bool IsFailingBuildsOnErrors
        {
            get => _isFailingBuildsOnErrors;
            set => Change(ref _isFailingBuildsOnErrors, value);
        }

        /// <summary>Whether validation checks table and entry names against the PascalCase convention.</summary>
        public bool IsCheckingPascalCase
        {
            get => _isCheckingPascalCase;
            set => Change(ref _isCheckingPascalCase, value);
        }

        /// <summary>
        /// Whether Play Mode and development builds offer the pseudo language as one of every catalog's languages. On by
        /// default; the editor's preview always offers it, and release builds never do.
        /// </summary>
        public bool IsOfferingPseudoLocalization
        {
            get => _isOfferingPseudoLocalization;
            set
            {
                if (Change(ref _isOfferingPseudoLocalization, value))
                {
                    // Localizers in Play Mode take the catalog again, with or without the language.
                    Localizer.ReloadAll();
                }
            }
        }

        private bool Change(ref bool field, bool value)
        {
            if (field == value)
            {
                return false;
            }
            field = value;
            Save(true);
            return true;
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/ReactiveLocalizer", SettingsScope.Project)
            {
                label = "ReactiveLocalizer",
                keywords = new[] { "localization", "validation", "build", "naming", "pseudo" },
                guiHandler = _ =>
                {
                    LocalizationSettings settings = instance;
                    EditorGUILayout.Space();
                    settings.IsFailingBuildsOnErrors = EditorGUILayout.ToggleLeft(
                        new GUIContent("Fail builds on validation errors", "Missing keys, orphans, broken messages and entries missing in required languages stop a player build."),
                        settings.IsFailingBuildsOnErrors);
                    settings.IsCheckingPascalCase = EditorGUILayout.ToggleLeft(
                        new GUIContent("Check names against PascalCase", "Validation warns about table and entry names such as main_menu or playButton."),
                        settings.IsCheckingPascalCase);
                    settings.IsOfferingPseudoLocalization = EditorGUILayout.ToggleLeft(
                        new GUIContent("Pseudo-localization in Play Mode and development builds", "Every catalog gets the Pseudo language: its source text accented, a third longer and bracketed, so untranslated text, truncation and overflow show before any translation exists. Release builds never have it."),
                        settings.IsOfferingPseudoLocalization);
                    EditorGUILayout.Space();
                    if (GUILayout.Button("Validate Now", GUILayout.Width(120f)))
                    {
                        LocalizationValidation.ValidateProject();
                    }
                }
            };
        }
    }
}
