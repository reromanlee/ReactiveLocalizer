using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Messages;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class MessageWriterTests
    {
        /// <summary>Describes a message's parts, quoting resolved, so two messages compare by what they show.</summary>
        private static string Describe(MessageBody body)
        {
            StringBuilder output = new();
            Describe(body, output);
            return output.ToString();
        }

        private static void Describe(MessageBody body, StringBuilder output)
        {
            foreach (MessagePart part in body.Parts)
            {
                switch (part)
                {
                    case TextPart text:
                        output.Append("text(").Append(text.Text).Append(')');
                        break;
                    case PoundPart:
                        output.Append("pound");
                        break;
                    case PlaceholderPart placeholder:
                        output.Append("arg(").Append(placeholder.Argument).Append(',').Append(placeholder.Format).Append(',')
                            .Append(placeholder.NumberStyle).Append(',').Append(placeholder.FormatterType).Append(',').Append(placeholder.FormatterStyle).Append(')');
                        break;
                    case PluralPart plural:
                        output.Append("plural(").Append(plural.Argument).Append(',').Append(plural.IsOrdinal).Append(',').Append(plural.Offset);
                        foreach (PluralCase form in plural.Cases)
                        {
                            output.Append(',').Append(form.IsExplicit ? "=" + form.ExplicitValue : form.Keyword).Append('[');
                            Describe(form.Body, output);
                            output.Append(']');
                        }
                        output.Append(')');
                        break;
                    case SelectPart select:
                        output.Append("select(").Append(select.Argument);
                        foreach (SelectCase branch in select.Cases)
                        {
                            output.Append(',').Append(branch.Keyword).Append('[');
                            Describe(branch.Body, output);
                            output.Append(']');
                        }
                        output.Append(')');
                        break;
                }
            }
        }

        [TestCase("Buy", "Buy")]
        [TestCase("Don't", "Don't")]
        [TestCase("Hello, {name}!", "Hello, {name}!")]
        [TestCase("{count, number}", "{count, number}")]
        [TestCase("{share, number, percent} done", "{share, number, percent} done")]
        [TestCase("Due {deadline, date, short}", "Due {deadline, date, short}")]
        [TestCase("{coins,plural,one{# coin}other{# coins}}", "{coins, plural, one {# coin} other {# coins}}")]
        [TestCase("{guests, plural, offset:1 =0 {Nobody} =1 {{host}} other {{host} and # others}}", "{guests, plural, offset:1 =0 {Nobody} =1 {{host}} other {{host} and # others}}")]
        [TestCase("{place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}", "{place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}")]
        [TestCase("{gender, select, female {She} other {They}} won", "{gender, select, female {She} other {They}} won")]
        [TestCase("Press '{'space'}'", "Press '{'space'}'")]
        [TestCase("{n, plural, other {'#'1 and #}}", "{n, plural, other {'#'1 and #}}")]
        [TestCase("{name}'s turn", "{name}'s turn")]
        [TestCase("Players'' {name}", "Players' {name}")]
        [TestCase("Players''{name}", "Players''{name}")]
        [TestCase("{n, plural, other {Players''}}", "{n, plural, other {Players''}}")]
        public void Write_WritesWhatParsesBackTheSame(string message, string expected)
        {
            ParsedMessage parsed = MessageParser.Parse(message);
            Assert.That(parsed.HasErrors, Is.False);

            string written = MessageWriter.Write(parsed);
            ParsedMessage reparsed = MessageParser.Parse(written);

            Assert.That(written, Is.EqualTo(expected));
            Assert.That(reparsed.HasErrors, Is.False);
            Assert.That(Describe(reparsed.Body), Is.EqualTo(Describe(parsed.Body)));
        }

        [Test]
        public void Write_KeepsEveryValidMessageAsItWas()
        {
            System.Random random = new(11);
            string[] pieces = { "a", " ", "'", "#", "{", "}", "{n}", "{n, plural, one {", "{g, select, x {", "} other {", "}}", "'{'", "''" };
            List<string> failures = new();
            int checkedCount = 0;
            for (int i = 0; i < 20000 && failures.Count < 10; i++)
            {
                StringBuilder message = new();
                int count = random.Next(1, 9);
                for (int p = 0; p < count; p++)
                {
                    message.Append(pieces[random.Next(pieces.Length)]);
                }
                ParsedMessage parsed = MessageParser.Parse(message.ToString());
                if (parsed.HasErrors)
                {
                    continue;
                }
                checkedCount++;
                string written = MessageWriter.Write(parsed);
                ParsedMessage reparsed = MessageParser.Parse(written);
                if (reparsed.HasErrors || Describe(reparsed.Body) != Describe(parsed.Body))
                {
                    failures.Add($"{message} -> {written}");
                }
            }

            Assert.That(failures, Is.Empty);
            Assert.That(checkedCount, Is.GreaterThan(500));
        }

        [Test]
        public void Write_RewritesOnlyLiteralText()
        {
            ParsedMessage parsed = MessageParser.Parse("Hi {name}, {n, plural, one {# apple} other {# apples}}");
            StringBuilder output = new();

            MessageWriter.Write(parsed.Body, output, (text, body) => text.ToUpperInvariant());

            Assert.That(output.ToString(), Is.EqualTo("HI {name}, {n, plural, one {# APPLE} other {# APPLES}}"));
        }
    }
}
