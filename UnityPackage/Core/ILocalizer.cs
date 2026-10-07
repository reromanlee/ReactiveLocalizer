using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// The text of one catalog in the current language: read it once, or bind it so it follows every language switch.
    /// </summary>
    /// <remarks>
    /// Reads work from any thread without locks. Changes requested from any thread are applied in order on the host's
    /// thread, which is also where bindings and events are notified: Unity's main thread under <c>UnityHost</c>.
    /// Nothing here throws over missing data: a missing key shows as <c>[Table.Key]</c> and is reported once.
    /// </remarks>
    public interface ILocalizer
    {
        /// <summary>The catalog being localized: its languages and tables. Null until initialization loaded it.</summary>
        CatalogInfo Catalog { get; }

        /// <summary>Whether initialization finished, so lookups find text.</summary>
        bool IsInitialized { get; }

        /// <summary>The language lookups are in. Null until initialization finished.</summary>
        LanguageInfo CurrentLanguage { get; }

        /// <summary>The languages that can be switched to. Empty until initialization loaded the catalog.</summary>
        IReadOnlyList<LanguageInfo> Languages { get; }

        /// <summary>How many unique keys were asked for that exist nowhere in the catalog. A smoke test can assert it stays zero.</summary>
        int MissingKeyCount { get; }

        /// <summary>
        /// Raised on the host thread when a language switch starts loading, before any text changes. A project can
        /// show a loading screen here.
        /// </summary>
        event Action<LanguageInfo> LanguageChanging;

        /// <summary>
        /// Raised on the host thread right after a language switch is applied: every lookup already reads the new
        /// language, and every binding already received its new text.
        /// </summary>
        event Action<LanguageInfo> LanguageChanged;

        /// <summary>
        /// Loads the catalog and the tables of its starting language: the one asked for with
        /// <see cref="SetLanguageAsync"/> before initializing, or else the source language.
        /// </summary>
        /// <remarks>
        /// Calling it again returns the same task. With tables that load synchronously, such as embedded ones, the
        /// task is already complete when it is returned, so text can be read right away without awaiting. The task
        /// never fails: when the catalog can't be loaded, the problem is reported, and <see cref="IsInitialized"/>
        /// stays false.
        /// </remarks>
        Task InitializeAsync();

        /// <summary>
        /// Switches to <paramref name="language"/>: loads its tables, then applies the switch in one step, so no
        /// frame ever shows two languages. Before initialization, it chooses the language initialization starts in.
        /// </summary>
        /// <remarks>
        /// Switches requested in quick succession collapse: only the latest one loads, and every task completes once
        /// it is applied. A language the catalog doesn't define is reported and changes nothing. The task never fails.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="language"/> is empty.</exception>
        Task SetLanguageAsync(LanguageKey language);

        /// <summary>
        /// Returns the text of <paramref name="key"/> in the current language, falling back through the language's
        /// fallbacks to the source language.
        /// </summary>
        /// <remarks>
        /// Returns the same string instance on every call, so reading text allocates nothing. A key that exists
        /// nowhere returns <c>[Table.Key]</c> and is reported once. Before initialization, returns an empty string.
        /// An entry with arguments read this way shows them as <c>{name}</c>; read it as a message instead.
        /// </remarks>
        string Get(in EntryKey key);

        /// <summary>
        /// Returns the text of a message, such as <c>LocalizationKeys.Shop.CoinBalance(coins: 5)</c>, formatted in the
        /// language it is found in: its plural forms, its digits and its separators.
        /// </summary>
        /// <remarks>
        /// Allocates the returned string; to format without allocating, use <see cref="TryFormat"/> or a binding. A
        /// missing argument shows as <c>{name}</c> and is reported once. Behaves like <see cref="Get(in EntryKey)"/>
        /// otherwise, and an entry without arguments returns its text, ignoring the arguments.
        /// </remarks>
        string Get(in EntryMessage message);

        /// <summary>
        /// Writes the text of a message into <paramref name="destination"/> without allocating, for consumers that
        /// accept characters, such as TextMeshPro.
        /// </summary>
        /// <returns>
        /// False when <paramref name="destination"/> is too small, with nothing written; call it again with a larger one.
        /// </returns>
        /// <remarks>Behaves like <see cref="Get(in EntryMessage)"/> otherwise.</remarks>
        bool TryFormat(in EntryMessage message, Span<char> destination, out int written);

        /// <summary>
        /// Returns the text of an entry named by data, such as dialogue lines a script refers to by name. Hashes the
        /// names without allocating.
        /// </summary>
        /// <remarks>Behaves like <see cref="Get(in EntryKey)"/> otherwise.</remarks>
        string Get(ReadOnlySpan<char> tableName, ReadOnlySpan<char> entryName);

        /// <summary>
        /// Returns whether <paramref name="key"/> exists, and its text if it does. Unlike <see cref="Get(in EntryKey)"/>,
        /// a missing key is not reported, which suits keys that may legitimately be absent.
        /// </summary>
        bool TryGet(in EntryKey key, out string text);

        /// <summary>
        /// Returns the characters of <paramref name="key"/>'s text without creating a string, for consumers that
        /// accept characters, such as TextMeshPro.
        /// </summary>
        /// <remarks>Behaves like <see cref="Get(in EntryKey)"/> otherwise.</remarks>
        ReadOnlyMemory<char> GetMemory(in EntryKey key);

        /// <summary>
        /// Calls <paramref name="apply"/> with the text of <paramref name="key"/> right away, and again every time it
        /// changes, until the returned binding is disposed.
        /// </summary>
        /// <remarks>
        /// Pass a static lambda that takes its target as a parameter, such as
        /// <c>static (label, text) =&gt; label.text = text</c>, and binding allocates nothing. The callback runs on
        /// the host thread; bound from another thread, its first call is scheduled there. Before initialization the
        /// first call receives an empty string. A callback that throws is reported, and other bindings still update.
        /// A binding whose target the engine destroyed is released and reported.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="apply"/> is null.</exception>
        TextBinding Bind<TTarget>(in EntryKey key, TTarget target, Action<TTarget, string> apply) where TTarget : class;

        /// <summary>
        /// Calls <paramref name="apply"/> with the text of <paramref name="message"/> right away, and again every time it
        /// changes, until the returned binding is disposed. <see cref="TextBinding.SetMessage"/> changes its arguments.
        /// </summary>
        /// <remarks>Behaves like the binding of a key otherwise; formatting allocates only the string the callback receives.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="apply"/> is null.</exception>
        TextBinding Bind<TTarget>(in EntryMessage message, TTarget target, Action<TTarget, string> apply) where TTarget : class;
    }
}
