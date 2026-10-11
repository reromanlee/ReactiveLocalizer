using reromanlee.ReactiveLocalizer.Formatting;
using reromanlee.ReactiveLocalizer.Messages;
using System;
using System.Globalization;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Authoring
{
    /// <summary>
    /// Writes parsed messages back as ICU text that parses into the same parts, optionally rewriting their literal
    /// text on the way, as pseudo-localization does.
    /// </summary>
    /// <remarks>
    /// Literal text is quoted only where ICU needs it, and the arguments, types, styles and keywords are written as the
    /// message wrote them. Spacing between forms is normalized to one space.
    /// </remarks>
    internal static class MessageWriter
    {
        /// <summary>Returns the text of <paramref name="message"/>, which must be free of errors.</summary>
        public static string Write(ParsedMessage message)
        {
            StringBuilder output = new();
            Write(message.Body, output, null);
            return output.ToString();
        }

        /// <summary>Appends the text of a message's <paramref name="body"/> to <paramref name="output"/>.</summary>
        /// <param name="body">The whole message's body.</param>
        /// <param name="output">Where the text goes.</param>
        /// <param name="rewrite">Rewrites each literal text before it is quoted, given the body it belongs to; null keeps every text.</param>
        public static void Write(MessageBody body, StringBuilder output, Func<string, MessageBody, string> rewrite)
        {
            WriteBody(body, output, rewrite, false, false);
        }

        /// <param name="isPluralForm">Whether the body is a plural form, where <c>#</c> shows the number.</param>
        /// <param name="isClosed">Whether a brace closes the body, which a final apostrophe would otherwise join.</param>
        private static void WriteBody(MessageBody body, StringBuilder output, Func<string, MessageBody, string> rewrite, bool isPluralForm, bool isClosed)
        {
            for (int i = 0; i < body.Parts.Count; i++)
            {
                switch (body.Parts[i])
                {
                    case TextPart text:
                        string shown = rewrite != null ? rewrite(text.Text, body) : text.Text;
                        bool isFollowedBySyntax = i + 1 < body.Parts.Count || isClosed;
                        output.Append(MessageQuoting.Quote(shown, isPluralForm, isFollowedBySyntax));
                        break;
                    case PoundPart:
                        output.Append('#');
                        break;
                    case PlaceholderPart placeholder:
                        WritePlaceholder(placeholder, output);
                        break;
                    case PluralPart plural:
                        WritePlural(plural, output, rewrite);
                        break;
                    case SelectPart select:
                        WriteSelect(select, output, rewrite);
                        break;
                }
            }
        }

        private static void WritePlaceholder(PlaceholderPart placeholder, StringBuilder output)
        {
            output.Append('{').Append(placeholder.Argument);
            switch (placeholder.Format)
            {
                case PlaceholderFormat.Number:
                    output.Append(", number");
                    if (placeholder.NumberStyle == NumberStyle.Integer)
                    {
                        output.Append(", integer");
                    }
                    else if (placeholder.NumberStyle == NumberStyle.Percent)
                    {
                        output.Append(", percent");
                    }
                    break;
                case PlaceholderFormat.Custom:
                    output.Append(", ").Append(placeholder.FormatterType);
                    if (placeholder.FormatterStyle.Length > 0)
                    {
                        output.Append(", ").Append(placeholder.FormatterStyle);
                    }
                    break;
            }
            output.Append('}');
        }

        private static void WritePlural(PluralPart plural, StringBuilder output, Func<string, MessageBody, string> rewrite)
        {
            output.Append('{').Append(plural.Argument).Append(plural.IsOrdinal ? ", selectordinal," : ", plural,");
            if (plural.Offset != 0d)
            {
                output.Append(" offset:").Append(FormatNumber(plural.Offset));
            }
            for (int i = 0; i < plural.Cases.Count; i++)
            {
                PluralCase form = plural.Cases[i];
                output.Append(' ').Append(form.IsExplicit ? "=" + FormatNumber(form.ExplicitValue) : form.Keyword).Append(" {");
                WriteBody(form.Body, output, rewrite, true, true);
                output.Append('}');
            }
            output.Append('}');
        }

        private static void WriteSelect(SelectPart select, StringBuilder output, Func<string, MessageBody, string> rewrite)
        {
            output.Append('{').Append(select.Argument).Append(", select,");
            for (int i = 0; i < select.Cases.Count; i++)
            {
                output.Append(' ').Append(select.Cases[i].Keyword).Append(" {");
                WriteBody(select.Cases[i].Body, output, rewrite, false, true);
                output.Append('}');
            }
            output.Append('}');
        }

        /// <summary>Writes an exact form's value or an offset the way the parser reads numbers: no exponent, no grouping.</summary>
        private static string FormatNumber(double value) => value.ToString("0.###############", CultureInfo.InvariantCulture);
    }
}
