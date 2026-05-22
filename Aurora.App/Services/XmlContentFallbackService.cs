using Aurora.Components.Models;
using Builder.Data;
using Builder.Data.Files;
using Builder.Data.Rules;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;

namespace Aurora.App.Services;

/// <summary>
/// Lightweight XML fallback index for cases where the SQLite projection loses
/// selection supports, list items, or structured starting equipment.
/// </summary>
public static class XmlContentFallbackService
{
    private static readonly object Gate = new();
    private static XmlFallbackSnapshot? _snapshot;

    public static void Invalidate()
    {
        lock (Gate)
            _snapshot = null;
    }

    public static IReadOnlyList<ElementBase> GetElementFallbacks(SelectRule rule)
    {
        try
        {
            XmlFallbackSnapshot snapshot = EnsureLoaded();
            if (string.IsNullOrWhiteSpace(rule.Attributes.Type))
                return [];

            if (!snapshot.ByType.TryGetValue(rule.Attributes.Type, out List<XmlFallbackElement>? xmlCandidates))
                return [];

            List<XmlFallbackElement> matched = MatchCandidates(rule, xmlCandidates);
            if (matched.Count == 0)
                return [];

            Dictionary<string, ElementBase> liveById = DataManager.Current.ElementsCollection
                .Where(e => !string.IsNullOrWhiteSpace(e.Id))
                .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            List<ElementBase> result = [];
            int materializedElements = 0;
            int replacedUserOverrides = 0;
            int unresolvedXmlElements = 0;
            foreach (XmlFallbackElement xmlElement in matched)
            {
                bool hasLiveElement = liveById.TryGetValue(xmlElement.Id, out ElementBase? liveElement);
                if (hasLiveElement && !xmlElement.IsUserOverride)
                {
                    result.Add(liveElement!);
                    continue;
                }

                ElementBase? materialized = TryMaterializeElement(xmlElement, replaceExisting: hasLiveElement);
                if (materialized != null)
                {
                    liveById[materialized.Id] = materialized;
                    result.Add(materialized);
                    if (hasLiveElement)
                        replacedUserOverrides++;
                    else
                        materializedElements++;
                }
                else if (hasLiveElement)
                {
                    result.Add(liveElement!);
                    unresolvedXmlElements++;
                }
                else
                {
                    unresolvedXmlElements++;
                }
            }

            if (result.Count > 0)
            {
                DebugLogService.Instance.Log(LogLevel.Warning,
                    $"[XmlContentFallback] recovered {result.Count} option(s) for " +
                    $"'{rule.Attributes.Name ?? rule.Attributes.Type}' from raw XML" +
                    (materializedElements > 0 ? $"; materialized {materializedElements} XML-only element(s)" : "") +
                    (replacedUserOverrides > 0 ? $"; replaced {replacedUserOverrides} live element(s) with custom/user XML" : "") +
                    (unresolvedXmlElements > 0 ? $"; left {unresolvedXmlElements} XML element(s) unresolved" : ""));
            }

            return result
                .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex,
                $"XmlContentFallbackService.GetElementFallbacks: {rule.Attributes.Name ?? rule.Attributes.Type}");
            return [];
        }
    }

    public static IReadOnlyList<ElementOption> GetListFallbackOptions(SelectRule rule)
    {
        try
        {
            string? ownerId = rule.ElementHeader?.Id;
            if (string.IsNullOrWhiteSpace(ownerId))
                return [];

            XmlFallbackSnapshot snapshot = EnsureLoaded();
            if (!snapshot.ById.TryGetValue(ownerId, out XmlFallbackElement? owner))
                return [];

            string? ruleName = rule.Attributes.Name;
            foreach (XmlNode rulesNode in owner.Node.ChildNodes.Cast<XmlNode>().Where(n => n.Name == "rules"))
            {
                foreach (XmlNode selectNode in rulesNode.ChildNodes.Cast<XmlNode>().Where(n => n.Name == "select"))
                {
                    if (!AttributeEquals(selectNode, "type", "List"))
                        continue;
                    if (ruleName != null && !AttributeEquals(selectNode, "name", ruleName))
                        continue;

                    List<ElementOption> items = [];
                    foreach (XmlNode itemNode in selectNode.ChildNodes.Cast<XmlNode>().Where(n => n.Name == "item"))
                    {
                        string text = itemNode.InnerText.Trim();
                        if (string.IsNullOrWhiteSpace(text))
                            continue;

                        string id = itemNode.Attributes?["id"]?.Value ?? (items.Count + 1).ToString(CultureInfo.InvariantCulture);
                        items.Add(new ElementOption(id, text, text, owner.Source, ""));
                    }

                    if (items.Count > 0)
                    {
                        DebugLogService.Instance.Log(LogLevel.Warning,
                            $"[XmlContentFallback] recovered {items.Count} list item(s) for " +
                            $"'{ruleName ?? "List"}' on '{ownerId}' from raw XML");
                        return items;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex,
                $"XmlContentFallbackService.GetListFallbackOptions: {rule.Attributes.Name ?? "List"}");
        }

        return [];
    }

    public static StartingEquipmentBlock GetStartingEquipmentBlock(string? elementId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(elementId))
                return StartingEquipmentBlock.Empty;

            XmlFallbackSnapshot snapshot = EnsureLoaded();
            if (!snapshot.ById.TryGetValue(elementId, out XmlFallbackElement? element))
                return StartingEquipmentBlock.Empty;

            StartingEquipmentBlock block = StartingEquipmentParser.Parse(element.Node);
            if (block.HasContent)
            {
                DebugLogService.Instance.Log(LogLevel.Warning,
                    $"[XmlContentFallback] recovered starting equipment for '{elementId}' from raw XML");
            }

            return block;
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex,
                $"XmlContentFallbackService.GetStartingEquipmentBlock: {elementId}");
            return StartingEquipmentBlock.Empty;
        }
    }

    private static XmlFallbackSnapshot EnsureLoaded()
    {
        lock (Gate)
        {
            if (_snapshot != null)
                return _snapshot;

            _snapshot = LoadSnapshot();
            return _snapshot;
        }
    }

    private static XmlFallbackSnapshot LoadSnapshot()
    {
        Dictionary<string, XmlFallbackElement> byId = new(StringComparer.OrdinalIgnoreCase);
        List<XmlFallbackAppend> appendNodes = [];
        int documentCount = 0;
        int skipped = 0;

        foreach (XmlDocument xmlDocument in DataManager.Current.LoadElementDocumentsFromResource())
        {
            documentCount++;
            LoadDocument(xmlDocument, byId, appendNodes, isUserOverride: false, ref skipped);
        }

        foreach (FileInfo file in GetOrderedCustomFiles())
        {
            try
            {
                ElementsFile ef = ElementsFile.FromFile(file);
                if (ef.Ignore)
                    continue;

                XmlDocument xmlDocument = CreateXmlDocument(file.FullName);
                documentCount++;
                LoadDocument(xmlDocument, byId, appendNodes, IsUserOverrideFile(file), ref skipped);
            }
            catch (Exception ex)
            {
                skipped++;
                DebugLogService.Instance.LogException(ex,
                    $"XmlContentFallbackService: {file.FullName}");
            }
        }

        foreach (XmlFallbackAppend appendNode in appendNodes)
            ApplyAppendNode(appendNode, byId);

        Dictionary<string, List<XmlFallbackElement>> byType = byId.Values
            .GroupBy(e => e.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        DebugLogService.Instance.Log(LogLevel.Info,
            $"[XmlContentFallback] indexed {byId.Count} XML element(s) from {documentCount} document(s); skipped {skipped}");

        return new XmlFallbackSnapshot(byId, byType);
    }

    private static void LoadDocument(
        XmlDocument xmlDocument,
        Dictionary<string, XmlFallbackElement> byId,
        List<XmlFallbackAppend> appendNodes,
        bool isUserOverride,
        ref int skipped)
    {
        if (xmlDocument.DocumentElement == null)
            return;

        foreach (XmlNode node in xmlDocument.DocumentElement.ChildNodes.Cast<XmlNode>())
        {
            if (node.NodeType == XmlNodeType.Comment)
                continue;

            if (node.Name == "element")
            {
                XmlFallbackElement? element = CreateElement(node, isUserOverride);
                if (element == null)
                {
                    skipped++;
                    continue;
                }

                byId[element.Id] = element;
                continue;
            }

            if (node.Name == "append")
                appendNodes.Add(new XmlFallbackAppend(node, isUserOverride));
        }
    }

    private static XmlFallbackElement? CreateElement(XmlNode node, bool isUserOverride)
    {
        string? id = node.Attributes?["id"]?.Value;
        string? name = node.Attributes?["name"]?.Value;
        string? type = node.Attributes?["type"]?.Value;
        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        string source = node.Attributes?["source"]?.Value ?? "";
        return new XmlFallbackElement(id, name, type, source, node, isUserOverride)
        {
            Supports = ExtractSupports(node),
            Requirements = node["requirements"]?.InnerText.Trim() ?? "",
            SpellLevel = ExtractSpellLevel(node),
        };
    }

    private static void ApplyAppendNode(XmlFallbackAppend append, Dictionary<string, XmlFallbackElement> byId)
    {
        XmlNode appendNode = append.Node;
        string? id = appendNode.Attributes?["id"]?.Value;
        if (string.IsNullOrWhiteSpace(id) || !byId.TryGetValue(id, out XmlFallbackElement? target))
            return;

        XmlNode targetNode = target.Node;
        foreach (XmlNode child in appendNode.ChildNodes.Cast<XmlNode>())
        {
            if (child.NodeType == XmlNodeType.Comment)
                continue;

            if (child.Name is "rules" or "setters")
            {
                MergeContainer(targetNode, child);
                continue;
            }

            XmlNode imported = targetNode.OwnerDocument!.ImportNode(child, true);
            targetNode.AppendChild(imported);
        }

        target.Supports = ExtractSupports(targetNode);
        target.Requirements = targetNode["requirements"]?.InnerText.Trim() ?? target.Requirements;
        target.SpellLevel = ExtractSpellLevel(targetNode);
        if (append.IsUserOverride)
            target.IsUserOverride = true;
    }

    private static ElementBase? TryMaterializeElement(XmlFallbackElement xmlElement, bool replaceExisting)
    {
        try
        {
            ElementParser defaultParser = new();
            ElementHeader header = defaultParser.ParseElementHeader(xmlElement.Node);
            ElementParser parser = ElementParserFactory.GetParsers()
                .FirstOrDefault(p => p.ParserType == header.Type) ?? defaultParser;

            ElementBase element = parser.ParseElement(xmlElement.Node);
            return UpsertLiveElement(element, replaceExisting);
        }
        catch (Exception ex)
        {
            DebugLogService.Instance.LogException(ex,
                $"XmlContentFallbackService.TryMaterializeElement: {xmlElement.Id}");
            return null;
        }
    }

    private static ElementBase UpsertLiveElement(ElementBase element, bool replaceExisting)
    {
        ElementBaseCollection collection = DataManager.Current.ElementsCollection;
        ElementBase? existing = collection
            .FirstOrDefault(e => e.Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            if (!replaceExisting)
                return existing;

            collection.Remove(existing);
        }

        collection.Add(element);
        return element;
    }

    private static void MergeContainer(XmlNode targetNode, XmlNode sourceContainer)
    {
        XmlNode? targetContainer = targetNode.ChildNodes
            .Cast<XmlNode>()
            .FirstOrDefault(n => n.Name == sourceContainer.Name);

        if (targetContainer == null)
        {
            XmlNode imported = targetNode.OwnerDocument!.ImportNode(sourceContainer, true);
            targetNode.AppendChild(imported);
            return;
        }

        foreach (XmlNode child in sourceContainer.ChildNodes.Cast<XmlNode>())
        {
            XmlNode imported = targetNode.OwnerDocument!.ImportNode(child, true);
            targetContainer.AppendChild(imported);
        }
    }

    private static List<XmlFallbackElement> MatchCandidates(
        SelectRule rule,
        IReadOnlyList<XmlFallbackElement> candidates)
    {
        if (!rule.Attributes.ContainsSupports())
            return candidates.ToList();

        if (rule.Attributes.Type?.Equals("Spell", StringComparison.OrdinalIgnoreCase) == true)
            return MatchSpellCandidates(rule, candidates);

        string supportsExpression = rule.Attributes.Supports ?? "";
        HashSet<string> ids = ExtractIds(supportsExpression);
        if (ids.Count > 0)
        {
            List<XmlFallbackElement> byId = candidates
                .Where(e => ids.Contains(e.Id) || e.Supports.Any(s => ids.Contains(s)))
                .ToList();
            if (byId.Count > 0)
                return byId;
        }

        List<string> terms = ExtractSupportTerms(supportsExpression);
        if (terms.Count == 0)
            return [];

        return candidates
            .Where(e => e.Supports.Any(s => terms.Any(t => ContainsToken(s, t))))
            .ToList();
    }

    private static List<XmlFallbackElement> MatchSpellCandidates(
        SelectRule rule,
        IReadOnlyList<XmlFallbackElement> candidates)
    {
        string? spellcastingName = ResolveSpellcastingName(rule);
        if (string.IsNullOrWhiteSpace(spellcastingName))
            return [];

        bool isCantrip = IsCantripRule(rule);
        int? exactSpellLevel = ResolveExactSpellLevel(rule);
        string supportsExpression = rule.Attributes.Supports ?? "";
        bool usesSlotRange = supportsExpression.Contains("$(spellcasting:slots)", StringComparison.OrdinalIgnoreCase);
        int maxSpellLevel = 9;
        if (!isCantrip && usesSlotRange)
            maxSpellLevel = ResolveMaxCastableSpellLevel(spellcastingName);

        return candidates
            .Where(e => e.Supports.Any(s => ContainsToken(s, spellcastingName)))
            .Where(e =>
            {
                if (isCantrip)
                    return e.SpellLevel == 0;
                if (exactSpellLevel.HasValue && !usesSlotRange)
                    return e.SpellLevel == exactSpellLevel.Value;
                return e.SpellLevel > 0 && e.SpellLevel <= maxSpellLevel;
            })
            .ToList();
    }

    private static int? ResolveExactSpellLevel(SelectRule rule)
    {
        if (!rule.Attributes.ContainsSupports())
            return null;

        Match match = Regex.Match(rule.Attributes.Supports ?? "", @"(^|[,\s])([1-9])($|[,\s])");
        return match.Success
            ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture)
            : null;
    }

    private static string? ResolveSpellcastingName(SelectRule rule)
    {
        if (rule.Attributes.ContainsSpellcastingName())
            return rule.Attributes.SpellcastingName;

        if (!rule.Attributes.ContainsSupports())
            return null;

        string supports = Regex.Replace(rule.Attributes.Supports ?? "", @"\$\([^)]*\)", " ");
        Match firstWord = Regex.Match(supports, @"[A-Za-z][A-Za-z0-9 ]+");
        string value = firstWord.Value.Trim(' ', ',');
        return string.IsNullOrWhiteSpace(value) || int.TryParse(value, out _) ? null : value;
    }

    private static bool IsCantripRule(SelectRule rule)
    {
        if (rule.Attributes.Name?.Contains("Cantrip", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        string supports = rule.Attributes.Supports ?? "";
        if (supports.Contains("Cantrip", StringComparison.OrdinalIgnoreCase))
            return true;

        return Regex.IsMatch(supports, @"(^|[,\s])0($|[,\s])");
    }

    private static int ResolveMaxCastableSpellLevel(string spellcastingClassName)
    {
        try
        {
            var cm = CharacterManager.Current;
            if (cm.Status.HasMulticlassSpellSlots)
            {
                dynamic mss = cm.Character.MulticlassSpellSlots;
                int[] slots =
                {
                    0, (int)mss.Slot1, (int)mss.Slot2, (int)mss.Slot3,
                    (int)mss.Slot4, (int)mss.Slot5, (int)mss.Slot6,
                    (int)mss.Slot7, (int)mss.Slot8, (int)mss.Slot9,
                };
                for (int i = 9; i >= 1; i--)
                    if (slots[i] > 0)
                        return i;
            }

            var stats = cm.StatisticsCalculator.StatisticValues;
            var info = cm.GetSpellcastingInformations()
                .FirstOrDefault(i => i.Name.Equals(spellcastingClassName, StringComparison.OrdinalIgnoreCase));
            if (info == null)
                return 9;

            int maxLevel = 0;
            for (int level = 1; level <= 9; level++)
            {
                try
                {
                    if (stats.GetValue(info.GetSlotStatisticName(level)) > 0)
                        maxLevel = level;
                }
                catch
                {
                    // Ignore missing slot statistics for partial spellcasting records.
                }
            }

            return maxLevel > 0 ? maxLevel : 9;
        }
        catch
        {
            return 9;
        }
    }

    private static HashSet<string> ExtractIds(string expression) =>
        Regex.Matches(expression, @"ID_[A-Za-z0-9_]+")
            .Cast<Match>()
            .Select(m => m.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static List<string> ExtractSupportTerms(string expression)
    {
        List<string> terms = [];
        foreach (string id in ExtractIds(expression))
        {
            if (id.StartsWith("ID_INTERNAL_SUPPORT_", StringComparison.OrdinalIgnoreCase))
            {
                foreach (string part in id["ID_INTERNAL_SUPPORT_".Length..].Split('_'))
                {
                    if (part.Length > 2)
                        terms.Add(part);
                }
            }
        }

        string withoutMacros = Regex.Replace(expression, @"\$\([^)]*\)", " ");
        string withoutIds = Regex.Replace(withoutMacros, @"ID_[A-Za-z0-9_]+", " ");
        terms.AddRange(Regex.Split(withoutIds, @"[,\|\&\!\(\)\[\];:]+")
            .Select(t => t.Trim())
            .Where(t => t.Length > 1)
            .Where(t => !int.TryParse(t, out _))
            .Where(t => !IsIgnoredSupportTerm(t)));

        return terms
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsIgnoredSupportTerm(string term) =>
        term.Equals("true", StringComparison.OrdinalIgnoreCase) ||
        term.Equals("false", StringComparison.OrdinalIgnoreCase) ||
        term.Equals("spellcasting", StringComparison.OrdinalIgnoreCase) ||
        term.Equals("list", StringComparison.OrdinalIgnoreCase) ||
        term.Equals("slots", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsToken(string value, string token)
    {
        if (value.Equals(token, StringComparison.OrdinalIgnoreCase))
            return true;

        return value
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(part => part.Equals(token, StringComparison.OrdinalIgnoreCase) ||
                         part.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ExtractSupports(XmlNode node)
    {
        return node.ChildNodes.Cast<XmlNode>()
            .Where(n => n.Name == "supports")
            .SelectMany(n => n.InnerText.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ExtractSpellLevel(XmlNode node)
    {
        XmlNode? setters = node["setters"];
        if (setters == null)
            return 0;

        foreach (XmlNode set in setters.ChildNodes.Cast<XmlNode>().Where(n => n.Name == "set"))
        {
            if (!AttributeEquals(set, "name", "level"))
                continue;

            string value = set.Attributes?["value"]?.Value ?? set.InnerText;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
                return level;
        }

        return 0;
    }

    private static bool AttributeEquals(XmlNode node, string attributeName, string expected) =>
        string.Equals(node.Attributes?[attributeName]?.Value, expected, StringComparison.OrdinalIgnoreCase);

    private static XmlDocument CreateXmlDocument(string filePath)
    {
        XmlDocument xmlDocument = new();
        xmlDocument.Load(filePath);
        return xmlDocument;
    }

    private static List<FileInfo> GetOrderedCustomFiles()
    {
        List<FileInfo> all = GetOrderedCustomFiles(DataManager.Current.UserDocumentsCustomElementsDirectory);

        string legacy = ApplicationContext.Current.Settings.AdditionalCustomDirectory;
        if (!string.IsNullOrWhiteSpace(legacy) && Directory.Exists(legacy))
            all.AddRange(GetOrderedCustomFiles(legacy));

        foreach (string dir in ApplicationContext.Current.Settings.AdditionalCustomDirectories)
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                continue;
            if (dir.Equals(DataManager.Current.UserDocumentsCustomElementsDirectory, StringComparison.OrdinalIgnoreCase))
                continue;
            if (dir.Equals(legacy, StringComparison.OrdinalIgnoreCase))
                continue;
            all.AddRange(GetOrderedCustomFiles(dir));
        }

        return all;
    }

    private static List<FileInfo> GetOrderedCustomFiles(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return [];

        try
        {
            List<FileInfo> files = GetFiles(path, "*.xml");
            List<FileInfo> includedFiles = [];
            List<FileInfo> list = files.Where(x => !IsPathInDirectory(x.FullName, Path.Combine(path, "ignore"))).ToList();

            includedFiles.AddRange(list.Where(x => IsPathInDirectory(x.FullName, Path.Combine(path, "srd"))));
            includedFiles.AddRange(list.Where(x => IsPathInDirectory(x.FullName, Path.Combine(path, "system-reference-document"))));
            list.RemoveAll(info => includedFiles.Contains(info));

            includedFiles.AddRange(list.Where(x => IsPathInDirectory(x.FullName, Path.Combine(path, "core"))));
            includedFiles.AddRange(list.Where(x => IsPathInDirectory(x.FullName, Path.Combine(path, "supplements"))));
            includedFiles.AddRange(list.Where(x => IsPathInDirectory(x.FullName, Path.Combine(path, "unearthed-arcana"))));
            includedFiles.AddRange(list.Where(x => IsPathInDirectory(x.FullName, Path.Combine(path, "third-party"))));
            includedFiles.AddRange(list.Where(x => IsPathInDirectory(x.FullName, Path.Combine(path, "homebrew"))));
            list.RemoveAll(info => includedFiles.Contains(info));

            List<FileInfo> user = list.Where(x => x.Directory != null && IsPathInDirectory(x.FullName, Path.Combine(path, "user")) && x.Directory.Name.Equals("user")).ToList();
            list.RemoveAll(info => user.Contains(info));

            List<FileInfo> userIndices = list.Where(x => x.Directory != null && IsPathInDirectory(x.FullName, Path.Combine(path, "user"))).ToList();
            list.RemoveAll(info => userIndices.Contains(info));

            List<FileInfo> root = list.Where(x => x.DirectoryName != null && PathsEqual(x.DirectoryName, path)).ToList();
            list.RemoveAll(info => root.Contains(info));

            includedFiles.AddRange(list);
            includedFiles.AddRange(userIndices);
            includedFiles.AddRange(root);
            includedFiles.AddRange(user);
            return includedFiles;
        }
        catch
        {
            return GetFiles(path, "*.xml");
        }
    }

    private static bool IsUserOverrideFile(FileInfo file)
    {
        for (DirectoryInfo? directory = file.Directory; directory != null; directory = directory.Parent)
        {
            if (directory.Name.Equals("user", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static List<FileInfo> GetFiles(string directory, string pattern = "*.*", bool includeSubdirectories = true)
    {
        if (!Directory.Exists(directory))
            return [];

        List<FileInfo> files = Directory.GetFiles(directory, pattern)
            .Select(f => new FileInfo(f))
            .ToList();
        if (includeSubdirectories)
        {
            foreach (string subdirectory in Directory.GetDirectories(directory))
                files.AddRange(GetFiles(subdirectory, pattern));
        }
        return files;
    }

    private static bool IsPathInDirectory(string filePath, string directoryPath)
    {
        string file = NormalizePath(filePath);
        string directory = NormalizePath(directoryPath) + Path.DirectorySeparatorChar;
        return file.StartsWith(directory, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool PathsEqual(string left, string right) =>
        string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase);

    private sealed record XmlFallbackSnapshot(
        Dictionary<string, XmlFallbackElement> ById,
        Dictionary<string, List<XmlFallbackElement>> ByType);

    private sealed record XmlFallbackAppend(XmlNode Node, bool IsUserOverride);

    private sealed class XmlFallbackElement(
        string id,
        string name,
        string type,
        string source,
        XmlNode node,
        bool isUserOverride)
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public string Type { get; } = type;
        public string Source { get; } = source;
        public XmlNode Node { get; } = node;
        public bool IsUserOverride { get; set; } = isUserOverride;
        public IReadOnlyList<string> Supports { get; set; } = [];
        public string Requirements { get; set; } = "";
        public int SpellLevel { get; set; }
    }
}
