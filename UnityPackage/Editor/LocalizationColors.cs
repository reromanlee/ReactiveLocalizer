using reromanlee.ReactiveLocalizer.Authoring;
using UnityEditor;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>The colors the editor tools share, readable on both the dark and the light editor skin.</summary>
    internal static class LocalizationColors
    {
        /// <summary>Secondary text, such as an entry's preview.</summary>
        public static Color Muted => EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.36f, 0.36f, 0.36f);

        /// <summary>Something broken, such as a reference to a key that doesn't exist.</summary>
        public static Color Problem => EditorGUIUtility.isProSkin ? new Color(0.96f, 0.43f, 0.38f) : new Color(0.72f, 0.12f, 0.08f);

        /// <summary>Something that works but wants attention, such as a reference by a former name.</summary>
        public static Color Warning => EditorGUIUtility.isProSkin ? new Color(0.93f, 0.73f, 0.3f) : new Color(0.55f, 0.4f, 0f);

        /// <summary>The outline of popups, which have no window frame of their own.</summary>
        public static Color Border => EditorGUIUtility.isProSkin ? new Color(0.1f, 0.1f, 0.1f) : new Color(0.55f, 0.55f, 0.55f);

        /// <summary>The background of a table cell, tinted by how its entry stands; clear when all is well.</summary>
        /// <param name="cell">The cell.</param>
        /// <param name="hasSource">Whether the entry has source text, which makes a missing cell a missing translation.</param>
        public static Color Tint(in TableSheetCell cell, bool hasSource)
        {
            if ((cell.Problems & TableSheetProblem.Error) != 0 || cell.State == TranslationState.Orphan)
            {
                return WithAlpha(Problem, 0.24f);
            }
            if ((cell.Problems & (TableSheetProblem.Warning | TableSheetProblem.OverLimit)) != 0 || cell.State == TranslationState.Outdated)
            {
                return WithAlpha(Warning, 0.22f);
            }
            if (cell.State == TranslationState.Unverified)
            {
                return WithAlpha(Warning, 0.08f);
            }
            return cell.State == TranslationState.Missing && hasSource ? WithAlpha(Muted, 0.14f) : Color.clear;
        }

        /// <summary>The color of a translation state's name, as the table window's detail shows it.</summary>
        public static Color Of(TranslationState state)
        {
            return state switch
            {
                TranslationState.Orphan => Problem,
                TranslationState.Outdated => Warning,
                TranslationState.Unverified => Warning,
                _ => Muted
            };
        }

        private static Color WithAlpha(Color color, float alpha) => new(color.r, color.g, color.b, alpha);
    }
}
