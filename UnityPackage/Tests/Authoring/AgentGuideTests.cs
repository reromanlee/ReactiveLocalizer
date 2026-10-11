using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class AgentGuideTests
    {
        private static readonly AgentGuideCatalog Game = new("Assets/Localization/Localization.catalog", "English",
            new[] { "English", "Russian", "Pirate" }, "LocalizationKeys", "Assets/Localization/LocalizationKeys.cs", false);

        private static readonly AgentGuideCatalog Tool = new("Assets/Editor/Tool/Tool.catalog", "English",
            new[] { "English" }, "ToolKeys", "Assets/Editor/Tool/ToolKeys.cs", true);

        [Test]
        public void Write_ListsEveryCatalogWithItsLanguagesAndKeys()
        {
            string section = AgentGuide.Write(new[] { Game, Tool }, "\n");

            Assert.That(section, Does.StartWith(AgentGuide.StartMarker + "\n"));
            Assert.That(section, Does.EndWith(AgentGuide.EndMarker + "\n"));
            Assert.That(section, Does.Contain("- `Assets/Localization/Localization.catalog`: English (source), Russian and Pirate. " +
                "Keys: `LocalizationKeys` in `Assets/Localization/LocalizationKeys.cs`.\n"));
            Assert.That(section, Does.Contain("- `Assets/Editor/Tool/Tool.catalog`, editor only: English (source)."));
            Assert.That(section, Does.Contain("`LocalizationKeys.Shop.Coins(coins: 5)`"));
            Assert.That(section, Does.Not.Contain("\r"));
        }

        [Test]
        public void Write_WithoutCatalogs_TellsHowToCreateOne()
        {
            string section = AgentGuide.Write(new AgentGuideCatalog[0], "\r\n");

            Assert.That(section, Does.Contain("The project has no catalog yet"));
            Assert.That(section.Replace("\r\n", string.Empty), Does.Not.Contain("\n"));
        }

        [Test]
        public void TryMerge_IntoAnEmptyDocument_GivesTheSection()
        {
            string section = AgentGuide.Write(new[] { Game }, "\n");

            Assert.That(AgentGuide.TryMerge(string.Empty, section, out string merged), Is.True);
            Assert.That(merged, Is.EqualTo(section));
        }

        [Test]
        public void TryMerge_IntoADocumentWithoutTheSection_AppendsItAfterABlankLine()
        {
            string section = AgentGuide.Write(new[] { Game }, "\r\n");

            Assert.That(AgentGuide.TryMerge("# Agents\r\n\r\nRun the tests.\r\n\r\n", section, out string merged), Is.True);
            Assert.That(merged, Is.EqualTo("# Agents\r\n\r\nRun the tests.\r\n\r\n" + section));
        }

        [Test]
        public void TryMerge_ReplacesTheSectionInPlace()
        {
            string old = AgentGuide.Write(new[] { Tool }, "\n");
            string section = AgentGuide.Write(new[] { Game }, "\n");
            string document = "# Agents\n\n" + old + "\n### Style\n\nUse tabs.\n";

            Assert.That(AgentGuide.TryMerge(document, section, out string merged), Is.True);
            Assert.That(merged, Is.EqualTo("# Agents\n\n" + section + "\n### Style\n\nUse tabs.\n"));
            Assert.That(AgentGuide.TryMerge(merged, section, out string again), Is.True);
            Assert.That(again, Is.EqualTo(merged));
        }

        [Test]
        public void TryMerge_WithTheStartButNotTheEnd_ChangesNothing()
        {
            string document = "# Agents\n\n" + AgentGuide.StartMarker + "\n### Localization\n\nMy own notes.\n";

            Assert.That(AgentGuide.TryMerge(document, AgentGuide.Write(new[] { Game }, "\n"), out string merged), Is.False);
            Assert.That(merged, Is.Null);
        }
    }
}
