#if UNITY_2023_3_OR_NEWER
using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using reromanlee.ReactiveLocalizer.Unity;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Samples.UIToolkit
{
    /// <summary>
    /// A data binding that shows an entry in any string property of an element, declared in UXML or added in UI
    /// Builder, in the current language, through every switch:
    /// <code>
    /// &lt;ui:Label&gt;
    ///     &lt;Bindings&gt;
    ///         &lt;reromanlee.ReactiveLocalizer.Samples.UIToolkit.LocalizedTextBinding property="text" catalog="QuickStart" entry="Demo.Title" /&gt;
    ///     &lt;/Bindings&gt;
    /// &lt;/ui:Label&gt;
    /// </code>
    /// </summary>
    /// <remarks>
    /// Unity 6 and newer; <see cref="ElementTextBindings.BindText"/> does the same from code on every version. It binds
    /// while its element is in a panel only, and UI Builder previews the entry in the editor's preview language.
    /// </remarks>
    [UxmlObject]
    public partial class LocalizedTextBinding : CustomBinding
    {
        private string _catalog = string.Empty;
        private string _entry = string.Empty;
        private TextBinding _binding;
        private EntryMessage _message;
        private string _text;
        private int _activeCount;

        public LocalizedTextBinding()
        {
            updateTrigger = BindingUpdateTrigger.WhenDirty;
        }

        /// <summary>The catalog the entry belongs to; empty for Quick Start's default catalog.</summary>
        [UxmlAttribute("catalog")]
        public string Catalog
        {
            get => _catalog;
            set
            {
                _catalog = value ?? string.Empty;
                Restart();
            }
        }

        /// <summary>The entry shown, written <c>Table.Entry</c>, such as <c>Demo.Title</c>.</summary>
        [UxmlAttribute("entry")]
        public string Entry
        {
            get => _entry;
            set
            {
                _entry = value ?? string.Empty;
                _message = default;
                Restart();
            }
        }

        /// <summary>
        /// Shows <paramref name="message"/>, such as <c>QuickStartKeys.Demo.Coins(coins)</c>, instead of the entry alone.
        /// Calling it every frame with equal arguments costs nothing.
        /// </summary>
        public void SetMessage(in EntryMessage message)
        {
            _message = message;
            _binding.SetMessage(in message);
        }

        protected override void OnActivated(in BindingActivationContext context)
        {
            base.OnActivated(in context);
            _activeCount++;
            if (_activeCount == 1)
            {
                Start();
            }
        }

        protected override void OnDeactivated(in BindingActivationContext context)
        {
            base.OnDeactivated(in context);
            _activeCount--;
            if (_activeCount == 0)
            {
                Stop();
            }
        }

        protected override BindingResult Update(in BindingContext context)
        {
            if (_text == null)
            {
                return new BindingResult(BindingStatus.Pending);
            }
            VisualElement element = context.targetElement;
            if (ConverterGroups.TrySetValueGlobal(ref element, context.bindingId, _text, out VisitReturnCode problem))
            {
                return new BindingResult(BindingStatus.Success);
            }
            return new BindingResult(BindingStatus.Failure, $"{nameof(LocalizedTextBinding)} can't set '{context.bindingId}' of {element.GetType().Name}: {problem}.");
        }

        private void Restart()
        {
            if (_activeCount > 0)
            {
                Stop();
                Start();
            }
        }

        private void Start()
        {
            int dot = _entry.IndexOf('.');
            EntryReference reference = new(_catalog, dot > 0 ? _entry.Substring(0, dot) : string.Empty, dot > 0 ? _entry.Substring(dot + 1) : string.Empty);
            EntryKey key = reference.ToKey();
            CatalogKey catalog = reference.ToCatalogKey();
            if (key.IsEmpty || (_catalog.Length > 0 && catalog.IsEmpty))
            {
                // As loud as a missing key, so a typo in UXML never passes for text.
                Show(this, _entry.Length == 0 ? string.Empty : $"[{_entry}]");
                if (_entry.Length > 0)
                {
                    Debug.LogWarning($"[ReactiveLocalizer] '{_entry}' of catalog '{_catalog}' isn't an entry written Table.Entry, with names of ASCII letters, digits and underscores that start with a letter.");
                }
                return;
            }
            ILocalizer localizer = GlobalLocalizer.For(catalog);
            if (localizer == null)
            {
                return;
            }
            _binding = localizer.Bind(in key, this, static (binding, text) => Show(binding, text));
            if (!_message.IsEmpty)
            {
                _binding.SetMessage(in _message);
            }
        }

        private void Stop()
        {
            _binding.Dispose();
            _binding = default;
        }

        private static void Show(LocalizedTextBinding binding, string text)
        {
            binding._text = text;
            binding.MarkDirty();
        }
    }
}
#endif
