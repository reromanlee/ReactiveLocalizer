using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Hosting;
using System;
using System.Collections.Generic;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class TextBindingTests
    {
        private static readonly EntryKey Purchase = new("Shop", "Purchase");
        private static readonly EntryKey Title = new("Shop", "Title");
        private static readonly LanguageKey Russian = new("Russian");
        private static readonly LanguageKey English = new("English");

        private static MemoryTableSource CreateSource()
        {
            return new MemoryTableSource("Localization", "@source English\n[English]\n[Russian]\n",
                ("Shop", "English", "Purchase = Buy\nTitle = Shop"),
                ("Shop", "Russian", "Purchase = Kupit\nTitle = Magazin"));
        }

        private static Localizer CreateInitialized(out MemoryTableSource source, out TestHost host)
        {
            source = CreateSource();
            host = new TestHost(source);
            Localizer localizer = new(source.CatalogKey, host);
            localizer.InitializeAsync();
            return localizer;
        }

        [Test]
        public void Bind_AppliesTheTextRightAwayAndAfterEverySwitch()
        {
            using Localizer localizer = CreateInitialized(out _, out _);
            List<string> label = new();

            TextBinding binding = localizer.Bind(Purchase, label, static (target, text) => target.Add(text));
            localizer.SetLanguageAsync(Russian);
            localizer.SetLanguageAsync(English);

            Assert.That(label, Is.EqualTo(new[] { "Buy", "Kupit", "Buy" }));
            Assert.That(binding.IsActive, Is.True);
            Assert.That(localizer.BindingCount, Is.EqualTo(1));
        }

        [Test]
        public void Dispose_StopsTheBindingAndIsSafeToRepeat()
        {
            using Localizer localizer = CreateInitialized(out _, out _);
            List<string> label = new();
            TextBinding binding = localizer.Bind(Purchase, label, static (target, text) => target.Add(text));
            TextBinding copy = binding;

            binding.Dispose();
            binding.Dispose();
            copy.Dispose();
            localizer.SetLanguageAsync(Russian);

            Assert.That(label, Is.EqualTo(new[] { "Buy" }));
            Assert.That(binding.IsActive, Is.False);
            Assert.That(localizer.BindingCount, Is.Zero);
            Assert.That(default(TextBinding).IsActive, Is.False);
            default(TextBinding).Dispose();
        }

        [Test]
        public void ReleasedSlots_AreReusedWithoutReactivatingOldHandles()
        {
            using Localizer localizer = CreateInitialized(out _, out _);
            List<string> first = new();
            List<string> second = new();
            TextBinding old = localizer.Bind(Purchase, first, static (target, text) => target.Add(text));
            old.Dispose();

            TextBinding reused = localizer.Bind(Title, second, static (target, text) => target.Add(text));
            old.Dispose();
            localizer.SetLanguageAsync(Russian);

            Assert.That(reused.IsActive, Is.True);
            Assert.That(old.IsActive, Is.False);
            Assert.That(second, Is.EqualTo(new[] { "Shop", "Magazin" }));
        }

        [Test]
        public void Bind_BeforeInitialization_ReceivesEmptyTextThenTheRealText()
        {
            MemoryTableSource source = CreateSource();
            source.IsDeferred = true;
            TestHost host = new(source);
            using Localizer localizer = new(source.CatalogKey, host);
            List<string> label = new();

            localizer.Bind(Purchase, label, static (target, text) => target.Add(text));
            localizer.InitializeAsync();
            source.DeliverPending();
            source.DeliverPending();

            Assert.That(label, Is.EqualTo(new[] { string.Empty, "Buy" }));
            Assert.That(host.Reports, Is.Empty);
        }

        [Test]
        public void ACallbackThatThrows_IsReportedAndTheOthersStillUpdate()
        {
            using Localizer localizer = CreateInitialized(out _, out TestHost host);
            List<string> label = new();
            localizer.Bind(Purchase, label, static (target, text) =>
            {
                if (text == "Kupit")
                {
                    throw new InvalidOperationException("Callback failure.");
                }
            });
            localizer.Bind(Title, label, static (target, text) => target.Add(text));

            localizer.SetLanguageAsync(Russian);

            Assert.That(label, Is.EqualTo(new[] { "Shop", "Magazin" }));
            Assert.That(host.CountReports(ReportSeverity.Error), Is.EqualTo(1));
            Assert.That(host.Reports[0].Target, Is.SameAs(label));
        }

        [Test]
        public void ADestroyedTarget_IsReleasedAndReported()
        {
            using Localizer localizer = CreateInitialized(out _, out TestHost host);
            List<string> label = new();
            TextBinding binding = localizer.Bind(Purchase, label, static (target, text) => target.Add(text));

            host.DestroyedTargets.Add(label);
            localizer.SetLanguageAsync(Russian);

            Assert.That(binding.IsActive, Is.False);
            Assert.That(label, Is.EqualTo(new[] { "Buy" }));
            Assert.That(host.CountReports(ReportSeverity.Warning), Is.EqualTo(1));
        }

        [Test]
        public void ABindingMadeOnAnotherThread_IsFirstAppliedOnTheHostThread()
        {
            using Localizer localizer = CreateInitialized(out _, out TestHost host);
            List<string> label = new();
            host.IsTestThreadTheHost = false;

            TextBinding binding = localizer.Bind(Purchase, label, static (target, text) => target.Add(text));

            Assert.That(binding.IsActive, Is.True);
            Assert.That(label, Is.Empty);
            host.RunUpdates();
            Assert.That(label, Is.EqualTo(new[] { "Buy" }));
        }

        [Test]
        public void ACallbackMayBindAndSwitchWhileBeingNotified()
        {
            using Localizer localizer = CreateInitialized(out _, out _);
            List<string> label = new();
            List<string> nested = new();
            localizer.Bind(Purchase, label, (target, text) =>
            {
                target.Add(text);
                if (text == "Kupit")
                {
                    localizer.Bind(Title, nested, static (inner, innerText) => inner.Add(innerText));
                    localizer.SetLanguageAsync(English);
                }
            });

            localizer.SetLanguageAsync(Russian);

            Assert.That(label, Is.EqualTo(new[] { "Buy", "Kupit", "Buy" }));
            Assert.That(nested, Is.EqualTo(new[] { "Magazin", "Shop" }));
            Assert.That(localizer.CurrentLanguage.Name, Is.EqualTo("English"));
        }

        [Test]
        public void Bind_RejectsNullTargetsAndCallbacks()
        {
            using Localizer localizer = CreateInitialized(out _, out _);

            Assert.That(() => { localizer.Bind<List<string>>(Purchase, null, static (target, text) => target.Add(text)); }, Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => { localizer.Bind(Purchase, new List<string>(), null); }, Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void BindAndDispose_AllocateNothing()
        {
            using Localizer localizer = CreateInitialized(out _, out _);
            object target = new();

            Allocations.AssertNone(() => localizer.Bind(Purchase, target, static (label, text) => { }).Dispose());
        }
    }
}
