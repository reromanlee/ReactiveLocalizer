using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Unity;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;

namespace reromanlee.ReactiveLocalizer.Tests
{
    public class UnityHostTests
    {
        private static readonly EntryKey Purchase = new("Shop", "Purchase");

        private static TextTableSource CreateSource()
        {
            return new TextTableSource("@source English\n[English]\n[Russian]\n",
                ("Shop", "English", "Purchase = Buy"),
                ("Shop", "Russian", "Purchase = Kupit"));
        }

        [Test]
        public void TableSources_AskTheGivenSourcesFirst()
        {
            TextTableSource source = CreateSource();

            UnityHost host = new(source);

            Assert.That(host.TableSources[0], Is.SameAs(source));
            Assert.That(host.TableSources[host.TableSources.Count - 2], Is.TypeOf<EmbeddedTableSource>());
            Assert.That(host.TableSources[host.TableSources.Count - 1], Is.TypeOf<StreamingTableSource>());
        }

        [Test]
        public void IsHostThread_IsTrueOnTheMainThread()
        {
            Assert.That(new UnityHost().IsHostThread, Is.True);
            Assert.That(Task.Run(() => new UnityHost().IsHostThread).Result, Is.False);
        }

        [Test]
        public void Report_LogsErrorsAndWarningsToTheConsole()
        {
            UnityHost host = new();

            LogAssert.Expect(LogType.Error, "[ReactiveLocalizer] Something is missing.");
            host.Report(new LocalizerReport(ReportSeverity.Error, "Something is missing.", null));
            LogAssert.Expect(LogType.Warning, "[ReactiveLocalizer] Something is odd.");
            host.Report(new LocalizerReport(ReportSeverity.Warning, "Something is odd.", null));
        }

        [Test]
        public void IsDestroyed_TellsDestroyedUnityObjectsApart()
        {
            UnityHost host = new();
            GameObject gameObject = new("Target");

            Assert.That(host.IsDestroyed(gameObject), Is.False);
            Assert.That(host.IsDestroyed(new object()), Is.False);
            Object.DestroyImmediate(gameObject);
            Assert.That(host.IsDestroyed(gameObject), Is.True);
        }

        [Test]
        public void Localizer_UnderUnityHost_ReadsAndSwitchesOnTheMainThread()
        {
            TextTableSource source = CreateSource();
            using Localizer localizer = new(source.CatalogKey, new UnityHost(source));

            Assert.That(localizer.InitializeAsync().IsCompleted, Is.True);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Buy"));
            localizer.SetLanguageAsync(new LanguageKey("Russian"));
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }

        [UnityTest]
        public IEnumerator WorkFromAnotherThread_IsAppliedOnALaterFrame()
        {
            TextTableSource source = CreateSource();
            using Localizer localizer = new(source.CatalogKey, new UnityHost(source));
            localizer.InitializeAsync();

            Task switching = null;
            Task.Run(() => { switching = localizer.SetLanguageAsync(new LanguageKey("Russian")); }).Wait();
            Assert.That(switching.IsCompleted, Is.False);
            yield return null;
            yield return null;

            Assert.That(switching.IsCompleted, Is.True);
            Assert.That(localizer.Get(Purchase), Is.EqualTo("Kupit"));
        }

        [UnityTest]
        public IEnumerator ABindingToADestroyedObject_IsReleasedWithAWarning()
        {
            TextTableSource source = CreateSource();
            using Localizer localizer = new(source.CatalogKey, new UnityHost(source));
            localizer.InitializeAsync();
            GameObject label = new("Label");
            TextBinding binding = localizer.Bind(Purchase, label, static (target, text) => target.name = text);
            Assert.That(label.name, Is.EqualTo("Buy"));

            Object.Destroy(label);
            yield return null;
            LogAssert.Expect(LogType.Warning, new Regex("was released because its target was destroyed"));
            localizer.SetLanguageAsync(new LanguageKey("Russian"));

            Assert.That(binding.IsActive, Is.False);
        }
    }
}
