using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Formatting;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class PluralRulesTests
    {
        [Test]
        public void EveryCldrCardinalSample_ChoosesItsForm()
        {
            Assert.That(CheckSamples(false), Is.Empty);
        }

        [Test]
        public void EveryCldrOrdinalSample_ChoosesItsForm()
        {
            Assert.That(CheckSamples(true), Is.Empty);
        }

        [TestCase("en", "1", "one")]
        [TestCase("en", "1.0", "other")]
        [TestCase("en", "2", "other")]
        [TestCase("ru", "21", "one")]
        [TestCase("ru", "22", "few")]
        [TestCase("ru", "25", "many")]
        [TestCase("ru", "1.5", "other")]
        [TestCase("ar", "0", "zero")]
        [TestCase("ar", "2", "two")]
        [TestCase("ar", "103", "few")]
        [TestCase("ar", "111", "many")]
        [TestCase("fr", "0", "one")]
        [TestCase("fr", "1000000", "many")]
        [TestCase("ja", "1", "other")]
        public void SelectCardinal_ChoosesTheFormOfTheLanguage(string culture, string number, string expected)
        {
            Assert.That(PluralRules.TryFindCardinal(culture, out int rules), Is.True);
            Assert.That(PluralOperands.TryParse(number, out PluralOperands operands), Is.True);

            Assert.That(PluralCategories.GetName(PluralRules.SelectCardinal(rules, in operands)), Is.EqualTo(expected));
        }

        [TestCase("1", "one")]
        [TestCase("2", "two")]
        [TestCase("3", "few")]
        [TestCase("11", "other")]
        [TestCase("22", "two")]
        [TestCase("113", "other")]
        public void SelectOrdinal_ChoosesEnglishSuffixes(string number, string expected)
        {
            Assert.That(PluralRules.TryFindOrdinal("en-US", out int rules), Is.True);
            Assert.That(PluralOperands.TryParse(number, out PluralOperands operands), Is.True);

            Assert.That(PluralCategories.GetName(PluralRules.SelectOrdinal(rules, in operands)), Is.EqualTo(expected));
        }

        [TestCase("pt_br", "pt-BR")]
        [TestCase("ZH-hant-tw", "zh-Hant-TW")]
        [TestCase(" es-419 ", "es-419")]
        [TestCase("ca-ES-VALENCIA", "ca-ES-valencia")]
        [TestCase("", "")]
        public void Normalize_WritesTagsTheWayCldrNamesLocales(string tag, string expected)
        {
            Assert.That(LanguageTags.Normalize(tag), Is.EqualTo(expected));
        }

        [Test]
        public void TryFind_FallsBackToShorterTagsButNotToTheRoot()
        {
            Assert.That(PluralRules.TryFindCardinal("pt-PT", out int portugal), Is.True);
            Assert.That(PluralRules.TryFindCardinal("pt-BR", out int brazil), Is.True);
            Assert.That(PluralRules.TryFindCardinal("pt", out int portuguese), Is.True);
            Assert.That(PluralRules.TryFindCardinal("xx-YY", out int unknown), Is.False);

            Assert.That(portugal, Is.Not.EqualTo(portuguese));
            Assert.That(brazil, Is.EqualTo(portuguese));
            Assert.That(unknown, Is.EqualTo(PluralRules.OtherOnly));
        }

        [TestCase("1.50", 1UL, 2, 50UL, 5UL, 1)]
        [TestCase("-3", 3UL, 0, 0UL, 0UL, 0)]
        [TestCase("1.1c6", 1100000UL, 0, 0UL, 0UL, 0)]
        [TestCase("1.0000001c6", 1000000UL, 1, 1UL, 1UL, 1)]
        public void TryParse_ReadsEveryOperand(string number, ulong integer, int fractionDigitCount, ulong fraction, ulong trimmedFraction, int trimmedCount)
        {
            Assert.That(PluralOperands.TryParse(number, out PluralOperands operands), Is.True);

            Assert.That(operands.I, Is.EqualTo(integer));
            Assert.That(operands.V, Is.EqualTo(fractionDigitCount));
            Assert.That(operands.F, Is.EqualTo(fraction));
            Assert.That(operands.T, Is.EqualTo(trimmedFraction));
            Assert.That(operands.W, Is.EqualTo(trimmedCount));
        }

        [Test]
        public void TryParse_KeepsTheLastDigitsOfHugeIntegers()
        {
            Assert.That(PluralOperands.TryParse("123456789012345678901", out PluralOperands operands), Is.True);

            Assert.That(operands.I % 1000, Is.EqualTo(901UL));
            Assert.That(operands.I, Is.GreaterThanOrEqualTo(PluralOperands.LargeIntegerMarker));
        }

        [TestCase("")]
        [TestCase("1.")]
        [TestCase(".5")]
        [TestCase("1x")]
        [TestCase("1c")]
        public void TryParse_RejectsMalformedNumbers(string number)
        {
            Assert.That(PluralOperands.TryParse(number, out _), Is.False);
        }

        /// <summary>Checks every sample of every row, returning one line per sample that chooses another form.</summary>
        private static List<string> CheckSamples(bool isOrdinal)
        {
            List<string> failures = new();
            foreach ((string locale, bool rowIsOrdinal, string category, string samples) in CldrPluralSamples.Rows)
            {
                if (rowIsOrdinal != isOrdinal)
                {
                    continue;
                }
                bool isFound = isOrdinal ? PluralRules.TryFindOrdinal(locale, out int rules) : PluralRules.TryFindCardinal(locale, out rules);
                if (!isFound || !PluralCategories.TryParse(category, out PluralCategory expected))
                {
                    failures.Add($"{locale}: no rules for {category}");
                    continue;
                }
                foreach (string sample in ExpandSamples(samples))
                {
                    if (!PluralOperands.TryParse(sample, out PluralOperands operands))
                    {
                        failures.Add($"{locale}: can't read the sample {sample}");
                        continue;
                    }
                    PluralCategory actual = isOrdinal ? PluralRules.SelectOrdinal(rules, in operands) : PluralRules.SelectCardinal(rules, in operands);
                    if (actual != expected)
                    {
                        failures.Add($"{locale}: {sample} chose {actual}, CLDR says {expected}");
                    }
                }
            }
            return failures;
        }

        /// <summary>Expands CLDR's sample syntax: <c>@integer 0, 2~4, ... @decimal 0.0~0.2</c> to every listed number.</summary>
        private static IEnumerable<string> ExpandSamples(string samples)
        {
            foreach (string section in samples.Split('@'))
            {
                string trimmed = section.Trim();
                int space = trimmed.IndexOf(' ');
                if (space < 0)
                {
                    continue;
                }
                foreach (string item in trimmed.Substring(space + 1).Split(','))
                {
                    string value = item.Trim();
                    if (value.Length == 0 || value == "\u2026" || value == "...")
                    {
                        continue;
                    }
                    int tilde = value.IndexOf('~');
                    if (tilde < 0)
                    {
                        yield return value;
                        continue;
                    }
                    string first = value.Substring(0, tilde);
                    string last = value.Substring(tilde + 1);
                    if (first.IndexOf('c') >= 0 || last.IndexOf('c') >= 0)
                    {
                        yield return first;
                        yield return last;
                        continue;
                    }
                    int decimals = first.IndexOf('.') < 0 ? 0 : first.Length - first.IndexOf('.') - 1;
                    decimal step = 1m;
                    for (int i = 0; i < decimals; i++)
                    {
                        step /= 10m;
                    }
                    decimal end = decimal.Parse(last, CultureInfo.InvariantCulture);
                    for (decimal current = decimal.Parse(first, CultureInfo.InvariantCulture); current <= end; current += step)
                    {
                        yield return current.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                    }
                }
            }
        }
    }
}
