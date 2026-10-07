using NUnit.Framework;
using System;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class EntryMessageTests
    {
        private static readonly EntryKey CoinBalance = new("Shop", "CoinBalance");

        [Test]
        public void MessageValue_ConvertsEveryKindWithoutLosingIt()
        {
            MessageValue text = "Alex";
            MessageValue integer = 5;
            MessageValue real = 2.5;
            MessageValue money = 19.99m;
            MessageValue date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            MessageValue duration = TimeSpan.FromMinutes(90);
            MessageValue custom = MessageValue.FromObject(new Uri("https://example.com"));
            MessageValue empty = (string)null;

            Assert.That(text.TryGetText(out string name) && name == "Alex", Is.True);
            Assert.That(integer.TryGetNumber(out MessageNumber five) && five == 5, Is.True);
            Assert.That(real.TryGetNumber(out MessageNumber half) && half.ToDouble() == 2.5, Is.True);
            Assert.That(money.TryGetNumber(out MessageNumber price) && price.TryGetDecimal(out decimal exact) && exact == 19.99m, Is.True);
            Assert.That(date.TryGetDateTime(out DateTime day) && day.Kind == DateTimeKind.Utc && day.Hour == 12, Is.True);
            Assert.That(duration.TryGetTimeSpan(out TimeSpan span) && span.TotalMinutes == 90, Is.True);
            Assert.That(custom.Kind, Is.EqualTo(MessageValueKind.Object));
            Assert.That(empty.Kind, Is.EqualTo(MessageValueKind.Empty));
            Assert.That(text.TryGetNumber(out _), Is.False);
        }

        [Test]
        public void MessageValue_FromObject_UnboxesKnownKinds()
        {
            Assert.That(MessageValue.FromObject(5).Kind, Is.EqualTo(MessageValueKind.Number));
            Assert.That(MessageValue.FromObject("text").Kind, Is.EqualTo(MessageValueKind.Text));
            Assert.That(MessageValue.FromObject(DateTime.MinValue).Kind, Is.EqualTo(MessageValueKind.DateTime));
            Assert.That(MessageValue.FromObject(null).Kind, Is.EqualTo(MessageValueKind.Empty));
            Assert.That(MessageValue.FromObject(5), Is.EqualTo((MessageValue)5));
        }

        [Test]
        public void MessageNumber_EqualsByValueAcrossTypes()
        {
            Assert.That((MessageNumber)5, Is.EqualTo((MessageNumber)5.0));
            Assert.That((MessageNumber)5, Is.EqualTo((MessageNumber)5m));
            Assert.That((MessageNumber)1.0m, Is.EqualTo((MessageNumber)1.00m));
            Assert.That((MessageNumber)double.NaN, Is.EqualTo((MessageNumber)double.NaN));
            Assert.That((MessageNumber)5, Is.Not.EqualTo((MessageNumber)6));
        }

        [Test]
        public void Equals_ComparesTheEntryAndEveryArgument()
        {
            EntryMessage message = new(CoinBalance, new MessageArgument("coins", 5));

            Assert.That(message, Is.EqualTo(new EntryMessage(CoinBalance, new MessageArgument("Coins", 5))));
            Assert.That(message, Is.Not.EqualTo(new EntryMessage(CoinBalance, new MessageArgument("coins", 6))));
            Assert.That(message, Is.Not.EqualTo(new EntryMessage(CoinBalance)));
            Assert.That(message.ToString(), Is.EqualTo("Shop.CoinBalance(coins: 5)"));
        }

        [Test]
        public void With_AddsOrReplacesArguments()
        {
            EntryMessage message = new EntryMessage(CoinBalance).With(new MessageArgument("coins", 1)).With(new MessageArgument("name", "Alex"));
            EntryMessage replaced = message.With(new MessageArgument("COINS", 2));

            Assert.That(message.ArgumentCount, Is.EqualTo(2));
            Assert.That(replaced.ArgumentCount, Is.EqualTo(2));
            Assert.That(replaced.TryGetValue("coins", out MessageValue coins) && coins == 2, Is.True);
            Assert.That(replaced.TryGetValue("missing", out _), Is.False);
        }

        [Test]
        public void Constructor_KeepsMoreThanFourArguments()
        {
            EntryMessage message = new(CoinBalance,
                new MessageArgument("a", 1), new MessageArgument("b", 2), new MessageArgument("c", 3),
                new MessageArgument("d", 4), new MessageArgument("e", 5), new MessageArgument("f", 6));
            EntryMessage extended = message.With(new MessageArgument("g", 7));

            Assert.That(message.ArgumentCount, Is.EqualTo(6));
            Assert.That(message.GetArgument(5).Name, Is.EqualTo("f"));
            Assert.That(extended.ArgumentCount, Is.EqualTo(7));
            Assert.That(extended.TryGetValue("e", out MessageValue e) && e == 5, Is.True);
        }

        [Test]
        public void Constructor_RejectsDuplicateAndEmptyArguments()
        {
            Assert.That(() => { new EntryMessage(CoinBalance, new MessageArgument("coins", 1), new MessageArgument("Coins", 2)); }, Throws.ArgumentException);
            Assert.That(() => { new EntryMessage(CoinBalance, default(MessageArgument)); }, Throws.ArgumentException);
            Assert.That(() => { new MessageArgument("1coins", 1); }, Throws.ArgumentException);
        }
    }
}
