using System;
using System.ComponentModel;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The text of one entry as an observable value, for view models, UI Toolkit data binding and reactive libraries:
    /// <see cref="Value"/> always holds the current text, and <see cref="Changed"/> and <see cref="PropertyChanged"/>
    /// are raised whenever it changes.
    /// <code>
    /// ReactiveText title = new ReactiveText(localizer, LocalizationKeys.MainMenu.Title);
    /// title.Changed += text => label.text = text;
    /// title.Dispose();
    /// </code>
    /// </summary>
    /// <remarks>
    /// It wraps a <see cref="TextBinding"/>, so it follows every language switch and holds its entry's table. Events
    /// are raised on the host thread, and only when the text actually differs, so a switch that leaves it as it was
    /// notifies nothing. Dispose it once nothing shows it anymore, which releases the binding.
    /// </remarks>
    public sealed class ReactiveText : INotifyPropertyChanged, IDisposable
    {
        private static readonly PropertyChangedEventArgs ValueArguments = new(nameof(Value));

        private readonly TextBinding _binding;
        private string _value = string.Empty;

        /// <summary>Creates the observable text of <paramref name="key"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="localizer"/> is null.</exception>
        public ReactiveText(ILocalizer localizer, in EntryKey key)
        {
            if (localizer == null)
            {
                throw new ArgumentNullException(nameof(localizer));
            }
            _binding = localizer.Bind(in key, this, static (reactive, text) => reactive.SetValue(text));
        }

        /// <summary>Creates the observable text of <paramref name="message"/>, whose arguments <see cref="SetMessage"/> changes.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="localizer"/> is null.</exception>
        public ReactiveText(ILocalizer localizer, in EntryMessage message)
        {
            if (localizer == null)
            {
                throw new ArgumentNullException(nameof(localizer));
            }
            _binding = localizer.Bind(in message, this, static (reactive, text) => reactive.SetValue(text));
        }

        /// <summary>Raised with the new text whenever it changes, on the host thread.</summary>
        public event Action<string> Changed;

        /// <inheritdoc/>
        /// <remarks>Raised for <see cref="Value"/> whenever it changes, on the host thread.</remarks>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The current text. Empty until the first text arrives, as before initialization.</summary>
        public string Value => _value;

        /// <summary>Whether the text still follows the localizer: neither disposed nor released with it.</summary>
        public bool IsActive => _binding.IsActive;

        /// <summary>Shows <paramref name="message"/> from now on, formatting it only when it differs from the current one.</summary>
        public void SetMessage(in EntryMessage message)
        {
            _binding.SetMessage(in message);
        }

        /// <summary>Stops following the localizer; <see cref="Value"/> keeps the last text. Safe to call any number of times.</summary>
        public void Dispose()
        {
            _binding.Dispose();
        }

        /// <inheritdoc/>
        public override string ToString() => _value;

        private void SetValue(string text)
        {
            if (string.Equals(_value, text, StringComparison.Ordinal))
            {
                return;
            }
            _value = text;
            try
            {
                Changed?.Invoke(text);
            }
            finally
            {
                // A Changed handler that throws is reported by the binding; property listeners still hear of the change.
                PropertyChanged?.Invoke(this, ValueArguments);
            }
        }
    }
}
