using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Samples.TextMeshPro.Editor
{
    /// <summary>The Inspector of <see cref="LanguageFonts"/>, with a check of which characters each language's font can't draw.</summary>
    [CustomEditor(typeof(LanguageFonts))]
    internal sealed class LanguageFontsEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new();
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            VisualElement results = new();
            Button check = new(() => ShowCoverage(results))
            {
                text = "Check Font Coverage",
                tooltip = "Lists every character a language can show that its font, the font's fallbacks and TextMeshPro's global fallbacks can't draw. The Console gets the same list to copy from."
            };
            check.style.marginTop = 8f;
            root.Add(check);
            root.Add(results);
            return root;
        }

        private void ShowCoverage(VisualElement results)
        {
            results.Clear();
            LanguageFonts fonts = (LanguageFonts)target;
            List<FontCoverageResult> coverage = FontCoverage.Check(fonts);
            foreach (FontCoverageResult result in coverage)
            {
                results.Add(new HelpBox(result.ToString(), result.MissingCount > 0 ? HelpBoxMessageType.Warning : HelpBoxMessageType.Info));
            }
            FontCoverage.Log(coverage, fonts);
        }
    }
}
