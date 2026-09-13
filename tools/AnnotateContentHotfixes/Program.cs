// Stages this session's six manifest-verified hotfixes; never writes installed content.
using Aurora.Importer;
using Builder.Data.Files;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Text.Json;
using System.Xml.Linq;

if (args.Length == 2 && args[0] == "--verify-installed")
{
    var evidence = JsonDocument.Parse(File.ReadAllText(args[1]));
    int files = 0, marked = 0;
    foreach (var entry in evidence.RootElement.GetProperty("files").EnumerateArray())
    {
        string path = entry.GetProperty("destination").GetString()!;
        Require(Hash(path) == entry.GetProperty("annotatedSha256").GetString(), "Installed annotation hash differs.");
        var evaluation = LocalCorrectionDocument.ForRuntime(path) ?? throw new InvalidDataException("Missing metadata.");
        Require(!evaluation.CanRetire && !evaluation.ReviewReasons.Any(r => r.Contains("unclassified", StringComparison.OrdinalIgnoreCase)), "Unexpected installed review state.");
        Require(evaluation.Corrections.All(c => c.State == "review-pending"), "Correction was unexpectedly accepted.");
        marked += evaluation.Corrections.Count;
        files++;
    }
    Require(files == 6 && marked == 10, "Unexpected installed counts.");
    Console.WriteLine("PASS: six installed files, ten protected corrections, no unclassified changes, none eligible for automatic retirement.");
    return;
}
if (args.Length != 3) throw new ArgumentException("Usage: <original-archive> <new-output-directory> <translator-exe>");
string archive = Path.GetFullPath(args[0]), work = Path.GetFullPath(args[1]), translator = Path.GetFullPath(args[2]);
if (Directory.Exists(work)) throw new IOException("Output directory must be new.");
var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(archive, "manifest.json")));
var entries = manifest.RootElement.EnumerateArray().ToArray();
Require(entries.Length == 6, "Expected the six reviewed hotfix files.");
Directory.CreateDirectory(work);
string root = Path.Combine(work, "content");
var report = new List<object>();
int operations = 0, elementCount = 0;

foreach (var entry in entries)
{
    string relative = entry.GetProperty("relativePath").GetString()!;
    string originalPath = Path.Combine(archive, relative);
    string overlay = entry.GetProperty("overlay").GetString()!;
    string origin = entry.GetProperty("destination").GetString()!;
    string originalHash = entry.GetProperty("originalSha256").GetString()!;
    string fixedHash = entry.GetProperty("fixedSha256").GetString()!;
    Require(Hash(originalPath).Equals(originalHash, StringComparison.OrdinalIgnoreCase), "Original hash changed: " + relative);
    Require(Hash(overlay).Equals(fixedHash, StringComparison.OrdinalIgnoreCase), "Local hotfix changed: " + relative);
    Require(Hash(origin).Equals(fixedHash, StringComparison.OrdinalIgnoreCase), "Installed origin changed: " + relative);
    string baseline = File.ReadAllText(originalPath), local = File.ReadAllText(overlay);
    var before = LocalCorrectionDocument.Parse(baseline).Root!.Elements("element").ToList();
    var after = LocalCorrectionDocument.Parse(local).Root!.Elements("element").ToList();
    var changes = new List<LocalCorrection>();
    var covered = new HashSet<XElement>();
    string fileKey = Path.GetFileNameWithoutExtension(relative);
    string? group = fileKey is "fighter-devout" or "race-tatsumi" ? "hotfix-20260913-" + fileKey : null;
    foreach (var rename in entry.GetProperty("changes").EnumerateObject())
    {
        string oldId = rename.Value[0].GetString()!, newId = rename.Value[1].GetString()!;
        var original = before.Single(e => Id(e) == oldId && (string?)e.Attribute("name") == rename.Name);
        Require(after.Count(e => Id(e) == newId) == 1, "Missing renamed definition " + newId);
        covered.Add(original);
        changes.Add(new("rename-" + newId, "rename", oldId, newId, Fingerprint(original),
            Group: group, Reason: "2026-09-13 reviewed content repair: give " + rename.Name + " its own identity; retain the other definition."));
    }
    foreach (var original in before.Where(e => !covered.Contains(e)))
    {
        var matches = after.Where(e => Id(e) == Id(original)).ToList();
        if (matches.Any(e => Fingerprint(e) == Fingerprint(original))) continue;
        if (fileKey == "guns" && (string?)original.Attribute("name") == "Musketball (20)")
            changes.Add(new("remove-musketball-pack", "remove", Id(original), null, Fingerprint(original),
                Reason: "Keep the single-piece Musketball; quantities represent multiples of 20."));
        else
        {
            Require(matches.Count == 1 && group != null, "Unexpected unclassified edit: " + Id(original));
            changes.Add(new("repair-parent-" + Id(original), "replace", Id(original), null, Fingerprint(original),
                Group: group, Reason: "Repair parent grants together with the related identity changes."));
        }
    }
    string annotated = LocalCorrectionDocument.Create(local, baseline, relative, changes);
    var parsed = LocalCorrectionDocument.Parse(annotated);
    parsed.Root!.Element(XName.Get("corrections", LocalCorrectionDocument.Namespace))!.Remove();
    Require(Content(parsed.ToString(SaveOptions.DisableFormatting)).SequenceEqual(Content(local)), "Annotation changed gameplay.");
    var oldEvaluation = LocalCorrectionDocument.Evaluate(annotated, baseline);
    var currentEvaluation = LocalCorrectionDocument.Evaluate(annotated, File.ReadAllText(origin));
    foreach (var evaluation in new[] { oldEvaluation, currentEvaluation })
    {
        Require(Content(evaluation.EffectiveXml).SequenceEqual(Content(local)), "Effective gameplay differs: " + relative);
        Require(!evaluation.CanRetire && evaluation.ReviewReasons.Count == changes.Count &&
            !evaluation.ReviewReasons.Any(r => r.Contains("unclassified", StringComparison.OrdinalIgnoreCase)), "Incomplete classification: " + relative);
    }
    string accepted = LocalCorrectionDocument.Create(local, baseline, relative, changes.Select(c => c with { State = "accepted-upstream" }));
    Require(LocalCorrectionDocument.Evaluate(accepted, local).CanRetire, "Reviewed matching content must be retireable.");
    string stagedOrigin = Path.Combine(root, relative);
    string stagedLocal = Path.Combine(root, "user/local/id-hotfixes-20260913", relative);
    Write(stagedOrigin, baseline); // Exercise an unfixed upstream reimport, not only the already-fixed installed copy.
    Write(stagedLocal, annotated);
    string backup = Path.Combine(work, "unannotated", relative);
    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
    File.Copy(overlay, backup);
    operations += changes.Count;
    elementCount += after.Count;
    report.Add(new { relativePath = relative, destination = overlay, sourcePath = origin,
        stagedPath = stagedLocal, backupPath = backup, previousSha256 = fixedHash,
        baselineSha256 = originalHash, annotatedSha256 = Hash(stagedLocal), elements = after.Count,
        corrections = changes, currentReviewReasons = currentEvaluation.ReviewReasons,
        suppressedIds = currentEvaluation.SuppressedIds });
}
Require(operations == 10 && elementCount == 247, "Unexpected repair/element totals.");

// Preserve the legitimate XGTE Staff while repairing the malformed DMG reuse.
string custom = Path.GetDirectoryName(entries[0].GetProperty("overlay").GetString()!)!;
while (Path.GetFileName(custom) != "custom") custom = Path.GetDirectoryName(custom) ?? throw new IOException("Missing content root.");
string xgtePath = Path.Combine(custom, "supplements/xanathars-guide-to-everything/items-wondrous.xml");
var xgte = LocalCorrectionDocument.Parse(File.ReadAllText(xgtePath));
var staff = xgte.Root!.Elements("element").Single(e => Id(e) == "ID_WOTC_XGTE_MAGIC_ITEM_STAFF_OF_FLOWERS");
Write(Path.Combine(root, "supplements/xanathars-guide-to-everything/items-wondrous.xml"),
    new XDocument(new XElement("elements", new XElement(staff))).ToString(SaveOptions.DisableFormatting));
string database = Path.Combine(work, "verified.sqlite");
var result = await LocalCorrectionSync.ImportAsync([root], database, async (prepared, candidate, token) =>
{
    using var process = new Process { StartInfo = new ProcessStartInfo(translator)
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true } };
    foreach (string arg in new[] { "sqlite-import", prepared[0], candidate }) process.StartInfo.ArgumentList.Add(arg);
    process.Start();
    var output = process.StandardOutput.ReadToEndAsync(token);
    var error = process.StandardError.ReadToEndAsync(token);
    await process.WaitForExitAsync(token);
    File.WriteAllText(Path.Combine(work, "translator.log"), await output + "\n" + await error);
    return process.ExitCode == 0 ? AuroraImportResult.Succeeded(0, 0, 0) : AuroraImportResult.Failed("See translator.log");
});
Require(result.Success, "Staged Translator import failed.");
using (var connection = AuroraContentImporter.OpenReadableConnection(database))
{
    long Scalar(string sql) { using var command = connection.CreateCommand(); command.CommandText = sql; return Convert.ToInt64(command.ExecuteScalar()); }
    Require(Scalar("SELECT COUNT(*) FROM elements") == 248, "Expected corrected corpus plus legitimate XGTE Staff.");
    Require(Scalar("SELECT COUNT(DISTINCT aurora_id) FROM elements") == 248, "Unexpected duplicate IDs.");
    Require(Scalar("SELECT COUNT(*) FROM local_override_files WHERE status='review-required'") == 6, "Mirror file count.");
    Require(Scalar("SELECT COUNT(*) FROM local_corrections WHERE state='review-pending'") == 10, "Mirror operation count.");
    Require(Scalar("SELECT COUNT(*) FROM grants WHERE target_aurora_id IN ('ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_DEFENDER_OF_KIN','ID_JONOMAN3000_ARCHETYPE_FEATURE_DEVOUT_ZEALOTS_DEVOTION_SLAYER_OF_FOES','ID_RGTTYR_RACIAL_TRAIT_TATSUMI_RYUJIN_HEARTENING_BREATH') AND target_element_id IS NOT NULL") == 3, "Repaired grants unresolved.");
}
Require(!AuroraContentImporter.IsStale(root, database), "Fresh staged import reports stale.");
File.WriteAllText(Path.Combine(work, "annotation-evidence.json"), JsonSerializer.Serialize(new {
    createdUtc = DateTimeOffset.UtcNow, workDirectory = work, database, translatorSha256 = Hash(translator),
    files = report, operationCount = operations, correctedElements = elementCount,
    importedElements = 248, distinctIds = 248, resolvedParentGrants = 3,
    baselineAndCurrentEvaluationPassed = true, installedContentModified = false
}, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"PASS: six files, {operations} corrections, 248 unique imported elements including both Staffs, three resolved grants. Evidence: {work}");

static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
static string Hash(string path) => LocalCorrectionDocument.FileFingerprint(path);
static string Id(XElement e) => (string)e.Attribute("id")!;
static string Fingerprint(XElement e) => LocalCorrectionDocument.Fingerprint(e);
static string[] Content(string xml) => LocalCorrectionDocument.Parse(xml).Root!.Elements()
    .Where(e => e.Name != "info").Select(Fingerprint).Order(StringComparer.Ordinal).ToArray();
static void Write(string path, string content) { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, content); }
