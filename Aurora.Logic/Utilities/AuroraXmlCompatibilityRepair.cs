using System.Xml;

namespace Builder.Presentation.Utilities;

public static class AuroraXmlCompatibilityRepair
{
  private static readonly Dictionary<string, string> AttributeNameFixes =
    new(StringComparer.OrdinalIgnoreCase)
    {
      ["allowreplace"] = "allowReplace",
      ["know"] = "known",
      ["spellcastin"] = "spellcasting",
      ["spellcaster"] = "spellcasting",
      ["suports"] = "supports"
    };

  public static void RepairDocument(XmlDocument document)
  {
    if (document.DocumentElement == null)
      return;

    RepairNode(document.DocumentElement);
  }

  public static void RepairNode(XmlNode? node)
  {
    if (node == null)
      return;

    if (node.NodeType == XmlNodeType.Element)
    {
      RepairAttributeNames(node);
      RepairDashCost(node);
    }

    foreach (XmlNode child in node.ChildNodes)
      RepairNode(child);
  }

  private static void RepairAttributeNames(XmlNode node)
  {
    if (node.Attributes == null || node.Attributes.Count == 0)
      return;

    List<XmlAttribute> attributes = node.Attributes.Cast<XmlAttribute>().ToList();
    foreach (XmlAttribute attribute in attributes)
    {
      if (!AttributeNameFixes.TryGetValue(attribute.Name, out string? correctedName))
        continue;

      if (attribute.Name.Equals(correctedName, StringComparison.Ordinal))
        continue;

      if (node.Attributes[correctedName] != null)
      {
        node.Attributes.Remove(attribute);
        continue;
      }

      XmlAttribute corrected = node.OwnerDocument!.CreateAttribute(correctedName);
      corrected.Value = attribute.Value;
      node.Attributes.Remove(attribute);
      node.Attributes.Append(corrected);
    }
  }

  private static void RepairDashCost(XmlNode node)
  {
    if (!node.Name.Equals("set", StringComparison.OrdinalIgnoreCase) || node.Attributes == null)
      return;

    XmlAttribute? name = node.Attributes["name"];
    if (name == null || !name.Value.Equals("cost", StringComparison.OrdinalIgnoreCase))
      return;

    string value = (node.InnerText ?? string.Empty).Trim();
    if (value != "-" && value != "\u2014" && value != "\u2013")
      return;

    if (node.Attributes["currency"] == null)
    {
      XmlAttribute currency = node.OwnerDocument!.CreateAttribute("currency");
      currency.Value = "gp";
      node.Attributes.Append(currency);
    }

    node.InnerText = "0";
  }
}
