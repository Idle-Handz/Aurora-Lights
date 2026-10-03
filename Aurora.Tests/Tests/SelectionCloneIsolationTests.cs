using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

/// <summary>
/// A repeated selection gets a copy of the catalog element so the two slots do not share one
/// acquisition record. The copy is made by reflecting over settable properties, which shares every
/// reference it does not explicitly replace - and per-character state hidden behind such a
/// reference is the bug this whole load investigation kept running into.
/// </summary>
public sealed class SelectionCloneIsolationTests
{
    /// <summary>
    /// The progression manager adds and removes spellcasting sections by UniqueIdentifier. Two
    /// copies sharing one instance means the second never registers a section, and removing either
    /// copy takes the section away from both.
    /// </summary>
    [Fact]
    public void ACopyGetsItsOwnSpellcastingSection()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var source = Parse("""
            <element name="Initiate" type="Feat Feature" source="Internal" id="ID_CLONE_SPELLS">
              <spellcasting name="Initiate" ability="Intelligence" prepare="true">
                <list>Wizard</list>
                <extend known="true">Cleric</extend>
              </spellcasting>
            </element>
            """);
        source.SpellcastingInformation.Should().NotBeNull("the fixture has to carry a section to copy");

        var copy = SelectionRuleRegistrationService.CloneSelectionElement(source);

        copy.SpellcastingInformation.Should().NotBeSameAs(source.SpellcastingInformation);
        copy.SpellcastingInformation!.UniqueIdentifier.Should()
            .NotBe(source.SpellcastingInformation!.UniqueIdentifier,
                "the identifier is what registration and removal are keyed on");
        copy.SpellcastingInformation.Name.Should().Be(source.SpellcastingInformation.Name);
        copy.SpellcastingInformation.AbilityName.Should().Be(source.SpellcastingInformation.AbilityName);
        copy.SpellcastingInformation.Prepare.Should().Be(source.SpellcastingInformation.Prepare);

        // The lists carry their own identifiers, which prepared and known spells are keyed on.
        var sourceLists = Lists(source.SpellcastingInformation);
        var copyLists = Lists(copy.SpellcastingInformation);
        copyLists.Select(list => list.Supports).Should().Equal(sourceLists.Select(list => list.Supports));
        copyLists.Select(list => list.UniqueIdentifier).Should()
            .NotIntersectWith(sourceLists.Select(list => list.UniqueIdentifier));
    }

    /// <summary>
    /// The three things the copy does replace, restated as a test: sharing any of them is what
    /// made a second selection overwrite the first slot's association.
    /// </summary>
    [Fact]
    public void ACopyKeepsItsTypeAndSharesNoAcquisitionRulesOrChildren()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var source = Parse("""
            <element name="Increase" type="Ability Score Improvement" source="Internal" id="ID_CLONE_ASI">
              <rules><select name="Choose" type="Language"/><stat name="intelligence" value="2"/></rules>
            </element>
            """).Construct<AbilityScoreImprovement>();
        source.RuleElements.Add(new ElementBase("Child", "Language", "Internal", "ID_CLONE_CHILD"));

        var copy = SelectionRuleRegistrationService.CloneSelectionElement(source);

        copy.Should().BeOfType<AbilityScoreImprovement>("GetFresh erasing the type is what lost the ASI");
        copy.AllowMultipleElements.Should().BeTrue();
        copy.Id.Should().Be(source.Id);
        copy.Aquisition.Should().NotBeSameAs(source.Aquisition);
        copy.Rules.Should().NotBeSameAs(source.Rules);
        copy.RuleElements.Should().BeEmpty("the copy must not adopt the original's acquired children");
        copy.SelectionRuleListItems.Should().NotBeSameAs(source.SelectionRuleListItems);
        copy.GetSelectRules().Single().UniqueIdentifier.Should()
            .NotBe(source.GetSelectRules().Single().UniqueIdentifier);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovingAnExtensionAlsoRemovesItFromClonedCastersWithoutRemovingTheirOwnLists(bool hasOwnedExtension)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var previousHandler = SelectionRuleExpanderContext.Current;
        var previousSpells = SpellcastingSectionContext.Current;
        SelectionRuleExpanderContext.Current = new TestSelectionRuleExpanderHandler();
        SpellcastingSectionContext.Current = new TestSpellHandler();
        var manager = CharacterManager.Current;
        try
        {
            await manager.New(false);
            var source = new ElementBase("Initiate", "Feat Feature", "Internal", "ID_CLONE_CASTER").Construct<FeatFeature>();
            source.AllowDuplicate = true;
            source.SpellcastingInformation = new(source.ElementHeader)
            {
                Name = "Initiate", AbilityName = "Intelligence",
                InitialSupportedSpellsExpression = new("Wizard")
            };
            if (hasOwnedExtension)
                source.SpellcastingInformation.ExtendedSupportedSpellsExpressions.Add(new("Cleric"));
            var extension = new ElementBase("Extra spells", "Feat Feature", "Internal", "ID_CLONE_EXTENSION").Construct<FeatFeature>();
            extension.AllowDuplicate = true;
            extension.SpellcastingInformation = new(extension.ElementHeader)
            { Name = "Initiate", AbilityName = "Intelligence", IsExtension = true };
            extension.SpellcastingInformation.ExtendedSupportedSpellsExpressions.Add(new("Cleric"));
            // The same spell expression can be granted by two independently removable features.
            var otherExtension = SelectionRuleRegistrationService.CloneSelectionElement(extension);
            manager.RegisterElement(source);
            manager.RegisterElement(extension);
            manager.RegisterElement(otherExtension);
            int ownedCount = hasOwnedExtension ? 1 : 0;
            source.SpellcastingInformation.ExtendedSupportedSpellsExpressions.Should().HaveCount(ownedCount + 2);

            var copy = SelectionRuleRegistrationService.CloneSelectionElement(source);
            var secondCopy = SelectionRuleRegistrationService.CloneSelectionElement(copy);
            manager.RegisterElement(copy);
            manager.RegisterElement(secondCopy);
            var casters = new[] { source, copy, secondCopy };
            foreach (var caster in casters)
                caster.SpellcastingInformation.ExtendedSupportedSpellsExpressions.Should().HaveCount(ownedCount + 2);

            manager.UnregisterElement(extension);
            foreach (var caster in casters)
                caster.SpellcastingInformation.ExtendedSupportedSpellsExpressions.Should().HaveCount(ownedCount + 1);
            manager.UnregisterElement(otherExtension);
            foreach (var caster in casters)
                caster.SpellcastingInformation.ExtendedSupportedSpellsExpressions.Should().HaveCount(ownedCount,
                    "removing the granting features must leave only each caster's own lists");
            if (hasOwnedExtension)
                casters.Select(c => c.SpellcastingInformation.ExtendedSupportedSpellsExpressions.Single().UniqueIdentifier)
                    .Should().OnlyHaveUniqueItems("authored lists belong to their individual caster instances");
        }
        finally
        {
            await manager.New(false);
            SelectionRuleExpanderContext.Current = previousHandler;
            SpellcastingSectionContext.Current = previousSpells;
        }
    }

    private static IReadOnlyList<SpellcastingInformation.SpellcastingList> Lists(SpellcastingInformation information)
    {
        var lists = new List<SpellcastingInformation.SpellcastingList>();
        if (information.InitialSupportedSpellsExpression is { } initial) lists.Add(initial);
        lists.AddRange(information.ExtendedSupportedSpellsExpressions);
        return lists;
    }

    private static ElementBase Parse(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return new ElementParser().ParseElement(doc.DocumentElement!);
    }
}
