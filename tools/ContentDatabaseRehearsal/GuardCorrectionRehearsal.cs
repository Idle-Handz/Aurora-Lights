using Aurora.Content.Contracts;
using System.Security.Cryptography;
using System.Xml.Linq;

internal static class GuardCorrectionRehearsal
{
    public static object Run(string output)
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "5e Character Builder", "custom");
        const string relative = "core/players-handbook-2024/items-packs.xml";
        string localPath = Path.Combine(root, "user/local/players-handbook-2024-items-packs.xml");
        string local = File.ReadAllText(localPath);
        string baseline = File.ReadAllText(Path.Combine(root, relative));
        const string id = "ID_WOTC_PHB24_ITEM_BACKGROUND_EQUIPMENT_PACK_GUARD";
        var original = LocalCorrectionDocument.Parse(baseline).Root!.Elements("element").Single(e => (string?)e.Attribute("id") == id);
        string annotated = LocalCorrectionDocument.Create(local, baseline, relative,
            [new LocalCorrection("guard-light-crossbow-reference", "replace", id, null,
                LocalCorrectionDocument.Fingerprint(original), Reason: "Guard starting equipment must extract ID_WOTC_PHB24_WEAPON_LIGHT_CROSSBOW, the weapon actually defined by PHB 2024.")]);
        var evaluation = LocalCorrectionDocument.Evaluate(annotated, baseline);
        var effective = LocalCorrectionDocument.Parse(evaluation.EffectiveXml);
        var guard = effective.Root!.Elements("element").Single(e => (string?)e.Attribute("id") == id);
        if (!guard.ToString().Contains("ID_WOTC_PHB24_WEAPON_LIGHT_CROSSBOW") || guard.ToString().Contains("ID_WOTC_PHB24_WEAPON_CROSSBOW_LIGHT"))
            throw new InvalidDataException("Guard correction did not resolve the weapon reference.");
        var withoutMetadata = LocalCorrectionDocument.Parse(annotated);
        withoutMetadata.Root!.Elements(XName.Get("corrections", LocalCorrectionDocument.Namespace)).Remove();
        if (LocalCorrectionDocument.Fingerprint(withoutMetadata.Root) != LocalCorrectionDocument.Fingerprint(LocalCorrectionDocument.Parse(local).Root!))
            throw new InvalidDataException("Annotation changed existing local XML content.");
        if (evaluation.CanRetire || evaluation.Corrections.Single().State != "review-pending")
            throw new InvalidDataException("Correction must remain protected pending published-fix verification.");
        string candidate = Path.Combine(output, "players-handbook-2024-items-packs.xml");
        File.WriteAllText(candidate, annotated);
        return new { candidate, originalSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(localPath))),
            candidateSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(candidate))),
            elements = effective.Root.Elements("element").Count(), checksPassed = 3,
            evaluation.ReviewReasons, evaluation.CanRetire };
    }
}
