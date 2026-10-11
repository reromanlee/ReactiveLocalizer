using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using System;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.TextMeshPro
{
    /// <summary>
    /// Shows an entry in the TextMeshPro text of its GameObject, in the current language, through every switch, in
    /// the font its <see cref="LanguageFonts"/> picks for the language.
    /// </summary>
    /// <remarks>
    /// Text arrives without creating strings: the characters go straight into the text's own buffer. In Edit Mode it
    /// previews the entry, font included, in the language the Scene view's overlay picks, and scenes and prefabs save
    /// the text and font it was authored with.
    /// </remarks>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("ReactiveLocalizer/Localized TextMeshPro")]
    public sealed class LocalizedTextMeshPro : LocalizedBehaviour
    {
        [Tooltip("The font each language shows this text with; empty to keep the text's own font in every language.")]
        [SerializeField] private LanguageFonts _fonts;

        private TMP_Text _text;
        private LanguageInfo _fontLanguage;
        private TMP_FontAsset _authoredFont;
        private Material _authoredMaterial;
        private TMP_FontAsset _shownFont;

        /// <summary>The fonts the languages show this text with; null keeps the text's own font. Setting it applies it at the next text.</summary>
        public LanguageFonts Fonts
        {
            get => _fonts;
            set
            {
                _fonts = value;
                _fontLanguage = null;
                Rebind();
            }
        }

        private TMP_Text Target => _text != null ? _text : _text = GetComponent<TMP_Text>();

        protected override string Text
        {
            get => Target != null ? Target.text : null;
            set
            {
                if (Target != null)
                {
                    Target.text = value;
                }
            }
        }

        protected override TextBinding Bind(ILocalizer localizer, in EntryKey key)
        {
            _fontLanguage = null;
            return localizer.BindCharacters(in key, this, static (component, characters) => component.Show(characters));
        }

        protected override void KeepAuthoredLook()
        {
            if (Target != null)
            {
                _authoredFont = Target.font;
                _authoredMaterial = Target.fontSharedMaterial;
            }
        }

        protected override void RestoreAuthoredLook()
        {
            // A font picked in the Inspector during the preview is the new authored font, and stays.
            if (Target != null && _authoredFont != null && Target.font == _shownFont)
            {
                Target.font = _authoredFont;
                Target.fontSharedMaterial = _authoredMaterial;
            }
            _authoredFont = null;
            _authoredMaterial = null;
            _shownFont = null;
            _fontLanguage = null;
        }

        /// <summary>Shows characters without creating a string: the text component copies them into its own buffer.</summary>
        private void Show(ReadOnlyMemory<char> characters)
        {
            TMP_Text text = Target;
            if (text == null)
            {
                return;
            }
            ApplyFont(text);
            if (MemoryMarshal.TryGetArray(characters, out ArraySegment<char> segment))
            {
                text.SetCharArray(segment.Array, segment.Offset, segment.Count);
                return;
            }
            // Characters a string already holds, such as a missing key's marker.
            text.SetText(characters.ToString());
        }

        /// <summary>Picks the font of the language the text is in, once per language.</summary>
        private void ApplyFont(TMP_Text text)
        {
            LanguageInfo language = Localizer?.CurrentLanguage;
            if (_fonts == null || language == null || language == _fontLanguage)
            {
                return;
            }
            _fontLanguage = language;
            if (_authoredFont == null)
            {
                // The text's own font is what languages without a font of their own show.
                KeepAuthoredLook();
            }
            TMP_FontAsset font = _fonts.GetFont(Localizer.Catalog, language, out Material material);
            if (font == null)
            {
                font = _authoredFont;
                material = _authoredMaterial;
            }
            if (font == null)
            {
                return;
            }
            if (text.font != font)
            {
                text.font = font;
            }
            Material shared = material != null ? material : font.material;
            if (text.fontSharedMaterial != shared)
            {
                text.fontSharedMaterial = shared;
            }
            _shownFont = font;
        }
    }
}
