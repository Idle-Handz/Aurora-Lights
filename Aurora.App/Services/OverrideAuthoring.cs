using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Aurora.Content.Contracts;

namespace Aurora.App.Services;

/// <summary>Raised with a message fit to show the reader when an override cannot be prepared or written.</summary>
public sealed class OverrideAuthoringException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>One fix inside a prepared override: an element and the rarity it should say.</summary>
public sealed record PlannedRepair(string AuroraId, string Name, string Written, string Suggested, string Key);

/// <summary>
/// Everything about an override file, worked out and checked but not yet written. Nothing on disk has
/// changed; <see cref="OverrideAuthoring.Write"/> is what changes it.
/// </summary>
public sealed record PlannedOverride(
    string ContentRoot,
    string SourcePath,
    string UpstreamPath,
    string UpstreamHash,
    string OverridePath,
    IReadOnlyList<PlannedRepair> Repairs,
    string OverrideXml);

/// <summary>
/// Turns Content Doctor suggestions into an override file: a copy of the upstream content file with
/// the repairs made, carrying the correction metadata the importer reads (see
/// <see cref="LocalCorrectionDocument"/>). The upstream file is never touched. An override pins its
/// repairs only until the people who publish the content adopt them, at which point the Content Doctor
/// can clear them and the file can retire.
///
/// Doing it takes two steps so the reader can confirm in between: <see cref="Plan"/> builds and checks the
/// file in memory, <see cref="Write"/> puts it on disk.
/// </summary>
public static class OverrideAuthoring
{
    /// <summary>Where Content Doctor overrides go inside <c>user/local</c>, mirroring the upstream path.</summary>
    public const string Folder = "content-doctor-repairs";

    private const string RarityField = "rarity";

    private static readonly Regex SourcePathAttribute = new(@"source-path\s*=\s*""([^""]*)""", RegexOptions.Compiled);

    /// <summary>
    /// Prepares one override for the content file the <paramref name="repairs"/> all belong to. Every
    /// repair must be a rarity typo with a suggested spelling. Throws <see cref="OverrideAuthoringException"/>
    /// when it cannot be done safely; the message says why.
    /// </summary>
    public static PlannedOverride Plan(string contentRoot, IReadOnlyList<SuggestedCorrectionModel> repairs, DateOnly today)
    {
        if (string.IsNullOrWhiteSpace(contentRoot) || !Directory.Exists(contentRoot))
            throw new OverrideAuthoringException("The content folder could not be found.");
        if (repairs.Count == 0)
            throw new OverrideAuthoringException("There is nothing to repair.");
        if (repairs.Any(repair => !string.Equals(repair.Field, RarityField, StringComparison.OrdinalIgnoreCase) || !repair.HasSuggestion))
            throw new OverrideAuthoringException("Only a rarity typo that comes with a suggested spelling can be written as an override.");

        string relative = NormalizeRelative(repairs[0].RelativePath);
        if (relative.Length == 0 || repairs.Any(repair => !string.Equals(NormalizeRelative(repair.RelativePath), relative, StringComparison.OrdinalIgnoreCase)))
            throw new OverrideAuthoringException("One override covers one content file; these repairs are not all in the same file.");

        var distinct = new List<SuggestedCorrectionModel>();
        foreach (var repair in repairs)
        {
            var seen = distinct.FirstOrDefault(other => string.Equals(other.AuroraId, repair.AuroraId, StringComparison.Ordinal));
            if (seen is null)
                distinct.Add(repair);
            else if (!string.Equals(seen.Suggested, repair.Suggested, StringComparison.Ordinal) || !string.Equals(seen.Written, repair.Written, StringComparison.Ordinal))
                throw new OverrideAuthoringException($"{repair.AuroraId} has two different repairs listed; resolve that first.");
        }

        string upstreamPath;
        try
        {
            upstreamPath = LocalCorrectionDocument.ResolveSourcePath(contentRoot, relative);
        }
        catch (InvalidDataException ex)
        {
            throw new OverrideAuthoringException($"That file cannot be corrected with an override. {ex.Message}", ex);
        }

        if (!File.Exists(upstreamPath))
            throw new OverrideAuthoringException($"The content file is no longer on disk: {relative}");

        string upstreamText = File.ReadAllText(upstreamPath);
        string upstreamHash = LocalCorrectionDocument.FileFingerprint(upstreamPath);

        XDocument document;
        try
        {
            document = LocalCorrectionDocument.Parse(upstreamText, relative);
            if (LocalCorrectionDocument.HasMetadata(upstreamText))
                throw new OverrideAuthoringException($"{relative} already carries correction metadata, so it is not an authoritative file to correct.");
        }
        catch (Exception ex) when (ex is XmlException or InvalidDataException)
        {
            throw new OverrideAuthoringException($"The content file could not be read: {ex.Message}", ex);
        }

        if (FindExistingOverride(contentRoot, relative) is { } existing)
            throw new OverrideAuthoringException(
                $"An override already exists for this file ({existing}). Add the repairs to it, or retire it, rather than writing a second one.");

        string overridePath = Path.Combine(contentRoot, "user", "local", Folder, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(overridePath))
            throw new OverrideAuthoringException($"A file already exists where the override would go: {overridePath}");

        var corrections = new List<LocalCorrection>();
        var planned = new List<PlannedRepair>();
        foreach (var repair in distinct)
        {
            var declared = document.Root!.Elements("element")
                .Where(element => string.Equals((string?)element.Attribute("id"), repair.AuroraId, StringComparison.Ordinal))
                .ToList();
            if (declared.Count == 0)
                throw new OverrideAuthoringException($"{repair.Name} ({repair.AuroraId}) is no longer declared in {relative}.");
            if (declared.Count > 1)
                throw new OverrideAuthoringException($"{repair.AuroraId} is declared {declared.Count} times in {relative}, so a repair cannot be aimed at just one of them.");

            var element = declared[0];
            var setters = element.Descendants("set")
                .Where(set => string.Equals((string?)set.Attribute("name"), RarityField, StringComparison.OrdinalIgnoreCase))
                .Where(set => string.Equals(set.Value.Trim(), repair.Written, StringComparison.Ordinal))
                .ToList();
            if (setters.Count != 1)
                throw new OverrideAuthoringException(
                    $"{repair.Name} no longer says its rarity is \"{repair.Written}\" exactly once in {relative}; re-check the Content Doctor.");

            // The fingerprint names this exact declaration, so it must be taken before the edit.
            string fingerprint = LocalCorrectionDocument.Fingerprint(element);
            setters[0].Value = repair.Suggested!;

            string key = "rarity-repair-" + repair.AuroraId;
            string reason = $"{today:yyyy-MM-dd} user-authorized local repair, written from the Content Doctor. " +
                $"Rarity \"{repair.Written}\" is a typo of \"{repair.Suggested}\" ({repair.Name}). Not accepted upstream.";
            corrections.Add(new LocalCorrection(key, "replace", repair.AuroraId, null, fingerprint, "review-pending", null, reason));
            planned.Add(new PlannedRepair(repair.AuroraId, repair.Name, repair.Written, repair.Suggested!, key));
        }

        string overrideXml;
        try
        {
            overrideXml = LocalCorrectionDocument.Create(
                document.ToString(SaveOptions.DisableFormatting), upstreamText, relative, corrections);
            VerifyOverride(overrideXml, upstreamText, planned);
        }
        catch (InvalidDataException ex)
        {
            throw new OverrideAuthoringException($"The override could not be built for {relative}: {ex.Message}", ex);
        }

        return new PlannedOverride(contentRoot, relative, upstreamPath, upstreamHash, overridePath, planned, overrideXml);
    }

    /// <summary>
    /// Writes a prepared override. It will not replace a file that is already there, and it refuses if the
    /// content file changed since <see cref="Plan"/>, since the repairs were checked against the old one.
    /// Returns the path written.
    /// </summary>
    public static string Write(PlannedOverride plan)
    {
        if (File.Exists(plan.OverridePath))
            throw new OverrideAuthoringException($"A file already exists where the override would go: {plan.OverridePath}");
        if (!File.Exists(plan.UpstreamPath) || LocalCorrectionDocument.FileFingerprint(plan.UpstreamPath) != plan.UpstreamHash)
            throw new OverrideAuthoringException($"{plan.SourcePath} changed since the override was prepared. Re-check the Content Doctor and try again.");
        if (FindExistingOverride(plan.ContentRoot, plan.SourcePath) is { } existing)
            throw new OverrideAuthoringException($"An override appeared for this file in the meantime ({existing}).");

        Directory.CreateDirectory(Path.GetDirectoryName(plan.OverridePath)!);
        string temporary = plan.OverridePath + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, plan.OverrideXml);
            if (File.ReadAllText(temporary) != plan.OverrideXml)
                throw new IOException("The override was not written out intact.");

            // Move never overwrites, so a file that appeared since the check above is left alone.
            File.Move(temporary, plan.OverridePath);
        }
        catch (IOException ex) when (File.Exists(plan.OverridePath))
        {
            throw new OverrideAuthoringException($"A file already exists where the override would go: {plan.OverridePath}", ex);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        return plan.OverridePath;
    }

    // The importer must read exactly what was meant: every repair pinned, nothing else changed, and the
    // corrected file saying what each repair says. Checked on the finished text, not on intentions.
    private static void VerifyOverride(string overrideXml, string upstreamText, IReadOnlyList<PlannedRepair> planned)
    {
        var evaluation = LocalCorrectionDocument.Evaluate(overrideXml, upstreamText);

        var expected = planned.Select(repair => repair.Key + ": pinned").ToHashSet(StringComparer.Ordinal);
        if (evaluation.Corrections.Count != planned.Count
            || !expected.SetEquals(evaluation.ReviewReasons)
            || evaluation.SuppressedIds.Count != 0
            || evaluation.CanRetire)
            throw new InvalidDataException(
                "The importer would read the override differently than intended: " + string.Join("; ", evaluation.ReviewReasons));

        var effective = LocalCorrectionDocument.Parse(evaluation.EffectiveXml);
        foreach (var repair in planned)
        {
            var said = effective.Root!.Elements("element")
                .Where(element => (string?)element.Attribute("id") == repair.AuroraId)
                .SelectMany(element => element.Descendants("set"))
                .Where(set => string.Equals((string?)set.Attribute("name"), RarityField, StringComparison.OrdinalIgnoreCase))
                .Select(set => set.Value.Trim())
                .ToList();
            if (said.Count != 1 || said[0] != repair.Suggested)
                throw new InvalidDataException($"The corrected {repair.Name} would not say \"{repair.Suggested}\".");
        }
    }

    // Any managed override of the same upstream file, wherever it sits under user/local. A second one would
    // contend with it, and the library cannot add a correction to a file that already has them.
    private static string? FindExistingOverride(string contentRoot, string relative)
    {
        string local = Path.Combine(contentRoot, "user", "local");
        if (!Directory.Exists(local))
            return null;

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (string path in Directory.EnumerateFiles(local, "*.xml", options))
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (IOException) { continue; }

            if (!text.Contains(LocalCorrectionDocument.Namespace, StringComparison.Ordinal))
                continue;

            var match = SourcePathAttribute.Match(text);
            if (match.Success && string.Equals(NormalizeRelative(match.Groups[1].Value), relative, StringComparison.OrdinalIgnoreCase))
                return path;
        }

        return null;
    }

    private static string NormalizeRelative(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim().Trim('/');
}
