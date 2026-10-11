using R3;
using System;

namespace reromanlee.ReactiveLocalizer.Samples.R3
{
    /// <summary>
    /// R3 observables of localized text: each subscription is a binding, so it emits the current text right away and
    /// again on every language switch, live file edit or argument change, on the localizer's host thread.
    /// <code>
    /// localizer.ObserveText(QuickStartKeys.Demo.Title).Subscribe(text => _title.text = text).AddTo(this);
    /// coins.SelectText(localizer, count => QuickStartKeys.Demo.Coins(count)).Subscribe(text => _coins.text = text).AddTo(this);
    /// </code>
    /// </summary>
    /// <remarks>
    /// Disposing a subscription releases its binding, and with it any table loaded on demand for it.
    /// </remarks>
    public static class LocalizerObservables
    {
        /// <summary>Observes the text of <paramref name="key"/>: the current text when subscribed, then every change.</summary>
        public static Observable<string> ObserveText(this ILocalizer localizer, EntryKey key)
        {
            return Observable.Create<string, (ILocalizer Localizer, EntryKey Key)>((localizer, key), static (observer, state) =>
            {
                TextBinding binding = state.Localizer.Bind(in state.Key, observer, static (target, text) => target.OnNext(text));
                return Disposable.Create(binding, static subscription => subscription.Dispose());
            });
        }

        /// <summary>Observes the text of <paramref name="message"/>: the current text when subscribed, then every change.</summary>
        public static Observable<string> ObserveText(this ILocalizer localizer, EntryMessage message)
        {
            return Observable.Create<string, (ILocalizer Localizer, EntryMessage Message)>((localizer, message), static (observer, state) =>
            {
                TextBinding binding = state.Localizer.Bind(in state.Message, observer, static (target, text) => target.OnNext(text));
                return Disposable.Create(binding, static subscription => subscription.Dispose());
            });
        }

        /// <summary>
        /// Observes the text of the message each value of <paramref name="source"/> makes, such as a coin counter
        /// shown as <c>You have 5 coins.</c>: one binding for the whole subscription, formatted again only when a
        /// value changes the message's arguments.
        /// </summary>
        public static Observable<string> SelectText<T>(this Observable<T> source, ILocalizer localizer, Func<T, EntryMessage> toMessage)
        {
            if (toMessage == null)
            {
                throw new ArgumentNullException(nameof(toMessage));
            }
            return Observable.Create<string, (Observable<T> Source, ILocalizer Localizer, Func<T, EntryMessage> ToMessage)>((source, localizer, toMessage),
                static (observer, state) =>
                {
                    MessageSubscription<T> subscription = new(observer, state.Localizer, state.ToMessage);
                    subscription.Source = state.Source.Subscribe(subscription,
                        static (value, target) => target.Show(value),
                        static (error, target) => target.Observer.OnErrorResume(error),
                        static (result, target) => target.Observer.OnCompleted(result));
                    return subscription;
                });
        }

        /// <summary>Observes the language: the current one when subscribed, if initialization finished, then every switch once applied.</summary>
        public static Observable<LanguageInfo> ObserveLanguage(this ILocalizer localizer)
        {
            return Observable.Create<LanguageInfo, ILocalizer>(localizer, static (observer, source) =>
            {
                if (source.CurrentLanguage != null)
                {
                    observer.OnNext(source.CurrentLanguage);
                }
                Action<LanguageInfo> handler = observer.OnNext;
                source.LanguageChanged += handler;
                return Disposable.Create((Source: source, Handler: handler), static state => state.Source.LanguageChanged -= state.Handler);
            });
        }

        /// <summary>Observes the value of <paramref name="text"/>: the current value when subscribed, then every change, until it's disposed.</summary>
        public static Observable<string> AsObservable(this ReactiveText text)
        {
            return Observable.Create<string, ReactiveText>(text, static (observer, source) =>
            {
                observer.OnNext(source.Value);
                Action<string> handler = observer.OnNext;
                source.Changed += handler;
                return Disposable.Create((Source: source, Handler: handler), static state => state.Source.Changed -= state.Handler);
            });
        }

        /// <summary>One subscription of <see cref="SelectText{T}"/>: the source's subscription and the binding its values feed.</summary>
        private sealed class MessageSubscription<T> : IDisposable
        {
            private readonly ILocalizer _localizer;
            private readonly Func<T, EntryMessage> _toMessage;
            private TextBinding _binding;
            private bool _isBound;
            private bool _isDisposed;

            public MessageSubscription(Observer<string> observer, ILocalizer localizer, Func<T, EntryMessage> toMessage)
            {
                Observer = observer;
                _localizer = localizer;
                _toMessage = toMessage;
            }

            public Observer<string> Observer { get; }

            public IDisposable Source { get; set; }

            public void Show(T value)
            {
                if (_isDisposed)
                {
                    return;
                }
                EntryMessage message = _toMessage(value);
                if (_isBound)
                {
                    _binding.SetMessage(in message);
                    return;
                }
                _binding = _localizer.Bind(in message, Observer, static (target, text) => target.OnNext(text));
                _isBound = true;
            }

            public void Dispose()
            {
                _isDisposed = true;
                Source?.Dispose();
                _binding.Dispose();
            }
        }
    }
}
