using Aurora.App.Services;
using Aurora.Components.Models;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Rules;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class SpellAcquisitionIntegrationTests
{
    [Fact]
    public async Task SelectingOneSpellThroughTwoFeaturesPreservesTypeOriginAndSurvivingOwner()
    {
        Aurora.Tests.Helpers.TestApplicationContextInstaller.EnsureInstalled();
        var manager = Builder.Presentation.CharacterManager.Current;
        Builder.Presentation.SelectionRuleExpanderContext.Current = new Aurora.Tests.Helpers.TestSelectionRuleExpanderHandler();
        await manager.New(false);
        var progression = (ProgressionManager)typeof(Builder.Presentation.CharacterManager)
            .GetField("_progressionManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(manager)!;
        progression.ProgressionLevel = 1;
        ElementBase Parse(string id, string type, string body)
        {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml($"<element id='{id}' type='{type}' name='{id}' source='Internal'><description><p>Fixture.</p></description>{body}</element>");
            return new Builder.Data.ElementParser().ParseElement(doc.DocumentElement!).Construct<FeatFeature>();
        }
        var owner1 = Parse("ID_SPELL_ROUTE_OWNER_1", "Feat Feature", "<rules><select type='Spell' name='Spell' /></rules>");
        var owner2 = Parse("ID_SPELL_ROUTE_OWNER_2", "Feat Feature", "<rules><select type='Spell' name='Spell' /></rules>");
        var spell = new ElementBase("Spell", "Spell", "Internal", "ID_TEST_ROUTE_SPELL").Construct<Spell>();
        spell.Level = 2;
        spell.MagicSchool = "Illusion";
        var catalog = Builder.Presentation.Services.Data.DataManager.Current.ElementsCollection;
        catalog.Add(spell);
        var registrations = new Dictionary<string, object>();
        try
        {
            manager.RegisterElement(owner1);
            manager.RegisterElement(owner2);
            manager.GetElements().Should().Contain(owner1).And.Contain(owner2);
            var rule1 = owner1.GetSelectRules().Single();
            var rule2 = owner2.GetSelectRules().Single();
            SelectionRuleRegistrationService.SetRegisteredElement(registrations, rule1, spell.Id);
            SelectionRuleRegistrationService.SetRegisteredElement(registrations, rule2, spell.Id);
            var acquired = manager.GetElements().Where(e => e.Id == spell.Id).ToList();
            acquired.Should().HaveCount(2);
            acquired.Should().OnlyContain(e => e is Spell);
            acquired.Cast<Spell>().Should().OnlyContain(s => s.Level == 2 && s.MagicSchool == "Illusion");
            acquired.Select(e => e.Aquisition.GetParentHeader().Id).Should().BeEquivalentTo([owner1.Id, owner2.Id]);
            manager.UnregisterElement(owner1);
            manager.GetElements().Where(e => e.Id == spell.Id).Should().ContainSingle()
                .Which.Aquisition.GetParentHeader().Id.Should().Be(owner2.Id);
        }
        finally
        {
            await manager.New(false);
            catalog.Remove(spell);
        }
    }

    [Fact]
    public void PreparationIsIsolatedBySourceAndSurvivesNewRuntimeProfileIdentifiers()
    {
        var first = new SpellcastingInformation(new ElementHeader("Wizard", "Class", "2014", "WIZARD_2014")) { Name = "Wizard" };
        var other = new SpellcastingInformation(new ElementHeader("Wizard", "Class", "2024", "WIZARD_2024")) { Name = "Wizard" };
        var restored = new SpellcastingInformation(first.ElementHeader) { Name = "Wizard" };
        var handler = new MauiSpellcastingSectionHandler();
        handler.SetPrepareSpell(first, "SPELL");
        handler.GetPreparedIds(other).Should().BeEmpty();
        handler.GetPreparedIds(restored).Should().ContainSingle().Which.Should().Be("SPELL");
        handler.UnsetPrepareSpell(other, "SPELL");
        handler.GetPreparedIds(first).Should().Contain("SPELL");
        handler.ResetPreparedState();
        handler.GetPreparedIds(first).Should().BeEmpty();
    }

    [Fact]
    public void UiDoesNotUpgradeAResolvedLimitedFeatureToUnrestrictedSlots()
    {
        var spell = new MagicSpellListEntryModel("SPELL", "Spell", 1, "Test", false, false);
        var limited = new MagicSpellAccessPathModel(MagicSpellAccessKind.Granted, "", "Feat", true, false,
            CanUseSpellSlots: false, Ability: "Wisdom", FreeUses: 1, Recharge: "Long Rest");
        var model = new MagicOverviewModel
        {
            KnownSpellGroups = [new("Feat", [new("choice", "Spell", "Spell", 1, SpellId: "SPELL",
                SelectionAccess: MagicSpellSelectionAccess.Granted, ResolvedAccessPaths: [limited])])],
            Sections = [new() { Id = "Wizard", Label = "Wizard", IsPreparedCaster = true, SpellLevels = [new(1, [spell], 2, 0)] }]
        };
        MagicSpellAccessClassifier.Apply(model);
        spell.AccessPaths.Should().ContainSingle().Which.Should().Be(limited);
        spell.IsPrepared.Should().BeFalse();
        model.Sections[0].PreparedCount.Should().Be(0);
    }

    [Fact]
    public void NormalizationKeepsIndependentSpellOrigins()
    {
        Aurora.Tests.Helpers.TestApplicationContextInstaller.EnsureInstalled();
        var first = new ElementBase("Spell", "Spell", "Test", "SPELL").Construct<Spell>();
        var second = new ElementBase("Spell", "Spell", "Test", "SPELL").Construct<Spell>();
        first.Aquisition.SelectedBy(new SelectRule(new ElementHeader("First", "Feat", "Test", "FIRST")));
        second.Aquisition.SelectedBy(new SelectRule(new ElementHeader("Second", "Feat", "Test", "SECOND")));
        var progression = new ProgressionManager();
        progression.Elements.Add(first);
        progression.Elements.Add(second);
        progression.NormalizeDuplicateProgressionState().Should().Be(0);
        progression.Elements.Should().HaveCount(2);
    }
}
