using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// A named value for a message, such as <c>coins</c> in <c>You have {coins, plural, one {# coin} other {# coins}}</c>.
    /// Generated methods create them for their entry; code that names entries by data can create them too.
    /// </summary>
    /// <remarks>Names follow the naming rule and ignore case, like every other name.</remarks>
    public readonly struct MessageArgument : IEquatable<MessageArgument>
    {
        /// <summary>Creates the argument <paramref name="name"/> with <paramref name="value"/>.</summary>
        /// <exception cref="ArgumentException"><paramref name="name"/> breaks the naming rule.</exception>
        public MessageArgument(string name, MessageValue value)
        {
            NameRules.ThrowIfInvalid(name, nameof(name));
            Name = name;
            Hash = Hashing.ComputeNameHash(name);
            Value = value;
        }

        /// <summary>Name of the argument, as the message writes it between braces.</summary>
        public string Name { get; }

        /// <summary>The value the message shows or chooses its form by.</summary>
        public MessageValue Value { get; }

        /// <summary>Whether this is the default value, which names no argument.</summary>
        public bool IsEmpty => Name == null;

        /// <summary>The hash of <see cref="Name"/>, which compiled messages refer to their arguments by.</summary>
        internal ulong Hash { get; }

        /// <inheritdoc/>
        public bool Equals(MessageArgument other) => Hash == other.Hash && IsEmpty == other.IsEmpty && Value.Equals(other.Value);

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is MessageArgument other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine(Hash, Value);

        /// <summary>Returns <c>name: value</c>.</summary>
        public override string ToString() => IsEmpty ? string.Empty : $"{Name}: {Value}";

        public static bool operator ==(MessageArgument left, MessageArgument right) => left.Equals(right);

        public static bool operator !=(MessageArgument left, MessageArgument right) => !left.Equals(right);
    }
}
