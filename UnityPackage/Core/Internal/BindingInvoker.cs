using System;

namespace reromanlee.ReactiveLocalizer.Internal
{
    /// <summary>Calls a binding's typed callback through untyped storage, so the registry can hold every target type.</summary>
    internal delegate void BindingInvoker(object target, Delegate apply, string text);
}
