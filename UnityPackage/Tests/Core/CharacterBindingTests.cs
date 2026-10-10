using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class CharacterBindingTests
    {
        private const string CatalogText = "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Coins = new("Shop", "Coins");
        private static readonly LanguageKey Russian = new("Russian");

        private static Localizer CreateLocalizer()
        {
            MemoryTableSource source = MemoryTableSource.Imported("Localization", CatalogText, null,
                ("Shop", "English", "Purchase = Buy\nCoins = {coins, plural, one {# coin} other {# coins}}\nLong = {coins} " + new string('x', 400)),
                ("Shop", "Russian", "Purchase = Kupit\nCoins = {coins, plural, one {# moneta} few {# monety} many {# monet} other {# monety}}"));
            Localizer localizer = new(source.CatalogKey, new TestHost(source));
            localizer.InitializeAsync();
            return localizer;
        }

        private static EntryMessage CoinsOf(int coins) => new(Coins, new MessageArgument("coins", coins));

        /// <summary>Collects what character bindings receive, copied, since the characters are valid only during the call.</summary>
        private sealed class Label
        {
            public List<string> Texts { get; } = new();

            public char[] Buffer { get; } = new char[512];

            public int Length { get; set; }
        }

        [Test]
        public void BindCharacters_GivesTheTextOfAKeyThroughEverySwitch()
        {
            using Localizer localizer = CreateLocalizer();
            Label label = new();

            using TextBinding binding = localizer.BindCharacters(Purchase, label, static (target, characters) => target.Texts.Add(characters.ToString()));
            localizer.SetLanguageAsync(Russian);

            Assert.That(label.Texts, Is.EqualTo(new[] { "Buy", "Kupit" }));
        }

        [Test]
        public void BindCharacters_FormatsMessagesAndTheirNewArguments()
        {
            using Localizer localizer = CreateLocalizer();
            Label label = new();

            TextBinding binding = localizer.BindCharacters(CoinsOf(1), label, static (target, characters) => target.Texts.Add(characters.ToString()));
            binding.SetMessage(CoinsOf(5));
            localizer.SetLanguageAsync(Russian);
            binding.Dispose();

            Assert.That(label.Texts, Is.EqualTo(new[] { "1 coin", "5 coins", "5 monet" }));
        }

        [Test]
        public void BindCharacters_GrowsItsBufferForLongMessages()
        {
            using Localizer localizer = CreateLocalizer();
            Label label = new();

            using TextBinding binding = localizer.BindCharacters(new EntryMessage(new EntryKey("Shop", "Long"), new MessageArgument("coins", 7)), label,
                static (target, characters) => target.Texts.Add(characters.ToString()));

            Assert.That(label.Texts[0], Is.EqualTo("7 " + new string('x', 400)));
        }

        [Test]
        public void BindCharacters_ShowsTheMarkerOfAMissingKey()
        {
            using Localizer localizer = CreateLocalizer();
            Label label = new();

            using TextBinding binding = localizer.BindCharacters(new EntryKey("Shop", "Refund"), label, static (target, characters) => target.Texts.Add(characters.ToString()));

            Assert.That(label.Texts, Is.EqualTo(new[] { "[Shop.Refund]" }));
        }

        [Test]
        public void ANestedUpdate_LeavesTheOuterCharactersIntact()
        {
            using Localizer localizer = CreateLocalizer();
            Label inner = new();
            List<string> seen = new();
            TextBinding innerBinding = localizer.BindCharacters(CoinsOf(2), inner, static (target, characters) => target.Texts.Add(characters.ToString()));
            (TextBinding Inner, List<string> Seen) state = (innerBinding, seen);

            using TextBinding outer = localizer.BindCharacters(CoinsOf(1), new object[] { state }, static (target, characters) =>
            {
                (TextBinding Inner, List<string> Seen) captured = ((TextBinding, List<string>))target[0];
                string before = characters.ToString();
                captured.Inner.SetMessage(new EntryMessage(new EntryKey("Shop", "Coins"), new MessageArgument("coins", 3)));
                captured.Seen.Add(before + "|" + characters.ToString());
            });

            Assert.That(seen, Is.EqualTo(new[] { "1 coin|1 coin" }));
            Assert.That(inner.Texts, Is.EqualTo(new[] { "2 coins", "3 coins" }));
            innerBinding.Dispose();
        }

        [Test]
        public void UpdatingAMessage_AllocatesNothing()
        {
            using Localizer localizer = CreateLocalizer();
            Label label = new();
            TextBinding binding = localizer.BindCharacters(CoinsOf(1), label, static (target, characters) =>
            {
                characters.Span.CopyTo(target.Buffer);
                target.Length = characters.Length;
            });
            int coins = 1;

            Allocations.AssertNone(() =>
            {
                coins++;
                binding.SetMessage(new EntryMessage(Coins, new MessageArgument("coins", coins)));
            });
            Assert.That(new string(label.Buffer, 0, label.Length), Is.EqualTo($"{coins} coins"));
            binding.Dispose();
        }

        [Test]
        public void BindCharacters_RejectsNullTargetsAndCallbacks()
        {
            using Localizer localizer = CreateLocalizer();

            Assert.That(() => localizer.BindCharacters<Label>(Purchase, null, static (_, _) => { }), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => localizer.BindCharacters(Purchase, new Label(), null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
