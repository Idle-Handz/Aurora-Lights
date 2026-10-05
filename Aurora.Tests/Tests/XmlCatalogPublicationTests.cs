using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Reflection;
using Aurora.Tests.Helpers;
using Builder.Core.Events;
using Builder.Core.Logging;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Events.Data;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Data;
using ApplicationContext = Builder.Presentation.ApplicationContext;

namespace Aurora.Tests.Tests;

public sealed class XmlCatalogPublicationTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);
    private static readonly PhaseLogger Trace = new();
    private const string OldAlias = "ID_XCP_FEAT_OLD";
    private const string NewFeat = "ID_XCP_FEAT_CURRENT";
    private const string GeneratedAlias = "ID_XCP_INTERNAL_ITEM_FEAT_PROXY_OLD";
    private const string GeneratedFeat = "ID_XCP_INTERNAL_ITEM_FEAT_PROXY_CURRENT";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuildsAwayFromCallerAndPublishesOneCompleteCatalog(bool deferProgress)
    {
        using var saved = new SavedState();
        using var releaseWorker = new ManualResetEventSlim();
        var workerEntered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        await CallerContext.Run(async context =>
        {
            var manager = DataManager.Current;
            var catalog = manager.ElementsCollection;
            var oldElements = catalog.ToArray();
            int callerThread = Environment.CurrentManagedThreadId;
            using var events = new LoadObserver();
            var changes = new ConcurrentQueue<(NotifyCollectionChangedAction Action, int Thread, bool Finalized)>();
            NotifyCollectionChangedEventHandler onChanged = (_, args) =>
                changes.Enqueue((args.Action, Environment.CurrentManagedThreadId, HasFinalizedContent(catalog)));
            catalog.CollectionChanged += onChanged;
            context.DeferProgress = deferProgress;
            Trace.OnInfo = message =>
            {
                if (message != "[content-phase] xml.item-details begin") return;
                workerEntered.TrySetResult(Environment.CurrentManagedThreadId);
                if (Environment.CurrentManagedThreadId == callerThread)
                    throw new InvalidOperationException("XML finalization ran on the caller thread.");
                if (!releaseWorker.Wait(Timeout)) throw new TimeoutException("Candidate release was not received.");
            };

            Task<IEnumerable<ElementBase>>? load = null;
            try
            {
                load = manager.InitializeElementDataAsync();
                (await workerEntered.Task.WaitAsync(Timeout)).Should().NotBe(callerThread);
                catalog.Should().Equal(oldElements, "candidate construction must not mutate the live collection");
                manager.IsElementsCollectionPopulated.Should().BeTrue();
                SpellProxyCatalog.Categories.Should().Equal("Additional LiveOnly Spell");
                ElementIdAliases.TryGetTarget(OldAlias, out string target).Should().BeTrue();
                target.Should().Be(NewFeat);
                ElementIdAliases.TryGetTarget(GeneratedAlias, out _).Should().BeFalse(
                    "generated aliases belong to the unpublished candidate");
                changes.Should().BeEmpty();
                events.Populated.Should().BeEmpty();

                releaseWorker.Set();
                await load.WaitAsync(Timeout);
                await events.WhenPopulated.Task.WaitAsync(Timeout);
                context.Drain();

                manager.ElementsCollection.Should().BeSameAs(catalog);
                catalog.GetElement("ID_XCP_CLASS").Should().NotBeNull("the fixture must parse; errors: {0}", string.Join("\n", Trace.Errors));
                catalog.GetElement("ID_INTERNAL_CLASS_FEATURE_ASI_4_PUBLICATIONMAGE").Should().NotBeNull("class features must be finalized");
                catalog.GetElement(GeneratedFeat).Should().NotBeNull("feat proxies must be generated before publication");
                catalog.GetElement("ID_XCP_WEAPON").Should().BeAssignableTo<Item>()
                    .Which.WeaponProperties.Should().Contain("Publication Property", "item details must be finalized");
                changes.Should().ContainSingle();
                changes.Single().Should().Be((NotifyCollectionChangedAction.Reset, callerThread, true));
                events.Populated.Should().ContainSingle();
                events.Populated.Single().Should().Be((callerThread, true));
                ElementIdAliases.TryGetTarget(GeneratedAlias, out string generatedTarget).Should().BeTrue();
                generatedTarget.Should().Be(GeneratedFeat);

                if (deferProgress)
                {
                    context.DeferredProgressCount.Should().BeGreaterThan(0);
                    int before = events.Progress.Count;
                    context.ReleaseProgress();
                    context.Drain();
                    events.Progress.Should().HaveCount(before,
                        "queued worker progress must be ignored after the completed catalog is published");
                }
                else
                {
                    events.Progress.Should().NotBeEmpty();
                    events.Progress.Should().OnlyContain(progress => progress.Thread == callerThread);
                    events.Progress.Select(progress => progress.Message).Distinct().Should().HaveCountGreaterThan(1,
                        "progress messages must be snapshots rather than one shared mutable event argument");
                }
            }
            finally
            {
                releaseWorker.Set();
                if (load != null)
                {
                    try { await load.WaitAsync(Timeout); }
                    catch { /* The original test failure is reported after worker cleanup. */ }
                }
                Trace.OnInfo = null;
                catalog.CollectionChanged -= onChanged;
                context.ReleaseProgress();
                context.Drain();
            }
        }).WaitAsync(Timeout + Timeout);
    }

    [Fact]
    public async Task LazyAliasesCreatedByAResetSubscriberRemainLiveAfterPublication()
    {
        using var saved = new SavedState();
        await CallerContext.Run(async context =>
        {
            var catalog = DataManager.Current.ElementsCollection;
            const string currentId = "ID_XCP_INTERNAL_ITEM_PUBLICATIONMAGE_SPELL_PROXY_SPELL_SPARK";
            const string oldId = "ID_XCP_INTERNAL_ITEM_PUBLICATIONMAGE_SPELL_PROXY_SPELL_OLD";
            ElementBase? requested = null;
            int resetThread = 0;
            int callerThread = Environment.CurrentManagedThreadId;
            NotifyCollectionChangedEventHandler onChanged = (_, args) =>
            {
                if (args.Action != NotifyCollectionChangedAction.Reset) return;
                resetThread = Environment.CurrentManagedThreadId;
                requested = SpellProxyCatalog.ResolveOrBuild(catalog, currentId);
            };
            catalog.CollectionChanged += onChanged;
            try
            {
                await DataManager.Current.InitializeElementDataAsync().WaitAsync(Timeout);
                context.Drain();
                resetThread.Should().Be(callerThread);
                requested.Should().NotBeNull("lazy spell lookup must be ready when Reset is delivered");
                requested!.Id.Should().Be(currentId);
                ElementIdAliases.TryGetTarget(oldId, out string target).Should().BeTrue(
                    "Reset subscribers must extend the live aliases, not a completed candidate scope");
                target.Should().Be(currentId);
                ElementIdAliases.Resolve(catalog, oldId).Should().BeSameAs(requested);
            }
            finally
            {
                catalog.CollectionChanged -= onChanged;
                context.Drain();
            }
        }).WaitAsync(Timeout + Timeout);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedCandidateKeepsThePreviousCatalogAndSideState(bool wasPopulated)
    {
        using var saved = new SavedState();
        SavedState.SetPopulated(wasPopulated);
        await CallerContext.Run(async context =>
        {
            var manager = DataManager.Current;
            var catalog = manager.ElementsCollection;
            var oldElements = catalog.ToArray();
            int callerThread = Environment.CurrentManagedThreadId;
            int workerThread = 0;
            using var events = new LoadObserver();
            var changes = new ConcurrentQueue<NotifyCollectionChangedAction>();
            NotifyCollectionChangedEventHandler onChanged = (_, args) => changes.Enqueue(args.Action);
            catalog.CollectionChanged += onChanged;
            Trace.OnInfo = message =>
            {
                if (!message.StartsWith("[content-phase] xml.item-details end", StringComparison.Ordinal)) return;
                workerThread = Environment.CurrentManagedThreadId;
                throw new InvalidOperationException("Reject the completed XML candidate.");
            };
            try
            {
                Func<Task> load = () => manager.InitializeElementDataAsync();
                await load.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Reject the completed XML candidate.");
                context.Drain();
                workerThread.Should().NotBe(0).And.NotBe(callerThread);
                manager.ElementsCollection.Should().BeSameAs(catalog);
                catalog.Should().Equal(oldElements);
                manager.IsElementsCollectionPopulated.Should().Be(wasPopulated);
                SpellProxyCatalog.Categories.Should().Equal("Additional LiveOnly Spell");
                ElementIdAliases.TryGetTarget(OldAlias, out string target).Should().BeTrue();
                target.Should().Be(NewFeat);
                ElementIdAliases.TryGetTarget(GeneratedAlias, out _).Should().BeFalse();
                changes.Should().BeEmpty();
                events.Populated.Should().BeEmpty();
            }
            finally
            {
                Trace.OnInfo = null;
                catalog.CollectionChanged -= onChanged;
                context.Drain();
            }
        }).WaitAsync(Timeout + Timeout);
    }

    [Fact]
    public async Task ChangingTheContentRootRejectsTheCandidateWithoutPublishingIt()
    {
        using var saved = new SavedState();
        using var releaseWorker = new ManualResetEventSlim();
        var workerEntered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await CallerContext.Run(async context =>
        {
            var manager = DataManager.Current;
            var catalog = manager.ElementsCollection;
            var oldElements = catalog.ToArray();
            int callerThread = Environment.CurrentManagedThreadId;
            using var events = new LoadObserver();
            Trace.OnInfo = message =>
            {
                if (message != "[content-phase] xml.item-details begin") return;
                workerEntered.TrySetResult(Environment.CurrentManagedThreadId);
                if (Environment.CurrentManagedThreadId == callerThread)
                    throw new InvalidOperationException("XML finalization ran on the caller thread.");
                if (!releaseWorker.Wait(Timeout)) throw new TimeoutException("Candidate release was not received.");
            };
            Task<IEnumerable<ElementBase>>? load = null;
            try
            {
                load = manager.InitializeElementDataAsync();
                (await workerEntered.Task.WaitAsync(Timeout)).Should().NotBe(callerThread);
                string changedRoot = Path.Combine(manager.UserDocumentsCustomElementsDirectory, "other-root");
                Directory.CreateDirectory(changedRoot);
                typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!
                    .SetValue(manager, changedRoot);
                releaseWorker.Set();
                Func<Task> finish = () => load.WaitAsync(Timeout);
                await finish.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Content directories changed during loading.*");
                context.Drain();
                manager.ElementsCollection.Should().BeSameAs(catalog);
                catalog.Should().Equal(oldElements);
                manager.IsElementsCollectionPopulated.Should().BeTrue();
                SpellProxyCatalog.Categories.Should().Equal("Additional LiveOnly Spell");
                ElementIdAliases.TryGetTarget(OldAlias, out string target).Should().BeTrue();
                target.Should().Be(NewFeat);
                ElementIdAliases.TryGetTarget(GeneratedAlias, out _).Should().BeFalse();
                events.Populated.Should().BeEmpty();
            }
            finally
            {
                releaseWorker.Set();
                if (load != null)
                {
                    try { await load.WaitAsync(Timeout); }
                    catch { /* Observe failure before restoring the singleton state. */ }
                }
                Trace.OnInfo = null;
                context.Drain();
            }
        }).WaitAsync(Timeout + Timeout);
    }

    private static bool HasFinalizedContent(ElementBaseCollection catalog) =>
        catalog.GetElement("ID_INTERNAL_CLASS_FEATURE_ASI_4_PUBLICATIONMAGE") != null &&
        catalog.GetElement(GeneratedFeat) != null &&
        catalog.GetElement("ID_XCP_WEAPON") is Item weapon && weapon.WeaponProperties.Contains("Publication Property");

    private sealed class LoadObserver : ISubscriber<DataManagerProgressChanged>, ISubscriber<ElementsCollectionPopulatedEvent>, IDisposable
    {
        private volatile bool active = true;
        public readonly ConcurrentQueue<(int Thread, string Message)> Progress = new();
        public readonly ConcurrentQueue<(int Thread, bool Ready)> Populated = new();
        public readonly TaskCompletionSource WhenPopulated = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public LoadObserver() => ApplicationContext.Current.EventAggregator.Subscribe(this);

        public void OnHandleEvent(DataManagerProgressChanged args)
        {
            if (active) Progress.Enqueue((Environment.CurrentManagedThreadId, args.ProgressMessage));
        }

        public void OnHandleEvent(ElementsCollectionPopulatedEvent args)
        {
            if (!active) return;
            var manager = DataManager.Current;
            bool ready = manager.IsElementsCollectionPopulated && HasFinalizedContent(manager.ElementsCollection) &&
                SpellProxyCatalog.Categories.Contains("Additional Publicationmage Spell") &&
                ElementIdAliases.TryGetTarget(GeneratedAlias, out string target) && target == GeneratedFeat;
            Populated.Enqueue((Environment.CurrentManagedThreadId, ready));
            WhenPopulated.TrySetResult();
        }

        // EventAggregator stores weak references and has no unsubscribe API. Inert observers
        // ensure a later test cannot receive notifications from this test's subscriptions.
        public void Dispose() => active = false;
    }

    private sealed class PhaseLogger : ILogger
    {
        public volatile Action<string>? OnInfo;
        public volatile bool CaptureErrors;
        public readonly ConcurrentQueue<string> Errors = new();
        public void Info(string message, params object[] args) => OnInfo?.Invoke(message);
        public void Debug(string message, params object[] args) { }
        public void Warning(string message, params object[] args) { }
        public void Exception(Exception ex)
        {
            if (CaptureErrors) Errors.Enqueue(ex.ToString());
        }
    }

    private sealed class CallerContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> queue = new();
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> deferred = new();
        public volatile bool DeferProgress;
        public int DeferredProgressCount => deferred.Count;

        public override void Post(SendOrPostCallback callback, object? state)
        {
            // Hold progress snapshots while allowing awaited continuations and populated
            // events to run. No private callback methods or loader helpers are inspected.
            if (DeferProgress && state is DataManagerProgressChanged) deferred.Enqueue((callback, state));
            else queue.Add((callback, state));
        }

        public void ReleaseProgress()
        {
            DeferProgress = false;
            while (deferred.TryDequeue(out var work)) queue.Add(work);
        }

        public void Drain()
        {
            while (queue.TryTake(out var work)) work.Callback(work.State);
        }

        public static Task Run(Func<CallerContext, Task> action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                var previous = Current;
                var context = new CallerContext();
                try
                {
                    SetSynchronizationContext(context);
                    Task task = action(context);
                    var deadline = DateTime.UtcNow + Timeout + Timeout;
                    while (!task.IsCompleted)
                    {
                        if (DateTime.UtcNow >= deadline) throw new TimeoutException("Caller context did not finish.");
                        if (context.queue.TryTake(out var work, 100)) work.Callback(work.State);
                    }
                    task.GetAwaiter().GetResult();
                    context.Drain();
                    completion.SetResult();
                }
                catch (Exception error) { completion.TrySetException(error); }
                finally { SetSynchronizationContext(previous); }
            }) { IsBackground = true, Name = "XML publication test caller" };
            thread.Start();
            return completion.Task;
        }
    }

    private sealed class SavedState : IDisposable
    {
        private readonly DataManager manager;
        private readonly ElementBase[] elements;
        private readonly bool populated;
        private readonly string? customPath;
        private readonly string[] additional;
        private readonly bool developer;
        private readonly bool lazy;
        private readonly FieldInfo[] proxyFields = [.. new[] { "_catalog", "_listNames", "_unavailable" }
            .Select(name => typeof(SpellProxyCatalog).GetField(name, BindingFlags.Static | BindingFlags.NonPublic)!)];
        private readonly object?[] proxyState;
        private readonly HashSet<string> materialized = (HashSet<string>)typeof(SpellProxyCatalog)
            .GetField("Materialized", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        private readonly string[] previousMaterialized;
        private readonly FieldInfo aliasField = typeof(ElementIdAliases).GetField("forwarding", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? aliases;
        private readonly string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "XmlPublication-" + Guid.NewGuid().ToString("N"));

        public SavedState()
        {
            TestApplicationContextInstaller.EnsureInstalled();
            manager = DataManager.Current;
            elements = manager.ElementsCollection.ToArray();
            populated = manager.IsElementsCollectionPopulated;
            customPath = manager.UserDocumentsCustomElementsDirectory;
            additional = ApplicationContext.Current.Settings.AdditionalCustomDirectories.ToArray();
            developer = ApplicationContext.Current.IsInDeveloperMode;
            lazy = SpellProxyCatalog.Enabled;
            proxyState = proxyFields.Select(field => field.GetValue(null)).ToArray();
            previousMaterialized = materialized.ToArray();
            aliases = aliasField.GetValue(null);
            Trace.Errors.Clear();
            Trace.CaptureErrors = true;
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "fixture.xml"), """
                <elements>
                  <element name="Publication Book" type="Source" source="Internal" id="ID_XCP_SOURCE">
                    <setters><set name="abbreviation">XCP</set><set name="url">https://example.test/xcp</set></setters>
                  </element>
                  <element name="Publicationmage" type="Class" source="Publication Book" id="ID_XCP_CLASS">
                    <setters><set name="hd">d6</set></setters>
                  </element>
                  <element name="Spellcasting" type="Class Feature" source="Publication Book" id="ID_XCP_CF_SPELLCASTING">
                    <spellcasting name="Publicationmage" ability="Intelligence"><list>Publicationmage</list></spellcasting>
                  </element>
                  <element name="Spark" type="Spell" source="Publication Book" id="ID_XCP_SPELL_SPARK">
                    <setters><set name="level">0</set><set name="school">Evocation</set><set name="time">1 action</set><set name="duration">Instantaneous</set><set name="range">60 feet</set></setters>
                  </element>
                  <element name="Current Feat" type="Feat" source="Publication Book" id="ID_XCP_FEAT_CURRENT" />
                  <element name="Publication Property" type="Weapon Property" source="Publication Book" id="ID_XCP_WEAPON_PROPERTY" />
                  <element name="Publication Weapon" type="Weapon" source="Publication Book" id="ID_XCP_WEAPON">
                    <supports>ID_XCP_WEAPON_PROPERTY</supports>
                    <setters><set name="damage" type="force">1d4</set></setters>
                  </element>
                </elements>
                """);
            typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!.SetValue(manager, root);
            ApplicationContext.Current.Settings.AdditionalCustomDirectories.Clear();
            ApplicationContext.Current.IsInDeveloperMode = false;
            manager.ElementsCollection.Clear();
            manager.ElementsCollection.Add(new ElementBase { ElementHeader = new ElementHeader("Live catalog", "Item", "Test", "ID_XCP_LIVE") });
            SetPopulated(true);
            SpellProxyCatalog.Enabled = true;
            SpellProxyCatalog.Prime(manager.ElementsCollection, ["LiveOnly"], ["ID_XCP_UNAVAILABLE"]);
            ElementIdAliases.Set(new Dictionary<string, string>
            {
                [OldAlias] = NewFeat,
                ["ID_XCP_SPELL_OLD"] = "ID_XCP_SPELL_SPARK"
            });
            Logger.RegisterLogger(Trace);
        }

        public static void SetPopulated(bool value) => typeof(DataManager)
            .GetProperty(nameof(DataManager.IsElementsCollectionPopulated))!.SetValue(DataManager.Current, value);

        public void Dispose()
        {
            Trace.OnInfo = null;
            Trace.CaptureErrors = false;
            Trace.Errors.Clear();
            manager.ElementsCollection.Clear();
            manager.ElementsCollection.AddRange(elements);
            SetPopulated(populated);
            typeof(DataManager).GetProperty(nameof(DataManager.UserDocumentsCustomElementsDirectory))!.SetValue(manager, customPath);
            var directories = ApplicationContext.Current.Settings.AdditionalCustomDirectories;
            directories.Clear();
            directories.AddRange(additional);
            ApplicationContext.Current.IsInDeveloperMode = developer;
            SpellProxyCatalog.Enabled = lazy;
            for (int i = 0; i < proxyFields.Length; i++) proxyFields[i].SetValue(null, proxyState[i]);
            materialized.Clear();
            materialized.UnionWith(previousMaterialized);
            aliasField.SetValue(null, aliases);
            Directory.Delete(root, recursive: true);
        }
    }
}
