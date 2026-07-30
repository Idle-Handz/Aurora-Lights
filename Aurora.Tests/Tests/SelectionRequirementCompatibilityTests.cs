using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class SelectionRequirementCompatibilityTests
{
    [Fact]
    public void ExpandForSelectionValidation_LetsGunnerSatisfy2024FirearmProficiencies()
    {
        IReadOnlySet<string> expanded =
            SelectionRequirementCompatibility.ExpandForSelectionValidation(
                [SelectionRequirementCompatibility.GunnerFeatId]);

        expanded.Should().Contain(SelectionRequirementCompatibility.Pistol2024ProficiencyId);
        expanded.Should().Contain(SelectionRequirementCompatibility.Musket2024ProficiencyId);
    }

    [Fact]
    public void ExpandForSelectionValidation_LetsBroadFirearmsProficiencySatisfy2024FirearmProficiencies()
    {
        IReadOnlySet<string> expanded =
            SelectionRequirementCompatibility.ExpandForSelectionValidation(
                [SelectionRequirementCompatibility.FirearmsProficiencyId]);

        expanded.Should().Contain(SelectionRequirementCompatibility.Pistol2024ProficiencyId);
        expanded.Should().Contain(SelectionRequirementCompatibility.Musket2024ProficiencyId);
    }

    [Fact]
    public void ExpandForSelectionValidation_MapsLegacySpecificFirearmProficiencies()
    {
        IReadOnlySet<string> expanded =
            SelectionRequirementCompatibility.ExpandForSelectionValidation(
                [
                    SelectionRequirementCompatibility.LegacyPistolProficiencyId,
                    SelectionRequirementCompatibility.LegacyMusketProficiencyId
                ]);

        expanded.Should().Contain(SelectionRequirementCompatibility.Pistol2024ProficiencyId);
        expanded.Should().Contain(SelectionRequirementCompatibility.Musket2024ProficiencyId);
    }

    [Fact]
    public void ExpandForSelectionValidation_DoesNotAddFirearmProficienciesWithoutEvidence()
    {
        IReadOnlySet<string> expanded =
            SelectionRequirementCompatibility.ExpandForSelectionValidation(["ID_TEST_UNRELATED"]);

        expanded.Should().NotContain(SelectionRequirementCompatibility.Pistol2024ProficiencyId);
        expanded.Should().NotContain(SelectionRequirementCompatibility.Musket2024ProficiencyId);
    }
}
