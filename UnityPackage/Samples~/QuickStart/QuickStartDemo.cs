using reromanlee.ReactiveLocalizer.Unity;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.QuickStart
{
    /// <summary>
    /// Shows texts of the Quick Start catalog through bindings, so every switch made with the language picker reaches
    /// them at once: one entry picked in the Inspector, one named by generated code, one read by name like data would,
    /// and a message whose plural form follows a counter.
    /// </summary>
    [AddComponentMenu("ReactiveLocalizer/Quick Start Demo")]
    public sealed class QuickStartDemo : MonoBehaviour
    {
        [Tooltip("An entry picked in the Inspector: its catalog, table and key are saved by name.")]
        [SerializeField] private EntryReference _title;

        private string _titleText = string.Empty;
        private string _greetingText = string.Empty;
        private string _coinsText = string.Empty;
        private string _addCoinText = string.Empty;
        private int _coins;
        private TextBinding _titleBinding;
        private TextBinding _greetingBinding;
        private TextBinding _coinsBinding;
        private TextBinding _addCoinBinding;
        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;

        private void OnEnable()
        {
            ILocalizer localizer = GlobalLocalizer.For(QuickStartKeys.CatalogKey);
            // Static lambdas take their target as a parameter, so binding allocates nothing.
            _titleBinding = localizer.Bind(_title.ToKey(), this, static (demo, text) => demo._titleText = text);
            _greetingBinding = localizer.Bind(QuickStartKeys.Demo.Greeting, this, static (demo, text) => demo._greetingText = text);
            _addCoinBinding = localizer.Bind(QuickStartKeys.Demo.AddCoin, this, static (demo, text) => demo._addCoinText = text);
            // A message: generated code checks its argument, and the language decides its plural form.
            _coinsBinding = localizer.Bind(QuickStartKeys.Demo.Coins(_coins), this, static (demo, text) => demo._coinsText = text);
        }

        private void OnDisable()
        {
            _titleBinding.Dispose();
            _greetingBinding.Dispose();
            _addCoinBinding.Dispose();
            _coinsBinding.Dispose();
        }

        private void OnGUI()
        {
            _titleStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _bodyStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };

            // A text read by name, the way a dialogue system would read lines its data refers to.
            string hint = GlobalLocalizer.For(QuickStartKeys.CatalogKey).Get("Demo", "Hint");

            Rect area = new(240f, Screen.height * 0.2f, Screen.width - 480f, Screen.height * 0.6f);
            GUILayout.BeginArea(area);
            GUILayout.Label(_titleText, _titleStyle);
            GUILayout.Space(24f);
            GUILayout.Label(_greetingText, _bodyStyle);
            GUILayout.Space(12f);
            GUILayout.Label(hint, _bodyStyle);
            GUILayout.Space(24f);
            if (GUILayout.Button(_addCoinText, GUILayout.Height(36f)))
            {
                _coins++;
                // Formats again only because the argument changed; the same arguments would cost nothing.
                _coinsBinding.SetMessage(QuickStartKeys.Demo.Coins(_coins));
            }
            GUILayout.Label(_coinsText, _bodyStyle);
            GUILayout.EndArea();
        }
    }
}
