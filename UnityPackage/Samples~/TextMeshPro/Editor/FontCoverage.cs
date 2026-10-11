using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Editor;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace reromanlee.ReactiveLocalizer.Samples.TextMeshPro.Editor
{
    /// <summary>
    /// Checks which characters each language's font would draw as an empty box: every character the language can
    /// show, looked up in its font, the font's fallbacks and TextMeshPro's global fallbacks.
    /// </summary>
    /// <remarks>
    /// A dynamic font counts the characters of its source font file, which it adds to its atlas when they're first
    /// shown; a static font counts only its atlas. Text that arguments bring at runtime, such as player names, isn't
    /// in the files, so it isn't checked.
    /// </remarks>
    public static class FontCoverage
    {
        internal const int ListedLimit = 200;

        private static Font _loadedFont;

        /// <summary>Checks every language of <paramref name="fonts"/>' catalog that has a font, the pseudo language included.</summary>
        public static List<FontCoverageResult> Check(LanguageFonts fonts)
        {
            List<FontCoverageResult> results = new();
            if (fonts == null)
            {
                return results;
            }
            CatalogKey catalog = fonts.Catalog.ToKey();
            CatalogInfo info = CharacterSets.GetCatalog(catalog);
            if (info == null)
            {
                results.Add(new FontCoverageResult(null, null, 0, 0, string.Empty,
                    fonts.Catalog.IsEmpty ? "The project has no usable default catalog." : $"The project has no usable catalog '{fonts.Catalog.CatalogName}'."));
                return results;
            }
            IReadOnlyList<LanguageInfo> languages = info.Languages;
            HashSet<TMP_FontAsset> visited = new();
            StringBuilder missing = new();
            _loadedFont = null;
            for (int l = 0; l < languages.Count; l++)
            {
                LanguageInfo language = languages[l];
                TMP_FontAsset font = fonts.GetFont(info, language, out _);
                if (font == null)
                {
                    results.Add(new FontCoverageResult(language.Name, null, 0, 0, string.Empty, "No font: each text keeps its own, which this check can't know."));
                    continue;
                }
                CharacterSet characters = CharacterSets.Collect(catalog, language.Key);
                int[] codePoints = characters.ToCodePoints();
                int missingCount = 0;
                missing.Clear();
                for (int i = 0; i < codePoints.Length; i++)
                {
                    visited.Clear();
                    if (IsDrawn(font, (uint)codePoints[i], visited) || IsGloballyDrawn((uint)codePoints[i], visited))
                    {
                        continue;
                    }
                    missingCount++;
                    if (missingCount <= ListedLimit)
                    {
                        missing.Append(char.ConvertFromUtf32(codePoints[i]));
                    }
                }
                string note = font.atlasPopulationMode != AtlasPopulationMode.Static && font.sourceFontFile == null
                    ? "This dynamic font has no source font file in the project, so only its atlas was checked."
                    : null;
                results.Add(new FontCoverageResult(language.Name, font, codePoints.Length, missingCount, missing.ToString(), note));
            }
            return results;
        }

        /// <summary>Logs <paramref name="results"/> of <see cref="Check"/> to the Console, a warning for each language its font can't fully draw.</summary>
        public static void Log(IReadOnlyList<FontCoverageResult> results, LanguageFonts fonts)
        {
            foreach (FontCoverageResult result in results)
            {
                if (result.MissingCount > 0)
                {
                    Debug.LogWarning($"[ReactiveLocalizer] {result}", result.Font);
                }
                else
                {
                    Debug.Log($"[ReactiveLocalizer] {result}", (Object)result.Font ?? fonts);
                }
            }
        }

        private static bool IsDrawn(TMP_FontAsset font, uint unicode, HashSet<TMP_FontAsset> visited)
        {
            if (font == null || !visited.Add(font))
            {
                return false;
            }
            if (font.characterLookupTable != null && font.characterLookupTable.ContainsKey(unicode))
            {
                return true;
            }
            if (font.atlasPopulationMode != AtlasPopulationMode.Static && HasGlyph(font.sourceFontFile, unicode))
            {
                return true;
            }
            List<TMP_FontAsset> fallbacks = font.fallbackFontAssetTable;
            for (int i = 0; fallbacks != null && i < fallbacks.Count; i++)
            {
                if (IsDrawn(fallbacks[i], unicode, visited))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsGloballyDrawn(uint unicode, HashSet<TMP_FontAsset> visited)
        {
            List<TMP_FontAsset> fallbacks = TMP_Settings.instance != null ? TMP_Settings.fallbackFontAssets : null;
            for (int i = 0; fallbacks != null && i < fallbacks.Count; i++)
            {
                if (IsDrawn(fallbacks[i], unicode, visited))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Whether the font file has a glyph for <paramref name="unicode"/>, read without adding it to any atlas.</summary>
        private static bool HasGlyph(Font font, uint unicode)
        {
            if (font == null)
            {
                return false;
            }
            if (font != _loadedFont)
            {
                if (FontEngine.LoadFontFace(font) != FontEngineError.Success)
                {
                    return false;
                }
                _loadedFont = font;
            }
            return FontEngine.TryGetGlyphIndex(unicode, out uint glyphIndex) && glyphIndex != 0;
        }
    }
}
