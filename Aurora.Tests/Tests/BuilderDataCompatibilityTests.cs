using System.Xml;
using Builder.Data;
using Builder.Data.ElementParsers;
using Builder.Data.Elements;
using Builder.Data.Files;
using Builder.Data.Rules;
using Builder.Data.Rules.Parsers;

namespace Aurora.Tests.Tests;

public sealed class BuilderDataCompatibilityTests
{
    [Fact]
    public void RestoredAssembly_PreservesLegacyIdentityAndExportedTypeCount()
    {
        var assembly = typeof(ElementBase).Assembly;

        assembly.GetName().Name.Should().Be("Builder.Data");
        assembly.GetName().Version.Should().Be(new Version(1, 0, 110, 7407));
        assembly.GetExportedTypes().Should().HaveCount(140);
    }

    [Fact]
    public void ParserFactories_DiscoverTheLegacyImplementations()
    {
        string[] expectedElementParsers =
        [
            "Ability Score Improvement",
            "Archetype",
            "Archetype Feature",
            "Armor",
            "Background Feature",
            "Class",
            "Class Feature",
            "Companion",
            "Deity",
            "Dragonmark",
            "Familiar",
            "Familiar Action",
            "Familiar Feature",
            "Familiar Trait",
            "Feat",
            "Feat Feature",
            "Grants",
            "Item",
            "Language",
            "Language Feature",
            "Level",
            "Magic Item",
            "Monster",
            "Multiclass",
            "Option",
            "Proficiency",
            "Race",
            "Racial Trait",
            "Source",
            "Spell",
            "Weapon"
        ];

        ElementParserFactory.GetParsers()
            .Select(parser => parser.ParserType)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal(expectedElementParsers.Order(StringComparer.Ordinal));
        ElementParserFactory.GetRuleParsers()
            .Select(parser => parser.Name)
            .Order(StringComparer.Ordinal)
            .Should()
            .Equal("grant", "select", "stat");
    }

    [Fact]
    public void ElementParser_PreservesSharedSectionsAndDefaults()
    {
        XmlNode node = LoadElement(
            """
            <element name="Arcane Initiate" type="Feat" source="Test Source" id="ID_TEST_FEAT">
              <supports>One, Two, One</supports>
              <prerequisite>Level 4</prerequisite>
              <requirements>strength:score >= 13</requirements>
              <description><p>Hello <strong>world</strong>.</p></description>
              <sheet alt="Arcane Student" display="false" usage="1/Rest" action="Bonus Action">
                <description level="2" usage="2/Rest" action="Action">Sheet text</description>
              </sheet>
              <setters>
                <set name="allow duplicate">true</set>
                <set name="keywords">Fire, Utility</set>
                <set name="custom" override="true" addition=" plus ">  value  </set>
              </setters>
              <equipment name="Initiate Pack"><description><![CDATA[Pack details]]></description></equipment>
              <compendium display="false" />
              <source name="Override Source" id="ID_SOURCE_OVERRIDE">
                <url>https://example.test/source</url>
                <page>42</page>
              </source>
            </element>
            """);

        ElementBase element = new ElementParser().ParseElement(node);

        element.Name.Should().Be("Arcane Initiate");
        element.Type.Should().Be("Feat");
        element.Source.Should().Be("Test Source");
        element.Id.Should().Be("ID_TEST_FEAT");
        element.Supports.Should().Equal("One", "Two");
        element.Prerequisite.Should().Be("Level 4");
        element.Requirements.Should().Be("strength:score >= 13");
        element.Description.Should().Be("<p>Hello <strong>world</strong>.</p>");
        element.AllowDuplicate.Should().BeTrue();
        element.Keywords.Should().Equal("fire", "utility");

        ElementSetters.Setter custom = element.ElementSetters.GetSetter("CUSTOM");
        custom.Value.Should().Be("value");
        custom.AdditionalAttributes.Should().Contain("override", "true");
        custom.AdditionalAttributes.Should().Contain("addition", " plus ");
        element.GetSetterOverrideAttributeValue("custom").Should().BeTrue();
        element.GetSetterAdditionAttribute("custom").Should().Be("plus");

        element.SheetDescription.AlternateName.Should().Be("Arcane Student");
        element.SheetDescription.DisplayOnSheet.Should().BeFalse();
        element.SheetDescription.Usage.Should().Be("1/Rest");
        element.SheetDescription.Action.Should().Be("Bonus Action");
        element.SheetDescription.Should().ContainSingle();
        element.SheetDescription[0].Description.Should().Be("Sheet text");
        element.SheetDescription[0].Level.Should().Be(2);
        element.SheetDescription[0].Usage.Should().Be("2/Rest");
        element.SheetDescription[0].Action.Should().Be("Action");

        element.Equipment.Name.Should().Be("Initiate Pack");
        element.Equipment.Description.Should().Be("Pack details");
        element.IncludeInCompendium.Should().BeFalse();
        element.ElementSource.Source.Should().Be("Override Source");
        element.ElementSource.SourceId.Should().Be("ID_SOURCE_OVERRIDE");
        element.ElementSource.OverrideUrl.Should().Be("https://example.test/source");
        element.ElementSource.Page.Should().Be("42");
    }

    [Fact]
    public void ElementParser_PreservesRuleAndSpellcastingSemantics()
    {
        XmlNode node = LoadElement(
            """
            <element name="Arcane Initiate" type="Feat" source="Test Source" id="ID_TEST_FEAT">
              <rules>
                <grant type="Language" name="ID_LANGUAGE_ELVISH" level="2" requirements="known" />
                <select type="Spell" name="Spell Choice" number="2" level="3" supports="Fire"
                        optional="true" prepared="true" allowReplace="false" />
                <stat name="Strength Maximum" value="-Dexterity Modifier" bonus="base"
                      level="4" minimum="1" maximum="20" merge="true" />
              </rules>
              <spellcasting name="Wizard" ability="Intelligence" prepare="true" allowReplace="true">
                <list known="true">Wizard</list>
                <extend known="false">School of Evocation</extend>
              </spellcasting>
            </element>
            """);

        ElementBase element = new ElementParser().ParseElement(node);

        element.Rules.Should().HaveCount(3);
        var grant = element.Rules[0].Should().BeOfType<GrantRule>().Subject;
        grant.Attributes.Type.Should().Be("Language");
        grant.Attributes.Id.Should().Be("ID_LANGUAGE_ELVISH");
        grant.Attributes.RequiredLevel.Should().Be(2);
        grant.Attributes.Requirements.Should().Be("known");

        var select = element.Rules[1].Should().BeOfType<SelectRule>().Subject;
        select.Attributes.Type.Should().Be("Spell");
        select.Attributes.Name.Should().Be("Spell Choice");
        select.Attributes.Number.Should().Be(2);
        select.Attributes.RequiredLevel.Should().Be(3);
        select.Attributes.Supports.Should().Be("Fire");
        select.Attributes.Optional.Should().BeTrue();
        select.Setters.GetSetter("prepared").Value.Should().Be("true");
        select.Setters.GetSetter("allowReplace").Value.Should().Be("false");

        var statistic = element.Rules[2].Should().BeOfType<StatisticRule>().Subject;
        statistic.Attributes.Name.Should().Be("strength:max");
        statistic.Attributes.Value.Should().Be("-dexterity:modifier");
        statistic.Attributes.Bonus.Should().Be("base");
        statistic.Attributes.Level.Should().Be(4);
        statistic.Attributes.Minimum.Should().Be("1");
        statistic.Attributes.Maximum.Should().Be("20");
        statistic.Attributes.Merge.Should().BeTrue();

        SpellcastingInformation spellcasting = element.SpellcastingInformation;
        spellcasting.Name.Should().Be("Wizard");
        spellcasting.AbilityName.Should().Be("Intelligence");
        spellcasting.Prepare.Should().BeTrue();
        spellcasting.AllowSpellSwap.Should().BeTrue();
        spellcasting.PrepareFromSpellList.Should().BeTrue();
        spellcasting.InitialSupportedSpellsExpression.Supports.Should().Be("Wizard");
        spellcasting.InitialSupportedSpellsExpression.Known.Should().BeTrue();
        spellcasting.ExtendedSupportedSpellsExpressions.Should().ContainSingle();
        spellcasting.ExtendedSupportedSpellsExpressions[0].Supports.Should().Be("School of Evocation");
        spellcasting.GetSpellAttackStatisticName().Should().Be("spellcasting:attack:int");
        spellcasting.GetSpellSaveStatisticName().Should().Be("spellcasting:dc:int");
    }

    [Fact]
    public void SpellParser_PreservesComputedDescriptionsComponentsAndSupports()
    {
        XmlNode node = LoadElement(
            """
            <element name="Royal Flame" type="Spell" source="Test Source" id="ID_TEST_SPELL">
              <description><p>Damage increases. At Higher Levels it burns brighter.</p></description>
              <setters>
                <set name="level">1</set>
                <set name="school" addition="Dunamancy">Evocation</set>
                <set name="time">1 bonus action</set>
                <set name="duration">Concentration, up to 1 minute</set>
                <set name="range">60 feet</set>
                <set name="hasVerbalComponent">true</set>
                <set name="hasSomaticComponent">true</set>
                <set name="hasMaterialComponent">true</set>
                <set name="materialComponent">a feather</set>
                <set name="hasRoyaltyComponent">true</set>
                <set name="royaltyComponent">5</set>
                <set name="isConcentration">true</set>
                <set name="isRitual">true</set>
              </setters>
            </element>
            """);

        var spell = new SpellElementParser().ParseElement(node).Should().BeOfType<Spell>().Subject;

        spell.GetShortDescription().Should().Be("1st-level evocation (ritual, dunamancy)");
        spell.GetComponentsString().Should().Be("V, S, M (a feather)R (5)");
        spell.Supports.Should().Contain(
        [
            "1",
            "Evocation",
            "evocation",
            "Ritual",
            "ID_INTERNAL_SUPPORT_BONUS_ACTION",
            "ID_INTERNAL_SUPPORT_VERBAL",
            "ID_INTERNAL_SUPPORT_SOMATIC",
            "ID_INTERNAL_SUPPORT_MATERIAL",
            "ID_INTERNAL_SUPPORT_ROYALTY",
            "ID_INTERNAL_SUPPORT_RITUAL",
            "ID_INTERNAL_SUPPORT_CONCENTRATION"
        ]);
        spell.Keywords.Should().Contain(["Dunamancy", "1 bonus action", "at higher levels"]);
    }

    [Fact]
    public void SpellParser_ReportsTheFirstMissingRequiredSetter()
    {
        XmlNode node = LoadElement(
            """
            <element name="Incomplete Spell" type="Spell" source="Test" id="ID_INCOMPLETE">
              <setters>
                <set name="level">0</set>
                <set name="school">Evocation</set>
                <set name="time">1 action</set>
                <set name="duration">Instantaneous</set>
              </setters>
            </element>
            """);

        Action parse = () => new SpellElementParser().ParseElement(node);

        parse.Should()
            .Throw<MissingSetterException>()
            .WithMessage("the required setter 'range' is missing on the 'Incomplete Spell (Spell)' element.")
            .Where(exception => exception.RequiredSetterName == "range");
    }

    [Fact]
    public void ElementsFile_LoadsMetadataContentAndVersionRules()
    {
        const string xml =
            """
            <elements app="2" ignore="true">
              <info>
                <name>Compatibility Pack</name>
                <description>Pack description</description>
                <author url="https://example.test/author">Aurora Team</author>
                <update version="1.2.3" revised="2026-07-25">
                  <file name="compatibility.xml" url="https://example.test/compatibility.xml" />
                </update>
              </info>
              <element name="One" type="Feat" source="Test" id="ID_ONE" />
              <append id="ID_ONE"><supports>Extra</supports></append>
            </elements>
            """;
        var file = new ElementsFile(xml);

        file.Load(xml);

        file.MinimumAppVersion.Should().Be(new Version(2, 0));
        file.Ignore.Should().BeTrue();
        file.Info.ContainsInfoNode.Should().BeTrue();
        file.Info.DisplayName.Should().Be("Compatibility Pack");
        file.Info.Description.Should().Be("Pack description");
        file.Info.Author.Should().Be("Aurora Team");
        file.Info.AuthorUrl.Should().Be("https://example.test/author");
        file.Info.Version.Should().Be(new Version(1, 2, 3));
        file.Info.Revised.Should().Be("2026-07-25");
        file.Info.UpdateFilename.Should().Be("compatibility.xml");
        file.Info.UpdateUrl.Should().Be("https://example.test/compatibility.xml");
        file.ElementNodes.Should().ContainSingle();
        file.ExtendNodes.Should().ContainSingle();
        file.MeetsMinimumAppVersionRequirements(new Version(1, 9)).Should().BeFalse();
        file.MeetsMinimumAppVersionRequirements(new Version(2, 0)).Should().BeTrue();

        var newer = new ElementsFile(xml);
        newer.Load(xml.Replace("version=\"1.2.3\"", "version=\"2.0\""));
        file.RequiresUpdate(newer).Should().BeTrue();
        newer.RequiresUpdate(file).Should().BeFalse();
    }

    [Fact]
    public void ElementsFile_PreservesMalformedInfoExceptionBehavior()
    {
        const string xml =
            """
            <elements>
              <info>
                <name>Broken Pack</name>
              </info>
            </elements>
            """;
        var file = new ElementsFile(xml);

        Action load = () => file.Load(xml);

        load.Should().Throw<NullReferenceException>()
            .WithMessage("missing update node in info block");
    }

    [Fact]
    public void ElementCollections_ReturnLegacyShallowFreshCopies()
    {
        var original = new ElementBase("Original", "Feat", "Test", "ID_ORIGINAL");
        original.Supports.Add("Support");
        original.RuleElements.Add(new ElementBase("Child", "Feature", "Test", "ID_CHILD"));
        var collection = new ElementBaseCollection([original]);

        ElementBase fresh = collection.GetFresh("ID_ORIGINAL");

        fresh.Should().NotBeSameAs(original);
        fresh.ElementHeader.Should().BeSameAs(original.ElementHeader);
        fresh.Supports.Should().BeSameAs(original.Supports);
        fresh.RuleElements.Should().BeSameAs(original.RuleElements);
        collection.GetElement("MISSING").Should().BeNull();
        collection.GetFresh("MISSING").Should().BeNull();
    }

    [Fact]
    public void ElementSetters_PreserveCaseInsensitiveLookupAndConversions()
    {
        var setters = new ElementSetters
        {
            new("Count", " 12 "),
            new("Enabled", " true "),
            new("Empty", " ")
        };

        setters.ContainsSetter("count").Should().BeTrue();
        setters.GetSetter("COUNT").Value.Should().Be("12");
        setters.GetSetter("Count").ValueAsInteger().Should().Be(12);
        setters.GetSetter("Enabled").ValueAsBool().Should().BeTrue();
        setters.GetSetter("Empty").ValueAsBool().Should().BeFalse();
        setters.AttemptGetSetterValue("missing", out ElementSetters.Setter? missing).Should().BeFalse();
        missing.Should().BeNull();
    }

    private static XmlNode LoadElement(string xml)
    {
        var document = new XmlDocument();
        document.LoadXml(xml);
        return document.DocumentElement!;
    }
}
