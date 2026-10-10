using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Orders names the way people count: <c>Line9</c> before <c>Line10</c>, ignoring case. The canonical writer sorts
    /// entries with it, so every tool and every person adding an entry ends up with the same file.
    /// </summary>
    public sealed class NaturalOrder : IComparer<string>
    {
        /// <summary>The one instance; the order has no settings.</summary>
        public static readonly NaturalOrder Instance = new();

        private NaturalOrder()
        {
        }

        /// <inheritdoc/>
        public int Compare(string left, string right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }
            if (left == null || right == null)
            {
                return left == null ? -1 : 1;
            }
            int leftIndex = 0;
            int rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                if (IsDigit(left[leftIndex]) && IsDigit(right[rightIndex]))
                {
                    int leftStart = leftIndex;
                    int rightStart = rightIndex;
                    while (leftIndex < left.Length && IsDigit(left[leftIndex]))
                    {
                        leftIndex++;
                    }
                    while (rightIndex < right.Length && IsDigit(right[rightIndex]))
                    {
                        rightIndex++;
                    }
                    int numbers = CompareNumbers(left.AsSpan(leftStart, leftIndex - leftStart), right.AsSpan(rightStart, rightIndex - rightStart));
                    if (numbers != 0)
                    {
                        return numbers;
                    }
                    continue;
                }
                int characters = ToLower(left[leftIndex]).CompareTo(ToLower(right[rightIndex]));
                if (characters != 0)
                {
                    return characters;
                }
                leftIndex++;
                rightIndex++;
            }
            int remaining = (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
            // Names equal but for case or leading zeros still get one fixed order.
            return remaining != 0 ? remaining : string.CompareOrdinal(left, right);
        }

        /// <summary>Compares two runs of digits by value, however long they are, without parsing them.</summary>
        private static int CompareNumbers(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
        {
            left = left.TrimStart('0');
            right = right.TrimStart('0');
            if (left.Length != right.Length)
            {
                return left.Length.CompareTo(right.Length);
            }
            return left.SequenceCompareTo(right);
        }

        private static bool IsDigit(char character) => (uint)(character - '0') <= 9;

        private static char ToLower(char character) => (uint)(character - 'A') <= 'Z' - 'A' ? (char)(character | 0x20) : character;
    }
}
