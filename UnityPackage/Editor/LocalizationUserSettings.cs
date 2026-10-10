using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Editor
{
    /// <summary>
    /// Each person's own ReactiveLocalizer state in a project, saved in <c>UserSettings/ReactiveLocalizer.asset</c>,
    /// which version control leaves out: the preview language, and the entries and table picked last.
    /// </summary>
    [FilePath("UserSettings/ReactiveLocalizer.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class LocalizationUserSettings : ScriptableSingleton<LocalizationUserSettings>
    {
        private const int RecentEntryLimit = 12;

        [SerializeField] private string _previewLanguage;
        [SerializeField] private string _lastTable;
        [SerializeField] private List<EntryReference> _recentEntries = new();

        /// <summary>Raised when the preview language changes.</summary>
        public static event Action PreviewLanguageChanged;

        /// <summary>
        /// The language the editor previews text in, by name. Empty means each catalog's source language, as does a
        /// language a catalog doesn't have.
        /// </summary>
        public string PreviewLanguage
        {
            get => _previewLanguage ?? string.Empty;
            set
            {
                value ??= string.Empty;
                if (string.Equals(value, PreviewLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                _previewLanguage = value;
                Save(true);
                PreviewLanguageChanged?.Invoke();
                InternalEditorUtility.RepaintAllViews();
            }
        }

        /// <summary>The table an entry was last created in, which the next one defaults to when nothing better is known.</summary>
        public string LastTable
        {
            get => _lastTable ?? string.Empty;
            set
            {
                if (string.Equals(value, _lastTable, StringComparison.Ordinal))
                {
                    return;
                }
                _lastTable = value;
                Save(true);
            }
        }

        /// <summary>The entries picked last, most recent first.</summary>
        public IReadOnlyList<EntryReference> RecentEntries => _recentEntries ??= new List<EntryReference>();

        /// <summary>Puts <paramref name="reference"/> first among the recent entries.</summary>
        public void AddRecentEntry(EntryReference reference)
        {
            if (reference.IsEmpty)
            {
                return;
            }
            _recentEntries ??= new List<EntryReference>();
            _recentEntries.Remove(reference);
            _recentEntries.Insert(0, reference);
            if (_recentEntries.Count > RecentEntryLimit)
            {
                _recentEntries.RemoveRange(RecentEntryLimit, _recentEntries.Count - RecentEntryLimit);
            }
            Save(true);
        }
    }
}
