using System.Reflection;
using Builder.Data;
using Builder.Data.Rules;
using Builder.Presentation.Models;
using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class CharacterFileSelectionRestoreTests
{
    private const string SmallDrakeId = "ID_TBOX_COMPANION_DRAKEWARDEN_DRAKE_COMPANION_SMALL";
    private const string MediumDrakeId = "ID_TBOX_COMPANION_DRAKEWARDEN_DRAKE_COMPANION_MEDIUM";
    private const string LargeDrakeId = "ID_TBOX_COMPANION_DRAKEWARDEN_DRAKE_COMPANION_LARGE";

    [Fact]
    public void ResolveSavedSelectRule_UsesRegisteredIdToDisambiguateSameChecksumRows()
    {
        var small = CreateDrakeCompanionRule(SmallDrakeId);
        var medium = CreateDrakeCompanionRule(MediumDrakeId);
        var large = CreateDrakeCompanionRule(LargeDrakeId);

        string checksum = CharacterFileVerification.GenerateCrC(medium, 1);
        CharacterFileVerification.GenerateCrC(small, 1).Should().Be(checksum);
        CharacterFileVerification.GenerateCrC(large, 1).Should().Be(checksum);

        var resolved = ResolveSavedSelectRule(
            [small, medium, large],
            checksum,
            MediumDrakeId,
            number: 1);

        resolved.Should().BeSameAs(medium);
    }

    private static SelectRule CreateDrakeCompanionRule(string defaultId)
    {
        var rule = new SelectRule(new ElementHeader(
            "Drake Companion Revised",
            "Archetype Feature",
            "The Book of Xellarant",
            "ID_TBOX_ARCHETYPE_FEATURE_DRAKEWARDEN_DRAKE_COMPANION"));

        rule.Attributes.Type = "Companion";
        rule.Attributes.Name = "Drake Companion";
        rule.Attributes.RequiredLevel = 1;
        rule.Attributes.Default = defaultId;
        return rule;
    }

    private static SelectRule? ResolveSavedSelectRule(
        IEnumerable<SelectRule> candidates,
        string existingChecksum,
        string registeredElementId,
        int number)
    {
        var method = typeof(CharacterFile).GetMethod(
            "ResolveSavedSelectRule",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        return (SelectRule?)method!.Invoke(null, [candidates, existingChecksum, registeredElementId, number]);
    }
}
