using System.Xml.Linq;
using Builder.Presentation.Services;

namespace Aurora.Tests.Tests;

public sealed class UnresolvedCharacterBuildTests
{
    private const string Saved = """
        <build><elements>
          <element id="LEVEL_9" type="Level" multiclass="true" starting="true" class="OLD_RANGER" rndhp="7">
            <element type="Multiclass" name="Multiclass" registered="OLD_RANGER" requiredLevel="9">
              <element id="RANGER_CHILD" type="Class Feature" />
            </element>
          </element>
        </elements><equipment><item id="OLD_ITEM" identifier="one" amount="2"><equipped>true</equipped></item></equipment>
        <sum><element id="OLD_RANGER"/><element id="RANGER_CHILD"/><element id="STALE_SUM"/></sum></build>
        """;

    [Fact]
    public void UnresolvedMulticlassAndSummarySurviveTwoWritesWithoutReassigningTheLevel()
    {
        var saved = XElement.Parse(Saved);
        var recovery = new UnresolvedCharacterBuild(saved, ["OLD_RANGER", "RANGER_CHILD", "STALE_SUM", "OLD_ITEM"], ["LEVEL_9"]);
        var rebuilt = XElement.Parse("<build><elements><element id='LEVEL_9' type='Level'/></elements><equipment/><sum/></build>");
        recovery.MergeInto(rebuilt, ["LEVEL_9"]);
        recovery.MergeInto(rebuilt, ["LEVEL_9"]);
        var level = rebuilt.Element("elements")!.Element("element")!;
        ((string?)level.Attribute("class")).Should().Be("OLD_RANGER");
        ((string?)level.Attribute("multiclass")).Should().Be("true");
        ((string?)level.Attribute("rndhp")).Should().Be("7");
        level.Elements().Should().ContainSingle();
        rebuilt.Element("sum")!.Elements().Select(e => (string?)e.Attribute("id"))
            .Should().BeEquivalentTo(["OLD_RANGER", "RANGER_CHILD", "STALE_SUM"]);
        rebuilt.Element("equipment")!.Elements("item").Should().ContainSingle()
            .Which.Should().BeEquivalentTo(saved.Element("equipment")!.Element("item"));
    }

    /// <summary>
    /// The identifier is what tells two rows of the same item apart, and the loader treats it as
    /// optional. Keying the merge on it alone means a row without one matches nothing and is added
    /// again on every save, so the file grows a copy each time it is written.
    /// </summary>
    [Fact]
    public void AnUnavailableItemWithNoIdentifierIsKeptOnceRatherThanOncePerSave()
    {
        var saved = XElement.Parse("""
            <build><elements/>
              <equipment><item id="OLD_ITEM" amount="2"/><item id="OLD_ITEM" amount="5"/></equipment>
              <sum/></build>
            """);
        var recovery = new UnresolvedCharacterBuild(saved, ["OLD_ITEM"], []);
        var rebuilt = XElement.Parse("<build><elements/><equipment/><sum/></build>");

        recovery.MergeInto(rebuilt, []);
        recovery.MergeInto(rebuilt, []);
        recovery.MergeInto(rebuilt, []);

        rebuilt.Element("equipment")!.Elements("item").Select(e => (string?)e.Attribute("amount"))
            .Should().Equal(["2", "5"], "both rows are kept, and neither is duplicated by a re-save");
    }

    [Fact]
    public void DeliberatelyReplacedChoiceOrRemovedParentDoesNotReturn()
    {
        var recovery = new UnresolvedCharacterBuild(XElement.Parse(Saved), ["OLD_RANGER", "RANGER_CHILD"], ["LEVEL_9"]);
        var replacement = XElement.Parse("""
            <build><elements><element id="LEVEL_9" type="Level" class="NEW_RANGER" multiclass="true">
              <element type="Multiclass" name="Multiclass" registered="NEW_RANGER" requiredLevel="9"/>
            </element></elements><sum/></build>
            """);
        recovery.MergeInto(replacement, ["LEVEL_9", "NEW_RANGER"]);
        replacement.ToString().Should().NotContain("OLD_RANGER").And.NotContain("RANGER_CHILD");
        var removed = XElement.Parse("<build><elements/><sum/></build>");
        recovery.MergeInto(removed, []);
        removed.Element("elements")!.Elements().Should().BeEmpty();
        removed.Element("sum")!.Elements().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnonymousInventoryRowsKeepTheirStateAndMultiplicityAcrossSaves(bool identicalRows)
    {
        var first = XElement.Parse("""
            <item id="MISSING_ITEM" amount="1">
              <equipped>true</equipped><details><name>First item</name><notes>Keep me</notes></details>
            </item>
            """);
        var second = identicalRows ? new XElement(first) : XElement.Parse("""
            <item id="MISSING_ITEM" amount="1">
              <equipped>false</equipped><details><name>Second item</name><notes>Also keep me</notes></details>
            </item>
            """);
        var saved = XElement.Parse("<build><elements/><equipment/><sum/></build>");
        saved.Element("equipment")!.Add(first, second);
        var recovery = new UnresolvedCharacterBuild(saved, ["MISSING_ITEM"], []);
        var rebuilt = XElement.Parse("<build><elements/><equipment/><sum/></build>");
        // An existing row can account for only one of the saved occurrences.
        rebuilt.Element("equipment")!.Add(new XElement(first));

        for (int save = 0; save < 3; save++)
        {
            recovery.MergeInto(rebuilt, []);
            rebuilt.Element("equipment")!.Elements("item").Select(e => e.ToString())
                .Should().Equal(saved.Element("equipment")!.Elements("item").Select(e => e.ToString()));
            rebuilt = XElement.Parse(rebuilt.ToString());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnresolvedCompanionDetailsSurviveSavesUntilTheirChoiceIsReplaced(bool hasCollection)
    {
        var record = XElement.Parse("<companion id='OLD_PET' name='Old pet'><portrait location='local'>old.png</portrait></companion>");
        var saved = XElement.Parse("""
            <build><elements><element id="OWNER" type="Feat Feature">
              <element type="Companion" name="Pet" registered="OLD_PET"/>
            </element></elements><sum/></build>
            """);
        saved.Add(hasCollection ? new XElement("companions", record) : record);
        var recovery = new UnresolvedCharacterBuild(saved, ["OLD_PET"], ["OWNER", "OTHER_PET"]);
        var rebuilt = XElement.Parse("""
            <build><elements><element id="OWNER" type="Feat Feature"/></elements>
              <companion id="OTHER_PET" name="Other pet"/>
              <companions><companion id="OTHER_PET" name="Other pet"/></companions><sum/>
            </build>
            """);
        recovery.MergeInto(rebuilt, ["OWNER", "OTHER_PET"]);
        recovery.MergeInto(rebuilt, ["OWNER", "OTHER_PET"]);
        rebuilt.Element("companion")!.Attribute("name")!.Value.Should().Be("Other pet");
        rebuilt.Element("companions")!.Elements("companion").Should().HaveCount(2);
        XNode.DeepEquals(rebuilt.Element("companions")!.Elements("companion").Last(), record).Should().BeTrue();

        var replaced = XElement.Parse("""
            <build><elements><element id="OWNER" type="Feat Feature">
              <element type="Companion" name="Pet" registered="NEW_PET"/>
            </element></elements><companions><companion id="NEW_PET" name="New pet"/></companions><sum/>
            </build>
            """);
        recovery.MergeInto(replaced, ["OWNER", "NEW_PET"]);
        replaced.ToString().Should().NotContain("OLD_PET").And.NotContain("old.png");
    }
}
