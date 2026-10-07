using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Authoring;
using reromanlee.ReactiveLocalizer.Documents;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TableSettingsTests
    {
        [Test]
        public void Read_KeepsTheDefaultsForAFileWithoutSettings()
        {
            TableSettings settings = TableSettings.Read(TableDocument.Parse("Purchase = Buy"), null);

            Assert.That(settings.Loading, Is.EqualTo(TableLoading.Preload));
            Assert.That(settings.Delivery, Is.EqualTo(TableDelivery.Embedded));
            Assert.That(settings.GeneratesCode, Is.True);
        }

        [Test]
        public void Read_ReadsEverySettingIgnoringCase()
        {
            List<DocumentIssue> issues = new();

            TableSettings settings = TableSettings.Read(TableDocument.Parse("@loading ondemand\n@delivery STREAMING\n@generateCode False\n\nLine = Text"), issues);

            Assert.That(issues, Is.Empty);
            Assert.That(settings.Loading, Is.EqualTo(TableLoading.OnDemand));
            Assert.That(settings.Delivery, Is.EqualTo(TableDelivery.Streaming));
            Assert.That(settings.GeneratesCode, Is.False);
        }

        [Test]
        public void Read_ReportsUnknownValuesAndKeepsTheDefault()
        {
            List<DocumentIssue> issues = new();

            TableSettings settings = TableSettings.Read(TableDocument.Parse("@loading Sometimes\n@generateCode maybe\n"), issues);

            Assert.That(issues.Count, Is.EqualTo(2));
            Assert.That(issues[0].Line, Is.EqualTo(1));
            Assert.That(issues[0].Message, Does.Contain("'Preload' or 'OnDemand'"));
            Assert.That(settings.Loading, Is.EqualTo(TableLoading.Preload));
            Assert.That(settings.GeneratesCode, Is.True);
        }
    }
}
