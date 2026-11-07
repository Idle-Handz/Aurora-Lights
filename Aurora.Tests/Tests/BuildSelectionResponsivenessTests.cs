using System.Collections.Concurrent;
using System.Xml;
using Aurora.Reflections.Services;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services.Data;
using Builder.Presentation.UserControls.Spellcasting;

namespace Aurora.Tests.Tests;

public sealed class BuildSelectionResponsivenessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LanguageSelectionRebuildsTheSnapshotOutsideTheCallerContext(bool saveToFile) =>
        BuildPendingSnapshotEditsTests.WithCharacter(async (manager, tab, _) =>
        {
            var document = new XmlDocument();
            document.LoadXml("""
                <element name="Language choices" type="Class Feature" source="Test" id="ID_RESPONSIVE_OWNER">
                  <spellcasting name="Test caster" ability="Intelligence" prepare="true">
                    <list>Test caster</list>
                  </spellcasting>
                  <rules><select type="Language" name="Language" supports="Standard" /></rules>
                </element>
                """);
            var owner = new ElementParser().ParseElement(document.DocumentElement!).Construct<ClassFeature>();
            var language = new ElementBase("Elvish", "Language", "Test", "ID_RESPONSIVE_LANGUAGE")
                .Construct<Language>();
            language.Supports.Add("Standard");
            DataManager.Current.ElementsCollection.Add(owner);
            DataManager.Current.ElementsCollection.Add(language);
            manager.RegisterElement(owner);
            manager.ReprocessCharacter();
            manager.GetSpellcastingInformations().Should().ContainSingle();
            var rule = manager.SelectionRules.Single(r => r.ElementHeader.Id == owner.Id);
            var observer = new SnapshotSpellHandler();
            SpellcastingSectionContext.Current = observer;

            var callerContext = new CallerContext();
            var previousContext = SynchronizationContext.Current;
            Task<IReadOnlyList<string>> apply;
            try
            {
                SynchronizationContext.SetSynchronizationContext(callerContext);
                apply = BuildService.ApplySelectionAndSaveAsync(
                    rule, language.Id, 1, tab, tab.File, saveToFile);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }

            (await apply).Should().BeEmpty();
            tab.Snapshot!.Languages.Should().Contain("Elvish");
            SelectionRuleExpanderContext.Current!.GetRegisteredElement(rule).Should().BeOfType<Language>();
            observer.ReadContexts.Should().NotBeEmpty().And.OnlyContain(context => context == null,
                "snapshot spell resolution must run off the UI context, including after the save finishes");
            File.ReadAllText(tab.File.FilePath).Contains(language.Id).Should().Be(saveToFile);
        });

    private sealed class SnapshotSpellHandler : ISpellcastingSectionHandler
    {
        public ConcurrentQueue<SynchronizationContext?> ReadContexts { get; } = new();
        public SpellcasterSelectionControlViewModel? GetSpellcasterSectionViewModel(string uniqueIdentifier) => null;
        public bool SetPrepareSpell(SpellcastingInformation information, string elementId) => true;
        public IReadOnlyCollection<string> GetPreparedIds(string spellcastingName)
        {
            ReadContexts.Enqueue(SynchronizationContext.Current);
            return [];
        }
    }

    // Preserve a UI-style context across awaits so a thread-pool test runner cannot hide
    // a continuation that incorrectly moves snapshot work back onto the caller's context.
    private sealed class CallerContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? state) =>
            ThreadPool.QueueUserWorkItem(_ =>
            {
                var previous = Current;
                try
                {
                    SetSynchronizationContext(this);
                    callback(state);
                }
                finally { SetSynchronizationContext(previous); }
            });
    }
}
