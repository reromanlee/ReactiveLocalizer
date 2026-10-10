using System;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>Calls a character binding's typed callback through untyped storage, like <see cref="BindingInvoker"/> does for text.</summary>
    internal delegate void CharacterInvoker(object target, Delegate apply, ReadOnlyMemory<char> characters);
}
