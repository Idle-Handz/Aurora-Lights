#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Builder.Data.Content.Review;

/// <summary>Read-only application content preparation; never writes SQLite or selects a winner.</summary>
public static class CanonicalContentAnalyzer
{
    public static CanonicalContentAnalysis Analyze(IEnumerable<ContentDocumentSnapshot> documents)
    {
        var inputs = documents.ToArray();
        if (inputs.Any(d => d.Layer != ContentLayer.Authoritative))
            throw new ArgumentException("Classify the authoritative catalog separately from local overlays.", nameof(documents));
        if (inputs.GroupBy(d => d.Version.File).Any(g => g.Count() > 1))
            throw new ArgumentException("Provide one revision per file; compare revisions through the review model.", nameof(documents));

        var definitions = inputs.Where(d => !d.IsIgnored).SelectMany(d => d.Definitions).ToArray();
        var identical = new List<IdenticalContentDeclarations>();
        var reviews = new List<ContentReviewCase>();
        // Preserve exact IDs. Flag potentially incompatible spellings without choosing a normalization.
        var spellingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var spellings in definitions.GroupBy(d => d.AuroraId.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (spellings.Select(d => d.AuroraId).Distinct(StringComparer.Ordinal).Count() < 2 &&
                spellings.All(d => d.AuroraId == d.AuroraId.Trim())) continue;
            foreach (var definition in spellings) spellingIds.Add(definition.AuroraId);
            reviews.Add(Review(ContentReviewKind.IdentitySpellingNeedsReview, spellings));
        }
        foreach (var sameId in definitions.GroupBy(d => d.AuroraId, StringComparer.Ordinal).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (sameId.Count() < 2) continue;
            if (sameId.Select(d => d.Fingerprint).Distinct(StringComparer.Ordinal).Count() != 1)
                reviews.Add(Review(ContentReviewKind.ConflictingDefinitions, sameId));
            else if (!spellingIds.Contains(sameId.Key))
                identical.Add(new(sameId.Key, Array.AsReadOnly(Ordered(sameId))));
        }
        return new(identical.AsReadOnly(), reviews.OrderBy(r => r.CaseId, StringComparer.Ordinal).ToList().AsReadOnly());
    }

    private static ContentDefinitionSnapshot[] Ordered(IEnumerable<ContentDefinitionSnapshot> definitions)
        => definitions.OrderBy(d => d.Declaration.FileVersion.File.RootId, StringComparer.Ordinal)
            .ThenBy(d => d.Declaration.FileVersion.File.RelativePath, StringComparer.Ordinal)
            .ThenBy(d => d.Declaration.FileVersion.Sha256, StringComparer.Ordinal)
            .ThenBy(d => d.Declaration.ElementOrdinal).ToArray();

    private static ContentReviewCase Review(ContentReviewKind kind, IEnumerable<ContentDefinitionSnapshot> definitions)
    {
        var ordered = Ordered(definitions);
        // Stable for this evidence set, independent of directory enumeration order. New file bytes invalidate it.
        string payload = JsonSerializer.Serialize(new { version = ContentReviewContract.Version, kind,
            declarations = ordered.Select(d => d.Declaration).ToArray() });
        string id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        return new(id, kind, Array.AsReadOnly(ordered.Select(d => d.AuroraId).Distinct(StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(ordered), new(ReferenceInspectionStatus.NotInspected, Array.Empty<ContentReference>()));
    }
}
