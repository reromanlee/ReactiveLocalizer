using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Messages;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class MessagePreviewTests
    {
        private static readonly CatalogInfo Catalog = CatalogInfo.FromDocument(new CatalogKey("Localization"),
            CatalogDocument.Parse("@source English\n[English]\nCulture = en\n[Russian]\nCulture = ru\n"), null, null);

        private const string RussianCoins = "{coins, plural, one {# \u043C\u043E\u043D\u0435\u0442\u0430} few {# \u043C\u043E\u043D\u0435\u0442\u044B} many {# \u043C\u043E\u043D\u0435\u0442} other {# \u043C\u043E\u043D\u0435\u0442\u044B}}";

        private static LanguageInfo Language(string name)
        {
            Assert.That(Catalog.TryGetLanguage(new LanguageKey(name), out LanguageInfo language), Is.True);
            return language;
        }

        [TestCase("1", "1 \u043C\u043E\u043D\u0435\u0442\u0430")]
        [TestCase("2", "2 \u043C\u043E\u043D\u0435\u0442\u044B")]
        [TestCase("5", "5 \u043C\u043E\u043D\u0435\u0442")]
        [TestCase("21", "21 \u043C\u043E\u043D\u0435\u0442\u0430")]
        [TestCase("1.5", "1,5 \u043C\u043E\u043D\u0435\u0442\u044B")]
        public void Render_ChoosesTheFormTheLanguageNeeds(string sample, string expected)
        {
            MessageArgument[] arguments = { MessagePreview.CreateArgument("coins", sample) };

            Assert.That(MessagePreview.Render(Catalog, Language("Russian"), RussianCoins, arguments, out string problem), Is.EqualTo(expected));
            Assert.That(problem, Is.Null);
        }

        [Test]
        public void Render_ShowsTextAsItIsAndMissingArgumentsByName()
        {
            Assert.That(MessagePreview.Render(Catalog, Language("English"), "Buy it", null, out _), Is.EqualTo("Buy it"));
            Assert.That(MessagePreview.Render(Catalog, Language("English"), "Hello, {name}!", new MessageArgument[0], out _), Is.EqualTo("Hello, {name}!"));
            Assert.That(MessagePreview.Render(Catalog, Language("English"), "Hello, {name}!", new[] { MessagePreview.CreateArgument("name", "Alex") }, out _), Is.EqualTo("Hello, Alex!"));
        }

        [Test]
        public void Render_OfBrokenText_SaysWhatIsWrong()
        {
            Assert.That(MessagePreview.Render(Catalog, Language("English"), "{coins, plural, one {# coin}", null, out string problem), Is.Null);
            Assert.That(problem, Is.Not.Empty);
        }

        [Test]
        public void GetArguments_ListsWhatTheTextTakesInOrder()
        {
            IReadOnlyList<(string Name, MessageArgumentKind Kind)> arguments =
                MessagePreview.GetArguments("{name} ({gender, select, other {they}}) has {coins, plural, one {# coin} other {# coins}}");

            Assert.That(arguments, Is.EqualTo(new[] { ("name", MessageArgumentKind.Value), ("gender", MessageArgumentKind.Keyword), ("coins", MessageArgumentKind.Number) }));
            Assert.That(MessagePreview.GetArguments("Plain text"), Is.Empty);
        }
    }
}
