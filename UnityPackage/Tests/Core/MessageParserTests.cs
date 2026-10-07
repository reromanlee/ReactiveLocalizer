using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Messages;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class MessageParserTests
    {
        [TestCase("Buy", "Buy")]
        [TestCase("", "")]
        [TestCase("Don't", "Don't")]
        [TestCase("It''s", "It's")]
        [TestCase("Use '{'braces'}'", "Use {braces}")]
        [TestCase("Write '{name}' literally", "Write {name} literally")]
        [TestCase("'{''}'", "{'}")]
        [TestCase("It's '{' and '", "It's { and '")]
        [TestCase("# is plain here", "# is plain here")]
        public void Parse_TextWithoutArguments_ResolvesQuoting(string text, string expected)
        {
            ParsedMessage message = MessageParser.Parse(text);

            Assert.That(message.HasErrors, Is.False);
            Assert.That(message.HasArguments, Is.False);
            Assert.That(message.LiteralText, Is.EqualTo(expected));
        }

        [Test]
        public void Parse_PlainArgument_IsAValue()
        {
            ParsedMessage message = MessageParser.Parse("Hello, {name}!");

            Assert.That(message.Issues, Is.Empty);
            Assert.That(message.LiteralText, Is.Null);
            Assert.That(message.Arguments.Count, Is.EqualTo(1));
            Assert.That(message.Arguments[0].Name, Is.EqualTo("name"));
            Assert.That(message.Arguments[0].Kind, Is.EqualTo(MessageArgumentKind.Value));
            Assert.That(message.Body.Parts.Count, Is.EqualTo(3));
            Assert.That(((TextPart)message.Body.Parts[0]).Text, Is.EqualTo("Hello, "));
            Assert.That(((PlaceholderPart)message.Body.Parts[1]).Argument, Is.EqualTo("name"));
        }

        [TestCase("{n, number}", "Default")]
        [TestCase("{n, number, integer}", "Integer")]
        [TestCase("{ n , NUMBER , Percent }", "Percent")]
        public void Parse_Number_ReadsItsStyle(string text, string style)
        {
            ParsedMessage message = MessageParser.Parse(text);
            PlaceholderPart placeholder = (PlaceholderPart)message.Body.Parts[0];

            Assert.That(message.Issues, Is.Empty);
            Assert.That(placeholder.Format, Is.EqualTo(PlaceholderFormat.Number));
            Assert.That(placeholder.NumberStyle.ToString(), Is.EqualTo(style));
            Assert.That(message.Arguments[0].Kind, Is.EqualTo(MessageArgumentKind.Number));
        }

        [Test]
        public void Parse_Plural_ReadsOffsetExplicitFormsAndNumberSigns()
        {
            ParsedMessage message = MessageParser.Parse("{count, plural, offset:1 =0 {Nobody} one {You and # other} other {You and # others}}");
            PluralPart plural = (PluralPart)message.Body.Parts[0];

            Assert.That(message.Issues, Is.Empty);
            Assert.That(plural.Argument, Is.EqualTo("count"));
            Assert.That(plural.IsOrdinal, Is.False);
            Assert.That(plural.Offset, Is.EqualTo(1d));
            Assert.That(plural.Cases.Count, Is.EqualTo(3));
            Assert.That(plural.Cases[0].IsExplicit, Is.True);
            Assert.That(plural.Cases[0].ExplicitValue, Is.EqualTo(0d));
            Assert.That(plural.Cases[1].Category, Is.EqualTo(Formatting.PluralCategory.One));
            Assert.That(plural.Cases[1].Body.Parts[1], Is.InstanceOf<PoundPart>());
            Assert.That(message.Arguments[0].Kind, Is.EqualTo(MessageArgumentKind.Number));
        }

        [Test]
        public void Parse_SelectOrdinal_IsAnOrdinalPlural()
        {
            ParsedMessage message = MessageParser.Parse("{place, selectordinal, one {#st} two {#nd} few {#rd} other {#th}}");
            PluralPart ordinal = (PluralPart)message.Body.Parts[0];

            Assert.That(message.Issues, Is.Empty);
            Assert.That(ordinal.IsOrdinal, Is.True);
            Assert.That(ordinal.Cases.Count, Is.EqualTo(4));
        }

        [Test]
        public void Parse_NestedSelectAndPlural_ReadsEveryArgument()
        {
            ParsedMessage message = MessageParser.Parse(
                "{gender, select, female {{count, plural, one {She has # coin} other {She has # coins}}} other {{name} has {count} coins}}");

            Assert.That(message.Issues, Is.Empty);
            Assert.That(message.Arguments.Count, Is.EqualTo(3));
            Assert.That(message.Arguments[0].Name, Is.EqualTo("count"));
            Assert.That(message.Arguments[0].Kind, Is.EqualTo(MessageArgumentKind.Number));
            Assert.That(message.Arguments[1].Name, Is.EqualTo("gender"));
            Assert.That(message.Arguments[1].Kind, Is.EqualTo(MessageArgumentKind.Keyword));
            Assert.That(message.Arguments[2].Name, Is.EqualTo("name"));
        }

        [Test]
        public void Parse_CustomType_KeepsItsStyleForTheFormatter()
        {
            ParsedMessage message = MessageParser.Parse("Due {deadline, date, 'on' dd MMM}");
            PlaceholderPart placeholder = (PlaceholderPart)message.Body.Parts[1];

            Assert.That(message.Issues, Is.Empty);
            Assert.That(placeholder.Format, Is.EqualTo(PlaceholderFormat.Custom));
            Assert.That(placeholder.FormatterType, Is.EqualTo("date"));
            Assert.That(placeholder.FormatterStyle, Is.EqualTo("'on' dd MMM"));
            Assert.That(message.Arguments[0].Kind, Is.EqualTo(MessageArgumentKind.Value));
        }

        [Test]
        public void Parse_SortsArgumentsByNameIgnoringCase()
        {
            ParsedMessage message = MessageParser.Parse("{beta} {Alpha} {gamma} {ALPHA}");

            Assert.That(message.Arguments.Count, Is.EqualTo(3));
            Assert.That(message.Arguments[0].Name, Is.EqualTo("Alpha"));
            Assert.That(message.Arguments[1].Name, Is.EqualTo("beta"));
            Assert.That(message.Arguments[2].Name, Is.EqualTo("gamma"));
        }

        [TestCase("Items: {0}", 8)]
        [TestCase("{count, plural, one {# item}}", 0)]
        [TestCase("{count, plural, one {a} one {b} other {c}}", 24)]
        [TestCase("{gender, select, male {He}}", 0)]
        [TestCase("{x, choice, 0#none|1#one}", 4)]
        [TestCase("Hello, {name", 7)]
        [TestCase("{n, number, #,##0.00}", 12)]
        [TestCase("{count} {count, select, a {x} other {y}} {count, number}", 42)]
        [TestCase("{2fast}", 1)]
        [TestCase("{count, plural, one # item other {#}}", 16)]
        [TestCase("{count, plural, other {#} offset:1}", 26)]
        public void Parse_BrokenMessage_ReportsAnErrorWhereItIs(string text, int index)
        {
            ParsedMessage message = MessageParser.Parse(text);

            Assert.That(message.HasErrors, Is.True);
            Assert.That(FirstError(message).Index, Is.EqualTo(index), FirstError(message).Message);
            Assert.That(message.LiteralText, Is.Null);
        }

        [TestCase("Close } nothing", 6)]
        [TestCase("'{never closed", 0)]
        [TestCase("{count, plural, other {<color=#FF0000>#</color>}}", 30)]
        [TestCase("{count, plural, other {{g, select, other {# items}}}}", 42)]
        [TestCase("{count, plural, ones {#} other {#}}", 16)]
        public void Parse_LikelyMistake_WarnsButKeepsTheMessage(string text, int index)
        {
            ParsedMessage message = MessageParser.Parse(text);

            Assert.That(message.HasErrors, Is.False);
            Assert.That(message.Issues.Count, Is.EqualTo(1));
            Assert.That(message.Issues[0].Severity, Is.EqualTo(IssueSeverity.Warning));
            Assert.That(message.Issues[0].Index, Is.EqualTo(index), message.Issues[0].Message);
        }

        [Test]
        public void Parse_MessageNestedTooDeeply_IsAnError()
        {
            string text = "{x, select, other {x}}";
            for (int i = 0; i < MessageParser.MaximumDepth; i++)
            {
                text = "{x, select, other {" + text + "}}";
            }

            Assert.That(MessageParser.Parse(text).HasErrors, Is.True);
        }

        [Test]
        public void Parse_RandomInput_NeverThrows()
        {
            System.Random random = new(20261007);
            const string alphabet = "{}'#,= abcxyz0123456789pluralselectordinaloffset:";
            List<string> failures = new();
            for (int i = 0; i < 2000; i++)
            {
                char[] characters = new char[random.Next(0, 40)];
                for (int c = 0; c < characters.Length; c++)
                {
                    characters[c] = alphabet[random.Next(alphabet.Length)];
                }
                string text = new(characters);
                try
                {
                    ParsedMessage message = MessageParser.Parse(text);
                    foreach (MessageIssue issue in message.Issues)
                    {
                        if (issue.Index < 0 || issue.Index > text.Length)
                        {
                            failures.Add($"{text}: issue at {issue.Index}");
                        }
                    }
                }
                catch (System.Exception exception)
                {
                    failures.Add($"{text}: {exception.GetType().Name}");
                }
            }

            Assert.That(failures, Is.Empty);
        }

        [TestCase("Don't", "Don't")]
        [TestCase("Press {space}", "Press '{'space'}'")]
        [TestCase("{'}", "'{''}'")]
        [TestCase("a'{b", "a'''{'b")]
        public void Quote_WritesTextThatShowsAsIs(string text, string expected)
        {
            Assert.That(MessageQuoting.Quote(text), Is.EqualTo(expected));
        }

        [Test]
        public void Quote_RoundTripsAnyText()
        {
            System.Random random = new(7);
            const string alphabet = "{}'# ab";
            List<string> failures = new();
            for (int i = 0; i < 3000; i++)
            {
                char[] characters = new char[random.Next(0, 12)];
                for (int c = 0; c < characters.Length; c++)
                {
                    characters[c] = alphabet[random.Next(alphabet.Length)];
                }
                string text = new(characters);
                ParsedMessage message = MessageParser.Parse(MessageQuoting.Quote(text));
                if (message.LiteralText != text || message.Issues.Count > 0)
                {
                    failures.Add($"{text} -> {MessageQuoting.Quote(text)} -> {message.LiteralText}");
                }
            }

            Assert.That(failures, Is.Empty);
        }

        private static MessageIssue FirstError(ParsedMessage message)
        {
            foreach (MessageIssue issue in message.Issues)
            {
                if (issue.Severity == IssueSeverity.Error)
                {
                    return issue;
                }
            }
            return default;
        }
    }
}
