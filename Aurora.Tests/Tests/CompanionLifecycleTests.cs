using System.ComponentModel;
using System.Xml;
using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.ElementParsers;
using Builder.Data.Elements;
using Builder.Presentation.Models;
using Builder.Presentation.Services;
using Builder.Presentation.Services.Calculator;
using Builder.Presentation.Services.Data;

namespace Aurora.Tests.Tests;

/// <summary>
/// A character can now hold several companions at once, which turned one fixed Companion object
/// into a list and a computed primary. Both of those are load-bearing for code that cannot see the
/// list: WPF binds to Character.Companion, and the statistics pass runs once per creature.
/// </summary>
public sealed class CompanionLifecycleTests
{
    /// <summary>
    /// Companion is computed, so a binding to Character.Companion.* resolves once and keeps
    /// whichever instance it first saw. Without a notification the legacy sheet and sliders sit on
    /// the empty companion for the life of the view.
    /// </summary>
    [Fact]
    public void AcquiringOrClearingACompanionAnnouncesThatThePrimaryChanged()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var character = new Character();
        var announced = new List<string?>();
        ((INotifyPropertyChanged)character).PropertyChanged += (_, e) => announced.Add(e.PropertyName);

        var drake = Companion("ID_COMPANION_DRAKE", "Drake");
        character.SynchronizeCompanions([drake]);

        announced.Should().Contain(nameof(Character.Companion));
        announced.Should().Contain(nameof(Character.Companions));
        character.Companion.Element.Should().BeSameAs(drake);

        // Synchronizing the same set again changes nothing, so it must stay quiet.
        announced.Clear();
        character.SynchronizeCompanions([drake]);
        announced.Should().BeEmpty("re-running the same set is not a change");

        character.ResetEntryFields();
        announced.Should().Contain(nameof(Character.Companion));
        character.Companions.Should().BeEmpty();
    }

    /// <summary>
    /// The list is rebuilt from the element graph on every reprocess. Rebuilding it must keep the
    /// existing Companion objects, because that is where the name and portrait a load just read
    /// are stored - a fresh object would silently discard them.
    /// </summary>
    [Fact]
    public void RebuildingTheListKeepsTheCompanionThatAlreadyHoldsItsName()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var character = new Character();
        var owl = Companion("ID_COMPANION_OWL", "Owl");
        var drake = Companion("ID_COMPANION_DRAKE", "Drake");

        character.SynchronizeCompanions([owl]);
        Companion first = character.Companions.Single();
        first.CompanionName.Content = "Archimedes";

        character.SynchronizeCompanions([owl, drake]);

        character.Companions.Should().HaveCount(2);
        character.Companions[0].Should().BeSameAs(first);
        character.Companions[0].CompanionName.Content.Should().Be("Archimedes");
        character.Companion.Element.Should().BeSameAs(drake, "the last creature is the legacy primary");
    }

    /// <summary>
    /// Two creatures from the same template are separate instances, so the scope must answer per
    /// instance rather than per id. An owner outside every creature's subtree keeps its old global
    /// meaning, which is what stops an unowned legacy bonus disappearing.
    /// </summary>
    [Fact]
    public void CompanionScopeFollowsTheAcquiringFeatureAndNotTheTemplateId()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var first = Companion("ID_COMPANION_SHARED", "Beast");
        var second = Companion("ID_COMPANION_SHARED", "Beast");
        var ownerOfFirst = new ElementBase("Warden", "Class Feature", "Internal", "ID_OWNER_ONE");
        var ownerOfSecond = new ElementBase("Handler", "Class Feature", "Internal", "ID_OWNER_TWO");
        var unrelated = new ElementBase("Aura", "Class Feature", "Internal", "ID_OWNER_NONE");
        ownerOfFirst.RuleElements.Add(first);
        ownerOfSecond.RuleElements.Add(second);
        var all = new[] { ownerOfFirst, ownerOfSecond, unrelated, first, second };

        var scopeOfFirst = CompanionRuleScope.For(first, all);

        scopeOfFirst(ownerOfFirst).Should().BeTrue();
        scopeOfFirst(ownerOfSecond).Should().BeFalse("the other creature's owner must not feed this one");
        scopeOfFirst(unrelated).Should().BeTrue("an unowned bonus keeps its global meaning");

        CompanionRuleScope.For(second, all)(ownerOfSecond).Should().BeTrue();
        CompanionRuleScope.For(second, all)(ownerOfFirst).Should().BeFalse();
    }

    /// <summary>
    /// Every other saved id in the loader is read through the alias table. Reading this one
    /// literally loses the name and portrait of any companion whose template was renamed, and the
    /// per-companion records shadow the legacy record, so there is nothing to fall back to.
    /// </summary>
    [Fact]
    public void ARenamedCompanionTemplateStillFindsItsSavedNameAndPortrait()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection;
        var current = Companion("ID_COMPANION_NEW", "Drake");
        catalog.Add(current);
        ElementIdAliases.Set(new Dictionary<string, string> { ["ID_COMPANION_OLD"] = "ID_COMPANION_NEW" });
        try
        {
            var character = new Character();
            character.SynchronizeCompanions([current]);

            ReadCompanions(character, """
                <build><companions><companion id="ID_COMPANION_OLD" name="Smaug"/></companions></build>
                """);

            character.Companions.Single().CompanionName.Content.Should().Be("Smaug");
        }
        finally
        {
            ElementIdAliases.Clear();
            catalog.Remove(current);
        }
    }

    /// <summary>
    /// ID-less legacy records remain compatible with the original primary-companion behavior.
    /// </summary>
    [Fact]
    public void AnUnmatchableCompanionRecordCanFallBackToAnIdlessLegacyRecord()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var character = new Character();
        character.SynchronizeCompanions([Companion("ID_COMPANION_PRESENT", "Drake")]);

        ReadCompanions(character, """
            <build>
              <companion name="Legacy name"/>
              <companions><companion id="ID_COMPANION_VANISHED" name="Unmatchable"/></companions>
            </build>
            """);

        character.Companions.Single().CompanionName.Content.Should().Be("Legacy name");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownUnrelatedLegacyCompanionMustNotOverwriteCurrentDetails(bool hasCollection)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var character = new Character();
        character.SynchronizeCompanions([Companion("ID_COMPANION_PRESENT", "Drake")]);
        character.Companion.CompanionName.Content = "Current name";
        character.Companion.Portrait.Content = "current.png";
        string collection = hasCollection
            ? "<companions><companion id='ID_COMPANION_VANISHED' name='Old pet'/></companions>" : "";

        ReadCompanions(character, $"""
            <build>
              <companion id="ID_COMPANION_VANISHED" name="Old pet"><portrait location="local">old.png</portrait></companion>
              {collection}
            </build>
            """);

        using var assertions = new FluentAssertions.Execution.AssertionScope();
        character.Companion.CompanionName.Content.Should().Be("Current name");
        character.Companion.Portrait.Content.Should().Be("current.png");
    }

    [Theory]
    [InlineData("ID_COMPANION_NEW")]
    [InlineData("ID_COMPANION_OLD")]
    public void LegacyCompanionIdentityFindsTheMatchingCreatureInsteadOfThePrimary(string savedId)
    {
        TestApplicationContextInstaller.EnsureInstalled();
        var catalog = DataManager.Current.ElementsCollection;
        var current = Companion("ID_COMPANION_NEW", "Drake");
        catalog.Add(current);
        ElementIdAliases.Set(new Dictionary<string, string> { ["ID_COMPANION_OLD"] = current.Id });
        try
        {
            var character = new Character();
            character.SynchronizeCompanions([current, Companion("ID_COMPANION_OTHER", "Owl")]);
            character.Companion.CompanionName.Content = "Other pet";
            character.Companion.Portrait.Content = "other.png";

            ReadCompanions(character, $"""
                <build>
                  <companion id="{savedId}" name="Smaug"><portrait location="local">smaug.png</portrait></companion>
                  <companions><companion id="ID_COMPANION_VANISHED" name="Unmatchable"/></companions>
                </build>
                """);

            character.Companions[0].CompanionName.Content.Should().Be("Smaug");
            character.Companions[0].Portrait.Content.Should().Be("smaug.png");
            character.Companion.CompanionName.Content.Should().Be("Other pet");
            character.Companion.Portrait.Content.Should().Be("other.png");
        }
        finally
        {
            ElementIdAliases.Clear();
            catalog.Remove(current);
        }
    }

    private static void ReadCompanions(Character character, string buildXml)
    {
        var document = new XmlDocument();
        document.LoadXml(buildXml);
        typeof(CharacterFile).GetMethod("ReadCompanionNode",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(new CharacterFile(Path.Combine(Path.GetTempPath(), "Aurora.Tests", "companions.dnd5e")),
                [document.DocumentElement!, character]);
    }

    private static CompanionElement Companion(string id, string name)
    {
        var doc = new XmlDocument();
        doc.LoadXml($"""
            <element name="{name}" type="Companion" source="Internal" id="{id}">
              <setters>
                <set name="strength">10</set><set name="dexterity">10</set>
                <set name="constitution">10</set><set name="intelligence">2</set>
                <set name="wisdom">10</set><set name="charisma">5</set>
                <set name="type">Beast</set><set name="size">Medium</set>
                <set name="alignment">Unaligned</set><set name="challenge">0</set>
                <set name="ac">10</set><set name="hp">4</set><set name="speed">30</set>
              </setters>
            </element>
            """);
        return (CompanionElement)new CompanionElementParser().ParseElement(doc.DocumentElement!);
    }
}
