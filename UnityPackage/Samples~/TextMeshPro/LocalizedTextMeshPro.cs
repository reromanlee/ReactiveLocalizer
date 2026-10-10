using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.TextMeshPro
{
    /// <summary>
    /// Shows an entry in the TextMeshPro text of its GameObject, in the current language, through every switch.
    /// </summary>
    /// <remarks>
    /// It binds while enabled only, so a language switch never touches hidden UI, and text enabled later arrives in
    /// the current language as it binds. In Edit Mode it previews the entry in the language the Scene view's overlay
    /// picks, and the text it was authored with is what scenes and prefabs save.
    /// </remarks>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("ReactiveLocalizer/Localized TextMeshPro")]
    public sealed class LocalizedTextMeshPro : MonoBehaviour
    {
        [Tooltip("The entry this text shows.")]
        [SerializeField] private EntryReference _entry;

        private TMP_Text _text;
        private TextBinding _binding;
        // The text the component had before previewing, which it gets back whenever the preview stops.
        private string _authoredText;
        private ILocalizer _preview;
        private bool _isWaitingForPreview;

        /// <summary>The entry shown. Setting it while enabled shows the new entry right away.</summary>
        public EntryReference Entry
        {
            get => _entry;
            set
            {
                _entry = value;
                Rebind();
            }
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Bind();
            }
            else
            {
                ShowPreview();
            }
        }

        private void OnDisable()
        {
            _binding.Dispose();
            if (!Application.isPlaying)
            {
                EditModePreview.Untrack(this);
                RestoreAuthoredText();
            }
        }

        private void OnValidate()
        {
            // Picking another entry in the Inspector shows it at once. Text can't change during validation itself.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += Rebind;
#else
            Rebind();
#endif
        }

        private void Rebind()
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }
            _binding.Dispose();
            if (Application.isPlaying)
            {
                Bind();
            }
            else
            {
                ShowPreview();
            }
        }

        /// <summary>Shows the entry in the preview language, keeping the authored text to give back.</summary>
        private void ShowPreview()
        {
            _binding.Dispose();
#if UNITY_EDITOR
            // Scripts reloading can enable this before the editor's preview starts, which it does right after.
            if (!EditModePreview.IsActive)
            {
                if (!_isWaitingForPreview)
                {
                    _isWaitingForPreview = true;
                    UnityEditor.EditorApplication.delayCall += Rebind;
                }
                return;
            }
            _isWaitingForPreview = false;
#endif
            ILocalizer preview = _entry.IsEmpty ? null : GlobalLocalizer.For(_entry.ToCatalogKey());
            if (preview == null)
            {
                RestoreAuthoredText();
                return;
            }
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
            _authoredText ??= _text.text;
            _preview = preview;
            _binding = preview.BindCharacters(_entry.ToKey(), _text, static (text, characters) => Show(text, characters));
            EditModePreview.Track(this, RestoreAuthoredText, ShowPreview);
        }

        private void RestoreAuthoredText()
        {
            _binding.Dispose();
            // Text typed over the preview is the new authored text, and stays.
            if (_authoredText != null && _text != null && _preview != null && _text.text == _preview.Get(_entry.ToKey()))
            {
                _text.text = _authoredText;
            }
            _authoredText = null;
            _preview = null;
        }

        private void Bind()
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
            _binding = GlobalLocalizer.For(_entry.ToCatalogKey()).BindCharacters(_entry.ToKey(), _text, static (text, characters) => Show(text, characters));
        }

        /// <summary>Shows characters without creating a string: the text component copies them into its own buffer.</summary>
        private static void Show(TMP_Text text, ReadOnlyMemory<char> characters)
        {
            if (MemoryMarshal.TryGetArray(characters, out ArraySegment<char> segment))
            {
                text.SetCharArray(segment.Array, segment.Offset, segment.Count);
                return;
            }
            // Characters a string already holds, such as a missing key's marker.
            text.SetText(characters.ToString());
        }
    }
}
