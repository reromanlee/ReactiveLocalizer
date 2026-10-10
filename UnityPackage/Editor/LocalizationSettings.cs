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

        private void Change(ref bool field, bool value)
        {
            if (field == value)
            {
                return;
            }
            field = value;
            Save(true);
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Project/ReactiveLocalizer", SettingsScope.Project)
            {
                label = "ReactiveLocalizer",
                keywords = new[] { "localization", "validation", "build", "naming" },
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
