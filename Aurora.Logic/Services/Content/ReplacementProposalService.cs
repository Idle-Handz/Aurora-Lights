using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Aurora.Content.Contracts;

namespace Builder.Presentation.Services.Content;

public enum ReplacementProposalStatus { Ready, Applied, Dismissed, Unavailable, NeedsReview }

public sealed record ReplacementProposal(string Id, string Revision, string Title, string Summary,
    string ProposalPath, string ProposalHash, ReplacementProposalStatus Status, string Detail,
    string? SourcePath = null);

public sealed record ReplacementProposalApplyResult(bool Success, string Message);

/// <summary>
/// Downloaded proposals are inert documents. Only an explicit Apply writes managed XML into
/// user/local; subsequent maintenance belongs to the existing local-correction lifecycle.
/// </summary>
public sealed class ReplacementProposalService
{
    public const string Namespace = "urn:aurora-reflections:replacements:1";
    public const string Extension = ".aurora-correction";
    private const string StateName = ".reflections-replacements-state.json";
    private const int DocumentLimit = 8 * 1024 * 1024;
    private static readonly XNamespace Ns = Namespace;
    private static readonly XNamespace CorrectionsNs = LocalCorrectionDocument.Namespace;
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public IReadOnlyList<ReplacementProposal> Scan(string contentRoot, IReadOnlyList<string>? additionalRoots = null)
    {
        string root = Path.GetFullPath(contentRoot);
        lock (Gates.GetOrAdd(root, _ => new object()))
        {
            if (!Directory.Exists(root)) return [];
            EnsureRegularPath(root, root);
            return ScanCore(root, additionalRoots).Select(p => p.Proposal).ToArray();
        }
    }

    public ReplacementProposalApplyResult Apply(string contentRoot, ReplacementProposal reviewed, IReadOnlyList<string>? additionalRoots = null)
    {
        string root = Path.GetFullPath(contentRoot);
        lock (Gates.GetOrAdd(root, _ => new object()))
        {
            string? created = null;
            string? createdHash = null;
            try
            {
                EnsureRegularPath(root, root);
                using var lease = AcquireLease(root);
                var current = FindReviewed(root, reviewed, additionalRoots);
                if (current.Proposal.Status != ReplacementProposalStatus.Ready || current.Variant is null)
                    return new(false, current.Proposal.Detail);
                var variant = current.Variant;
                var state = ReadState(root);
                string destination = Destination(root, reviewed.Id);
                EnsureRegularPath(root, destination);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                EnsureRegularPath(root, destination);
                // The second read closes the review-to-apply window, including updates to the feed.
                VerifyInputs(root, reviewed, variant, current.LocalSignature, additionalRoots);
                string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, variant.Payload, Utf8);
                    EnsureRegularPath(root, destination);
                    VerifyInputs(root, reviewed, variant, current.LocalSignature, additionalRoots);
                    File.Move(temporary, destination, overwrite: false);
                    created = destination;
                    createdHash = Hash(Utf8.GetBytes(variant.Payload));
                    VerifyInputs(root, reviewed, variant, current.LocalSignature, additionalRoots, destination);
                    WriteReceipt(root, state, reviewed, "applied");
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                return new(true, $"Applied {reviewed.Title}. Refresh the database to use this local correction.");
            }
            catch (Exception ex) when (IsContentException(ex))
            {
                // Never remove someone else's concurrent edit while rolling back a failed activation.
                if (created is not null && File.Exists(created) && FileHash(created) == createdHash)
                    File.Delete(created);
                return new(false, $"Correction was not applied: {ex.Message}");
            }
        }
    }

    public void Dismiss(string contentRoot, ReplacementProposal reviewed)
    {
        string root = Path.GetFullPath(contentRoot);
        lock (Gates.GetOrAdd(root, _ => new object()))
        {
            EnsureRegularPath(root, root);
            using var lease = AcquireLease(root);
            var current = FindReviewed(root, reviewed);
            if (current.Proposal.Status == ReplacementProposalStatus.Applied) return;
            WriteReceipt(root, ReadState(root), reviewed, "dismissed");
        }
    }

    public void Reconsider(string contentRoot, ReplacementProposal reviewed)
    {
        string root = Path.GetFullPath(contentRoot);
        lock (Gates.GetOrAdd(root, _ => new object()))
        {
            EnsureRegularPath(root, root);
            using var lease = AcquireLease(root);
            var current = FindReviewed(root, reviewed);
            if (current.Proposal.Status != ReplacementProposalStatus.Dismissed) return;
            var snapshot = ReadState(root);
            var receipts = snapshot.State.Receipts.Where(r => !(r.Id == reviewed.Id && r.Revision == reviewed.Revision &&
                r.ProposalHash == reviewed.ProposalHash && r.Action == "dismissed")).ToList();
            WriteState(root, snapshot, receipts);
        }
    }

    private static Candidate FindReviewed(string root, ReplacementProposal reviewed, IReadOnlyList<string>? additionalRoots = null)
    {
        EnsureRegularPath(root, reviewed.ProposalPath);
        var current = ScanCore(root, additionalRoots).SingleOrDefault(c =>
            string.Equals(c.Proposal.ProposalPath, reviewed.ProposalPath, StringComparison.OrdinalIgnoreCase));
        if (current is null || current.Proposal.Id != reviewed.Id || current.Proposal.Revision != reviewed.Revision ||
            current.Proposal.ProposalHash != reviewed.ProposalHash || current.Proposal.SourcePath != reviewed.SourcePath)
            throw new InvalidDataException("The proposal changed since it was reviewed. Review the current version first.");
        return current;
    }

    private static List<Candidate> ScanCore(string root, IReadOnlyList<string>? additionalRoots = null)
    {
        StateSnapshot? state = null;
        string? stateError = null;
        try { state = ReadState(root); }
        catch (Exception ex) when (IsContentException(ex)) { stateError = "Correction history needs review: " + ex.Message; }
        var inventory = new Lazy<Inventory>(() => ReadInventory(root, additionalRoots));
        var candidates = new List<Candidate>();
        foreach (string path in SafeFiles(root).Where(p => p.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) && !IsLocal(root, p)))
        {
            string hash = "";
            ProposalDocument? document = null;
            try
            {
                byte[] bytes = ReadBytes(path, DocumentLimit);
                hash = Hash(bytes);
                document = ReadProposal(DecodeXml(bytes));
                var basic = new ReplacementProposal(document.Id, document.Revision, document.Title, document.Summary,
                    path, hash, ReplacementProposalStatus.Unavailable, "");
                if (stateError is not null)
                {
                    candidates.Add(new(basic with { Status = ReplacementProposalStatus.NeedsReview, Detail = stateError }, null));
                    continue;
                }
                var receipt = state!.State.Receipts.FirstOrDefault(r => r.Id == document.Id &&
                    r.Revision == document.Revision && r.ProposalHash == hash);
                if (receipt is not null)
                {
                    candidates.Add(new(basic with
                    {
                        Status = receipt.Action == "applied" ? ReplacementProposalStatus.Applied : ReplacementProposalStatus.Dismissed,
                        Detail = receipt.Action == "applied"
                            ? "Previously applied. The local correction follows its normal review and retirement lifecycle."
                            : "Dismissed for this version. A revised proposal can be reviewed again."
                    }, null));
                    continue;
                }
                candidates.Add(Assess(root, basic, document, inventory));
            }
            catch (Exception ex) when (IsContentException(ex))
            {
                candidates.Add(new(new(document?.Id ?? Path.GetFileNameWithoutExtension(path), document?.Revision ?? "",
                    document?.Title ?? Path.GetFileName(path), document?.Summary ?? "", path, hash,
                    ReplacementProposalStatus.NeedsReview, "Invalid correction proposal: " + ex.Message), null));
            }
        }
        // Ambiguous identities must not be silently resolved by filesystem enumeration order.
        foreach (var group in candidates.GroupBy(c => c.Proposal.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            foreach (var item in group.ToArray())
            {
                int index = candidates.IndexOf(item);
                candidates[index] = item with { Proposal = item.Proposal with
                { Status = ReplacementProposalStatus.NeedsReview, Detail = "Multiple downloaded proposals use this identity. Keep one feed copy before applying." } };
            }
        return candidates.OrderBy(c => c.Proposal.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static Candidate Assess(string root, ReplacementProposal basic, ProposalDocument document, Lazy<Inventory> inventory)
    {
        var installed = document.Variants.Where(v => !v.SourcePath.StartsWith("ignore/", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(LocalCorrectionDocument.ResolveSourcePath(root, v.SourcePath))).ToArray();
        if (installed.Length == 0)
            return new(basic with { Detail = "The source file is not installed: " + string.Join(" or ", document.Variants.Select(v => v.SourcePath)) }, null);
        if (installed.Length != 1)
            return new(basic with { Status = ReplacementProposalStatus.NeedsReview, Detail = "More than one supported source layout is installed. Review the duplicate sources first." }, null);
        var variant = installed[0];
        basic = basic with { SourcePath = variant.SourcePath };
        string source = LocalCorrectionDocument.ResolveSourcePath(root, variant.SourcePath);
        byte[] sourceBytes = ReadBytes(source, DocumentLimit);
        string sourceXml = DecodeXml(sourceBytes);
        var evaluation = LocalCorrectionDocument.Evaluate(variant.Payload, sourceXml);
        var existing = inventory.Value.Managed.Where(m => string.Equals(m.SourcePath, variant.SourcePath, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (existing.Length != 0)
        {
            if (existing.Length == 1 && Equivalent(existing[0].Xml, variant.Payload, sourceXml))
                return new(basic with { Status = ReplacementProposalStatus.Applied, Detail = "These corrections are already present in " + existing[0].RelativePath }, null);
            return new(basic with { Status = ReplacementProposalStatus.NeedsReview,
                Detail = "An existing local correction already manages this source: " + string.Join(", ", existing.Select(m => m.RelativePath)) + ". It will not be overwritten." }, null);
        }
        if (evaluation.Corrections.All(c => evaluation.ReviewReasons.Contains(c.Key + ": incorporated; review before clearing")))
            return new(basic with { Status = ReplacementProposalStatus.Unavailable, Detail = "The installed source already contains these corrections." }, null);
        if (Hash(sourceBytes) != variant.SourceHash)
            return new(basic with { Status = ReplacementProposalStatus.NeedsReview, Detail = "The installed source differs from the reviewed version. An updated proposal is needed: " + variant.SourcePath }, null);
        if (LocalCorrectionDocument.Fingerprint(ParseXml(evaluation.BaselineXml).Root!) != LocalCorrectionDocument.Fingerprint(ParseXml(sourceXml).Root!))
            throw new InvalidDataException("The correction baseline does not match its reviewed source.");
        if (inventory.Value.LocalErrors.Count > 0)
            return new(basic with { Status = ReplacementProposalStatus.NeedsReview, Detail = "An unreadable local override prevents a safe overlap check: " + inventory.Value.LocalErrors[0] }, null);
        var touched = evaluation.Corrections.SelectMany(c => new[] { c.TargetId, c.ReplacementId }).Where(id => id is not null).ToHashSet(StringComparer.Ordinal);
        var overlaps = inventory.Value.Unmanaged.Where(m => m.Ids.Any(touched.Contains)).ToArray();
        if (overlaps.Length > 0)
            return new(basic with { Status = ReplacementProposalStatus.NeedsReview, Detail = "An existing user override contains affected definitions: " + string.Join(", ", overlaps.Select(m => m.RelativePath)) + ". Review it before applying." }, null);
        string[] missing = variant.Requires.Where(id => !inventory.Value.Ids.Contains(id)).ToArray();
        if (missing.Length > 0)
            return new(basic with { Detail = "Required content is not installed: " + string.Join(", ", missing) }, null);
        string destination = Destination(root, document.Id);
        EnsureRegularPath(root, destination);
        if (File.Exists(destination) || Directory.Exists(destination))
            return new(basic with { Status = ReplacementProposalStatus.NeedsReview, Detail = "The proposed local filename is already in use. It will not be overwritten." }, null);
        return new(basic with { Status = ReplacementProposalStatus.Ready, Detail = "Ready to create a local correction for " + variant.SourcePath }, variant, inventory.Value.LocalSignature);
    }

    private static bool Equivalent(string existing, string proposal, string upstream)
    {
        try
        {
            var actual = LocalCorrectionDocument.Evaluate(existing, upstream);
            var expected = LocalCorrectionDocument.Evaluate(proposal, upstream);
            string[] Content(string xml) => ParseXml(xml).Root!.Elements().Where(e => e.Name != "info" && e.Name != CorrectionsNs + "corrections")
                .Select(LocalCorrectionDocument.Fingerprint).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            return Content(actual.EffectiveXml).SequenceEqual(Content(expected.EffectiveXml)) &&
                expected.Corrections.All(e => actual.Corrections.Any(a => a.Operation == e.Operation &&
                    a.TargetId == e.TargetId && a.ReplacementId == e.ReplacementId && a.OriginalFingerprint == e.OriginalFingerprint));
        }
        catch (Exception ex) when (IsContentException(ex)) { return false; }
    }

    private static ProposalDocument ReadProposal(string xml)
    {
        var root = ParseXml(xml).Root!;
        if (root.Name != "elements" || root.Elements().Any(e => e.Name != "info" && e.Name != Ns + "proposal") ||
            root.Elements(Ns + "proposal").Count() != 1)
            throw new InvalidDataException("Expected an inert elements envelope with one proposal and no gameplay declarations.");
        var proposal = root.Element(Ns + "proposal")!;
        if ((string?)proposal.Attribute("schema-version") != "1" ||
            proposal.Elements().Any(e => e.Name != Ns + "title" && e.Name != Ns + "summary" && e.Name != Ns + "variant"))
            throw new InvalidDataException("Unsupported correction proposal schema.");
        string id = Required(proposal, "id");
        if (!Regex.IsMatch(id, "^[a-z0-9][a-z0-9-]{0,79}$", RegexOptions.CultureInvariant) ||
            Regex.IsMatch(id, "^(con|prn|aux|nul|com[1-9]|lpt[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            throw new InvalidDataException("Proposal identity must be a portable lowercase slug.");
        string revision = Required(proposal, "revision");
        if (revision.Length > 100) throw new InvalidDataException("Proposal revision is too long.");
        string Text(string name, int max)
        {
            var elements = proposal.Elements(Ns + name).ToArray();
            if (elements.Length != 1 || elements[0].HasElements || string.IsNullOrWhiteSpace(elements[0].Value) || elements[0].Value.Length > max)
                throw new InvalidDataException($"Proposal needs one text-only {name}.");
            return elements[0].Value;
        }
        var variants = proposal.Elements(Ns + "variant").Select(v =>
        {
            string source = Required(v, "source-path");
            if (source.Contains('\\') || source.Split('/').Any(p => p is "" or "." or ".." || p.Contains(':')) || Path.IsPathRooted(source))
                throw new InvalidDataException("Proposal source-path must be a canonical relative path without traversal.");
            // ResolveSourcePath applies authoritative-source rules even if the source is absent.
            LocalCorrectionDocument.ResolveSourcePath(Path.GetTempPath(), source);
            string sourceHash = RequiredHash(v, "source-sha256");
            if (v.Elements().Any(e => e.Name != Ns + "requires" && e.Name != Ns + "payload") || v.Elements(Ns + "payload").Count() != 1)
                throw new InvalidDataException("Invalid proposal variant.");
            var payloadElement = v.Element(Ns + "payload")!;
            if (payloadElement.HasElements) throw new InvalidDataException("Proposal payload must contain escaped XML text.");
            string payload = payloadElement.Value;
            if (Hash(Utf8.GetBytes(payload)) != RequiredHash(payloadElement, "sha256"))
                throw new InvalidDataException("Proposal payload checksum does not match.");
            var payloadRoot = ParseXml(payload).Root;
            var metadata = payloadRoot?.Elements(CorrectionsNs + "corrections").SingleOrDefault();
            string? baseline = metadata?.Element(CorrectionsNs + "baseline")?.Value;
            if (metadata is null || baseline is null || (string?)metadata.Attribute("source-path") != source)
                throw new InvalidDataException("Managed payload origin does not match its proposal variant.");
            var validated = LocalCorrectionDocument.Evaluate(payload, baseline);
            if (validated.Corrections.Count == 0 || validated.Corrections.Any(c => c.State != "review-pending") ||
                validated.ReviewReasons.Any(r => !validated.Corrections.Any(c => r == c.Key + ": pinned")))
                throw new InvalidDataException("Proposal must contain only explicit, active corrections to its baseline.");
            return new Variant(source, sourceHash, payload, v.Elements(Ns + "requires").Select(r => Required(r, "id")).Distinct(StringComparer.Ordinal).ToArray());
        }).ToArray();
        if (variants.Length is 0 or > 16 || variants.Select(v => v.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != variants.Length)
            throw new InvalidDataException("Proposal needs distinct supported source variants.");
        return new(id, revision, Text("title", 300), Text("summary", 8000), variants);
    }

    private static Inventory ReadInventory(string root, IReadOnlyList<string>? additionalRoots)
    {
        var result = ReadRootInventory(root);
        foreach (string additional in ContentRoots(root, additionalRoots).Skip(1))
        {
            if (!Directory.Exists(additional)) continue;
            EnsureRegularPath(additional, additional);
            var other = ReadRootInventory(additional);
            result.Ids.UnionWith(other.Ids);
            result.Unmanaged.AddRange(other.UserDocuments.Select(d => d with { RelativePath = Path.Combine(additional, d.RelativePath) }));
            result.LocalErrors.AddRange(other.LocalErrors.Select(e => additional + ": " + e));
            result.LocalFiles.AddRange(other.LocalFiles);
        }
        return result;
    }

    private static Inventory ReadRootInventory(string root)
    {
        var result = new Inventory();
        var fileIds = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var overriddenSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in SafeFiles(root).Where(p => p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
        {
            bool local = IsLocal(root, path);
            bool user = IsUser(root, path);
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            try
            {
                byte[] bytes = ReadBytes(path, DocumentLimit);
                if (user) result.LocalFiles.Add(path + "\0" + Hash(bytes));
                string xml = DecodeXml(bytes);
                var document = ParseXml(xml);
                if (document.Root?.Name != "elements") continue;
                if (bool.TryParse((string?)document.Root.Attribute("ignore"), out bool ignored) && ignored) continue;
                var metadata = document.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "corrections");
                if (local && metadata is not null)
                {
                    string source = Required(metadata, "source-path");
                    string origin = LocalCorrectionDocument.ResolveSourcePath(root, source);
                    string canonicalSource = Path.GetRelativePath(root, origin).Replace('\\', '/');
                    result.Managed.Add(new(canonicalSource, xml, relative));
                    document = ParseXml(LocalCorrectionDocument.Evaluate(xml, DecodeXml(ReadBytes(origin, DocumentLimit))).EffectiveXml);
                    overriddenSources.Add(canonicalSource);
                }
                else if (metadata is not null)
                {
                    if (user) result.LocalErrors.Add(relative + ": managed correction metadata belongs under user/local.");
                    continue;
                }
                var ids = document.Root!.Elements("element").Select(e => (string?)e.Attribute("id")).Where(id => !string.IsNullOrEmpty(id)).Cast<string>().ToArray();
                fileIds[relative] = ids;
                if (user && metadata is null) result.Unmanaged.Add(new(relative, ids));
                if (user) result.UserDocuments.Add(new(relative, ids));
            }
            catch (Exception ex) when (IsContentException(ex))
            {
                if (user) result.LocalErrors.Add(relative + ": " + ex.Message);
            }
        }
        foreach (var file in fileIds.Where(f => !overriddenSources.Contains(f.Key))) result.Ids.UnionWith(file.Value);
        return result;
    }

    private static void VerifyInputs(string root, ReplacementProposal reviewed, Variant variant, string? localSignature,
        IReadOnlyList<string>? additionalRoots, string? excludeLocal = null)
    {
        EnsureRegularPath(root, reviewed.ProposalPath);
        if (FileHash(reviewed.ProposalPath) != reviewed.ProposalHash ||
            FileHash(LocalCorrectionDocument.ResolveSourcePath(root, variant.SourcePath)) != variant.SourceHash)
            throw new InvalidDataException("The proposal or source changed while applying. Review the current version first.");
        if (localSignature is not null)
        {
            var localFiles = ContentRoots(root, additionalRoots).Where(Directory.Exists).SelectMany(contentRoot =>
            {
                EnsureRegularPath(contentRoot, contentRoot);
                return SafeFiles(contentRoot).Where(p => IsUser(contentRoot, p) && p.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(p, excludeLocal, StringComparison.OrdinalIgnoreCase)).Select(p => p + "\0" + FileHash(p));
            });
            if (InventorySignature(localFiles) != localSignature)
                throw new InvalidDataException("Local overrides changed while applying. Review their overlap with this correction first.");
        }
    }

    private static StateSnapshot ReadState(string root)
    {
        string path = Path.Combine(root, StateName);
        EnsureRegularPath(root, path);
        if (!File.Exists(path)) return new(new State(1, []), null);
        byte[] bytes = ReadBytes(path, 1024 * 1024);
        var state = JsonSerializer.Deserialize<State>(bytes) ?? throw new InvalidDataException("Correction history is empty.");
        if (state.Version != 1 || state.Receipts is null || state.Receipts.Any(r => r is null || string.IsNullOrEmpty(r.Id) ||
            string.IsNullOrEmpty(r.Revision) || !IsHash(r.ProposalHash) || r.Action is not ("applied" or "dismissed")))
            throw new InvalidDataException("Unsupported correction history; preserve it for review.");
        return new(state, Hash(bytes));
    }

    private static void WriteReceipt(string root, StateSnapshot snapshot, ReplacementProposal proposal, string action)
    {
        var receipts = snapshot.State.Receipts.Where(r => r.Id != proposal.Id || r.Revision != proposal.Revision || r.ProposalHash != proposal.ProposalHash).ToList();
        receipts.Add(new(proposal.Id, proposal.Revision, proposal.ProposalHash, action));
        WriteState(root, snapshot, receipts);
    }

    private static void WriteState(string root, StateSnapshot snapshot, List<Receipt> receipts)
    {
        string path = Path.Combine(root, StateName);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new State(1, receipts), new JsonSerializerOptions { WriteIndented = true }), Utf8);
            EnsureRegularPath(root, path);
            if ((File.Exists(path) ? FileHash(path) : null) != snapshot.Hash)
                throw new IOException("Correction history changed concurrently. Try again after reviewing it.");
            File.Move(temporary, path, overwrite: snapshot.Hash is not null);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static FileStream AcquireLease(string root)
    {
        string path = Path.Combine(root, ".reflections-replacements.lock");
        EnsureRegularPath(root, path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private static string Destination(string root, string id) => Path.Combine(root, "user", "local", "reflections-replacements", id + ".xml");
    private static bool IsLocal(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith("user/local/", StringComparison.OrdinalIgnoreCase);
    private static bool IsUser(string root, string path) => Path.GetRelativePath(root, path).Replace('\\', '/').StartsWith("user/", StringComparison.OrdinalIgnoreCase);
    private static IEnumerable<string> ContentRoots(string root, IReadOnlyList<string>? additional) =>
        new[] { root }.Concat((additional ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(Path.GetFullPath)).Distinct(StringComparer.OrdinalIgnoreCase);
    private static string Required(XElement element, string name) => (string?)element.Attribute(name) is { Length: > 0 } value
        ? value : throw new InvalidDataException("Missing " + name + ".");
    private static bool IsHash(string? hash) => hash?.Length == 64 && hash.All(Uri.IsHexDigit);
    private static string RequiredHash(XElement element, string name)
    {
        string value = Required(element, name);
        return IsHash(value) ? value.ToUpperInvariant() : throw new InvalidDataException("Invalid " + name + ".");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string FileHash(string path) => Hash(ReadBytes(path, DocumentLimit));
    private static byte[] ReadBytes(string path, int limit)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > limit) throw new InvalidDataException("File exceeds the correction document size limit.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        if (output.Length > limit) throw new InvalidDataException("File exceeds the correction document size limit.");
        return output.ToArray();
    }
    private static string DecodeXml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
    private static XDocument ParseXml(string xml)
    {
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = DocumentLimit });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }
    private static bool IsContentException(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or XmlException or JsonException
        or ArgumentException or InvalidOperationException;

    private static void EnsureRegularPath(string root, string path)
    {
        string full = Path.GetFullPath(path);
        string relative = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Correction paths must stay inside the content directory.");
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if (File.Exists(current) || Directory.Exists(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Correction paths cannot traverse symbolic links or junctions.");
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase)) break;
        }
    }

    private static IEnumerable<string> SafeFiles(string root)
    {
        var directories = new Stack<string>();
        directories.Push(root);
        while (directories.Count > 0)
        {
            string directory = directories.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory).OrderBy(s => s, StringComparer.Ordinal))
            {
                var attributes = File.GetAttributes(entry);
                if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                if (string.Equals(Path.GetRelativePath(root, entry), "ignore", StringComparison.OrdinalIgnoreCase)) continue;
                if ((attributes & FileAttributes.Directory) != 0) directories.Push(entry);
                else yield return entry;
            }
        }
    }

    private sealed record Variant(string SourcePath, string SourceHash, string Payload, string[] Requires);
    private sealed record ProposalDocument(string Id, string Revision, string Title, string Summary, Variant[] Variants);
    private sealed record Candidate(ReplacementProposal Proposal, Variant? Variant, string? LocalSignature = null);
    private sealed record Managed(string SourcePath, string Xml, string RelativePath);
    private sealed record Unmanaged(string RelativePath, string[] Ids);
    private sealed record Receipt(string Id, string Revision, string ProposalHash, string Action);
    private sealed record State(int Version, List<Receipt> Receipts);
    private sealed record StateSnapshot(State State, string? Hash);
    private sealed class Inventory
    {
        public HashSet<string> Ids { get; } = new(StringComparer.Ordinal);
        public List<Managed> Managed { get; } = [];
        public List<Unmanaged> Unmanaged { get; } = [];
        public List<Unmanaged> UserDocuments { get; } = [];
        public List<string> LocalErrors { get; } = [];
        public List<string> LocalFiles { get; } = [];
        public string LocalSignature => InventorySignature(LocalFiles);
    }
    private static string InventorySignature(IEnumerable<string> files) => Hash(Utf8.GetBytes(string.Join("\n", files.OrderBy(f => f, StringComparer.Ordinal))));
}
