using System;
using System.Collections.Generic;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Unity
{
    /// <summary>
    /// Shows localized text in the editor outside Play Mode: localizers in the preview language picked in the Scene
    /// view's overlay. A component that runs in Edit Mode binds to <see cref="For"/> there, and its text follows every
    /// language picked and every edit of a table file. Builds never preview.
    /// </summary>
    /// <remarks>
    /// Preview text must never be saved in place of the authored text. A component showing it calls
    /// <see cref="Track"/>, and the editor puts the authored text back for every save of the component's scene or
    /// prefab, showing the preview again right after.
    /// <code>
    /// if (!Application.isPlaying &amp;&amp; EditModePreview.For(catalog) is ILocalizer preview)
    /// {
    ///     _binding = preview.Bind(key, _label, static (label, text) =&gt; label.text = text);
    ///     EditModePreview.Track(this, RestoreAuthoredText, ShowPreview);
    /// }
    /// </code>
    /// </remarks>
    public static class EditModePreview
    {
        private static readonly Dictionary<Component, (Action Restore, Action Show)> TrackedComponents = new();

        /// <summary>Whether text is previewed now: in the editor, outside Play Mode.</summary>
        public static bool IsActive => Provider != null && !Application.isPlaying;

        /// <summary>Hands out the preview localizer of a catalog; set by the editor, null in builds.</summary>
        internal static Func<CatalogKey, ILocalizer> Provider { get; set; }

        /// <summary>The components showing preview text, with how each puts its authored text back and shows the preview again.</summary>
        internal static IReadOnlyDictionary<Component, (Action Restore, Action Show)> Tracked => TrackedComponents;

        /// <summary>
        /// Returns the preview localizer of <paramref name="catalog"/>, or of the default catalog when it is empty.
        /// Null while <see cref="IsActive"/> is false, or when the project has no such catalog.
        /// </summary>
        public static ILocalizer For(CatalogKey catalog)
        {
            return IsActive ? Provider(catalog) : null;
        }

        /// <summary>
        /// Tells the editor <paramref name="component"/> shows preview text: <paramref name="restore"/> puts its authored
        /// text back before its scene or prefab saves, and <paramref name="show"/> shows the preview again after.
        /// </summary>
        public static void Track(Component component, Action restore, Action show)
        {
            if (component != null && restore != null && show != null)
            {
                TrackedComponents[component] = (restore, show);
            }
        }

        /// <summary>Tells the editor <paramref name="component"/> shows its authored text again.</summary>
        public static void Untrack(Component component)
        {
            if (!ReferenceEquals(component, null))
            {
                TrackedComponents.Remove(component);
            }
        }
    }
}
