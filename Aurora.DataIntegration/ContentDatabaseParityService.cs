using Aurora.Content.Contracts;
using Aurora.Content.Preparation;
using Builder.Data;
using Builder.Presentation.Services;
using Builder.Presentation.Utilities;
using System.Xml;
using System.Xml.Linq;

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
    IReadOnlyList<ContentDatabaseParityMismatch> SourceMismatches,
    int DefinitionMismatchCount = 0,
    IReadOnlyList<string>? DefinitionMismatchSample = null)
{
    public int TotalMismatchCount =>
        MissingInDatabaseCount + MissingInXmlCount + TypeMismatches.Count +
        SourceMismatches.Count + DefinitionMismatchCount;

    public IReadOnlyList<string> DefinitionMismatches => DefinitionMismatchSample ?? [];

    public ContentDatabaseParityStatus Status =>
        !Success ? ContentDatabaseParityStatus.Error
            : TotalMismatchCount > 0 || XmlSkippedElements > 0 || DatabaseSkippedElements > 0
                ? ContentDatabaseParityStatus.Warning : ContentDatabaseParityStatus.Healthy;
}

/// <summary>
/// Compares current XML with the database's prepared definitions. Both sides honor preparation
/// decisions (including exclusions and local corrections); this is not a Legacy runtime audit.
/// </summary>
public sealed class ContentDatabaseParityService
{
    public async Task<ContentDatabaseParityReport> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var xmlElements = await Task.Run(() => LoadXmlSnapshot(cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var dbElements = new ElementBaseCollection();
            DbLoadResult dbResult = await DbElementLoader.TryLoadSnapshotAsync(dbElements);
            cancellationToken.ThrowIfCancellationRequested();
            if (!dbResult.Success)
                return Failure(dbResult.Summary, xmlElements.Count);

            return await Task.Run(() => CompareSnapshots(xmlElements, dbElements, 0,
                dbResult.SkippedElementCount, cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failure($"{ex.GetType().Name}: {ex.Message}");
        }
    }

    private static ContentDatabaseParityReport Failure(string? reason, int xmlCount = 0) =>
        new(false, reason, xmlCount, 0, 0, 0, 0, 0, [], [], [], []);

    internal static ElementBaseCollection LoadXmlSnapshot(CancellationToken cancellationToken)
    {
        if (DbElementLoader.DbPath is not { } path || !File.Exists(path))
            throw new InvalidOperationException("A prepared content database is required before checking parity.");
        using var connection = ContentDatabase.OpenReadableConnection(path);
        if (!PreparedCatalogReader.IsPrepared(connection))
            throw new InvalidOperationException("Refresh the content database before checking parity; its preparation contract is missing or unsupported.");
        if (!PreparedCatalogReader.InputsMatch(connection, [ContentDirectoryResolver.GetPrimaryContentDirectory()]))
            throw new InvalidOperationException("Primary XML has changed since preparation. Refresh the database before checking parity.");

        var projection = DbElementLoader.ReadPreparedProjection(connection, fromXml: true);
        var result = new ElementBaseCollection();
        var fallback = new ElementParser();
        var parsers = ElementParserFactory.GetParsers().ToList();
        foreach (var entry in projection.Elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = new XmlDocument();
            document.LoadXml(entry.Xml);
            AuroraXmlCompatibilityRepair.RepairNode(document.DocumentElement!);
            var header = fallback.ParseElementHeader(document.DocumentElement!);
            var element = (parsers.FirstOrDefault(p => p.ParserType == header.Type) ?? fallback)
                .ParseElement(document.DocumentElement!);
            ElementProvenance.SetContentFilePath(element, entry.Source.FilePath);
            result.Add(element);
        }
        return result;
    }

    internal static ContentDatabaseParityReport CompareSnapshots(
        IEnumerable<ElementBase> xmlElements,
        IEnumerable<ElementBase> databaseElements,
        int xmlSkippedElements = 0,
        int databaseSkippedElements = 0,
        CancellationToken cancellationToken = default)
    {
        var xmlList = xmlElements.Where(e => !string.IsNullOrWhiteSpace(e.Id)).ToList();
        var dbList = databaseElements.Where(e => !string.IsNullOrWhiteSpace(e.Id)).ToList();
        // Declaration identity is exact. Case-only differences must not look like parity.
        var xmlById = xmlList.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var dbById = dbList.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var missingInDatabase = xmlById.Keys.Except(dbById.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToList();
        var missingInXml = dbById.Keys.Except(xmlById.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToList();
        var definitionMismatches = new List<string>();
        foreach (string id in xmlById.Keys.Intersect(dbById.Keys, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (DefinitionFingerprint(xmlById[id]) != DefinitionFingerprint(dbById[id]))
                definitionMismatches.Add(id);
        }
        definitionMismatches.Sort(StringComparer.Ordinal);

        return new ContentDatabaseParityReport(true, null, xmlList.Count, dbList.Count,
            xmlSkippedElements, databaseSkippedElements, missingInDatabase.Count, missingInXml.Count,
            missingInDatabase.Take(15).ToList(), missingInXml.Take(15).ToList(),
            BuildMismatches(xmlList, dbList, e => e.Type),
            BuildMismatches(xmlList, dbList, e => e.Source ?? "(none)"),
            DefinitionMismatchCount: definitionMismatches.Count,
            DefinitionMismatchSample: definitionMismatches.Take(15).ToList());
    }

    private static string DefinitionFingerprint(ElementBase element)
    {
        // The same parser builds both snapshots. Compare its complete definition, including
        // rules, setters, supports, selection items and presentation metadata, rather than counts.
        // Ignore attribute ordering, comments and formatting-only indentation, but preserve text
        // and child order because authored rule order and multiplicity affect engine behavior.
        return LocalCorrectionDocument.Fingerprint(XElement.Parse(element.ElementNode.OuterXml));
    }

    private static IReadOnlyList<ContentDatabaseParityMismatch> BuildMismatches(
        IEnumerable<ElementBase> xmlElements,
        IEnumerable<ElementBase> dbElements,
        Func<ElementBase, string> keySelector)
    {
        var xmlCounts = xmlElements.GroupBy(keySelector, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var dbCounts = dbElements.GroupBy(keySelector, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return xmlCounts.Keys.Union(dbCounts.Keys, StringComparer.Ordinal)
            .Select(key => new ContentDatabaseParityMismatch(key,
                xmlCounts.GetValueOrDefault(key), dbCounts.GetValueOrDefault(key)))
            .Where(m => m.XmlCount != m.DatabaseCount)
            .OrderByDescending(m => Math.Abs(m.XmlCount - m.DatabaseCount))
            .ThenBy(m => m.Key, StringComparer.Ordinal).Take(12).ToList();
    }
}
