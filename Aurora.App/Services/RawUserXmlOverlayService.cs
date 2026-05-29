using Builder.Data;
using Builder.Data.Extensions;
using Builder.Data.Files;
using Builder.Data.Rules;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using System.Xml;

namespace Aurora.App.Services;

internal sealed record RawUserXmlOverlayResult(
    int FileCount,
    int ElementCount,
    int AppendCount,
    int SkippedCount);

/// <summary>
/// Applies local user XML on top of the SQLite content snapshot.
/// Files under custom/user are intentionally treated as immediate local overrides
/// and do not require the content database to be rebuilt.
/// </summary>
internal static class RawUserXmlOverlayService
{
    public static RawUserXmlOverlayResult ApplyTo(ElementBaseCollection target)
    {
        List<FileInfo> files = GetUserXmlFiles();
        if (files.Count == 0)
            return new RawUserXmlOverlayResult(0, 0, 0, 0);

        List<ElementParser> parsers = ElementParserFactory.GetParsers().ToList();
        ElementParser defaultParser = new();
        ElementParser currentParser = new();
        List<XmlNode> appendNodes = [];

        int parsedElements = 0;
        int skipped = 0;

        foreach (FileInfo file in files)
        {
            try
            {
                ElementsFile elementsFile = ElementsFile.FromFile(file);
                if (elementsFile.Ignore)
                    continue;

                XmlDocument xmlDocument = CreateXmlDocument(file.FullName);
                AuroraXmlCompatibilityRepair.RepairDocument(xmlDocument);
                if (xmlDocument.DocumentElement == null)
                    continue;

                foreach (XmlNode node in xmlDocument.DocumentElement.ChildNodes.Cast<XmlNode>())
                {
                    if (node.NodeType == XmlNodeType.Comment)
                        continue;

                    if (node.Name == "element")
                    {
                        try
                        {
                            ElementHeader header = currentParser.ParseElementHeader(node);
                            if (currentParser.ParserType != header.Type)
                                currentParser = parsers.FirstOrDefault(p => p.ParserType == header.Type) ?? defaultParser;

                            ElementBase element = currentParser.ParseElement(node);
                            UpsertElement(target, element);
                            parsedElements++;
                        }
                        catch (Exception ex)
                        {
                            skipped++;
                            DebugLogService.Instance.Warn(
                                $"RawUserXmlOverlay: skipped element in {file.FullName}: {ex.GetType().Name}: {ex.Message}");
                        }

                        continue;
                    }

                }

                appendNodes.AddRange(elementsFile.ExtendNodes.Cast<XmlNode>());
            }
            catch (Exception ex)
            {
                skipped++;
                DebugLogService.Instance.LogException(ex, $"RawUserXmlOverlay: {file.FullName}");
            }
        }

        int appliedAppends = ApplyAppendNodes(appendNodes, target, currentParser, defaultParser, parsers, ref skipped);

        var result = new RawUserXmlOverlayResult(files.Count, parsedElements, appliedAppends, skipped);
        DebugLogService.Instance.Info(
            "RawUserXmlOverlay: applied custom/user XML.",
            $"files={result.FileCount}, elements={result.ElementCount}, appends={result.AppendCount}, skipped={result.SkippedCount}");
        return result;
    }

    private static void UpsertElement(ElementBaseCollection collection, ElementBase element)
    {
        ElementBase? existing = collection.FirstOrDefault(x => x.Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            collection.Remove(existing);
        collection.Add(element);
    }

    private static int ApplyAppendNodes(
        IEnumerable<XmlNode> appendNodes,
        ElementBaseCollection target,
        ElementParser currentParser,
        ElementParser defaultParser,
        List<ElementParser> parsers,
        ref int skipped)
    {
        int applied = 0;
        foreach (XmlNode appendNode in appendNodes)
        {
            AuroraXmlCompatibilityRepair.RepairNode(appendNode);
            if (!appendNode.ContainsAttribute("id"))
                continue;

            string appendId = appendNode.GetAttributeValue("id");
            string appendType = appendNode.ContainsAttribute("type") ? appendNode.GetAttributeValue("type") : string.Empty;
            if (currentParser.ParserType != appendType)
                currentParser = parsers.FirstOrDefault(p => p.ParserType == appendType) ?? defaultParser;

            ElementBase? existing = target.FirstOrDefault(x => x.Id.Equals(appendId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                skipped++;
                DebugLogService.Instance.Warn($"RawUserXmlOverlay: unable to append to missing element '{appendId}'.");
                continue;
            }

            try
            {
                ElementBase extension = currentParser.ParseElement(appendNode, existing.ElementHeader);
                bool updated = false;

                if (extension.HasSupports)
                {
                    foreach (string support in extension.Supports)
                    {
                        if (!existing.Supports.Contains(support))
                            existing.Supports.Add(support);
                    }
                    updated = true;
                }

                if (extension.ElementSetters.Any())
                {
                    foreach (ElementSetters.Setter setter in extension.ElementSetters)
                    {
                        if (!existing.ElementSetters.ContainsSetter(setter.Name))
                            existing.ElementSetters.Add(setter);
                    }
                    updated = true;
                }

                if (extension.HasRules)
                {
                    existing.Rules.AddRange(extension.Rules.Cast<RuleBase>());
                    updated = true;
                }

                if (extension.HasSpellcastingInformation && !existing.HasSpellcastingInformation)
                {
                    existing.SpellcastingInformation = extension.SpellcastingInformation;
                    updated = true;
                }

                if (updated)
                {
                    existing.IsExtended = true;
                    applied++;
                }
            }
            catch (Exception ex)
            {
                skipped++;
                DebugLogService.Instance.Warn(
                    $"RawUserXmlOverlay: failed append '{appendId}': {ex.GetType().Name}: {ex.Message}");
            }
        }

        return applied;
    }

    private static List<FileInfo> GetUserXmlFiles()
    {
        string customDirectory = DataManager.Current.UserDocumentsCustomElementsDirectory;
        if (string.IsNullOrWhiteSpace(customDirectory))
            return [];

        string userDirectory = Path.Combine(customDirectory, "user");
        if (!Directory.Exists(userDirectory))
            return [];

        return Directory.GetFiles(userDirectory, "*.xml", SearchOption.AllDirectories)
            .Select(path => new FileInfo(path))
            .OrderBy(file => IsDirectChild(file, userDirectory) ? 1 : 0)
            .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsDirectChild(FileInfo file, string parentDirectory) =>
        file.DirectoryName != null &&
        string.Equals(
            Path.GetFullPath(file.DirectoryName).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(parentDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static XmlDocument CreateXmlDocument(string filePath)
    {
        XmlDocument xmlDocument = new();
        xmlDocument.Load(filePath);
        return xmlDocument;
    }
}
