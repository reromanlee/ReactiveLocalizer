using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using reromanlee.ReactiveLocalizer.Unity;
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace reromanlee.ReactiveLocalizer.Samples.UIToolkit
{
    /// <summary>
    /// Shows an entry in a UI Toolkit text element while the element is in a panel, in the current language, through
    /// every switch. Made with <see cref="ElementTextBindings.BindText"/>; disposing it stops it.
    /// </summary>
    /// <remarks>
    /// It binds while the element is attached only, so a language switch never touches UI that isn't shown, and an
    /// element attached later arrives in the current language. Outside Play Mode it shows the editor's preview language.
    /// </remarks>
    public sealed class ElementTextBinding : IDisposable
    {
        // One instance each, so unregistering finds the callbacks registered.
        private static readonly EventCallback<AttachToPanelEvent, ElementTextBinding> OnAttached = static (_, binding) => binding.Start();
        private static readonly EventCallback<DetachFromPanelEvent, ElementTextBinding> OnDetached = static (_, binding) => binding.Stop();

        private readonly TextElement _element;
        private readonly EntryKey _key;
        private readonly CatalogKey _catalog;
        private TextBinding _binding;
        private EntryMessage _message;
        private bool _isDisposed;

        internal ElementTextBinding(TextElement element, in EntryKey key, CatalogKey catalog)
        {
            _element = element;
            _key = key;
            _catalog = catalog;
            element.RegisterCallback(OnAttached, this);
            element.RegisterCallback(OnDetached, this);
            if (element.panel != null)
            {
                Start();
            }
        }

        /// <summary>The element whose text is shown.</summary>
        public TextElement Element => _element;

        /// <summary>
        /// Shows <paramref name="message"/>, such as <c>QuickStartKeys.Demo.Coins(coins)</c>, instead of the entry alone,
        /// and keeps showing it when the element is attached again. Calling it every frame with equal arguments costs nothing.
        /// </summary>
        public void SetMessage(in EntryMessage message)
        {
            _message = message;
            _binding.SetMessage(in message);
        }

        /// <summary>Stops showing the entry. The element keeps the text it shows.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }
            _isDisposed = true;
            Stop();
            _element.UnregisterCallback(OnAttached);
            _element.UnregisterCallback(OnDetached);
            ElementTextBindings.Forget(this);
        }

        private void Start()
        {
            if (_isDisposed || _binding.IsActive)
            {
                return;
            }
            ILocalizer localizer = GlobalLocalizer.For(_catalog);
            if (localizer == null)
            {
                // Right after scripts reload, the editor's preview starts a moment later.
                if (!Application.isPlaying && !EditModePreview.IsActive)
                {
                    _element.schedule.Execute(Start);
                }
                return;
            }
            _binding = localizer.Bind(in _key, _element, static (element, text) => element.text = text);
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
    }
}
