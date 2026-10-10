using System;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// The invokers for targets of type <typeparamref name="TTarget"/>, created once per type, so binding never
    /// allocates one.
    /// </summary>
    internal static class BindingInvokers<TTarget> where TTarget : class
    {
        public static readonly BindingInvoker Invoker = Invoke;

        public static readonly CharacterInvoker CharacterInvoker = InvokeWithCharacters;

        private static void Invoke(object target, Delegate apply, string text) => ((Action<TTarget, string>)apply)((TTarget)target, text);

        private static void InvokeWithCharacters(object target, Delegate apply, ReadOnlyMemory<char> characters) =>
            ((Action<TTarget, ReadOnlyMemory<char>>)apply)((TTarget)target, characters);
    }
}
