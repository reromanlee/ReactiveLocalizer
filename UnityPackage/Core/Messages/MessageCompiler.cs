using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>Turns a parsed message into the operations <see cref="MessageOperation"/> describes.</summary>
    internal static class MessageCompiler
    {
        /// <summary>
        /// Appends the program of <paramref name="message"/>, which must be free of errors, to <paramref name="program"/>,
        /// storing its texts in <paramref name="characters"/>. Returns where the program starts.
        /// </summary>
        public static int Compile(ParsedMessage message, List<int> program, CharacterPool characters)
        {
            int start = program.Count;
            IReadOnlyList<MessageArgumentInfo> arguments = message.Arguments;
            program.Add(arguments.Count);
            for (int i = 0; i < arguments.Count; i++)
            {
                AddUInt64(program, arguments[i].Hash);
                program.Add(characters.Add(arguments[i].Name));
                program.Add(arguments[i].Name.Length);
            }
            int bodyEnd = program.Count;
            program.Add(0);
            AddBody(message.Body, message, program, characters);
            program[bodyEnd] = program.Count;
            return start;
        }

        private static void AddBody(MessageBody body, ParsedMessage message, List<int> program, CharacterPool characters)
        {
            for (int i = 0; i < body.Parts.Count; i++)
            {
                switch (body.Parts[i])
                {
                    case TextPart text:
                        if (text.Text.Length > 0)
                        {
                            program.Add(MessageOperation.Text);
                            program.Add(characters.Add(text.Text));
                            program.Add(text.Text.Length);
                        }
                        break;
                    case PoundPart:
                        program.Add(MessageOperation.Pound);
                        break;
                    case PlaceholderPart placeholder:
                        AddPlaceholder(placeholder, message, program, characters);
                        break;
                    case PluralPart plural:
                        AddPlural(plural, message, program, characters);
                        break;
                    case SelectPart select:
                        AddSelect(select, message, program, characters);
                        break;
                }
            }
        }

        private static void AddPlaceholder(PlaceholderPart placeholder, ParsedMessage message, List<int> program, CharacterPool characters)
        {
            int argument = IndexOf(message, placeholder.Argument);
            switch (placeholder.Format)
            {
                case PlaceholderFormat.Number:
                    program.Add(MessageOperation.Number);
                    program.Add(argument);
                    program.Add((int)placeholder.NumberStyle);
                    break;
                case PlaceholderFormat.Custom:
                    program.Add(MessageOperation.Custom);
                    program.Add(argument);
                    AddUInt64(program, Hashing.ComputeNameHash(placeholder.FormatterType));
                    program.Add(characters.Add(placeholder.FormatterType));
                    program.Add(placeholder.FormatterType.Length);
                    program.Add(characters.Add(placeholder.FormatterStyle));
                    program.Add(placeholder.FormatterStyle.Length);
                    break;
                default:
                    program.Add(MessageOperation.Plain);
                    program.Add(argument);
                    break;
            }
        }

        private static void AddPlural(PluralPart plural, ParsedMessage message, List<int> program, CharacterPool characters)
        {
            program.Add(MessageOperation.Plural);
            program.Add(IndexOf(message, plural.Argument));
            program.Add(plural.IsOrdinal ? 1 : 0);
            AddDouble(program, plural.Offset);
            int end = program.Count;
            program.Add(0);
            int count = program.Count;
            program.Add(0);
            int forms = 0;
            for (int i = 0; i < plural.Cases.Count; i++)
            {
                PluralCase form = plural.Cases[i];
                // A keyword that names no category can never be chosen, so it isn't compiled.
                if (!form.IsExplicit && !form.IsCategory)
                {
                    continue;
                }
                program.Add(form.IsExplicit ? MessageOperation.Explicit : (int)form.Category);
                AddDouble(program, form.IsExplicit ? form.ExplicitValue : 0d);
                int bodyEnd = program.Count;
                program.Add(0);
                AddBody(form.Body, message, program, characters);
                program[bodyEnd] = program.Count;
                forms++;
            }
            program[count] = forms;
            program[end] = program.Count;
        }

        private static void AddSelect(SelectPart select, ParsedMessage message, List<int> program, CharacterPool characters)
        {
            program.Add(MessageOperation.Select);
            program.Add(IndexOf(message, select.Argument));
            int end = program.Count;
            program.Add(0);
            program.Add(select.Cases.Count);
            for (int i = 0; i < select.Cases.Count; i++)
            {
                SelectCase branch = select.Cases[i];
                program.Add(characters.Add(branch.Keyword));
                program.Add(branch.Keyword.Length);
                int bodyEnd = program.Count;
                program.Add(0);
                AddBody(branch.Body, message, program, characters);
                program[bodyEnd] = program.Count;
            }
            program[end] = program.Count;
        }

        private static int IndexOf(ParsedMessage message, string argument)
        {
            ulong hash = Hashing.ComputeNameHash(argument);
            for (int i = 0; i < message.Arguments.Count; i++)
            {
                if (message.Arguments[i].Hash == hash)
                {
                    return i;
                }
            }
            throw new InvalidOperationException($"The message has no argument '{argument}'.");
        }

        private static void AddUInt64(List<int> program, ulong value)
        {
            program.Add(unchecked((int)value));
            program.Add(unchecked((int)(value >> 32)));
        }

        private static void AddDouble(List<int> program, double value) => AddUInt64(program, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    }
}
