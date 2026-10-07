using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class MessageLookupTests
    {
        private const string CatalogText =
            "@source English\n" +
            "[English]\nCulture = en\n" +
            "[Russian]\nCulture = ru\n" +
            "[Pirate]\nFallback = English\n" +
            "[Arabic]\nCulture = ar\nDigits = 0123456789\n";

        private static readonly EntryKey Balance = new("Shop", "Balance");
        private static readonly EntryKey Gift = new("Shop", "Gift");
        private static readonly EntryKey Title = new("Shop", "Title");
        private static readonly LanguageKey Russian = new("Russian");
        private static readonly LanguageKey Pirate = new("Pirate");

        private static MemoryTableSource CreateSource()
        {
            return new MemoryTableSource("Localization", CatalogText,
                ("Shop", "English", "Balance = You have {coins, plural, one {# coin} other {# coins}}.\n" +
                                    "Gift = {name} sent you {coins, plural, one {a coin} other {# coins}}.\n" +
                                    "Due = Due {deadline, date}\n" +
                                    "Title = Shop"),
                ("Shop", "Russian", "Balance = \u0423 \u0432\u0430\u0441 {coins, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430} few {# \u043C\u043E\u043D\u0435\u0442\u044B} many {# \u043C\u043E\u043D\u0435\u0442} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}."),
                ("Shop", "Pirate", "Title = Booty"));
        }

        private static Localizer CreateInitialized(out TestHost host)
        {
            MemoryTableSource source = CreateSource();
            host = new TestHost(source);
            Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();
            return localizer;
        }

        private static EntryMessage BalanceOf(MessageNumber coins) => new(Balance, new MessageArgument("coins", coins));

        [Test]
        public void Get_FormatsTheMessageInTheCurrentLanguage()
        {
            using Localizer localizer = CreateInitialized(out TestHost host);

            Assert.That(localizer.Get(BalanceOf(1)), Is.EqualTo("You have 1 coin."));
            Assert.That(localizer.Get(BalanceOf(1234)), Is.EqualTo("You have 1,234 coins."));
            localizer.SetLanguageAsync(Russian);
            Assert.That(localizer.Get(BalanceOf(21)), Is.EqualTo("\u0423 \u0432\u0430\u0441 21 \u043C\u043E\u043D\u0435\u0442\u0430."));
            Assert.That(localizer.Get(BalanceOf(1234)), Is.EqualTo("\u0423 \u0432\u0430\u0441 1\u00A0234 \u043C\u043E\u043D\u0435\u0442\u044B."));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void Get_FormatsFallbackTextWithTheRulesOfItsLanguage()
        {
            using Localizer localizer = CreateInitialized(out _);
            localizer.SetLanguageAsync(Russian);

            // Russian has no Gift, so the English text shows, with English plural forms: 21 is 'other' in English.
            Assert.That(localizer.Get(new EntryMessage(Gift, new MessageArgument("name", "Ann"), new MessageArgument("coins", 21))),
                Is.EqualTo("Ann sent you 21 coins."));
            localizer.SetLanguageAsync(Pirate);
            Assert.That(localizer.Get(BalanceOf(1)), Is.EqualTo("You have 1 coin."));
            Assert.That(localizer.Get(Title), Is.EqualTo("Booty"));
        }

        [Test]
        public void Get_UsesTheDigitsTheCatalogGivesTheLanguage()
        {
            using Localizer localizer = CreateInitialized(out _);

            localizer.SetLanguageAsync(new LanguageKey("Arabic"));

            Assert.That(localizer.Get(BalanceOf(1234)), Is.EqualTo("You have 1,234 coins."));
        }

        [Test]
        public void Get_OfAnEntryWithoutArguments_ReturnsItsText()
        {
            using Localizer localizer = CreateInitialized(out TestHost host);

            Assert.That(localizer.Get(new EntryMessage(Title, new MessageArgument("unused", 1))), Is.EqualTo("Shop"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void Get_WithAMissingArgument_ShowsItsNameAndReportsOnce()
        {
            using Localizer localizer = CreateInitialized(out TestHost host);
            EntryMessage withoutName = new(Gift, new MessageArgument("coins", 2));

            Assert.That(localizer.Get(withoutName), Is.EqualTo("{name} sent you 2 coins."));
            Assert.That(localizer.Get(withoutName), Is.EqualTo("{name} sent you 2 coins."));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
            Assert.That(host.Reports[0].Message, Does.Contain("{name}"));
        }

        [Test]
        public void Get_OfAMessageByItsKey_ShowsArgumentNamesAndWarnsOnce()
        {
            using Localizer localizer = CreateInitialized(out TestHost host);

            Assert.That(localizer.Get(Balance), Is.EqualTo("You have {coins}."));
            Assert.That(localizer.Get(Balance), Is.SameAs(localizer.Get(Balance)));
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void Get_OfAMissingKey_ShowsTheMarker()
        {
            using Localizer localizer = CreateInitialized(out TestHost host);

            Assert.That(localizer.Get(new EntryMessage(new EntryKey("Shop", "Nope"), new MessageArgument("coins", 1))), Is.EqualTo("[Shop.Nope]"));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
        }

        [Test]
        public void TryFormat_WritesIntoTheCallersBufferOrAsksForMore()
        {
            using Localizer localizer = CreateInitialized(out _);
            char[] large = new char[64];
            char[] small = new char[8];

            Assert.That(localizer.TryFormat(BalanceOf(5), large, out int written), Is.True);
            Assert.That(new string(large, 0, written), Is.EqualTo("You have 5 coins."));
            Assert.That(localizer.TryFormat(BalanceOf(5), small, out written), Is.False);
            Assert.That(written, Is.Zero);
            Assert.That(localizer.TryFormat(new EntryMessage(Title), large, out written), Is.True);
            Assert.That(new string(large, 0, written), Is.EqualTo("Shop"));
        }

        [Test]
        public void SetFormatter_FormatsItsType()
        {
            using Localizer localizer = CreateInitialized(out TestHost host);
            localizer.SetFormatter("date", FormatDay);

            string text = localizer.Get(new EntryMessage(new EntryKey("Shop", "Due"), new MessageArgument("deadline", new DateTime(2026, 10, 7))));

            Assert.That(text, Is.EqualTo("Due day 7"));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void SetFormatter_RejectsTheTypesMessagesDefine()
        {
            using Localizer localizer = CreateInitialized(out _);

            Assert.That(() => localizer.SetFormatter("number", FormatDay), Throws.ArgumentException);
            Assert.That(() => localizer.SetFormatter("Plural", FormatDay), Throws.ArgumentException);
            Assert.That(() => localizer.SetFormatter("not a name", FormatDay), Throws.ArgumentException);
        }

        [Test]
        public void Bind_FormatsRightAwayAndAfterEverySwitch()
        {
            using Localizer localizer = CreateInitialized(out _);
            List<string> label = new();

            TextBinding binding = localizer.Bind(BalanceOf(3), label, static (target, text) => target.Add(text));
            localizer.SetLanguageAsync(Russian);

            Assert.That(label, Is.EqualTo(new[] { "You have 3 coins.", "\u0423 \u0432\u0430\u0441 3 \u043C\u043E\u043D\u0435\u0442\u044B." }));
            Assert.That(binding.IsActive, Is.True);
        }

        [Test]
        public void SetMessage_FormatsOnlyWhenTheArgumentsChange()
        {
            using Localizer localizer = CreateInitialized(out _);
            List<string> label = new();
            TextBinding binding = localizer.Bind(BalanceOf(1), label, static (target, text) => target.Add(text));

            binding.SetMessage(BalanceOf(1));
            binding.SetMessage(BalanceOf(2));
            binding.SetMessage(BalanceOf(2.0));

            Assert.That(label, Is.EqualTo(new[] { "You have 1 coin.", "You have 2 coins." }));
        }

        [Test]
        public void SetMessage_TurnsAKeyBindingIntoAMessageBinding()
        {
            using Localizer localizer = CreateInitialized(out _);
            List<string> label = new();
            TextBinding binding = localizer.Bind(Title, label, static (target, text) => target.Add(text));

            binding.SetMessage(BalanceOf(7));
            localizer.SetLanguageAsync(Russian);

            Assert.That(label, Is.EqualTo(new[] { "Shop", "You have 7 coins.", "\u0423 \u0432\u0430\u0441 7 \u043C\u043E\u043D\u0435\u0442." }));
        }

        [Test]
        public void SetMessage_FromAnotherThread_AppliesOnTheHostThread()
        {
            using Localizer localizer = CreateInitialized(out TestHost host);
            List<string> label = new();
            TextBinding binding = localizer.Bind(BalanceOf(1), label, static (target, text) => target.Add(text));

            host.IsTestThreadTheHost = false;
            Task.Run(() => binding.SetMessage(BalanceOf(5))).Wait();
            Assert.That(label.Count, Is.EqualTo(1));
            host.RunUpdates();

            Assert.That(label, Is.EqualTo(new[] { "You have 1 coin.", "You have 5 coins." }));
        }

        [Test]
        public void MessageBindings_ReuseTheirArgumentHoldersAfterDisposal()
        {
            using Localizer localizer = CreateInitialized(out _);
            List<string> label = new();
            for (int i = 0; i < 3; i++)
            {
                TextBinding binding = localizer.Bind(BalanceOf(i), label, static (target, text) => target.Add(text));
                binding.Dispose();
            }
            TextBinding last = localizer.Bind(BalanceOf(9), label, static (target, text) => target.Add(text));
            localizer.SetLanguageAsync(Russian);

            Assert.That(label, Is.EqualTo(new[] { "You have 0 coins.", "You have 1 coin.", "You have 2 coins.", "You have 9 coins.", "\u0423 \u0432\u0430\u0441 9 \u043C\u043E\u043D\u0435\u0442." }));
            Assert.That(localizer.BindingCount, Is.EqualTo(1));
            last.Dispose();
        }

        [Test]
        public void SetMessage_WithIdenticalArguments_AllocatesNothing()
        {
            using Localizer localizer = CreateInitialized(out _);
            List<string> label = new();
            TextBinding binding = localizer.Bind(BalanceOf(4), label, static (target, text) => target.Add(text));

            Allocations.AssertNone(() => binding.SetMessage(BalanceOf(4)));
        }

        [Test]
        public void TryFormat_AllocatesNothing()
        {
            using Localizer localizer = CreateInitialized(out _);
            char[] buffer = new char[64];

            Allocations.AssertNone(() => localizer.TryFormat(BalanceOf(1234), buffer, out _));
        }

        [Test]
        public void Get_OfCachedText_AllocatesNothing()
        {
            using Localizer localizer = CreateInitialized(out _);

            Allocations.AssertNone(() => localizer.Get(Title));
        }

        private static bool FormatDay(in MessageValue value, ReadOnlySpan<char> style, LanguageInfo language, Span<char> destination, out int written)
        {
            value.TryGetDateTime(out DateTime date);
            string text = "day " + date.Day;
            text.AsSpan().CopyTo(destination);
            written = text.Length;
            return true;
        }
    }
}
