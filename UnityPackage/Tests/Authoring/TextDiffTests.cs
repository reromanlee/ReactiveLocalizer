using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using System.Collections.Generic;
using System.Text;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TextDiffTests
    {
        private static string Join(List<TextDiffPart> parts, TextDiffKind skipped)
        {
            StringBuilder text = new();
            foreach (TextDiffPart part in parts)
            {
                if (part.Kind != skipped)
                {
                    text.Append(part.Text);
                }
            }
            return text.ToString();
        }

        [Test]
        public void Compare_KeepsWhatBothTextsShare()
        {
            List<TextDiffPart> parts = TextDiff.Compare("You have 5 coins.", "You now have 5 gold coins.");

            Assert.That(Join(parts, TextDiffKind.Added), Is.EqualTo("You have 5 coins."));
            Assert.That(Join(parts, TextDiffKind.Removed), Is.EqualTo("You now have 5 gold coins."));
            Assert.That(parts.FindAll(part => part.Kind == TextDiffKind.Added).ConvertAll(part => part.Text.Trim()), Is.EqualTo(new[] { "now", "gold" }));
            Assert.That(parts.FindAll(part => part.Kind == TextDiffKind.Removed), Is.Empty);
        }

        [Test]
        public void Compare_ShowsRemovedWords()
        {
            List<TextDiffPart> parts = TextDiff.Compare("Buy the shiny sword", "Buy the sword");

            Assert.That(parts.FindAll(part => part.Kind == TextDiffKind.Removed).ConvertAll(part => part.Text.Trim()), Is.EqualTo(new[] { "shiny" }));
            Assert.That(Join(parts, TextDiffKind.Removed), Is.EqualTo("Buy the sword"));
        }

        [Test]
        public void Compare_OfEmptyTexts_IsEmpty()
        {
            Assert.That(TextDiff.Compare(string.Empty, null), Is.Empty);
            Assert.That(TextDiff.Compare(string.Empty, "New")[0].Kind, Is.EqualTo(TextDiffKind.Added));
        }
    }
}
