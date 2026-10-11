using reromanlee.ReactiveLocalizer.Unity;
using UnityEngine;

namespace reromanlee.ReactiveLocalizer.Samples.QuickStart
{
    /// <summary>
    /// The base of components that show an entry in a text of their GameObject, in the current language, through
    /// every switch, such as the TextMeshPro and uGUI Text samples' components. A subclass only binds its own kind of
    /// text.
    /// </summary>
    /// <remarks>
    /// It binds while enabled only, so a language switch never touches hidden UI, and text enabled later arrives in
    /// the current language as it binds. In Edit Mode it previews the entry in the language the Scene view's overlay
    /// picks, and the text it was authored with is what scenes and prefabs save.
    /// </remarks>
    [ExecuteAlways]
    public abstract class LocalizedBehaviour : MonoBehaviour
    {
        [Tooltip("The entry this text shows.")]
        [SerializeField] private EntryReference _entry;

        private TextBinding _binding;
        private EntryMessage _message;
        // The text the component had before previewing, which it gets back whenever the preview stops.
        private string _authoredText;
        private ILocalizer _preview;
        private bool _isWaitingForPreview;

        /// <summary>The entry shown. Setting it while enabled shows the new entry right away, without the arguments of an earlier message.</summary>
        public EntryReference Entry
        {
            get => _entry;
            set
            {
                _entry = value;
                _message = default;
                Rebind();
            }
        }

        /// <summary>The localizer the text comes from: the game's in Play Mode, the editor's preview outside it. Null while unbound.</summary>
        protected ILocalizer Localizer { get; private set; }

        /// <summary>The text the GameObject shows now, read and given back around the Edit Mode preview.</summary>
        protected abstract string Text { get; set; }

        /// <summary>
        /// Shows <paramref name="message"/>, such as <c>QuickStartKeys.Demo.Coins(coins)</c>, instead of the entry alone,
        /// and keeps showing it after the component is enabled again. Calling it every frame with equal arguments costs
        /// nothing.
        /// </summary>
        public void SetMessage(in EntryMessage message)
        {
            _message = message;
            _binding.SetMessage(in message);
        }

        /// <summary>Binds <paramref name="key"/> of <paramref name="localizer"/> to the text this component shows.</summary>
        protected abstract TextBinding Bind(ILocalizer localizer, in EntryKey key);

        /// <summary>Keeps whatever else the preview may change besides the text, such as a font, when the preview first shows.</summary>
        protected virtual void KeepAuthoredLook()
        {
        }

        /// <summary>Gives back what <see cref="KeepAuthoredLook"/> kept, as the preview stops or before the scene saves.</summary>
        protected virtual void RestoreAuthoredLook()
        {
        }

        protected virtual void OnEnable()
        {
            if (Application.isPlaying)
            {
                BindToGame();
            }
            else
            {
                ShowPreview();
            }
        }

        protected virtual void OnDisable()
        {
            _binding.Dispose();
            Localizer = null;
            if (!Application.isPlaying)
            {
                EditModePreview.Untrack(this);
                RestoreAuthoredText();
            }
        }

        protected virtual void OnValidate()
        {
            // Picking another entry in the Inspector shows it at once. Text can't change during validation itself.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += Rebind;
#else
            Rebind();
#endif
        }

        /// <summary>Binds again, as after the entry changed.</summary>
        protected void Rebind()
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }
            _binding.Dispose();
            if (Application.isPlaying)
            {
                BindToGame();
            }
            else
            {
                ShowPreview();
            }
        }

        private void BindToGame()
        {
            _binding.Dispose();
            Localizer = _entry.IsEmpty ? null : GlobalLocalizer.For(_entry.ToCatalogKey());
            if (Localizer != null)
            {
                Start(Localizer);
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
            if (_authoredText == null)
            {
                _authoredText = Text ?? string.Empty;
                KeepAuthoredLook();
            }
            _preview = preview;
            Localizer = preview;
            Start(preview);
            EditModePreview.Track(this, RestoreAuthoredText, ShowPreview);
        }

        private void Start(ILocalizer localizer)
        {
            _binding = Bind(localizer, _entry.ToKey());
            if (!_message.IsEmpty)
            {
                _binding.SetMessage(in _message);
            }
        }

        private void RestoreAuthoredText()
        {
            _binding.Dispose();
            // Text typed over the preview is the new authored text, and stays.
            if (_authoredText != null && _preview != null && Text == ShownPreview())
            {
                Text = _authoredText;
            }
            if (_authoredText != null)
            {
                RestoreAuthoredLook();
            }
            _authoredText = null;
            _preview = null;
            Localizer = null;
        }

        private string ShownPreview() => _message.IsEmpty ? _preview.Get(_entry.ToKey()) : _preview.Get(in _message);
    }
}
