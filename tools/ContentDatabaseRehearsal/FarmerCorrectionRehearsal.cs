using Builder.Data.Files;
using System.Security.Cryptography;
using System.Xml.Linq;

internal static class FarmerCorrectionRehearsal
{
    public static object Run(string output)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "5e Character Builder", "custom");
        const string relative = "core/players-handbook-2024/background-farmer.xml";
        string localPath = Path.Combine(root, "user/local/background-farmer.xml");
        string local = File.ReadAllText(localPath);
        string baseline = File.ReadAllText(Path.Combine(root, relative));
        const string farmerId = "ID_WOTC_PHB24_BACKGROUND_FARMER";
        const string featureId = "ID_WOTC_PHB24_BACKGROUND_FEATURE_FARMER_TOUGH";
        const string group = "farmer-local-feat-presentation";
        var original = LocalCorrectionDocument.Parse(baseline).Root!.Elements("element").Single(e => (string?)e.Attribute("id") == farmerId);
        string annotated = LocalCorrectionDocument.Create(local, baseline, relative,
            [new LocalCorrection("farmer-local-background", "replace", farmerId, null,
                LocalCorrectionDocument.Fingerprint(original), Group: group,
                Reason: "Intentional user preference: present Tough through an Origin Feat background feature while retaining the canonical Farmer identity. This is a local presentation/behavior preference, not an upstream authoring-error claim."),
             new LocalCorrection("farmer-local-tough-feature", "add", featureId, null, null, Group: group,
                Reason: "Companion to the intentional Farmer replacement: retain the Tough feature wrapper and ID_INTERNAL_GRANTS_BACKGROUND_WITH_A_FEAT grant. Review together with the Farmer replacement.")]);
        var evaluation = LocalCorrectionDocument.Evaluate(annotated, baseline);
        var stripped = LocalCorrectionDocument.Parse(annotated);
        stripped.Root!.Elements(XName.Get("corrections", LocalCorrectionDocument.Namespace)).Remove();
        if (LocalCorrectionDocument.Fingerprint(stripped.Root) != LocalCorrectionDocument.Fingerprint(LocalCorrectionDocument.Parse(local).Root!))
            throw new InvalidDataException("Annotation changed existing local XML content.");
        var effective = LocalCorrectionDocument.Parse(evaluation.EffectiveXml).Root!.Elements("element").ToArray();
        if (effective.Length != 2 || effective.Single(e => (string?)e.Attribute("id") == farmerId)
            .Descendants("grant").Count(e => (string?)e.Attribute("id") == featureId) != 1)
            throw new InvalidDataException("Farmer identity or feature reference changed.");
        var feature = effective.Single(e => (string?)e.Attribute("id") == featureId);
        if (!feature.Descendants("grant").Select(e => (string?)e.Attribute("id"))
            .ToHashSet().SetEquals(["ID_WOTC_PHB24_FEAT_TOUGH", "ID_INTERNAL_GRANTS_BACKGROUND_WITH_A_FEAT"]))
            throw new InvalidDataException("Intentional Tough feature grants changed.");
        if (evaluation.CanRetire || evaluation.Corrections.Any(c => c.State != "review-pending" || c.Group != group)
            || evaluation.ReviewReasons.Any(r => r.Contains("unclassified", StringComparison.Ordinal)))
            throw new InvalidDataException("Preference intent was not completely protected/classified.");
        var incoming = LocalCorrectionDocument.Parse(baseline);
        incoming.Root!.Elements("element").Single().SetAttributeValue("name", "Upstream changed Farmer");
        var updated = LocalCorrectionDocument.Evaluate(annotated, incoming.ToString(SaveOptions.DisableFormatting));
        if (updated.EffectiveXml.Contains("Upstream changed Farmer", StringComparison.Ordinal))
            throw new InvalidDataException("Unreviewed upstream content replaced the local preference.");
        string candidate = Path.Combine(output, "background-farmer.xml");
        File.WriteAllText(candidate, annotated);
        return new { candidate, checksPassed = 5, evaluation.ReviewReasons, evaluation.CanRetire,
            originalSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(localPath))),
            candidateSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(candidate))) };
    }
}
