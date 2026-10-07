using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Formatting;
using System;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class NumberFormatterTests
    {
        private const string NoBreakSpace = "\u00A0";

        [TestCase("en", 1234.5, "1,234.5")]
        [TestCase("en", 1234567.891, "1,234,567.891")]
        [TestCase("en", -1234.5, "-1,234.5")]
        [TestCase("en", 0.0005, "0")]
        [TestCase("en", 0.0015, "0.002")]
        [TestCase("en", -0.0001, "-0")]
        [TestCase("ru", 1234.5, "1" + NoBreakSpace + "234,5")]
        [TestCase("es", 1234, "1234")]
        [TestCase("es", 12345, "12.345")]
        [TestCase("de-CH", 1234567, "1'234'567")]
        [TestCase("hi", 1234567, "12,34,567")]
        [TestCase("fa", 1234.5, "\u06F1\u066C\u06F2\u06F3\u06F4\u066B\u06F5")]
        [TestCase("ar", -5, "\u200E-5")]
        [TestCase("ff-Adlm", 12, "\uD83A\uDD51\uD83A\uDD52")]
        public void Append_WritesNumbersTheWayTheLanguageDoes(string culture, double value, string expected)
        {
            Assert.That(Format(culture, value, NumberStyle.Default), Is.EqualTo(expected));
        }

        [TestCase("en", 0.25, "25%")]
        [TestCase("en", -0.25, "-25%")]
        [TestCase("fr", 0.25, "25" + NoBreakSpace + "%")]
        [TestCase("tr", 0.25, "%25")]
        [TestCase("en", 12.345, "1,234%")]
        public void Append_WritesPercentages(string culture, double value, string expected)
        {
            Assert.That(Format(culture, value, NumberStyle.Percent), Is.EqualTo(expected));
        }

        [TestCase(1234.5, "1,234")]
        [TestCase(1235.5, "1,236")]
        [TestCase(-0.4, "-0")]
        public void Append_RoundsIntegersHalfToEven(double value, string expected)
        {
            Assert.That(Format("en", value, NumberStyle.Integer), Is.EqualTo(expected));
        }

        [Test]
        public void Append_KeepsDecimalsAndLongsExact()
        {
            Assert.That(Format("en", 123456789012345678.125m, NumberStyle.Default), Is.EqualTo("123,456,789,012,345,678.125"));
            Assert.That(Format("en", long.MinValue, NumberStyle.Default), Is.EqualTo("-9,223,372,036,854,775,808"));
            Assert.That(Format("en", ulong.MaxValue, NumberStyle.Default), Is.EqualTo("18,446,744,073,709,551,615"));
            Assert.That(Format("en", decimal.MaxValue, NumberStyle.Default), Is.EqualTo("79,228,162,514,264,337,593,543,950,335"));
            Assert.That(Format("en", 0.1f + 0.2f, NumberStyle.Default), Is.EqualTo("0.3"));
            Assert.That(Format("en", 0.1 + 0.2, NumberStyle.Default), Is.EqualTo("0.3"));
        }

        [Test]
        public void Append_WritesNumbersBeyondDecimal()
        {
            Assert.That(Format("en", 1e30, NumberStyle.Default), Is.EqualTo("1,000,000,000,000,000,000,000,000,000,000"));
            Assert.That(Format("en", double.PositiveInfinity, NumberStyle.Default), Is.EqualTo("\u221E"));
            Assert.That(Format("en", double.NegativeInfinity, NumberStyle.Default), Is.EqualTo("-\u221E"));
            Assert.That(Format("en", double.NaN, NumberStyle.Default), Is.EqualTo("NaN"));
        }

        [Test]
        public void Root_WritesLikeCldrsRootLocale()
        {
            Assert.That(Format(NumberSymbols.Root, 1234.5, NumberStyle.Default), Is.EqualTo("1,234.5"));
            Assert.That(Format(NumberSymbols.Root, 0.5, NumberStyle.Percent), Is.EqualTo("50%"));
        }

        [Test]
        public void WithOverrides_ReplacesOnlyTheGivenSymbols()
        {
            Assert.That(NumberSymbols.TryFind("ru", out NumberSymbols russian), Is.True);
            NumberSymbols latin = russian.WithOverrides("0123456789", ".", null);
            NumberSymbols ungrouped = russian.WithOverrides(null, null, string.Empty);

            Assert.That(Format(latin, 1234.5, NumberStyle.Default), Is.EqualTo("1" + NoBreakSpace + "234.5"));
            Assert.That(Format(ungrouped, 1234.5, NumberStyle.Default), Is.EqualTo("1234,5"));
            Assert.That(russian.WithOverrides(null, null, null), Is.SameAs(russian));
        }

        [TestCase("1.0004", 1UL, 0)]
        [TestCase("1.5", 1UL, 1)]
        [TestCase("2.0015", 2UL, 3)]
        public void TryGetOperands_UsesTheDigitsTheNumberShowsWith(string value, ulong integer, int fractionDigits)
        {
            MessageNumber number = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

            Assert.That(NumberFormatter.TryGetOperands(in number, NumberSymbols.Root, out PluralOperands operands), Is.True);
            Assert.That(operands.I, Is.EqualTo(integer));
            Assert.That(operands.V, Is.EqualTo(fractionDigits));
        }

        [Test]
        public void TryGetOperands_FailsForInfinitiesAndNaN()
        {
            MessageNumber infinity = double.PositiveInfinity;
            MessageNumber notANumber = double.NaN;

            Assert.That(NumberFormatter.TryGetOperands(in infinity, NumberSymbols.Root, out _), Is.False);
            Assert.That(NumberFormatter.TryGetOperands(in notANumber, NumberSymbols.Root, out _), Is.False);
        }

        [Test]
        public void TryFind_FindsRegionsThroughTheirLanguage()
        {
            Assert.That(NumberSymbols.TryFind("en-GB", out NumberSymbols british), Is.True);
            Assert.That(NumberSymbols.TryFind("es-MX", out NumberSymbols mexican), Is.True);
            Assert.That(NumberSymbols.TryFind("xx", out _), Is.False);

            Assert.That(Format(british, 1234.5, NumberStyle.Default), Is.EqualTo("1,234.5"));
            Assert.That(Format(mexican, 1234.5, NumberStyle.Default), Is.EqualTo("1,234.5"));
        }

        private static string Format(string culture, MessageNumber number, NumberStyle style)
        {
            Assert.That(NumberSymbols.TryFind(culture, out NumberSymbols symbols), Is.True, culture);
            return Format(symbols, number, style);
        }

        private static string Format(NumberSymbols symbols, MessageNumber number, NumberStyle style)
        {
            Span<char> buffer = stackalloc char[8];
            TextBuilder output = new(buffer);
            NumberFormatter.Append(ref output, in number, style, symbols);
            string text = output.ToString();
            output.Dispose();
            return text;
        }
    }
}
