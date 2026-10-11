using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using UnityEngine;
using UnityEngine.UI;

namespace reromanlee.ReactiveLocalizer.Samples.UguiText
{
    /// <summary>
    /// Shows an entry in the legacy uGUI <see cref="UnityEngine.UI.Text"/> of its GameObject, in the current language,
    /// through every switch.
    /// </summary>
    /// <remarks>
    /// Legacy text takes strings, so each new text costs the one string the localizer caches anyway. In Edit Mode it
    /// previews the entry in the language the Scene view's overlay picks, and scenes and prefabs save the text it was
    /// authored with. Prefer the TextMeshPro sample for new UI.
    /// </remarks>
    [RequireComponent(typeof(Text))]
    [AddComponentMenu("ReactiveLocalizer/Localized Text (Legacy)")]
    public sealed class LocalizedLegacyText : LocalizedBehaviour
    {
        private Text _text;

        private Text Target => _text != null ? _text : _text = GetComponent<Text>();

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
            return localizer.Bind(in key, this, static (component, text) => component.Text = text);
        }
    }
}
