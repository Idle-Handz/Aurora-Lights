using Aurora.Tests.Helpers;
using Builder.Data;
using Builder.Data.Elements;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Services.Sources;

namespace Aurora.Tests.Tests;

public sealed class SourcesManagerCatalogTests : IDisposable
{
    private readonly ElementBaseCollection _catalog;
    private readonly ElementBase[] _originalCatalog;
    private readonly string _originalDefaults;

    public SourcesManagerCatalogTests()
    {
        TestApplicationContextInstaller.EnsureInstalled();
        _catalog = DataManager.Current.ElementsCollection;
        _originalCatalog = _catalog.ToArray();
        _originalDefaults = Builder.Presentation.ApplicationContext.Current.Settings.DefaultSourceRestrictions;
        _catalog.Clear();
    }

    public void Dispose()
    {
        _catalog.Clear();
        _catalog.AddRange(_originalCatalog);
        Builder.Presentation.ApplicationContext.Current.Settings.DefaultSourceRestrictions = _originalDefaults;
    }

    [Fact]
    public void MembershipIgnoresNameCaseAndUsesFirstSourceInReleaseOrder()
    {
        // Insertion order differs from the release-date order used by SourceItems.
        _catalog.Add(MakeSource("Shared Book", "ID_LATER", "2025-01-01"));
        _catalog.Add(MakeSource("SHARED BOOK", "ID_EARLIER", "2014-01-01"));
        _catalog.Add(MakeSource("Other Book", "ID_OTHER"));
        _catalog.Add(MakeElement("ID_FIRST", "shared book"));
        _catalog.Add(MakeElement("ID_SECOND", "Shared Book"));
        _catalog.Add(MakeElement("ID_OTHER_ELEMENT", "OTHER BOOK"));

        var manager = new SourcesManager();

        manager.SourceItems.Single(item => item.Source.Id == "ID_EARLIER")
            .Elements.Select(header => header.Id).Should().Equal("ID_FIRST", "ID_SECOND");
        manager.SourceItems.Single(item => item.Source.Id == "ID_LATER").Elements.Should().BeEmpty();
        manager.SourceItems.Single(item => item.Source.Id == "ID_OTHER")
            .Elements.Select(header => header.Id).Should().Equal("ID_OTHER_ELEMENT");
        manager.SourceGroups.Should().NotContain(group => group.Name == SourcesManager.UndefinedSources);
    }

    [Fact]
    public void UndefinedNamesRetainFirstSeenOrderAndSpellingWithoutTrimming()
    {
        _catalog.Add(MakeSource("Known Book", "ID_KNOWN"));
        string[] labels = ["Zulu", "alpha", "zULU", "ALPHA", " Zulu ", " Known Book ", "known book", "", ""];
        for (int index = 0; index < labels.Length; index++)
            _catalog.Add(MakeElement($"ID_ELEMENT_{index}", labels[index]));

        var manager = new SourcesManager();

        manager.SourceGroups.Single(group => group.Name == SourcesManager.UndefinedSources)
            .Sources.Select(item => item.Source.Name)
            .Should().Equal("Zulu", "alpha", " Zulu ", " Known Book ", "");
        manager.SourceItems.Single(item => item.Source.Id == "ID_KNOWN")
            .Elements.Select(header => header.Id).Should().Equal("ID_ELEMENT_6");
    }

    [Theory]
    [InlineData("Source")]
    [InlineData("Internal")]
    [InlineData("Core")]
    [InlineData("Ability Score Improvement")]
    [InlineData("Level")]
    [InlineData("Multiclass")]
    [InlineData("Skill")]
    [InlineData("Support")]
    public void InfrastructureElementTypesDoNotCreateMembershipOrUndefinedNames(string type)
    {
        _catalog.Add(MakeSource("Known Book", "ID_KNOWN"));
        foreach (string label in new[] { "Known Book", "Undeclared Book" })
        {
            string id = "ID_EXCLUDED_" + label;
            ElementBase element = type == "Source"
                ? new Source { ElementHeader = new ElementHeader(id, type, label, id), IsOfficialContent = true }
                : MakeElement(id, label, type);
            _catalog.Add(element);
        }

        var manager = new SourcesManager();

        manager.SourceItems.Should().OnlyContain(item => item.Elements.Count == 0);
        manager.SourceGroups.Should().NotContain(group => group.Name == SourcesManager.UndefinedSources);
    }

    [Fact]
    public void ElementTypeExclusionsRetainTheirCaseSensitiveSemantics()
    {
        _catalog.Add(MakeSource("Known Book", "ID_KNOWN"));
        _catalog.Add(MakeElement("ID_LOWERCASE_TYPE", "Known Book", "skill"));

        var manager = new SourcesManager();

        manager.SourceItems.Single(item => item.Source.Id == "ID_KNOWN")
            .Elements.Select(header => header.Id).Should().Equal("ID_LOWERCASE_TYPE");
    }

    [Theory]
    [InlineData("Core")]
    [InlineData("Internal")]
    [InlineData("Aurora Legacy Essentials")]
    [InlineData(" aurora essentials ")]
    public void RequiredSourceLabelsAreVisibleButDoNotCreateRestrictableMembership(string label)
    {
        _catalog.Add(MakeSource(label, "ID_REQUIRED"));
        _catalog.Add(MakeElement("ID_REQUIRED_ELEMENT", label));

        var manager = new SourcesManager();

        var required = manager.SourceGroups.Single(group => group.Name == SourcesManager.RequiredGroupName)
            .Sources.Single(item => item.Source.Id == "ID_REQUIRED");
        required.Elements.Should().BeEmpty();
        required.AllowUnchecking.Should().BeFalse();
        required.IsChecked.Should().BeTrue();
        manager.SourceGroups.Should().NotContain(group => group.Name == SourcesManager.UndefinedSources);
    }

    [Fact]
    public void RepeatedRefreshReplacesMembershipAndRetainsDeclaredAndUndefinedRestrictions()
    {
        _catalog.Add(MakeSource("Known Book", "ID_KNOWN"));
        var oldElement = MakeElement("ID_OLD", "Known Book");
        _catalog.Add(oldElement);
        _catalog.Add(MakeElement("ID_UNDEFINED", "Missing Book"));
        var manager = new SourcesManager();
        manager.Load(["ID_KNOWN", "Missing Book"]);

        _catalog.Remove(oldElement);
        _catalog.Add(MakeElement("ID_NEW", "KNOWN BOOK"));
        manager.Refresh();
        manager.Refresh();

        manager.RestrictedSources.Select(item => item.Source.Id)
            .Should().BeEquivalentTo("ID_KNOWN", "Missing Book");
        manager.SourceItems.Single(item => item.Source.Id == "ID_KNOWN")
            .Elements.Select(header => header.Id).Should().Equal("ID_NEW");
        manager.GetRestrictedElementIds().Should().Equal("ID_NEW");
        manager.SourceGroups.Single(group => group.Name == SourcesManager.UndefinedSources)
            .Sources.Select(item => item.Source.Name).Should().Equal("Missing Book");
    }

    private static Source MakeSource(string name, string id, string releaseDate = "2020-01-01") => new()
    {
        ElementHeader = new ElementHeader(name, "Source", "Core", id),
        IsOfficialContent = true,
        ReleaseDate = releaseDate
    };

    private static ElementBase MakeElement(string id, string source, string type = "Feature") => new()
    {
        ElementHeader = new ElementHeader(id, type, source, id)
    };
}
