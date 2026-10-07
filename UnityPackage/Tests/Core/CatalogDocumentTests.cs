using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class CatalogDocumentTests
    {
        private const string Catalog =
            "# Languages of the game.\n" +
            "\n" +
            "@source English\n" +
            "@namespace MyGame\n" +
            "\n" +
            "[English]\n" +
            "DisplayName = English\n" +
            "Culture = en\n" +
            "\n" +
            "# Shipped on day one.\n" +
            "[Russian]\n" +
            "DisplayName = \u0420\u0443\u0441\u0441\u043A\u0438\u0439\n" +
            "Culture = ru\n" +
            "required = true\n" +
            "\n" +
            "[ Pirate ]\n" +
            "DisplayName = Pirate Speak\n" +
            "Fallback = English\n";

        [Test]
        public void Parse_ReadsAttributesSectionsAndFields()
        {
            CatalogDocument document = CatalogDocument.Parse(Catalog);

            Assert.That(document.Issues, Is.Empty);
            Assert.That(document.HeaderComments, Is.EqualTo(new[] { "Languages of the game." }));
            Assert.That(document.TryGetAttribute(DocumentNames.Source, out DocumentProperty source), Is.True);
            Assert.That(source.Value, Is.EqualTo("English"));
            Assert.That(document.Languages.Count, Is.EqualTo(3));
            Assert.That(document.Languages[1].Name, Is.EqualTo("Russian"));
            Assert.That(document.Languages[1].Comments, Is.EqualTo(new[] { "Shipped on day one." }));
            Assert.That(document.Languages[1].TryGetField(DocumentNames.Required, out DocumentProperty required), Is.True);
            Assert.That(required.Name, Is.EqualTo(DocumentNames.Required));
            Assert.That(required.Value, Is.EqualTo("true"));
            Assert.That(document.Languages[2].Name, Is.EqualTo("Pirate"));
            Assert.That(document.Languages[2].Fields.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parse_ResolvesEscapesInFieldValues()
        {
            CatalogDocument document = CatalogDocument.Parse("[English]\nDisplayName = \\u0020English\\u0020");

            Assert.That(document.Languages[0].Fields[0].Value, Is.EqualTo(" English "));
        }

        [Test]
        public void Parse_KeepsUnknownFieldsAndAttributesWithWarnings()
        {
            CatalogDocument document = CatalogDocument.Parse("@team Writers\n[English]\nFont = Serif");

            Assert.That(document.Attributes[0].Name, Is.EqualTo("team"));
            Assert.That(document.Languages[0].Fields[0].Name, Is.EqualTo("Font"));
            Assert.That(document.Issues.Count, Is.EqualTo(2));
            Assert.That(document.HasErrors, Is.False);
        }

        [Test]
        public void Parse_RejectsADuplicateSectionAndSkipsItsFields()
        {
            CatalogDocument document = CatalogDocument.Parse("[English]\nCulture = en\n[english]\nCulture = en-GB\nDisplayName = British");

            Assert.That(document.Languages.Count, Is.EqualTo(1));
            Assert.That(document.Languages[0].Fields.Count, Is.EqualTo(1));
            Assert.That(document.Issues.Count, Is.EqualTo(1));
            Assert.That(document.Issues[0].Line, Is.EqualTo(3));
        }

        [TestCase("[English", 1)]
        [TestCase("[English] extra", 1)]
        [TestCase("[Old English]", 2)]
        public void Parse_RejectsMalformedSectionHeaders(string header, int expectedColumn)
        {
            CatalogDocument document = CatalogDocument.Parse(header + "\nCulture = en");

            Assert.That(document.Languages, Is.Empty);
            Assert.That(document.Issues.Count, Is.EqualTo(1));
            Assert.That(document.Issues[0].Column, Is.EqualTo(expectedColumn));
        }

        [Test]
        public void Parse_RejectsFieldsOutsideASection()
        {
            CatalogDocument document = CatalogDocument.Parse("DisplayName = English\n[English]");

            Assert.That(document.HasErrors, Is.True);
            Assert.That(document.Languages[0].Fields, Is.Empty);
        }

        [Test]
        public void Parse_RejectsAttributesAfterTheFirstSection()
        {
            CatalogDocument document = CatalogDocument.Parse("[English]\n@source English");

            Assert.That(document.Attributes, Is.Empty);
            Assert.That(document.Issues[0].Line, Is.EqualTo(2));
        }

        [Test]
        public void Parse_RejectsDuplicateFieldsAndAttributes()
        {
            CatalogDocument document = CatalogDocument.Parse("@source English\n@SOURCE Russian\n[English]\nCulture = en\nculture = en-US");

            Assert.That(document.Attributes.Count, Is.EqualTo(1));
            Assert.That(document.Languages[0].Fields.Count, Is.EqualTo(1));
            Assert.That(document.Issues.Count, Is.EqualTo(2));
        }

        [Test]
        public void Parse_ReportsMergeConflictMarkers()
        {
            CatalogDocument document = CatalogDocument.Parse("[English]\n<<<<<<< HEAD\nCulture = en\n=======\nCulture = en-US\n>>>>>>> branch");

            Assert.That(document.HasErrors, Is.True);
        }
    }
}
