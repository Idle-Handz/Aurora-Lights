using Builder.Data;
using Builder.Data.Elements;
using Builder.Data.Extensions;
using Builder.Data.Files;
using Builder.Presentation;
using Builder.Presentation.Services.Data;
using Builder.Presentation.Utilities;
using System.Xml;

namespace Aurora.App.Services;

public enum ContentDatabaseParityStatus
{
    Healthy,
    Warning,
    Error
}

public sealed record ContentDatabaseParityMismatch(string Key, int XmlCount, int DatabaseCount);

public sealed record ContentDatabaseParityReport(
    bool Success,
    string? FailureReason,
    int XmlElementCount,
    int DatabaseElementCount,
    int XmlSkippedElements,
    int DatabaseSkippedElements,
    int MissingInDatabaseCount,
    int MissingInXmlCount,
    IReadOnlyList<string> MissingInDatabaseSample,
    IReadOnlyList<string> MissingInXmlSample,
    IReadOnlyList<ContentDatabaseParityMismatch> TypeMismatches,
    IReadOnlyList<ContentDatabaseParityMismatch> SourceMismatches)
{
    public int TotalMismatchCount =>
        MissingInDatabaseCount +
        MissingInXmlCount +
        TypeMismatches.Count +
        SourceMismatches.Count;

    public ContentDatabaseParityStatus Status =>
        !Success
            ? ContentDatabaseParityStatus.Error
            : TotalMismatchCount > 0
                ? ContentDatabaseParityStatus.Warning
                : XmlSkippedElements > 0 || DatabaseSkippedElements > 0
                    ? ContentDatabaseParityStatus.Warning
                    : ContentDatabaseParityStatus.Healthy;
}

public sealed class ContentDatabaseParityService
{
    public async Task<ContentDatabaseParityReport> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            DataManager.Current.InitializeDirectories();

            XmlSnapshotResult xmlSnapshot = await LoadXmlSnapshotAsync(cancellationToken);
            if (!xmlSnapshot.Success)
            {
                return new ContentDatabaseParityReport(
                    false,
                    xmlSnapshot.FailureReason,
                    0,
                    0,
                    xmlSnapshot.SkippedElements,
                    0,
                    0,
                    0,
                    [],
                    [],
                    [],
                    []);
            }

            var dbCollection = new ElementBaseCollection();
            DbLoadResult dbResult = await DbElementLoader.TryLoadSnapshotAsync(dbCollection);
            if (!dbResult.Success)
            {
                return new ContentDatabaseParityReport(
                    false,
                    dbResult.Summary,
                    xmlSnapshot.Elements.Count,
                    0,
                    xmlSnapshot.SkippedElements,
                    0,
                    0,
                    0,
                    [],
                    [],
                    [],
                    []);
            }

            // Filter the XML snapshot to only elements from enabled sources so the comparison
            // is symmetric with the DB, which uses resolved_elements_cache (enabled only).
            HashSet<string> enabledSources = await DbElementLoader.LoadEnabledSourceNamesAsync();
            IEnumerable<ElementBase> filteredXml = enabledSources.Count > 0
                ? xmlSnapshot.Elements.Where(e => string.IsNullOrEmpty(e.Source) || enabledSources.Contains(e.Source))
                : xmlSnapshot.Elements;

            return CompareSnapshots(filteredXml, dbCollection, xmlSnapshot.SkippedElements, dbResult.SkippedElementCount);
        }
        catch (Exception ex)
        {
            return new ContentDatabaseParityReport(
                false,
                $"{ex.GetType().Name}: {ex.Message}",
                0,
                0,
                0,
                0,
                0,
                0,
                [],
                [],
                [],
                []);
        }
    }

    private sealed record XmlSnapshotResult(
        bool Success,
        string? FailureReason,
        ElementBaseCollection Elements,
        int SkippedElements);

    private static async Task<XmlSnapshotResult> LoadXmlSnapshotAsync(CancellationToken cancellationToken)
    {
        var parserCollection = ElementParserFactory.GetParsers().ToList();
        ElementParser defaultParser = new();
        ElementParser currentParser = new();
        var elements = new ElementBaseCollection();
        var appendNotes = new List<XmlNode>();
        int skipped = 0;

        foreach (XmlDocument xmlDocument in DataManager.Current.LoadElementDocumentsFromResource())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (xmlDocument.DocumentElement == null)
                continue;
            AuroraXmlCompatibilityRepair.RepairDocument(xmlDocument);

            foreach (XmlNode elementNode in xmlDocument.DocumentElement.ChildNodes
                         .Cast<XmlNode>()
                         .Where(x => x.NodeType != XmlNodeType.Comment && x.Name.Equals("element")))
            {
                try
                {
                    ElementHeader header = currentParser.ParseElementHeader(elementNode);
                    if (currentParser.ParserType != header.Type)
                        currentParser = parserCollection.FirstOrDefault(x => x.ParserType == header.Type) ?? defaultParser;

                    ElementBase element = currentParser.ParseElement(elementNode);
                    UpsertElement(elements, element);
                }
                catch
                {
                    skipped++;
                }
            }
        }

        foreach (FileInfo file in GetOrderedCustomFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ElementsFile ef = ElementsFile.FromFile(file);
                if (ef.Ignore)
                    continue;

                XmlDocument xmlDocument = await CreateXmlDocumentAsync(file.FullName);
                if (xmlDocument.DocumentElement != null)
                {
                    AuroraXmlCompatibilityRepair.RepairDocument(xmlDocument);
                    foreach (XmlNode elementNode in xmlDocument.DocumentElement.ChildNodes
                                 .Cast<XmlNode>()
                                 .Where(x => x.NodeType != XmlNodeType.Comment && x.Name.Equals("element")))
                    {
                        try
                        {
                            ElementHeader header = currentParser.ParseElementHeader(elementNode);
                            if (currentParser.ParserType != header.Type)
                                currentParser = parserCollection.FirstOrDefault(p => p.ParserType == header.Type) ?? defaultParser;

                            ElementBase element = currentParser.ParseElement(elementNode);
                            UpsertElement(elements, element);
                        }
                        catch
                        {
                            skipped++;
                        }
                    }
                }

                appendNotes.AddRange(ef.ExtendNodes.Cast<XmlNode>());
            }
            catch
            {
                skipped++;
            }
        }

        try
        {
            AppendElements(appendNotes, elements, currentParser, defaultParser, parserCollection);
        }
        catch (Exception ex)
        {
            return new XmlSnapshotResult(false, $"Append failure: {ex.GetType().Name}: {ex.Message}", new ElementBaseCollection(), skipped);
        }

        return new XmlSnapshotResult(true, null, elements, skipped);
    }

    private static ContentDatabaseParityReport CompareSnapshots(
        IEnumerable<ElementBase> xmlElements,
        IEnumerable<ElementBase> databaseElements,
        int xmlSkippedElements,
        int databaseSkippedElements)
    {
        var xmlList = xmlElements
            .Where(e => !string.IsNullOrWhiteSpace(e.Id))
            .ToList();
        var dbList = databaseElements
            .Where(e => !string.IsNullOrWhiteSpace(e.Id))
            .ToList();

        var xmlById = new Dictionary<string, ElementBase>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in xmlList)
            xmlById[element.Id] = element;

        var dbById = new Dictionary<string, ElementBase>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in dbList)
            dbById[element.Id] = element;

        List<string> missingInDatabaseAll = xmlById.Keys
            .Except(dbById.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> missingInDatabase = missingInDatabaseAll
            .Take(15)
            .ToList();

        List<string> missingInXmlAll = dbById.Keys
            .Except(xmlById.Keys, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<string> missingInXml = missingInXmlAll
            .Take(15)
            .ToList();

        IReadOnlyList<ContentDatabaseParityMismatch> typeMismatches = BuildMismatches(xmlList, dbList, x => x.Type, 12);
        IReadOnlyList<ContentDatabaseParityMismatch> sourceMismatches = BuildMismatches(xmlList, dbList, x => x.Source ?? "(none)", 12);

        return new ContentDatabaseParityReport(
            true,
            null,
            xmlList.Count,
            dbList.Count,
            xmlSkippedElements,
            databaseSkippedElements,
            missingInDatabaseAll.Count,
            missingInXmlAll.Count,
            missingInDatabase,
            missingInXml,
            typeMismatches,
            sourceMismatches);
    }

    private static IReadOnlyList<ContentDatabaseParityMismatch> BuildMismatches(
        IEnumerable<ElementBase> xmlElements,
        IEnumerable<ElementBase> dbElements,
        Func<ElementBase, string> keySelector,
        int take)
    {
        Dictionary<string, int> xmlCounts = xmlElements
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        Dictionary<string, int> dbCounts = dbElements
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return xmlCounts.Keys
            .Union(dbCounts.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(key => new ContentDatabaseParityMismatch(
                key,
                xmlCounts.TryGetValue(key, out int xmlCount) ? xmlCount : 0,
                dbCounts.TryGetValue(key, out int dbCount) ? dbCount : 0))
            .Where(x => x.XmlCount != x.DatabaseCount)
            .OrderByDescending(x => Math.Abs(x.XmlCount - x.DatabaseCount))
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    private static void UpsertElement(ElementBaseCollection collection, ElementBase element)
    {
        ElementBase? existing = collection.FirstOrDefault(x => x.Id.Equals(element.Id, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            collection.Remove(existing);
        collection.Add(element);
    }

    private static void AppendElements(
        IEnumerable<XmlNode> appendNodes,
        ElementBaseCollection coreElements,
        ElementParser elementParser,
        ElementParser defaultParser,
        List<ElementParser> elementParserCollection)
    {
        foreach (XmlNode appendNode in appendNodes)
        {
            AuroraXmlCompatibilityRepair.RepairNode(appendNode);
            if (!appendNode.ContainsAttribute("id"))
                continue;

            string appendId = appendNode.GetAttributeValue("id");
            string appendType = appendNode.ContainsAttribute("type") ? appendNode.GetAttributeValue("type") : string.Empty;
            if (elementParser.ParserType != appendType)
                elementParser = elementParserCollection.FirstOrDefault(p => p.ParserType == appendType) ?? defaultParser;

            ElementBase? elementBase = coreElements.FirstOrDefault(x => x.Id == appendId);
            if (elementBase == null)
                continue;

            ElementBase element = elementParser.ParseElement(appendNode, elementBase.ElementHeader);
            bool updated = false;

            if (element.HasSupports)
            {
                foreach (string support in element.Supports)
                {
                    if (!elementBase.Supports.Contains(support))
                        elementBase.Supports.Add(support);
                }
                updated = true;
            }

            if (element.ElementSetters.Any())
            {
                foreach (ElementSetters.Setter setter in element.ElementSetters)
                {
                    if (!elementBase.ElementSetters.ContainsSetter(setter.Name))
                        elementBase.ElementSetters.Add(setter);
                }
                updated = true;
            }

            if (element.HasRules)
            {
                elementBase.Rules.AddRange(element.Rules);
                updated = true;
            }

            if (element.HasSpellcastingInformation && !elementBase.HasSpellcastingInformation)
            {
                elementBase.SpellcastingInformation = element.SpellcastingInformation;
                updated = true;
            }

            if (updated)
                elementBase.IsExtended = true;
        }
    }

    private static async Task<XmlDocument> CreateXmlDocumentAsync(string filePath)
    {
        using StreamReader reader = new(filePath);
        string xmlText = await reader.ReadToEndAsync();
        XmlDocument xmlDocument = new();
        xmlDocument.LoadXml(xmlText);
        return xmlDocument;
    }

    private static List<FileInfo> GetOrderedCustomFiles()
    {
        List<FileInfo> all = [];
        foreach (string dir in ContentDirectoryResolver.GetContentDirectories())
            all.AddRange(GetOrderedCustomFiles(dir));

        return all;
    }

    private static List<FileInfo> GetOrderedCustomFiles(string path)
    {
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

    private static List<FileInfo> GetFiles(string directory, string pattern = "*.*", bool includeSubdirectories = true)
    {
        List<FileInfo> files = [];
        files.AddRange(Directory.GetFiles(directory, pattern).Select(f => new FileInfo(f)));
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
}
