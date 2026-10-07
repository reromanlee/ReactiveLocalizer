using reromanlee.ReactiveLocalizer.Formatting;
using System;
using System.Globalization;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// Runs a compiled message with the arguments of an <see cref="EntryMessage"/>, appending the text to a
    /// <see cref="TextBuilder"/>. It allocates nothing of its own; only registered formatters and the fallback text of
    /// custom objects can.
    /// </summary>
    /// <remarks>
    /// Arguments are found by the hashes of their names, so a translation may use them in any order. Nothing throws
    /// over a mistake in the arguments: a missing one shows as <c>{name}</c>, a value of the wrong kind is shown as is
    /// or chooses <c>other</c>, and every problem is recorded in <see cref="MessageProblems"/> for the localizer to report.
    /// </remarks>
    internal static class MessageRenderer
    {
        private const string FallbackDateFormat = "yyyy-MM-dd HH:mm:ss";
        private const int FirstFormatterCapacity = 64;
        private const int FormatterAttempts = 5;

        /// <summary>Renders the message at <paramref name="start"/> of <paramref name="program"/>.</summary>
        /// <param name="program">The table's compiled messages.</param>
        /// <param name="characters">The table's characters, which the messages' texts point into.</param>
        /// <param name="start">Where the message starts in <paramref name="program"/>.</param>
        /// <param name="message">The arguments.</param>
        /// <param name="format">The plural rules and number symbols of the text's language.</param>
        /// <param name="formatters">The formatters registered for custom types.</param>
        /// <param name="language">The text's language, which formatters receive.</param>
        /// <param name="output">Where the text is appended.</param>
        /// <param name="problems">Where problems with the arguments are recorded.</param>
        public static void Render(int[] program, char[] characters, int start, in EntryMessage message, LanguageFormat format,
            FormatterTable formatters, LanguageInfo language, ref TextBuilder output, ref MessageProblems problems)
        {
            State state = new(program, characters, start, message, format, formatters, language);
            PoundValue noPound = default;
            RenderBody(ref state, ref output, MessageProgram.GetBodyStart(program, start), MessageProgram.GetBodyEnd(program, start), in noPound);
            problems.MissingArguments |= state.Problems.MissingArguments;
            problems.MistypedArguments |= state.Problems.MistypedArguments;
            if (problems.UnformattedArguments == 0 && state.Problems.UnformattedArguments != 0)
            {
                problems.UnformattedTypeStart = state.Problems.UnformattedTypeStart;
                problems.UnformattedTypeLength = state.Problems.UnformattedTypeLength;
            }
            problems.UnformattedArguments |= state.Problems.UnformattedArguments;
            problems.FailedArguments |= state.Problems.FailedArguments;
            problems.FormatterException ??= state.Problems.FormatterException;
        }

        /// <summary>Returns the name of argument <paramref name="argument"/> of the message at <paramref name="start"/>.</summary>
        public static ReadOnlySpan<char> GetArgumentName(int[] program, char[] characters, int start, int argument)
        {
            int entry = MessageProgram.GetArgumentEntry(start, argument);
            return characters.AsSpan(program[entry + 2], program[entry + 3]);
        }

        private static void RenderBody(ref State state, ref TextBuilder output, int position, int end, in PoundValue pound)
        {
            int[] program = state.Program;
            while (position < end)
            {
                switch (program[position])
                {
                    case MessageOperation.Text:
                        output.Append(state.Characters.AsSpan(program[position + 1], program[position + 2]));
                        position += 3;
                        break;
                    case MessageOperation.Plain:
                        if (TryGetArgument(ref state, program[position + 1], out MessageValue plain))
                        {
                            AppendValue(ref state, ref output, in plain);
                        }
                        else
                        {
                            AppendMarker(ref state, ref output, program[position + 1]);
                        }
                        position += 2;
                        break;
                    case MessageOperation.Number:
                        RenderNumber(ref state, ref output, program[position + 1], (NumberStyle)program[position + 2]);
                        position += 3;
                        break;
                    case MessageOperation.Custom:
                        RenderCustom(ref state, ref output, position);
                        position += 8;
                        break;
                    case MessageOperation.Pound:
                        if (pound.IsActive)
                        {
                            AppendValue(ref state, ref output, pound.Value);
                        }
                        else
                        {
                            output.Append('#');
                        }
                        position++;
                        break;
                    case MessageOperation.Plural:
                        position = RenderPlural(ref state, ref output, position);
                        break;
                    case MessageOperation.Select:
                        position = RenderSelect(ref state, ref output, position, in pound);
                        break;
                    default:
                        // Verified programs never get here; stopping is the safe answer if one ever did.
                        return;
                }
            }
        }

        private static void RenderNumber(ref State state, ref TextBuilder output, int argument, NumberStyle style)
        {
            if (!TryGetArgument(ref state, argument, out MessageValue value))
            {
                AppendMarker(ref state, ref output, argument);
                return;
            }
            if (value.TryGetNumber(out MessageNumber number))
            {
                NumberFormatter.Append(ref output, in number, style, state.Format.Numbers);
                return;
            }
            state.Problems.MistypedArguments |= 1u << argument;
            AppendValue(ref state, ref output, in value);
        }

        private static void RenderCustom(ref State state, ref TextBuilder output, int position)
        {
            int[] program = state.Program;
            int argument = program[position + 1];
            if (!TryGetArgument(ref state, argument, out MessageValue value))
            {
                AppendMarker(ref state, ref output, argument);
                return;
            }
            if (!state.Formatters.TryGet(MessageProgram.ReadUInt64(program, position + 2), out ArgumentFormatter formatter))
            {
                if (state.Problems.UnformattedArguments == 0)
                {
                    state.Problems.UnformattedTypeStart = program[position + 4];
                    state.Problems.UnformattedTypeLength = program[position + 5];
                }
                state.Problems.UnformattedArguments |= 1u << argument;
                AppendValue(ref state, ref output, in value);
                return;
            }
            ReadOnlySpan<char> style = state.Characters.AsSpan(program[position + 6], program[position + 7]);
            int capacity = FirstFormatterCapacity;
            for (int attempt = 0; attempt < FormatterAttempts; attempt++)
            {
                Span<char> destination = output.GetFreeSpace(capacity);
                bool isDone;
                int written;
                try
                {
                    isDone = formatter(in value, style, state.Language, destination, out written);
                }
                catch (Exception exception)
                {
                    state.Problems.FailedArguments |= 1u << argument;
                    state.Problems.FormatterException ??= exception;
                    AppendMarker(ref state, ref output, argument);
                    return;
                }
                if (isDone)
                {
                    if (written >= 0 && written <= destination.Length)
                    {
                        output.Advance(written);
                        return;
                    }
                    break;
                }
                capacity = Math.Max(capacity, destination.Length) * 4;
            }
            state.Problems.FailedArguments |= 1u << argument;
            AppendMarker(ref state, ref output, argument);
        }

        private static int RenderPlural(ref State state, ref TextBuilder output, int position)
        {
            int[] program = state.Program;
            int argument = program[position + 1];
            bool isOrdinal = program[position + 2] != 0;
            double offset = MessageProgram.ReadDouble(program, position + 3);
            int end = program[position + 5];
            int count = program[position + 6];
            int forms = position + 7;
            if (!TryGetArgument(ref state, argument, out MessageValue value))
            {
                AppendMarker(ref state, ref output, argument);
                return end;
            }

            int chosen = -1;
            PoundValue pound;
            if (value.TryGetNumber(out MessageNumber number))
            {
                // An exact form matches the number itself, before the offset is subtracted, as ICU does.
                for (int i = 0, form = forms; i < count; i++, form = program[form + 3])
                {
                    if (program[form] == MessageOperation.Explicit && NumberFormatter.IsEqual(in number, MessageProgram.ReadDouble(program, form + 1)))
                    {
                        chosen = form;
                        break;
                    }
                }
                MessageNumber adjusted = NumberFormatter.Subtract(in number, offset);
                if (chosen < 0)
                {
                    PluralCategory category = PluralCategory.Other;
                    if (NumberFormatter.TryGetOperands(in adjusted, state.Format.Numbers, out PluralOperands operands))
                    {
                        category = isOrdinal
                            ? PluralRules.SelectOrdinal(state.Format.OrdinalRules, in operands)
                            : PluralRules.SelectCardinal(state.Format.CardinalRules, in operands);
                    }
                    chosen = FindForm(program, forms, count, category);
                }
                pound = new PoundValue(adjusted);
            }
            else
            {
                state.Problems.MistypedArguments |= 1u << argument;
                chosen = FindForm(program, forms, count, PluralCategory.Other);
                pound = new PoundValue(value);
            }
            if (chosen >= 0)
            {
                RenderBody(ref state, ref output, chosen + 4, program[chosen + 3], in pound);
            }
            return end;
        }

        private static int RenderSelect(ref State state, ref TextBuilder output, int position, in PoundValue pound)
        {
            int[] program = state.Program;
            int argument = program[position + 1];
            int end = program[position + 2];
            int count = program[position + 3];
            if (!TryGetArgument(ref state, argument, out MessageValue value))
            {
                AppendMarker(ref state, ref output, argument);
                return end;
            }
            bool hasText = value.TryGetText(out string text);
            if (!hasText)
            {
                state.Problems.MistypedArguments |= 1u << argument;
            }
            int chosen = -1;
            int other = -1;
            for (int i = 0, branch = position + 4; i < count; i++, branch = program[branch + 2])
            {
                ReadOnlySpan<char> keyword = state.Characters.AsSpan(program[branch], program[branch + 1]);
                if (hasText && chosen < 0 && keyword.SequenceEqual(text.AsSpan()))
                {
                    chosen = branch;
                }
                if (other < 0 && keyword.SequenceEqual("other".AsSpan()))
                {
                    other = branch;
                }
            }
            if (chosen < 0)
            {
                chosen = other;
            }
            if (chosen >= 0)
            {
                RenderBody(ref state, ref output, chosen + 3, program[chosen + 2], in pound);
            }
            return end;
        }

        /// <summary>Returns the form of <paramref name="category"/>, falling back to <c>other</c>; -1 when there is neither.</summary>
        private static int FindForm(int[] program, int forms, int count, PluralCategory category)
        {
            int other = -1;
            for (int i = 0, form = forms; i < count; i++, form = program[form + 3])
            {
                if (program[form] == (int)category)
                {
                    return form;
                }
                if (program[form] == (int)PluralCategory.Other && other < 0)
                {
                    other = form;
                }
            }
            return other;
        }

        private static bool TryGetArgument(ref State state, int argument, out MessageValue value)
        {
            int entry = MessageProgram.GetArgumentEntry(state.Start, argument);
            int index = state.Message.IndexOf(MessageProgram.ReadUInt64(state.Program, entry));
            if (index < 0)
            {
                state.Problems.MissingArguments |= 1u << argument;
                value = default;
                return false;
            }
            value = state.Message.GetArgument(index).Value;
            return true;
        }

        /// <summary>Appends a value the way a plain placeholder shows it.</summary>
        private static void AppendValue(ref State state, ref TextBuilder output, in MessageValue value)
        {
            switch (value.Kind)
            {
                case MessageValueKind.Text:
                    value.TryGetText(out string text);
                    output.Append(text);
                    break;
                case MessageValueKind.Number:
                    value.TryGetNumber(out MessageNumber number);
                    NumberFormatter.Append(ref output, in number, NumberStyle.Default, state.Format.Numbers);
                    break;
                case MessageValueKind.DateTime:
                    // Dates belong to a registered formatter; without one they show unambiguously rather than not at all.
                    value.TryGetDateTime(out DateTime dateTime);
                    Span<char> dateSpace = output.GetFreeSpace(32);
                    if (dateTime.TryFormat(dateSpace, out int dateLength, FallbackDateFormat, CultureInfo.InvariantCulture))
                    {
                        output.Advance(dateLength);
                    }
                    break;
                case MessageValueKind.TimeSpan:
                    value.TryGetTimeSpan(out TimeSpan timeSpan);
                    Span<char> spanSpace = output.GetFreeSpace(32);
                    if (timeSpan.TryFormat(spanSpace, out int spanLength, "c", CultureInfo.InvariantCulture))
                    {
                        output.Advance(spanLength);
                    }
                    break;
                case MessageValueKind.Object:
                    value.TryGetObject(out object custom);
                    output.Append(custom?.ToString());
                    break;
            }
        }

        private static void AppendMarker(ref State state, ref TextBuilder output, int argument)
        {
            output.Append('{');
            output.Append(GetArgumentName(state.Program, state.Characters, state.Start, argument));
            output.Append('}');
        }

        private struct State
        {
            public readonly int[] Program;
            public readonly char[] Characters;
            public readonly int Start;
            public readonly EntryMessage Message;
            public readonly LanguageFormat Format;
            public readonly FormatterTable Formatters;
            public readonly LanguageInfo Language;
            public MessageProblems Problems;

            public State(int[] program, char[] characters, int start, in EntryMessage message, LanguageFormat format,
                FormatterTable formatters, LanguageInfo language)
            {
                Program = program;
                Characters = characters;
                Start = start;
                Message = message;
                Format = format;
                Formatters = formatters;
                Language = language;
                Problems = default;
            }
        }

        /// <summary>What a plural form's <c>#</c> shows: the plural's number minus its offset.</summary>
        private readonly struct PoundValue
        {
            public PoundValue(MessageValue value)
            {
                IsActive = true;
                Value = value;
            }

            public bool IsActive { get; }

            public MessageValue Value { get; }
        }
    }
}
