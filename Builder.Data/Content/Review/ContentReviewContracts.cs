#nullable enable
using Builder.Data.Files;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Builder.Data.Content.Review;

/// <summary>Read-only review contract v1; not a database schema or an authorization API.</summary>
public static class ContentReviewContract
{
    public const int Version = 1;
    public const string DefinitionFingerprintAlgorithm = "aurora-corrections-v1";
}

/// <summary>RootId is a stable configured-root identity, not its path, index, or display name.</summary>
public sealed record ContentFileKey
{
    public string RootId { get; }
    public string RelativePath { get; }

    public ContentFileKey(string rootId, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        string normalized = relativePath.Replace('\\', '/');
        if (normalized.StartsWith('/') || normalized.Contains(':') ||
            normalized.Split('/').Any(part => part is "" or "." or ".."))
            throw new ArgumentException("File identity requires a relative path without traversal.", nameof(relativePath));
        RootId = rootId;
        RelativePath = normalized;
    }
}

public sealed record ContentFileVersion(ContentFileKey File, string Sha256);

/// <summary>Zero-based ordinal among direct element children; only meaningful in this file revision.</summary>
public sealed record ContentDeclarationKey(ContentFileVersion FileVersion, int ElementOrdinal);

public enum ContentLayer { Authoritative, LocalOverride }

public sealed record ContentDefinitionSnapshot(
    ContentDeclarationKey Declaration,
    string AuroraId,
    string ElementType,
    string Name,
    string SourceLabel,
    string Fingerprint,
    string Xml);

/// <summary>Captured bytes and parsed definitions; declared update URLs are claims, not fetch proof.</summary>
public sealed class ContentDocumentSnapshot
{
    public ContentFileVersion Version { get; }
    public ContentLayer Layer { get; }
    public string Xml { get; }
    public bool IsIgnored { get; }
    public string? DeclaredUpdateUrl { get; }
    public IReadOnlyList<ContentDefinitionSnapshot> Definitions { get; }

    private ContentDocumentSnapshot(ContentFileVersion version, ContentLayer layer, XDocument document)
    {
        Version = version;
        Layer = layer;
        Xml = document.ToString(SaveOptions.DisableFormatting);
        IsIgnored = bool.TryParse((string?)document.Root!.Attribute("ignore"), out bool ignored) && ignored;
        DeclaredUpdateUrl = (string?)document.Root.Element("info")?.Element("update")?.Element("file")?.Attribute("url");
        Definitions = Array.AsReadOnly(document.Root.Elements("element").Select((element, ordinal) =>
        {
            string id = (string?)element.Attribute("id") ?? "";
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidDataException($"Missing Aurora ID in {version.File.RelativePath}, declaration {ordinal}.");
            return new ContentDefinitionSnapshot(new(version, ordinal), id,
                (string?)element.Attribute("type") ?? "", (string?)element.Attribute("name") ?? "",
                (string?)element.Attribute("source") ?? "", LocalCorrectionDocument.Fingerprint(element),
                element.ToString(SaveOptions.DisableFormatting));
        }).ToArray());
    }

    public static ContentDocumentSnapshot FromBytes(ContentFileKey file, ContentLayer layer, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(bytes);
        // Copy so parsing and the revision hash always describe the same captured bytes.
        byte[] captured = (byte[])bytes.Clone();
        using var stream = new MemoryStream(captured, writable: false);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
            { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        if (document.Root?.Name != "elements")
            throw new InvalidDataException("Content requires an unnamespaced elements root.");
        return new(new(file, Convert.ToHexString(SHA256.HashData(captured))), layer, document);
    }

    public static ContentDocumentSnapshot FromXml(ContentFileKey file, ContentLayer layer, string xml)
        => FromBytes(file, layer, Encoding.UTF8.GetBytes(xml));
}

/// <summary>
/// Shape for evidence supplied by the trusted downloader. Merely constructing or loading this
/// record does not verify authority, match a correction, or authorize acceptance.
/// </summary>
public sealed record ContentDownloadEvidence(
    ContentFileVersion Payload,
    Uri RequestedUrl,
    Uri ResponseUrl,
    DateTimeOffset FetchedAtUtc,
    string? RepositoryRevision,
    string? ETag);

public enum ContentReviewKind
{
    ConflictingDefinitions,
    IdentitySpellingNeedsReview,
    CorrectionNeedsReview,
    PublishedMatchPendingImport,
    LegacyLocalNeedsClassification,
    OriginNeedsReview,
    AmbiguousReference
}

public enum ContentReferenceKind { Grant, Append, Selection, Requirement, Support, Other }
public enum ReferenceInspectionStatus { NotInspected, Partial, Complete }

public sealed record ContentReference(
    ContentFileVersion FileVersion,
    string NodePath,
    string TargetAuroraId,
    ContentReferenceKind Kind,
    ContentDeclarationKey? Owner);

public sealed record ContentReferenceImpact(
    ReferenceInspectionStatus Status,
    IReadOnlyList<ContentReference> References);

/// <summary>All suppliers of an identical same-ID definition, including repetitions within one file.</summary>
public sealed record IdenticalContentDeclarations(
    string AuroraId,
    IReadOnlyList<ContentDefinitionSnapshot> Declarations);

public sealed record ContentReviewCase(
    string CaseId,
    ContentReviewKind Kind,
    IReadOnlyList<string> AuroraIds,
    IReadOnlyList<ContentDefinitionSnapshot> Definitions,
    ContentReferenceImpact ReferenceImpact);

public sealed record CanonicalContentAnalysis(
    IReadOnlyList<IdenticalContentDeclarations> IdenticalDeclarations,
    IReadOnlyList<ContentReviewCase> ReviewCases)
{
    // This only reports this analyzer's findings; it is not proof an import is safe to activate.
    public bool HasReviewCases => ReviewCases.Count != 0;
}

public sealed record ContentCorrectionKey(ContentFileKey LocalFile, string CorrectionKey);

/// <summary>Three-way review input. Current correction XML remains the durable operation contract.</summary>
public sealed record ContentCorrectionReview(
    ContentDocumentSnapshot Baseline,
    ContentDocumentSnapshot Local,
    ContentDocumentSnapshot Incoming,
    IReadOnlyList<LocalCorrection> Operations,
    IReadOnlyList<ContentCorrectionKey> RelatedOperations,
    ContentDownloadEvidence? DownloadEvidence,
    ContentReferenceImpact ReferenceImpact);

/// <summary>
/// An authoring tool finding bound to the inspected file revision. Preserve its original
/// diagnostic/repair payload instead of inventing a competing repair vocabulary.
/// Null declaration means the tool's locator has not been unambiguously bound yet.
/// </summary>
public sealed record ContentAuthoringFinding(
    string Provider,
    ContentFileVersion InspectedFile,
    ContentDeclarationKey? Declaration,
    string Category,
    string Severity,
    JsonElement Diagnostic);

public enum ContentResolutionKind
{
    KeepLocalCorrection,
    AcceptIncomingCorrection,
    WriteLocalCorrection,
    ConsolidateIdenticalDeclarations
}

/// <summary>
/// Proposed intent only. A future executor must validate expected versions, evidence,
/// full group membership, references, candidate import and atomic persistence before applying it.
/// </summary>
public sealed record ContentResolutionProposal(
    ContentResolutionKind Kind,
    IReadOnlyList<string> CaseIds,
    IReadOnlyList<ContentFileVersion> ExpectedInputs,
    IReadOnlyList<ContentCorrectionKey> CorrectionKeys,
    IReadOnlyList<ContentDownloadEvidence> DownloadEvidence);
