using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using reromanlee.ReactiveLocalizer.Unity;
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
            // The text component copies the characters into its own buffer, so this assignment allocates nothing.
            _binding = GlobalLocalizer.For(_entry.ToCatalogKey()).Bind(_entry.ToKey(), _text, static (text, value) => text.SetText(value));
        }
    }
}
