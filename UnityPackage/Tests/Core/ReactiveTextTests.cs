using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class ReactiveTextTests
    {
        private const string CatalogText = "@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n";

        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Title = new("Shop", "Title");
        private static readonly EntryKey Coins = new("Shop", "Coins");
        private static readonly LanguageKey English = new("English");
        private static readonly LanguageKey Russian = new("Russian");

        private static Localizer CreateLocalizer(out TestHost host)
        {
            MemoryTableSource source = MemoryTableSource.Imported("Localization", CatalogText, null,
                ("Shop", "English", "Purchase = Buy\nTitle = Shop\nCoins = {coins, plural, one {# coin} other {# coins}}"),
                ("Shop", "Russian", "Purchase = Kupit"));
            host = new TestHost(source);
            Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();
            return localizer;
        }

        [Test]
        public void Value_FollowsEveryLanguageSwitch()
        {
            using Localizer localizer = CreateLocalizer(out _);
            using ReactiveText purchase = new(localizer, Purchase);
            List<string> changes = new();
            List<string> properties = new();
            purchase.Changed += text => changes.Add(text);
            ((INotifyPropertyChanged)purchase).PropertyChanged += (sender, arguments) => properties.Add(arguments.PropertyName);

            Assert.That(purchase.Value, Is.EqualTo("Buy"));
            localizer.SetLanguageAsync(Russian);

            Assert.That(purchase.Value, Is.EqualTo("Kupit"));
            Assert.That(purchase.ToString(), Is.EqualTo("Kupit"));
            Assert.That(changes, Is.EqualTo(new[] { "Kupit" }));
            Assert.That(properties, Is.EqualTo(new[] { "Value" }));
        }

        [Test]
        public void Changed_IsRaisedOnlyWhenTheTextDiffers()
        {
            using Localizer localizer = CreateLocalizer(out _);
            using ReactiveText title = new(localizer, Title);
            int changes = 0;
            title.Changed += _ => changes++;

            // Russian has no Title, so it keeps the English text.
            localizer.SetLanguageAsync(Russian);
            localizer.SetLanguageAsync(English);

            Assert.That(changes, Is.Zero);
            Assert.That(title.Value, Is.EqualTo("Shop"));
        }

        [Test]
        public void SetMessage_FormatsNewArguments()
        {
            using Localizer localizer = CreateLocalizer(out _);
            using ReactiveText coins = new(localizer, new EntryMessage(Coins, new MessageArgument("coins", 1)));

            Assert.That(coins.Value, Is.EqualTo("1 coin"));
            coins.SetMessage(new EntryMessage(Coins, new MessageArgument("coins", 3)));

            Assert.That(coins.Value, Is.EqualTo("3 coins"));
        }

        [Test]
        public void Dispose_StopsFollowingAndKeepsTheLastText()
        {
            using Localizer localizer = CreateLocalizer(out _);
            ReactiveText purchase = new(localizer, Purchase);

            purchase.Dispose();
            purchase.Dispose();
            localizer.SetLanguageAsync(Russian);

            Assert.That(purchase.IsActive, Is.False);
            Assert.That(purchase.Value, Is.EqualTo("Buy"));
        }

        [Test]
        public void AThrowingHandler_IsReportedAndPropertyListenersStillHear()
        {
            using Localizer localizer = CreateLocalizer(out TestHost host);
            using ReactiveText purchase = new(localizer, Purchase);
            int properties = 0;
            purchase.Changed += _ => throw new InvalidOperationException("Handler failure.");
            purchase.PropertyChanged += (_, _) => properties++;

            localizer.SetLanguageAsync(Russian);

            Assert.That(properties, Is.EqualTo(1));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
        }

        [Test]
        public void SetMessage_WithEqualArguments_AllocatesNothing()
        {
            using Localizer localizer = CreateLocalizer(out _);
            using ReactiveText coins = new(localizer, new EntryMessage(Coins, new MessageArgument("coins", 2)));

            Allocations.AssertNone(() => coins.SetMessage(new EntryMessage(Coins, new MessageArgument("coins", 2))));
        }

        [Test]
        public void Constructor_RejectsANullLocalizer()
        {
            Assert.That(() => new ReactiveText(null, Purchase), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
