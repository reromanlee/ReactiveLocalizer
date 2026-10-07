using reromanlee.ReactiveLocalizer.Formatting;
using System.Collections.Generic;
using System.Threading;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// Decides whether a plural message really lacks a form its language uses. Exact forms can stand in for one: in
    /// English, <c>=1</c> covers every number <c>one</c> would, since that's only 1; in Russian it doesn't, since
    /// <c>one</c> is also 21 and 31.
    /// </summary>
    /// <remarks>
    /// It tries numbers that reach every form of every CLDR language: the integers up to 1100, round millions, and
    /// fractions with one or two digits. A form counts as covered when every one of them that would choose it is an
    /// exact form of the message.
    /// </remarks>
    internal static class PluralCoverage
    {
        private static decimal[] _candidates;

        /// <summary>Returns the forms of <paramref name="missing"/> that no exact form of <paramref name="plural"/> stands in for.</summary>
        public static int RemoveCovered(PluralPart plural, int missing, LanguageFormat format)
        {
            if (missing == 0 || !HasExplicitForm(plural))
            {
                return missing;
            }
            decimal[] candidates = GetCandidates();
            int stillMissing = missing;
            for (int category = 0; category <= (int)PluralCategory.Other; category++)
            {
                if (!PluralCategories.Contains(missing, (PluralCategory)category))
                {
                    continue;
                }
                bool isCovered = true;
                for (int i = 0; i < candidates.Length && isCovered; i++)
                {
                    MessageNumber number = candidates[i];
                    MessageNumber adjusted = NumberFormatter.Subtract(in number, plural.Offset);
                    if (!NumberFormatter.TryGetOperands(in adjusted, format.Numbers, out PluralOperands operands))
                    {
                        continue;
                    }
                    PluralCategory chosen = plural.IsOrdinal
                        ? PluralRules.SelectOrdinal(format.OrdinalRules, in operands)
                        : PluralRules.SelectCardinal(format.CardinalRules, in operands);
                    if (chosen == (PluralCategory)category && !IsExplicit(plural, in number))
                    {
                        isCovered = false;
                    }
                }
                if (isCovered)
                {
                    stillMissing &= ~(1 << category);
                }
            }
            return stillMissing;
        }

        private static bool HasExplicitForm(PluralPart plural)
        {
            for (int i = 0; i < plural.Cases.Count; i++)
            {
                if (plural.Cases[i].IsExplicit)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsExplicit(PluralPart plural, in MessageNumber number)
        {
            for (int i = 0; i < plural.Cases.Count; i++)
            {
                if (plural.Cases[i].IsExplicit && NumberFormatter.IsEqual(in number, plural.Cases[i].ExplicitValue))
                {
                    return true;
                }
            }
            return false;
        }

        private static decimal[] GetCandidates()
        {
            decimal[] candidates = Volatile.Read(ref _candidates);
            if (candidates != null)
            {
                return candidates;
            }
            List<decimal> list = new();
            for (int i = 0; i <= 1100; i++)
            {
                list.Add(i);
            }
            for (int millions = 1; millions <= 3; millions++)
            {
                list.Add(millions * 1000000m);
                list.Add(millions * 1000000m + 1m);
            }
            for (int hundredths = 1; hundredths < 300; hundredths++)
            {
                if (hundredths % 100 != 0)
                {
                    list.Add(hundredths / 100m);
                }
            }
            candidates = list.ToArray();
            Interlocked.CompareExchange(ref _candidates, candidates, null);
            return candidates;
        }
    }
}
