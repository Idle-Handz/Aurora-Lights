using Aurora.Reflections.Services;
using Aurora.Components.Models;
using Aurora.Components.Shared;
using Builder.Presentation.Utilities;
using Bunit;

namespace Aurora.Tests.Tests;

public sealed class SpellSlotTrackerTests : BunitContext
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PlayPagesTrackSameLevelPactAndRegularSlotsIndependently(bool magicPage)
    {
        IReadOnlyList<SpellSlotPool> pools = [
            new(SpellSlotPoolResolver.SpellcastingId, "Spellcasting", false, [new(2, 3), new(3, 3)]),
            new(SpellSlotPoolResolver.PactMagicId, "Pact Magic", true, [new(2, 2)])];
        var state = new SessionState();
        state.EnsureSpellSlotPools(pools);
        void Toggle(MagicSlotToggleModel change)
        {
            var capacity = pools.Single(p => p.Id == change.PoolId).Levels.Single(l => l.Level == change.Level);
            state.ToggleSlot(change.PoolId, change.Level, change.SlotIndex, capacity.TotalSlots);
        }
        Func<string, AngleSharp.Dom.IElement> find;
        Action refresh;
        if (magicPage)
        {
            MagicOverviewModel Model() => new() { HasSpellcasting = true, SlotPools = pools.Select(p =>
                new MagicSlotPoolModel(p.Id, p.Label, p.RecoversOnShortRest, p.Levels.Select(l =>
                    new MagicSpellLevelModel(l.Level, [], l.TotalSlots, state.UsedSlots(p.Id, l.Level, l.TotalSlots))).ToList())).ToList() };
            var magic = Render<CharacterMagicWorkspace>(p => p.Add(x => x.Model, Model()).Add(x => x.OnToggleSlot, Toggle));
            find = magic.Find;
            refresh = () => magic.Render(p => p.Add(x => x.Model, Model()));
        }
        else
        {
            var session = Render<SessionSpellSlotTracker>(p => p.Add(x => x.Pools, pools)
                .Add(x => x.UsedSlots, state.UsedSlots).Add(x => x.OnToggleSlot, Toggle));
            find = session.Find;
            refresh = () => session.Render();
        }
        var regular = find("section[aria-label='Spellcasting slots']");
        regular.QuerySelectorAll("button").Length.Should().Be(6, "third-level upcasting slots are displayed");
        regular.QuerySelectorAll("button")[1].Click();
        find("section[aria-label='Pact Magic slots']").QuerySelectorAll("button")[1].Click();
        refresh();
        state.SpellSlotsUsed[2].Should().Be(2);
        state.PactSpellSlotsUsed[2].Should().Be(2);
        state.ShortRest();
        refresh();
        find("section[aria-label='Spellcasting slots']").QuerySelectorAll("button[aria-pressed='true']").Length.Should().Be(2);
        find("section[aria-label='Pact Magic slots']").QuerySelectorAll("button[aria-pressed='true']").Should().BeEmpty();
    }
}
