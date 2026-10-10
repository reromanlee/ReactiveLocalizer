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
    /// the current language as it binds. Nothing is bound in Edit Mode, so opening a scene never marks it changed.
    /// </remarks>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("ReactiveLocalizer/Localized TextMeshPro")]
    public sealed class LocalizedTextMeshPro : MonoBehaviour
    {
        [Tooltip("The entry this text shows.")]
        [SerializeField] private EntryReference _entry;

        private TMP_Text _text;
        private TextBinding _binding;

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
            Bind();
        }

        private void OnDisable()
        {
            _binding.Dispose();
        }

        private void OnValidate()
        {
            // Picking another entry in the Inspector during Play Mode shows it at once.
            Rebind();
        }

        private void Rebind()
        {
            if (!isActiveAndEnabled || !Application.isPlaying)
            {
                return;
            }
            _binding.Dispose();
            Bind();
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
