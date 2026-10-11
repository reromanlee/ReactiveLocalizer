using reromanlee.ReactiveLocalizer.Unity;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.TextMeshPro
{
    /// <summary>
    /// The TextMeshPro font each language of a catalog shows its text with, such as a font with Cyrillic letters for
    /// Russian, picked by every <see cref="LocalizedTextMeshPro"/> that uses this asset as the language switches.
    /// </summary>
    /// <remarks>
    /// A language without a font of its own takes the font of its fallback language, then the default font. Its
    /// Inspector checks which characters each language's font can't draw.
    /// </remarks>
    [CreateAssetMenu(menuName = "ReactiveLocalizer/Language Fonts", fileName = "LanguageFonts", order = 100)]
    public sealed class LanguageFonts : ScriptableObject
    {
        [Tooltip("The catalog whose languages these fonts are for; empty for the default catalog. The coverage check reads its texts.")]
        [SerializeField] private CatalogReference _catalog;

        [Tooltip("The font of languages that have none of their own; empty to keep each text's own font.")]
        [SerializeField] private TMP_FontAsset _defaultFont;

        [Tooltip("The fonts of the languages that need their own.")]
        [SerializeField] private List<LanguageFont> _fonts = new();

        /// <summary>The catalog whose languages these fonts are for; empty for the default catalog.</summary>
        public CatalogReference Catalog => _catalog;

        /// <summary>The font of languages that have none of their own; null to keep each text's own font.</summary>
        public TMP_FontAsset DefaultFont => _defaultFont;

        /// <summary>The fonts of the languages that need their own.</summary>
        public IReadOnlyList<LanguageFont> Fonts => _fonts;

        /// <summary>
        /// Returns the font <paramref name="language"/> of <paramref name="catalog"/> shows text with: its own, else
        /// its fallback language's, else the default font. Null when there is none, which keeps a text's own font.
        /// </summary>
        public TMP_FontAsset GetFont(CatalogInfo catalog, LanguageInfo language, out Material material)
        {
            material = null;
            if (language == null)
            {
                return _defaultFont;
            }
            // The chain ends with the source language, which counts like any other.
            IReadOnlyList<LanguageInfo> chain = catalog != null ? catalog.GetFallbackChain(language) : new[] { language };
            for (int c = 0; c < chain.Count; c++)
            {
                for (int i = 0; i < _fonts.Count; i++)
                {
                    if (_fonts[i].Font != null && string.Equals(_fonts[i].Language, chain[c].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        material = _fonts[i].Material;
                        return _fonts[i].Font;
                    }
                }
            }
            return _defaultFont;
        }
    }

    /// <summary>The font one language shows its text with.</summary>
    [Serializable]
    public struct LanguageFont
    {
        [Tooltip("The language's name, as its catalog writes it, such as Russian.")]
        [SerializeField] private string _language;

        [SerializeField] private TMP_FontAsset _font;

        [Tooltip("A material preset of the font, such as one with an outline; empty for the font's own material.")]
        [SerializeField] private Material _material;

        public LanguageFont(string language, TMP_FontAsset font, Material material = null)
        {
            _language = language;
            _font = font;
            _material = material;
        }

        /// <summary>The language's name, as its catalog writes it.</summary>
        public string Language => _language;

        public TMP_FontAsset Font => _font;

        /// <summary>A material preset of the font; null for the font's own material.</summary>
        public Material Material => _material;
    }
}
