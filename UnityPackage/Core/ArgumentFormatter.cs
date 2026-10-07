using System;

namespace reromanlee.ReactiveLocalizer
{
    /// <summary>
    /// Formats arguments of a type the core leaves to the project, such as dates in <c>{deadline, date}</c> or prices
    /// in <c>{price, currency, EUR}</c>. Register one per type name with <see cref="Localizer.SetFormatter"/>.
    /// </summary>
    /// <param name="value">The argument's value.</param>
    /// <param name="style">The text after the type, such as <c>short</c> in <c>{deadline, date, short}</c>; empty when there is none.</param>
    /// <param name="language">The language of the text being formatted, whose culture names the conventions to follow.</param>
    /// <param name="destination">Where to write the formatted text.</param>
    /// <param name="written">How many characters were written to <paramref name="destination"/>.</param>
    /// <returns>False when <paramref name="destination"/> is too small; the formatter is then called again with more room.</returns>
    /// <remarks>
    /// It runs on whichever thread formats the message. An exception it throws is reported once, and the argument
    /// shows as <c>{name}</c>.
    /// </remarks>
    public delegate bool ArgumentFormatter(in MessageValue value, ReadOnlySpan<char> style, LanguageInfo language, Span<char> destination, out int written);
}
