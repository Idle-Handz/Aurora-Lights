using System.Xml;
using Builder.Data;
using Builder.Data.Elements;

namespace Aurora.Tests.Tests;

public sealed class DetachedXmlCopyTests
{
    [Fact]
    public void SourceCopyKeepsItsXmlAndMutableFieldsIndependentWithoutCopyingSiblings()
    {
        var document = CreateDocument();
        var original = new Source
        {
            ElementHeader = new ElementHeader("Book", "Source", "Book", "SOURCE_BOOK"),
            ElementNode = document.DocumentElement!.FirstChild!,
            Description = "Original description"
        };
        original.AlternativeNames.Add("Alternate book");
        original.Supports.Add("Test support");
        original.SourceElement = original;
        original.RuleElements.Add(original);
        string originalXml = document.OuterXml;

        var copy = original.CopyWithDetachedXml();

        copy.ElementNode.OuterXml.Should().Be(original.ElementNode.OuterXml);
        copy.ElementNode.ParentNode.Should().BeNull();
        copy.ElementNode.PreviousSibling.Should().BeNull();
        copy.ElementNode.NextSibling.Should().BeNull();
        copy.ElementNode.OwnerDocument.Should().NotBeSameAs(document);
        copy.ElementNode.OwnerDocument!.SelectNodes("//unrelated")!.Count.Should().Be(0);
        copy.SourceElement.Should().BeSameAs(copy);
        copy.RuleElements.Single().Should().BeSameAs(copy);

        copy.AlternativeNames.Add("Only on copy");
        copy.Supports.Clear();
        copy.Description = "Changed description";
        ((XmlElement)copy.ElementNode).SetAttribute("name", "Changed book");
        copy.ElementNode["description"]!.InnerText = "Changed XML";

        original.AlternativeNames.Should().Equal("Alternate book");
        original.Supports.Should().Equal("Test support");
        original.Description.Should().Be("Original description");
        document.OuterXml.Should().Be(originalXml);
    }

    [Fact]
    public void RepeatedXmlReferencesShareTheSameCopiedNode()
    {
        var node = CreateDocument().DocumentElement!.FirstChild!;
        var original = new[] { new Source { ElementNode = node }, new Source { ElementNode = node } };

        var copy = original.CopyWithDetachedXml();

        copy[0].ElementNode.Should().BeSameAs(copy[1].ElementNode);
        copy[0].ElementNode.Should().NotBeSameAs(node);
    }

    [Fact]
    public void ExplicitDocumentCopyRetainsItsWholeIndependentDocument()
    {
        var original = CreateDocument();

        var copy = original.CopyWithDetachedXml();

        copy.Should().NotBeSameAs(original);
        copy.OuterXml.Should().Be(original.OuterXml);
        copy.DocumentElement!.RemoveChild(copy.DocumentElement.LastChild!);
        original.DocumentElement!.ChildNodes.Count.Should().Be(2);
    }

    [Fact]
    public void EmptyPairedElementsKeepTheirAttributesAndEmptyValue()
    {
        var document = new XmlDocument();
        document.LoadXml("<elements><element><set name=\"url\"></set></element><unrelated /></elements>");
        var original = new Source { ElementNode = document.DocumentElement!.FirstChild! };

        var copy = original.CopyWithDetachedXml();

        // ImportNode may write <set ... /> instead of paired empty tags. Both carry
        // the same setter; source snapshots must preserve its name and empty value.
        var setter = copy.ElementNode["set"]!;
        setter.Attributes.Count.Should().Be(1);
        setter.GetAttribute("name").Should().Be("url");
        setter.InnerText.Should().BeEmpty();
        setter.ChildNodes.Count.Should().Be(0);
        original.ElementNode["set"]!.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void ExistingCopyStillCopiesTheOwnerDocumentGraph()
    {
        var document = CreateDocument();
        var original = new Source { ElementNode = document.DocumentElement!.FirstChild! };

        var copy = original.Copy();

        copy.ElementNode.OwnerDocument.Should().NotBeSameAs(document);
        copy.ElementNode.OwnerDocument!.OuterXml.Should().Be(document.OuterXml);
        copy.ElementNode.ParentNode.Should().NotBeNull();
        copy.ElementNode.NextSibling!.Name.Should().Be("unrelated");
    }

    private static XmlDocument CreateDocument()
    {
        var document = new XmlDocument();
        document.LoadXml("""
            <elements><element id="SOURCE_BOOK" type="Source" name="Book"><description format="html"><![CDATA[<p>Book details</p>]]></description></element><unrelated id="OTHER"><content>Not part of this source</content></unrelated></elements>
            """);
        return document;
    }
}
