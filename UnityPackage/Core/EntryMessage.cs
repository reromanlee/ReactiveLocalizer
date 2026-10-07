using System;
using System.Text;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// An entry together with the arguments its message is formatted with. Generated code hands one out per entry with
    /// arguments, such as <c>LocalizationKeys.Shop.CoinBalance(coins: 5)</c>, so the compiler checks every argument.
    /// </summary>
    /// <remarks>
    /// A struct, so creating one allocates nothing for up to four arguments; more are kept in an array. Two messages
    /// are equal when their entry and their arguments are, which is how a binding skips formatting the same text
    /// again. Argument names ignore case and are unique within a message.
    /// </remarks>
    public readonly struct EntryMessage : IEquatable<EntryMessage>
    {
        private const int InlineCapacity = 4;

        private readonly MessageArgument _first;
        private readonly MessageArgument _second;
        private readonly MessageArgument _third;
        private readonly MessageArgument _fourth;
        private readonly MessageArgument[] _more;

        /// <summary>Creates a message of <paramref name="key"/> without arguments; add them with <see cref="With"/>.</summary>
        public EntryMessage(in EntryKey key) : this(in key, 0, default, default, default, default, null)
        {
        }

        /// <summary>Creates a message of <paramref name="key"/> with one argument.</summary>
        /// <exception cref="ArgumentException">The argument is empty.</exception>
        public EntryMessage(in EntryKey key, in MessageArgument first) : this(in key, 1, in first, default, default, default, null)
        {
        }

        /// <summary>Creates a message of <paramref name="key"/> with two arguments.</summary>
        /// <exception cref="ArgumentException">An argument is empty, or two share a name.</exception>
        public EntryMessage(in EntryKey key, in MessageArgument first, in MessageArgument second)
            : this(in key, 2, in first, in second, default, default, null)
        {
        }

        /// <summary>Creates a message of <paramref name="key"/> with three arguments.</summary>
        /// <exception cref="ArgumentException">An argument is empty, or two share a name.</exception>
        public EntryMessage(in EntryKey key, in MessageArgument first, in MessageArgument second, in MessageArgument third)
            : this(in key, 3, in first, in second, in third, default, null)
        {
        }

        /// <summary>Creates a message of <paramref name="key"/> with four arguments.</summary>
        /// <exception cref="ArgumentException">An argument is empty, or two share a name.</exception>
        public EntryMessage(in EntryKey key, in MessageArgument first, in MessageArgument second, in MessageArgument third, in MessageArgument fourth)
            : this(in key, 4, in first, in second, in third, in fourth, null)
        {
        }

        /// <summary>Creates a message of <paramref name="key"/> with any number of arguments, copying them.</summary>
        /// <exception cref="ArgumentException">An argument is empty, or two share a name.</exception>
        public EntryMessage(in EntryKey key, params MessageArgument[] arguments)
            : this(in key, arguments?.Length ?? 0, Get(arguments, 0), Get(arguments, 1), Get(arguments, 2), Get(arguments, 3), CopyMore(arguments))
        {
        }

        private EntryMessage(in EntryKey key, int count, in MessageArgument first, in MessageArgument second,
            in MessageArgument third, in MessageArgument fourth, MessageArgument[] more)
        {
            Key = key;
            ArgumentCount = count;
            _first = first;
            _second = second;
            _third = third;
            _fourth = fourth;
            _more = more;
            for (int i = 0; i < count; i++)
            {
                MessageArgument argument = GetArgument(i);
                if (argument.IsEmpty)
                {
                    throw new ArgumentException($"Argument {i} of the message of '{key}' is empty.");
                }
                for (int j = 0; j < i; j++)
                {
                    if (GetArgument(j).Hash == argument.Hash)
                    {
                        throw new ArgumentException($"The message of '{key}' has the argument '{argument.Name}' twice.");
                    }
                }
            }
        }

        /// <summary>The entry whose message is formatted.</summary>
        public EntryKey Key { get; }

        /// <summary>How many arguments the message has.</summary>
        public int ArgumentCount { get; }

        /// <summary>Whether this is the default value, which names no entry.</summary>
        public bool IsEmpty => Key.IsEmpty;

        /// <summary>Returns the argument at <paramref name="index"/>, in the order they were given.</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not below <see cref="ArgumentCount"/>.</exception>
        public MessageArgument GetArgument(int index)
        {
            if ((uint)index >= (uint)ArgumentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return index switch
            {
                0 => _first,
                1 => _second,
                2 => _third,
                3 => _fourth,
                _ => _more[index - InlineCapacity]
            };
        }

        /// <summary>Returns the value of the argument named <paramref name="name"/>, ignoring case.</summary>
        public bool TryGetValue(string name, out MessageValue value)
        {
            int index = NameRules.IsValid(name) ? IndexOf(Hashing.ComputeNameHash(name)) : -1;
            value = index >= 0 ? GetArgument(index).Value : default;
            return index >= 0;
        }

        /// <summary>
        /// Returns this message with <paramref name="argument"/> added, or replacing the argument of the same name.
        /// Allocates nothing while the result has up to four arguments.
        /// </summary>
        /// <exception cref="ArgumentException"><paramref name="argument"/> is empty.</exception>
        public EntryMessage With(in MessageArgument argument)
        {
            if (argument.IsEmpty)
            {
                throw new ArgumentException("An empty argument can't be added to a message.", nameof(argument));
            }
            int index = IndexOf(argument.Hash);
            int count = index >= 0 ? ArgumentCount : ArgumentCount + 1;
            int target = index >= 0 ? index : ArgumentCount;
            MessageArgument[] more = null;
            if (count > InlineCapacity)
            {
                more = new MessageArgument[count - InlineCapacity];
                for (int i = InlineCapacity; i < count; i++)
                {
                    more[i - InlineCapacity] = i == target ? argument : GetArgument(i);
                }
            }
            return new EntryMessage(Key, count,
                target == 0 ? argument : _first,
                target == 1 ? argument : _second,
                target == 2 ? argument : _third,
                target == 3 ? argument : _fourth,
                more);
        }

        /// <summary>Returns the index of the argument whose name has <paramref name="nameHash"/>, or -1.</summary>
        internal int IndexOf(ulong nameHash)
        {
            for (int i = 0; i < ArgumentCount; i++)
            {
                if (GetArgument(i).Hash == nameHash)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <inheritdoc/>
        public bool Equals(EntryMessage other)
        {
            if (Key != other.Key || ArgumentCount != other.ArgumentCount)
            {
                return false;
            }
            for (int i = 0; i < ArgumentCount; i++)
            {
                if (!GetArgument(i).Equals(other.GetArgument(i)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is EntryMessage other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Key, ArgumentCount, _first, _second);

        /// <summary>Returns <c>Table.Entry(name: value, ...)</c>, for logs and the debugger.</summary>
        public override string ToString()
        {
            StringBuilder builder = new(Key.ToString());
            if (ArgumentCount > 0)
            {
                builder.Append('(');
                for (int i = 0; i < ArgumentCount; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }
                    builder.Append(GetArgument(i));
                }
                builder.Append(')');
            }
            return builder.ToString();
        }

        public static bool operator ==(EntryMessage left, EntryMessage right) => left.Equals(right);

        public static bool operator !=(EntryMessage left, EntryMessage right) => !left.Equals(right);

        private static MessageArgument Get(MessageArgument[] arguments, int index) =>
            arguments != null && index < arguments.Length ? arguments[index] : default;

        private static MessageArgument[] CopyMore(MessageArgument[] arguments)
        {
            if (arguments == null || arguments.Length <= InlineCapacity)
            {
                return null;
            }
            MessageArgument[] more = new MessageArgument[arguments.Length - InlineCapacity];
            Array.Copy(arguments, InlineCapacity, more, 0, more.Length);
            return more;
        }
    }
}
