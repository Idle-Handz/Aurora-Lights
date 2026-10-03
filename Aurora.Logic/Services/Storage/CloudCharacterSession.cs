using System.Security.Cryptography;
using System.Text.Json;
using System.Xml;
using Builder.Presentation.Utilities;

namespace Builder.Presentation.Services.Storage;

/// <summary>
/// A private recovery workspace for one cloud document, not a synchronized local character.
/// The receipt advances only after the provider confirms the exact uploaded bytes.
/// </summary>
public sealed class CloudCharacterSession : IDisposable
{
    private const string SessionNode = "aurora-cloud-session";
    private readonly ICharacterDocumentStore _store;
    private readonly FileStream _lease;
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private Receipt _receipt;
    public string FilePath { get; }
    public string SessionPath => FilePath + ".session.json";
    private string ReceiptPath => FilePath + ".cloud.json";
    public CharacterDocumentMetadata Metadata => _receipt.Metadata;
    public string? LastError { get; private set; }
    public bool IsSaving { get; private set; }
    public string? RecoveryPath { get; private set; }
    public bool HasPendingChanges
    {
        get
        {
            try { return HashFile(FilePath) != _receipt.CharacterHash || HashFile(SessionPath) != _receipt.SessionHash; }
            catch (IOException) { return true; }
            catch (UnauthorizedAccessException) { return true; }
        }
    }

    private CloudCharacterSession(ICharacterDocumentStore store, string path, FileStream lease, Receipt receipt)
        => (_store, FilePath, _lease, _receipt) = (store, path, lease, receipt);

    public static async Task<CloudCharacterSession> OpenAsync(
        ICharacterDocumentStore store, string workspaceRoot, string accountId,
        CharacterDocumentReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        string key = Hash(System.Text.Encoding.UTF8.GetBytes(accountId + "\n" + store.ProviderId + "\n" + reference.DocumentId));
        string directory = Path.Combine(workspaceRoot, key);
        Directory.CreateDirectory(directory);
        var lease = new FileStream(Path.Combine(directory, "workspace.lock"), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);
        string path = Path.Combine(directory, "character.dnd5e");
        try
        {
            Receipt? receipt = File.Exists(path + ".cloud.json")
                ? JsonSerializer.Deserialize<Receipt>(CharacterFileIo.LoadTextFile(path + ".cloud.json"))
                : null;
            if (receipt is not null)
            {
                if (receipt.Metadata.Reference.DocumentId != reference.DocumentId)
                    throw new InvalidDataException("Cloud recovery metadata refers to a different character.");
                var recovered = new CloudCharacterSession(store, path, lease, receipt);
                // Drive is authoritative on every open. Archive unsent edits before replacing them.
                if (recovered.HasPendingChanges)
                    recovered.PreserveRecovery();
            }
            CharacterDocument document = await store.OpenAsync(reference, cancellationToken);
            ValidateCharacter(document.Content);
            var xml = LoadXml(document.Content);
            string? sessionJson = xml.DocumentElement?[SessionNode]?.InnerText;
            if (sessionJson is not null)
                using (JsonDocument.Parse(sessionJson)) { }
            // Store the original downloaded XML; the engine preserves/ignores the extra node.
            WriteBytes(path, document.Content);
            if (sessionJson is not null)
                CharacterFileIo.SaveTextFileAtomic(path + ".session.json", sessionJson);
            else if (File.Exists(path + ".session.json"))
                File.Delete(path + ".session.json");
            receipt = new(document.Metadata, HashFile(path), HashFile(path + ".session.json"));
            CharacterFileIo.SaveTextFileAtomic(path + ".cloud.json", JsonSerializer.Serialize(receipt));
            return new CloudCharacterSession(store, path, lease, receipt);
        }
        catch { lease.Dispose(); throw; }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        IsSaving = true;
        try
        {
            byte[] character = File.ReadAllBytes(FilePath);
            byte[]? session = File.Exists(SessionPath) ? File.ReadAllBytes(SessionPath) : null;
            byte[] payload = Pack(character, session);
            // This exact candidate remains recoverable even if the process exits mid-request.
            WriteBytes(FilePath + ".pending", payload);
            var saved = await _store.SaveAsync(_receipt.Metadata, payload, cancellationToken);
            string? sessionHash = session is null ? null : Hash(session);
            // Once Drive confirms, keep an exact local mirror unless a later local edit arrived.
            if (HashFile(FilePath) == Hash(character) && HashFile(SessionPath) == sessionHash)
                WriteBytes(FilePath, payload);
            var receipt = new Receipt(saved, Hash(payload), sessionHash);
            CharacterFileIo.SaveTextFileAtomic(ReceiptPath, JsonSerializer.Serialize(receipt));
            _receipt = receipt;
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            throw;
        }
        finally { IsSaving = false; _saveGate.Release(); }
    }

    public async Task ReloadAuthoritativeAsync(CancellationToken cancellationToken = default)
    {
        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var remote = await _store.OpenAsync(Metadata.Reference, cancellationToken);
            ValidateCharacter(remote.Content);
            var xml = LoadXml(remote.Content);
            string? session = xml.DocumentElement?[SessionNode]?.InnerText;
            if (session is not null) using (JsonDocument.Parse(session)) { }
            PreserveRecovery();
            WriteBytes(FilePath, remote.Content);
            if (session is not null) CharacterFileIo.SaveTextFileAtomic(SessionPath, session);
            else if (File.Exists(SessionPath)) File.Delete(SessionPath);
            var receipt = new Receipt(remote.Metadata, HashFile(FilePath), HashFile(SessionPath));
            CharacterFileIo.SaveTextFileAtomic(ReceiptPath, JsonSerializer.Serialize(receipt));
            _receipt = receipt;
            LastError = null;
        }
        finally { _saveGate.Release(); }
    }

    private void PreserveRecovery()
    {
        if (!File.Exists(FilePath)) return;
        string recovery = Path.Combine(Path.GetDirectoryName(FilePath)!, "recovery");
        Directory.CreateDirectory(recovery);
        RecoveryPath = Path.Combine(recovery, $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.dnd5e");
        // Keep the exact local bytes even if they cannot currently be parsed.
        WriteBytes(RecoveryPath, File.ReadAllBytes(FilePath));
        if (File.Exists(SessionPath)) WriteBytes(RecoveryPath + ".session.json", File.ReadAllBytes(SessionPath));
    }

    public static byte[] Pack(byte[] character, byte[]? session)
    {
        ValidateCharacter(character);
        var xml = LoadXml(character);
        var root = xml.DocumentElement!;
        if (root[SessionNode] is { } old) root.RemoveChild(old);
        if (session is not null)
        {
            using (JsonDocument.Parse(session)) { }
            var node = xml.CreateElement(SessionNode);
            node.InnerText = System.Text.Encoding.UTF8.GetString(session);
            root.AppendChild(node);
        }
        using var stream = new MemoryStream();
        xml.Save(stream);
        return stream.ToArray();
    }

    public static void ValidateCharacter(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > GoogleDriveCharacterDocumentStore.MaximumCharacterBytes)
            throw new InvalidDataException("The cloud character is empty or too large.");
        var xml = LoadXml(bytes);
        if (xml.DocumentElement?.Name != "character" || xml.DocumentElement["build"] is null)
            throw new InvalidDataException("This is not an Aurora character file.");
    }

    private static XmlDocument LoadXml(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        var xml = new XmlDocument { XmlResolver = null };
        xml.Load(reader);
        return xml;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string? HashFile(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : null;
    private static void WriteBytes(string path, byte[] bytes)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public void Dispose() => _lease.Dispose();
    public sealed record Receipt(CharacterDocumentMetadata Metadata, string? CharacterHash, string? SessionHash);
}
