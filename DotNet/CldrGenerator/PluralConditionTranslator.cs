using System;
using System.Collections.Generic;
using System.Globalization;

namespace reromanlee.ReactiveLocalizer.CldrGenerator
{
    /// <summary>
    /// Turns a CLDR plural condition, such as <c>v = 0 and i % 10 = 2..4 and i % 100 != 12..14</c>, into a C#
    /// expression over the core's <c>PluralOperands</c> parameter <c>o</c>.
    /// </summary>
    /// <remarks>
    /// Follows the syntax of Unicode Technical Standard #35. A relation on <c>n</c> only holds while the number has no
    /// fraction, where <c>n</c> equals its integer digits, so it becomes a test of <c>o.T == 0</c> and <c>o.I</c>.
    /// </remarks>
    internal sealed class PluralConditionTranslator
    {
        private readonly List<string> _tokens;
        private readonly string _condition;
        private int _position;

        private PluralConditionTranslator(string condition)
        {
            _condition = condition;
            _tokens = Tokenize(condition);
        }

        /// <summary>Returns the C# expression of <paramref name="condition"/>, or throws when it isn't valid CLDR syntax.</summary>
        public static string Translate(string condition)
        {
            PluralConditionTranslator translator = new(condition);
            string expression = translator.ReadCondition();
            if (translator._position != translator._tokens.Count)
            {
                throw translator.Fail("unexpected text after the condition");
            }
            return expression;
        }

        private string ReadCondition()
        {
            List<string> alternatives = new() { ReadAndCondition() };
            while (Accept("or"))
            {
                alternatives.Add(ReadAndCondition());
            }
            if (alternatives.Count == 1)
            {
                return alternatives[0];
            }
            // && already binds tighter than ||; the parentheses only make each alternative easy to read.
            for (int i = 0; i < alternatives.Count; i++)
            {
                alternatives[i] = HasTopLevel(alternatives[i], "&&") ? $"({alternatives[i]})" : alternatives[i];
            }
            return string.Join(" || ", alternatives);
        }

        private string ReadAndCondition()
        {
            List<string> relations = new() { ReadRelation() };
            while (Accept("and"))
            {
                relations.Add(ReadRelation());
            }
            if (relations.Count == 1)
            {
                return relations[0];
            }
            for (int i = 0; i < relations.Count; i++)
            {
                relations[i] = GroupOr(relations[i]);
            }
            return string.Join(" && ", relations);
        }

        private string ReadRelation()
        {
            string operand = Next();
            string member = operand switch
            {
                "n" or "i" => "o.I",
                "v" => "o.V",
                "w" => "o.W",
                "f" => "o.F",
                "t" => "o.T",
                "e" or "c" => "o.E",
                _ => throw Fail($"'{operand}' is not a plural operand")
            };
            string value = member;
            if (Accept("%"))
            {
                value = $"{member} % {ReadNumber()}";
            }
            bool isNegated;
            if (Accept("="))
            {
                isNegated = false;
            }
            else if (Accept("!="))
            {
                isNegated = true;
            }
            else
            {
                throw Fail("a relation needs '=' or '!='");
            }

            List<string> tests = new();
            do
            {
                long low = ReadNumber();
                if (Accept(".."))
                {
                    long high = ReadNumber();
                    // Every operand is zero or more, so a range starting at zero only needs its upper bound.
                    tests.Add(low == 0 ? $"{value} <= {high}" : $"{value} >= {low} && {value} <= {high}");
                }
                else
                {
                    tests.Add($"{value} == {low}");
                }
            }
            while (Accept(","));

            string membership;
            if (tests.Count == 1)
            {
                membership = tests[0];
            }
            else
            {
                for (int i = 0; i < tests.Count; i++)
                {
                    tests[i] = HasTopLevel(tests[i], "&&") ? $"({tests[i]})" : tests[i];
                }
                membership = string.Join(" || ", tests);
            }
            if (operand == "n")
            {
                membership = $"o.T == 0 && {GroupOr(membership)}";
            }
            else if (isNegated && tests.Count == 1 && membership.Contains(" == "))
            {
                return membership.Replace(" == ", " != ");
            }
            return isNegated ? $"!({membership})" : membership;
        }

        private long ReadNumber()
        {
            string token = Next();
            if (!long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
            {
                throw Fail($"'{token}' is not a number");
            }
            return value;
        }

        private bool Accept(string token)
        {
            if (_position < _tokens.Count && _tokens[_position] == token)
            {
                _position++;
                return true;
            }
            return false;
        }

        private string Next()
        {
            if (_position >= _tokens.Count)
            {
                throw Fail("the condition ends early");
            }
            return _tokens[_position++];
        }

        private Exception Fail(string problem) => new FormatException($"The CLDR condition '{_condition}' can't be read: {problem}.");

        /// <summary>Wraps an expression in parentheses when it has a top-level ||, so it can be joined with &&.</summary>
        private static string GroupOr(string expression) => HasTopLevel(expression, "||") ? $"({expression})" : expression;

        /// <summary>Returns whether <paramref name="expression"/> has <paramref name="operation"/> outside every parenthesis.</summary>
        private static bool HasTopLevel(string expression, string operation)
        {
            int depth = 0;
            for (int i = 0; i < expression.Length; i++)
            {
                char character = expression[i];
                if (character == '(')
                {
                    depth++;
                }
                else if (character == ')')
                {
                    depth--;
                }
                else if (depth == 0 && string.CompareOrdinal(expression, i, operation, 0, operation.Length) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static List<string> Tokenize(string condition)
        {
            List<string> tokens = new();
            int position = 0;
            while (position < condition.Length)
            {
                char character = condition[position];
                if (char.IsWhiteSpace(character))
                {
                    position++;
                    continue;
                }
                int start = position;
                if (char.IsLetter(character))
                {
                    while (position < condition.Length && char.IsLetter(condition[position]))
                    {
                        position++;
                    }
                }
                else if (char.IsDigit(character))
                {
                    while (position < condition.Length && char.IsDigit(condition[position]))
                    {
                        position++;
                    }
                }
                else if (character == '!' && position + 1 < condition.Length && condition[position + 1] == '=')
                {
                    position += 2;
                }
                else if (character == '.' && position + 1 < condition.Length && condition[position + 1] == '.')
                {
                    position += 2;
                }
                else if (character == '=' || character == '%' || character == ',')
                {
                    position++;
                }
                else
                {
                    throw new FormatException($"The CLDR condition '{condition}' has an unexpected '{character}'.");
                }
                tokens.Add(condition.Substring(start, position - start));
            }
            return tokens;
        }
    }
}
