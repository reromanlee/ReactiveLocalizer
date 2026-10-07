using System;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>
    /// The invoker for targets of type <typeparamref name="TTarget"/>, created once per type, so binding never
    /// allocates one.
    /// </summary>
    internal static class BindingInvokers<TTarget> where TTarget : class
    {
        public static readonly BindingInvoker Invoker = Invoke;

        private static void Invoke(object target, Delegate apply, string text) => ((Action<TTarget, string>)apply)((TTarget)target, text);
    }
}
