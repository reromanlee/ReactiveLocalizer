using System;

namespace reromanlee.ReactiveLocalizer.Messages
{
    /// <summary>
    /// Reads and checks compiled messages. A table can come from a mod folder or an interrupted download, so every
    /// message is checked before rendering ever sees it: each operation is known, each operand lies inside the program
    /// or the character buffer, each argument exists, and nesting stays bounded.
    /// </summary>
    internal static class MessageProgram
    {
        /// <summary>Returns the argument count of the message at <paramref name="start"/>.</summary>
        public static int GetArgumentCount(int[] program, int start) => program[start];

        /// <summary>Returns where the argument table entry of argument <paramref name="argument"/> starts.</summary>
        public static int GetArgumentEntry(int start, int argument) => start + 1 + argument * MessageOperation.ArgumentSize;

        /// <summary>Returns where the body of the message at <paramref name="start"/> begins.</summary>
        public static int GetBodyStart(int[] program, int start) => start + 2 + program[start] * MessageOperation.ArgumentSize;

        /// <summary>Returns where the body of the message at <paramref name="start"/> ends.</summary>
        public static int GetBodyEnd(int[] program, int start) => program[GetBodyStart(program, start) - 1];

        public static ulong ReadUInt64(int[] program, int index) => unchecked((uint)program[index] | ((ulong)(uint)program[index + 1] << 32));

        public static double ReadDouble(int[] program, int index) => BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64(program, index)));

        /// <summary>Checks the message starting at <paramref name="start"/>, failing with a reason instead of throwing.</summary>
        public static bool TryVerify(int[] program, int start, int characterCount, out string error)
        {
            if ((uint)start >= (uint)program.Length)
            {
                error = "A message starts outside the table's messages.";
                return false;
            }
            int argumentCount = program[start];
            if (argumentCount < 0 || argumentCount > MessageParser.MaximumArguments ||
                (long)start + 2 + (long)argumentCount * MessageOperation.ArgumentSize > program.Length)
            {
                error = "A message's argument table is damaged.";
                return false;
            }
            for (int i = 0; i < argumentCount; i++)
            {
                int entry = GetArgumentEntry(start, i);
                if (!IsText(program[entry + 2], program[entry + 3], characterCount))
                {
                    error = "A message's argument name lies outside the table's characters.";
                    return false;
                }
            }
            int bodyStart = GetBodyStart(program, start);
            int bodyEnd = program[bodyStart - 1];
            if (bodyEnd < bodyStart || bodyEnd > program.Length)
            {
                error = "A message's body lies outside the table's messages.";
                return false;
            }
            Verifier verifier = new(program, argumentCount, characterCount);
            if (!verifier.TryVerifyBody(bodyStart, bodyEnd, 0))
            {
                error = "A message is damaged.";
                return false;
            }
            error = null;
            return true;
        }

        private static bool IsText(int start, int length, int characterCount) => start >= 0 && length >= 0 && (long)start + length <= characterCount;

        private readonly struct Verifier
        {
            // Bodies nest one level per plural or select, which the parser already limits.
            private const int MaximumDepth = MessageParser.MaximumDepth + 1;

            private readonly int[] _program;
            private readonly int _argumentCount;
            private readonly int _characterCount;

            public Verifier(int[] program, int argumentCount, int characterCount)
            {
                _program = program;
                _argumentCount = argumentCount;
                _characterCount = characterCount;
            }

            public bool TryVerifyBody(int position, int end, int depth)
            {
                if (depth > MaximumDepth)
                {
                    return false;
                }
                while (position < end)
                {
                    int operation = _program[position];
                    switch (operation)
                    {
                        case MessageOperation.Text:
                            if (!Fits(position, 3, end) || !IsText(_program[position + 1], _program[position + 2], _characterCount))
                            {
                                return false;
                            }
                            position += 3;
                            break;
                        case MessageOperation.Plain:
                            if (!Fits(position, 2, end) || !IsArgument(_program[position + 1]))
                            {
                                return false;
                            }
                            position += 2;
                            break;
                        case MessageOperation.Number:
                            if (!Fits(position, 3, end) || !IsArgument(_program[position + 1]) || (uint)_program[position + 2] > 2)
                            {
                                return false;
                            }
                            position += 3;
                            break;
                        case MessageOperation.Custom:
                            if (!Fits(position, 8, end) || !IsArgument(_program[position + 1]) ||
                                !IsText(_program[position + 4], _program[position + 5], _characterCount) ||
                                !IsText(_program[position + 6], _program[position + 7], _characterCount))
                            {
                                return false;
                            }
                            position += 8;
                            break;
                        case MessageOperation.Pound:
                            position++;
                            break;
                        case MessageOperation.Plural:
                            if (!TryVerifyPlural(ref position, end, depth))
                            {
                                return false;
                            }
                            break;
                        case MessageOperation.Select:
                            if (!TryVerifySelect(ref position, end, depth))
                            {
                                return false;
                            }
                            break;
                        default:
                            return false;
                    }
                }
                return position == end;
            }

            private bool TryVerifyPlural(ref int position, int end, int depth)
            {
                if (!Fits(position, 7, end) || !IsArgument(_program[position + 1]) || (uint)_program[position + 2] > 1)
                {
                    return false;
                }
                int nodeEnd = _program[position + 5];
                int count = _program[position + 6];
                if (nodeEnd > end || nodeEnd < position + 7 || count < 0)
                {
                    return false;
                }
                int form = position + 7;
                for (int i = 0; i < count; i++)
                {
                    if (!Fits(form, 4, nodeEnd) || (uint)_program[form] > MessageOperation.Explicit)
                    {
                        return false;
                    }
                    int bodyEnd = _program[form + 3];
                    if (bodyEnd < form + 4 || bodyEnd > nodeEnd || !TryVerifyBody(form + 4, bodyEnd, depth + 1))
                    {
                        return false;
                    }
                    form = bodyEnd;
                }
                position = nodeEnd;
                return form == nodeEnd;
            }

            private bool TryVerifySelect(ref int position, int end, int depth)
            {
                if (!Fits(position, 4, end) || !IsArgument(_program[position + 1]))
                {
                    return false;
                }
                int nodeEnd = _program[position + 2];
                int count = _program[position + 3];
                if (nodeEnd > end || nodeEnd < position + 4 || count < 0)
                {
                    return false;
                }
                int branch = position + 4;
                for (int i = 0; i < count; i++)
                {
                    if (!Fits(branch, 3, nodeEnd) || !IsText(_program[branch], _program[branch + 1], _characterCount))
                    {
                        return false;
                    }
                    int bodyEnd = _program[branch + 2];
                    if (bodyEnd < branch + 3 || bodyEnd > nodeEnd || !TryVerifyBody(branch + 3, bodyEnd, depth + 1))
                    {
                        return false;
                    }
                    branch = bodyEnd;
                }
                position = nodeEnd;
                return branch == nodeEnd;
            }

            private static bool Fits(int position, int size, int end) => (long)position + size <= end;

            private bool IsArgument(int argument) => (uint)argument < (uint)_argumentCount;
        }
    }
}
