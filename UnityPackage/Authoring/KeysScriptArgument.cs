namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>One argument of an entry's message, as generated code takes it: a named parameter of the type its use needs.</summary>
    public sealed class KeysScriptArgument
    {
        /// <summary>Creates the argument <paramref name="name"/> of <paramref name="kind"/>.</summary>
        public KeysScriptArgument(string name, MessageArgumentKind kind)
        {
            Name = name;
            Kind = kind;
        }

        /// <summary>Name of the argument, as the source text writes it.</summary>
        public string Name { get; }

        /// <summary>How the message uses it, which decides the parameter's type.</summary>
        public MessageArgumentKind Kind { get; }
    }
}
