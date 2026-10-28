// Decompiled with JetBrains decompiler
// Type: Builder.Presentation.Views.Development.Xtensions
// Assembly: Aurora Builder, Version=1.0.166.7407, Culture=neutral, PublicKeyToken=null
// MVID: 09D35420-8FA0-4A71-9A21-FF952C48F8A3
// Assembly location: C:\Program Files (x86)\Aurora\Aurora Character Builder\Aurora Builder.exe

using Builder.Data;
using Builder.Data.Rules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;

#nullable disable
namespace Builder.Presentation.Views.Development;

public static class Xtensions
{
  public static XmlNode GenerateElementNode(this ElementBase elementBase)
  {
    XmlDocument xmlDocument = new XmlDocument();
    XmlNode xmlNode1 = xmlDocument.AppendChild((XmlNode) xmlDocument.CreateElement("element"));
    xmlNode1.Attributes.Append(xmlDocument.CreateAttribute("name")).Value = elementBase.Name;
    xmlNode1.Attributes.Append(xmlDocument.CreateAttribute("type")).Value = elementBase.Type;
    xmlNode1.Attributes.Append(xmlDocument.CreateAttribute("source")).Value = elementBase.Source;
    xmlNode1.Attributes.Append(xmlDocument.CreateAttribute("id")).Value = elementBase.Id;
    if (elementBase.HasSupports)
      xmlNode1.AppendChild((XmlNode) xmlDocument.CreateElement("supports")).InnerText = string.Join(",", (IEnumerable<string>) elementBase.Supports);
    if (elementBase.HasDescription)
      xmlNode1.AppendChild((XmlNode) xmlDocument.CreateElement("description")).InnerXml = elementBase.Description;
    if (elementBase.SheetDescription.DisplayOnSheet && elementBase.SheetDescription.Any<ElementSheetDescriptions.SheetDescription>() || elementBase.SheetDescription.DisplayOnSheet && elementBase.SheetDescription.HasAlternateName)
    {
      XmlNode xmlNode2 = xmlNode1.AppendChild((XmlNode) xmlDocument.CreateElement("sheet"));
      if (elementBase.SheetDescription.HasAlternateName)
        xmlNode2.Attributes.Append(xmlDocument.CreateAttribute("alt")).Value = elementBase.SheetDescription.AlternateName;
      if (!elementBase.SheetDescription.DisplayOnSheet)
        xmlNode2.Attributes.Append(xmlDocument.CreateAttribute("display")).Value = "false";
      foreach (ElementSheetDescriptions.SheetDescription sheetDescription in (List<ElementSheetDescriptions.SheetDescription>) elementBase.SheetDescription)
      {
        XmlNode xmlNode3 = xmlNode2.AppendChild((XmlNode) xmlDocument.CreateElement("description"));
        xmlNode3.InnerText = sheetDescription.Description;
        if (sheetDescription.Level > 1)
          xmlNode3.Attributes.Append(xmlDocument.CreateAttribute("level")).Value = sheetDescription.Level.ToString();
      }
    }
    if (elementBase.ElementSetters.Any<ElementSetters.Setter>())
    {
      XmlNode xmlNode4 = xmlNode1.AppendChild((XmlNode) xmlDocument.CreateElement("setters"));
      foreach (ElementSetters.Setter elementSetter in (List<ElementSetters.Setter>) elementBase.ElementSetters)
      {
        XmlNode xmlNode5 = xmlNode4.AppendChild((XmlNode) xmlDocument.CreateElement("set"));
        xmlNode5.Attributes.Append(xmlDocument.CreateAttribute("name")).Value = elementSetter.Name;
        xmlNode5.InnerText = elementSetter.Value;
        foreach (KeyValuePair<string, string> additionalAttribute in elementSetter.AdditionalAttributes)
          xmlNode5.Attributes.Append(xmlDocument.CreateAttribute(additionalAttribute.Key)).Value = additionalAttribute.Value;
      }
    }
    if (elementBase.HasRules)
    {
      XmlNode xmlNode6 = xmlNode1.AppendChild((XmlNode) xmlDocument.CreateElement("rules"));
      foreach (RuleBase rule in elementBase.Rules)
      {
        XmlNode xmlNode7 = xmlNode6.AppendChild((XmlNode) xmlDocument.CreateElement(rule.RuleName));
        if (rule.RuleName == "grant")
        {
          GrantRule grantRule = rule as GrantRule;
          xmlNode7.Attributes.Append(xmlDocument.CreateAttribute("type")).Value = grantRule.Attributes.Type;
          xmlNode7.Attributes.Append(xmlDocument.CreateAttribute("name")).Value = grantRule.Attributes.Name;
          xmlNode7.Attributes.Append(xmlDocument.CreateAttribute("level")).Value = grantRule.Attributes.RequiredLevel.ToString();
        }
      }
    }
    return (XmlNode) xmlDocument.DocumentElement;
  }

  public static string GenerateCleanOutput(this XmlNode node, string indentChars = "\t")
  {
    XmlDocument xmlDocument1 = new XmlDocument();
    xmlDocument1.InnerXml = node.OuterXml;
    XmlDocument xmlDocument2 = xmlDocument1;
    StringBuilder output = new StringBuilder();
    XmlWriterSettings settings = new XmlWriterSettings()
    {
      Indent = true,
      IndentChars = indentChars,
      NewLineChars = Environment.NewLine,
      NewLineHandling = NewLineHandling.Replace
    };
    using (XmlWriter w = XmlWriter.Create(output, settings))
      xmlDocument2.Save(w);
    string[] strArray = output.ToString().Split('\n');
    return output.ToString().Replace(strArray[0] + "\n", "");
  }
}
