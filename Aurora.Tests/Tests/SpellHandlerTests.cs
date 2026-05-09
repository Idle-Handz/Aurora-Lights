using Aurora.Tests.Helpers;
using Aurora.Components.Models;
using Builder.Presentation;
using Builder.Presentation.Interfaces;

namespace Aurora.Tests.Tests;

/// <summary>
/// Unit tests for the spell-preparation handler pattern that backs the Magic page.
/// These run without the Aurora content database — no element data required.
/// </summary>
public sealed class SpellHandlerTests
{
    // ── ISpellcastingSectionHandler contract ─────────────────────────────────

    [Fact]
    public void SetPrepareSpell_AddsSpellToClass()
    {
        var handler = new TestSpellHandler();
        var info = FakeSpellcastingInfo("Paladin");

        handler.SetPrepareSpell(info, "ID_PHB_SPELL_HEROISM");

        handler.GetPreparedIds("Paladin").Should().Contain("ID_PHB_SPELL_HEROISM");
    }

    [Fact]
    public void SetPrepareSpell_IsCaseInsensitive_OnClassName()
    {
        var handler = new TestSpellHandler();
        var info = FakeSpellcastingInfo("Cleric");

        handler.SetPrepareSpell(info, "ID_PHB_SPELL_CURE_WOUNDS");

        handler.GetPreparedIds("cleric").Should().Contain("ID_PHB_SPELL_CURE_WOUNDS");
        handler.GetPreparedIds("CLERIC").Should().Contain("ID_PHB_SPELL_CURE_WOUNDS");
    }

    [Fact]
    public void SetPrepareSpell_IsCaseInsensitive_OnSpellId()
    {
        var handler = new TestSpellHandler();
        var info = FakeSpellcastingInfo("Druid");

        handler.SetPrepareSpell(info, "id_phb_spell_entangle");

        handler.GetPreparedIds("Druid").Should().Contain("ID_PHB_SPELL_ENTANGLE",
            because: "spell ID lookup should be case-insensitive");
    }

    [Fact]
    public void SetPrepareSpell_NullOrEmptyId_ReturnsFalse()
    {
        var handler = new TestSpellHandler();
        var info = FakeSpellcastingInfo("Cleric");

        handler.SetPrepareSpell(info, "").Should().BeFalse();
        handler.SetPrepareSpell(info, null!).Should().BeFalse();
    }

    [Fact]
    public void UnsetPrepareSpell_RemovesSpell()
    {
        var handler = new TestSpellHandler();
        var info = FakeSpellcastingInfo("Paladin");

        handler.SetPrepareSpell(info, "ID_PHB_SPELL_HEROISM");
        handler.SetPrepareSpell(info, "ID_PHB_SPELL_SHIELD_OF_FAITH");
        handler.UnsetPrepareSpell("Paladin", "ID_PHB_SPELL_HEROISM");

        handler.GetPreparedIds("Paladin").Should().NotContain("ID_PHB_SPELL_HEROISM");
        handler.GetPreparedIds("Paladin").Should().Contain("ID_PHB_SPELL_SHIELD_OF_FAITH");
    }

    [Fact]
    public void UnsetPrepareSpell_IsIdempotent_WhenSpellNotPrepared()
    {
        var handler = new TestSpellHandler();

        // Should not throw even if the class or spell doesn't exist in the dict.
        var act = () => handler.UnsetPrepareSpell("Cleric", "ID_PHB_SPELL_CURE_WOUNDS");
        act.Should().NotThrow();
    }

    [Fact]
    public void ResetPreparedState_ClearsAllClasses()
    {
        var handler = new TestSpellHandler();
        handler.SetPrepareSpell(FakeSpellcastingInfo("Paladin"), "ID_PHB_SPELL_HEROISM");
        handler.SetPrepareSpell(FakeSpellcastingInfo("Cleric"), "ID_PHB_SPELL_CURE_WOUNDS");

        handler.ResetPreparedState();

        handler.GetPreparedIds("Paladin").Should().BeEmpty();
        handler.GetPreparedIds("Cleric").Should().BeEmpty();
    }

    [Fact]
    public void GetPreparedIds_UnknownClass_ReturnsEmpty()
    {
        var handler = new TestSpellHandler();
        handler.GetPreparedIds("Wizard").Should().BeEmpty();
    }

    [Fact]
    public void DefaultInterface_UnsetPrepareSpell_IsNoOp()
    {
        // The default interface implementation (for WPF) should be a no-op and not throw.
        ISpellcastingSectionHandler handler = new NoOpSpellHandler();
        var act = () => handler.UnsetPrepareSpell("Cleric", "ID_PHB_SPELL_CURE_WOUNDS");
        act.Should().NotThrow();
    }

    // ── MagicPreparedChangeModel ─────────────────────────────────────────────

    [Fact]
    public void MagicPreparedChangeModel_CarriesSpellcastingClass()
    {
        var model = new MagicPreparedChangeModel("ID_PHB_SPELL_HEROISM", true, "section-id", "Paladin");

        model.SpellId.Should().Be("ID_PHB_SPELL_HEROISM");
        model.Value.Should().BeTrue();
        model.SpellcastingClass.Should().Be("Paladin");
    }

    [Fact]
    public void MagicPreparedChangeModel_PrepareFalse_CarriesCorrectValue()
    {
        var model = new MagicPreparedChangeModel("ID_PHB_SPELL_HEROISM", false, "section-id", "Paladin");
        model.Value.Should().BeFalse();
    }

    // ── SpellcastingSectionContext ────────────────────────────────────────────

    [Fact]
    public void SpellcastingSectionContext_CanBeAssignedAndRead()
    {
        var original = SpellcastingSectionContext.Current;
        var handler = new TestSpellHandler();

        SpellcastingSectionContext.Current = handler;
        SpellcastingSectionContext.Current.Should().BeSameAs(handler);

        SpellcastingSectionContext.Current = original; // restore
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static Builder.Data.Elements.SpellcastingInformation FakeSpellcastingInfo(string className)
    {
        // SpellcastingInformation is constructed by Aurora.Logic from element data.
        // We approximate it for unit tests by reflecting or constructing indirectly.
        // Using the builder pattern exposed via DataManager is too heavy for unit tests,
        // so we test via the handler directly (which only uses info.Name).
        var info = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
            typeof(Builder.Data.Elements.SpellcastingInformation))
            as Builder.Data.Elements.SpellcastingInformation;

        // Set Name via reflection since there's no public constructor.
        typeof(Builder.Data.Elements.SpellcastingInformation)
            .GetProperty("Name")
            ?.SetValue(info, className);

        return info!;
    }

    /// <summary>Minimal no-op implementation — simulates the WPF default interface implementation.</summary>
    private sealed class NoOpSpellHandler : ISpellcastingSectionHandler
    {
        public Builder.Presentation.UserControls.Spellcasting.SpellcasterSelectionControlViewModel?
            GetSpellcasterSectionViewModel(string uniqueIdentifier) => null;

        public bool SetPrepareSpell(Builder.Data.Elements.SpellcastingInformation information, string elementId) => false;
        // UnsetPrepareSpell intentionally NOT overridden — tests the interface default.
    }
}
