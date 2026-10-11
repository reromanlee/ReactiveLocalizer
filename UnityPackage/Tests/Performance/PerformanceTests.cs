using NUnit.Framework;
using reromanlee.ReactiveLocalizer.Documents;
using reromanlee.ReactiveLocalizer.Hosting;
using reromanlee.ReactiveLocalizer.Tables;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.PerformanceTesting;

namespace reromanlee.ReactiveLocalizer.Tests
{
    /// <summary>
    /// Times what localization costs a game, from cached lookups to switching the language of 1,000 bound texts and
    /// loading a 50,000-entry table, and counts the allocations of each. The Performance Testing package records the
    /// results; nothing here asserts a speed, since that depends on the machine.
    /// </summary>
    public class PerformanceTests
    {
        private const string CatalogText =
            "@source English\n" +
            "[English]\nCulture = en\n" +
            "[Russian]\nCulture = ru\n";
        private const string InterfaceTable = "Interface";
        private const int EntryCount = 1000;
        private const int CallCount = 10000;
        private const int DialogueLineCount = 50000;
        private const int MeasurementCount = 10;

        private static readonly TableKey Dialogue = new("Dialogue");
        private static readonly EntryKey Coins = new("Shop", "Coins");
        private static readonly LanguageKey English = new("English");
        private static readonly LanguageKey Russian = new("Russian");

        private readonly EntryKey[] _keys = new EntryKey[EntryCount];
        private readonly string[] _entryNames = new string[EntryCount];
        private readonly char[] _buffer = new char[256];
        private MemoryTableSource _source;
        private string _dialogueText;
        private PerformanceHost _host;
        private Localizer _localizer;
        private int _count;

        [OneTimeSetUp]
        public void CreateTables()
        {
            StringBuilder english = new();
            StringBuilder russian = new();
            for (int i = 0; i < EntryCount; i++)
            {
                _entryNames[i] = "Entry" + i;
                _keys[i] = new EntryKey(InterfaceTable, _entryNames[i]);
                english.Append(_entryNames[i]).Append(" = Interface text number ").Append(i).Append('\n');
                russian.Append(_entryNames[i]).Append(" = Tekst interfeisa nomer ").Append(i).Append('\n');
            }
            StringBuilder dialogue = new();
            for (int i = 0; i < DialogueLineCount; i++)
            {
                dialogue.Append("Line").Append(i).Append(" = A line of dialogue about as long as most lines are, number ").Append(i).Append(".\n");
            }
            _dialogueText = dialogue.ToString();
            _source = MemoryTableSource.Imported("Localization", CatalogText,
                new[] { TableLoading.Preload, TableLoading.Preload, TableLoading.OnDemand },
                (InterfaceTable, "English", english.ToString()),
                ("Shop", "English", "Coins = You have {coins, plural, one {# coin} other {# coins}}."),
                ("Dialogue", "English", _dialogueText),
                (InterfaceTable, "Russian", russian.ToString()),
                ("Shop", "Russian", "Coins = U vas {coins, plural, one {# moneta} few {# monety} many {# monet} other {# monety}}."));
        }

        [SetUp]
        public void CreateLocalizer()
        {
            _host = new PerformanceHost(_source);
            _localizer = new Localizer(_source.CatalogKey, _host);
            _localizer.InitializeAsync();
            _count = 0;
        }

        [TearDown]
        public void DisposeLocalizer()
        {
            _localizer.Dispose();
            // A missing key or a failed load would mean timing the wrong path.
            Assert.That(_host.Reports, Is.Empty);
        }

        [Test, Performance]
        public void GetText()
        {
            MeasureCalls(() =>
            {
                for (int i = 0; i < CallCount; i++)
                {
                    _localizer.Get(_keys[i % EntryCount]);
                }
            });
        }

        [Test, Performance]
        public void GetTextByName()
        {
            MeasureCalls(() =>
            {
                for (int i = 0; i < CallCount; i++)
                {
                    _localizer.Get(InterfaceTable, _entryNames[i % EntryCount]);
                }
            });
        }

        [Test, Performance]
        public void FormatMessageIntoBuffer()
        {
            MeasureCalls(() =>
            {
                for (int i = 0; i < CallCount; i++)
                {
                    _localizer.TryFormat(CoinsMessage(i), _buffer, out _);
                }
            });
        }

        [Test, Performance]
        public void FormatMessageToString()
        {
            MeasureCalls(() =>
            {
                for (int i = 0; i < CallCount; i++)
                {
                    _localizer.Get(CoinsMessage(i));
                }
            });
        }

        [Test, Performance]
        public void SwitchLanguageOf1000Bindings()
        {
            Label[] labels = CreateLabels();
            TextBinding[] bindings = new TextBinding[EntryCount];
            for (int i = 0; i < EntryCount; i++)
            {
                bindings[i] = _localizer.Bind(_keys[i], labels[i], static (label, text) => label.Text = text);
            }

            Measure.Method(() =>
                {
                    _localizer.SetLanguageAsync(++_count % 2 == 1 ? Russian : English);
                    _host.RunUntilIdle();
                })
                .WarmupCount(2)
                .MeasurementCount(MeasurementCount)
                .SampleGroup(new SampleGroup("Switch", SampleUnit.Millisecond))
                .GC()
                .Run();

            _localizer.SetLanguageAsync(Russian);
            _host.RunUntilIdle();
            Assert.That(labels[EntryCount - 1].Text, Is.EqualTo("Tekst interfeisa nomer 999"));
            DisposeAll(bindings);
        }

        [Test, Performance]
        public void UpdateMessagesOf1000Bindings()
        {
            Label[] labels = CreateLabels();
            TextBinding[] bindings = new TextBinding[EntryCount];
            for (int i = 0; i < EntryCount; i++)
            {
                bindings[i] = _localizer.Bind(CoinsMessage(0), labels[i], static (label, text) => label.Text = text);
            }

            MeasureMessageUpdates(bindings);

            Assert.That(labels[0].Text, Is.EqualTo($"You have {_count} coins."));
            DisposeAll(bindings);
        }

        [Test, Performance]
        public void UpdateMessagesOf1000CharacterBindings()
        {
            Label[] labels = CreateLabels();
            TextBinding[] bindings = new TextBinding[EntryCount];
            for (int i = 0; i < EntryCount; i++)
            {
                bindings[i] = _localizer.BindCharacters(CoinsMessage(0), labels[i], static (label, characters) => label.Length = characters.Length);
            }

            MeasureMessageUpdates(bindings);

            Assert.That(labels[0].Length, Is.EqualTo($"You have {_count} coins.".Length));
            DisposeAll(bindings);
        }

        [Test, Performance]
        public void Load50000EntryTable()
        {
            TableHandle handle = default;
            int requestCount = 0;

            Measure.Method(() =>
                {
                    handle = _localizer.HoldTable(Dialogue);
                    _host.RunUntilIdle();
                })
                .SetUp(() => requestCount = _source.RequestCount)
                .CleanUp(() =>
                {
                    // Every measurement loads the table again, rather than finding it still loaded.
                    Assert.That(handle.IsLoaded, Is.True);
                    Assert.That(_source.RequestCount, Is.EqualTo(requestCount + 1));
                    handle.Dispose();
                    _host.RunUntilIdle();
                })
                .WarmupCount(1)
                .MeasurementCount(MeasurementCount)
                .SampleGroup(new SampleGroup("Load", SampleUnit.Millisecond))
                .GC()
                .Run();
        }

        [Test, Performance]
        public void MemoryOf50000EntryTable()
        {
            long before = GC.GetTotalMemory(true);
            TableHandle handle = _localizer.HoldTable(Dialogue);
            _host.RunUntilIdle();
            long loaded = GC.GetTotalMemory(true);
            for (int i = 0; i < DialogueLineCount; i++)
            {
                _localizer.Get(Dialogue.Name, "Line" + i);
            }
            long read = GC.GetTotalMemory(true);

            Assert.That(handle.IsLoaded, Is.True);
            Measure.Custom(new SampleGroup("Loaded", SampleUnit.Megabyte), (loaded - before) / 1048576d);
            Measure.Custom(new SampleGroup("Every line read", SampleUnit.Megabyte), (read - before) / 1048576d);
            handle.Dispose();
        }

        [Test, Performance]
        public void Compile50000EntryTable()
        {
            List<DocumentIssue> issues = new();
            byte[] compiled = null;

            Measure.Method(() => compiled = TableCompiler.Compile(_source.CatalogKey, Dialogue, English, TableDocument.Parse(_dialogueText), issues))
                .WarmupCount(1)
                .MeasurementCount(MeasurementCount)
                .SampleGroup(new SampleGroup("Compile", SampleUnit.Millisecond))
                .GC()
                .Run();

            Assert.That(issues, Is.Empty);
            Measure.Custom(new SampleGroup("Compiled size", SampleUnit.Megabyte), compiled.Length / 1048576d);
        }

        private static EntryMessage CoinsMessage(int count) => new(Coins, new MessageArgument("coins", count));

        private static Label[] CreateLabels()
        {
            Label[] labels = new Label[EntryCount];
            for (int i = 0; i < EntryCount; i++)
            {
                labels[i] = new Label();
            }
            return labels;
        }

        private static void DisposeAll(TextBinding[] bindings)
        {
            for (int i = 0; i < bindings.Length; i++)
            {
                bindings[i].Dispose();
            }
        }

        /// <summary>Times <paramref name="calls"/>, which makes 10,000 calls, and counts its allocations.</summary>
        private static void MeasureCalls(Action calls)
        {
            Measure.Method(calls)
                .WarmupCount(1)
                .MeasurementCount(MeasurementCount)
                .SampleGroup(new SampleGroup("10000 calls", SampleUnit.Microsecond))
                .GC()
                .Run();
        }

        /// <summary>Times giving every binding a message with a new count, so each one formats and shows it.</summary>
        private void MeasureMessageUpdates(TextBinding[] bindings)
        {
            Measure.Method(() =>
                {
                    _count++;
                    for (int i = 0; i < bindings.Length; i++)
                    {
                        bindings[i].SetMessage(CoinsMessage(_count));
                    }
                    _host.RunUntilIdle();
                })
                .WarmupCount(1)
                .MeasurementCount(MeasurementCount)
                .SampleGroup(new SampleGroup("Update", SampleUnit.Millisecond))
                .GC()
                .Run();
        }

        private sealed class Label
        {
            public string Text;
            public int Length;
        }

        /// <summary>A host on the test's thread whose updates run when the test runs them, without allocating.</summary>
        private sealed class PerformanceHost : ILocalizerHost
        {
            private readonly ITableSource[] _sources;
            private List<Action> _scheduled = new();
            private List<Action> _running = new();

            public PerformanceHost(params ITableSource[] sources)
            {
                _sources = sources;
            }

            public List<LocalizerReport> Reports { get; } = new();

            public IReadOnlyList<ITableSource> TableSources => _sources;

            public bool IsHostThread => true;

            public void ScheduleUpdate(Action update) => _scheduled.Add(update);

            public void Report(in LocalizerReport report) => Reports.Add(report);

            public bool IsDestroyed(object target) => false;

            /// <summary>Runs the scheduled updates, and the ones they schedule, until none is left.</summary>
            public void RunUntilIdle()
            {
                for (int round = 0; round < 16 && _scheduled.Count > 0; round++)
                {
                    (_scheduled, _running) = (_running, _scheduled);
                    for (int i = 0; i < _running.Count; i++)
                    {
                        _running[i]();
                    }
                    _running.Clear();
                }
            }
        }
    }
}
