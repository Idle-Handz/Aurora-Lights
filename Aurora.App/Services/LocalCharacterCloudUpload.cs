using Builder.Presentation.Services.Storage;
using Builder.Presentation.Utilities;

namespace Aurora.App.Services;

/// <summary>Converts a local character to a Drive character after a confirmed upload.</summary>
public static class LocalCharacterCloudUpload
{
    public static async Task<CharacterDocumentMetadata> UploadAsync(
        ICharacterDocumentStore store, CharacterTab tab, string workspaceRoot, string accountId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (tab.CloudSession is not null)
            throw new InvalidOperationException("This character already uses Drive. Use Save to Drive instead.");
        if (tab.IsLoading || tab.IsSaving || tab.Character is null)
            throw new InvalidOperationException("Wait for the character to finish loading or saving.");
        cancellationToken.ThrowIfCancellationRequested();

        // Capture current Overview/build edits and session state, not just the last saved XML.
        long savedEditVersion = tab.EditVersion;
        string? error = await BuildService.SaveTabAsync(tab);
        if (error is not null) throw new IOException(error);
        if (tab.EditVersion == savedEditVersion) tab.IsDirty = false;
        tab.IsSaving = true;
        try
        {
            await tab.FileSaveSemaphore.WaitAsync(cancellationToken);
            try
            {
                string originalPath = tab.File.FilePath;
                tab.File.EnsureNoExternalFileChanges();
                byte[] character = await File.ReadAllBytesAsync(originalPath, cancellationToken);
                string sidecarPath = SessionStore.GetSidecarPath(originalPath);
                byte[] session = await File.ReadAllBytesAsync(sidecarPath, cancellationToken);
                byte[] payload = await Task.Run(() =>
                {
                    var packed = CloudCharacterSession.Pack(character, session);
                    CloudCharacterSession.ValidateCharacter(packed);
                    return packed;
                }, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                var created = await store.CreateAsync(Path.GetFileName(originalPath), payload,
                    cancellationToken: cancellationToken);
                CloudCharacterSession? cloud = null;
                try
                {
                    // Once Drive confirms, finish the local handoff even if navigation cancels.
                    cloud = await CloudCharacterSession.FromUploadedAsync(store, workspaceRoot, accountId,
                        new CharacterDocument(created, payload));
                    tab.File.EnsureNoExternalFileChanges();
                    if (!File.ReadAllBytes(sidecarPath).AsSpan().SequenceEqual(session))
                        throw new IOException("The session file changed during upload.");
                    var cloudStamp = CharacterFileDiskStamp.Capture(cloud.FilePath);
                    // Removing the standalone character is the commit point. The complete
                    // character/session is already durable both on Drive and in its workspace.
                    File.Delete(originalPath);
                    tab.File.FilePath = cloud.FilePath;
                    tab.File.RestoreKnownDiskStamp(cloudStamp);
                    tab.CloudSession = cloud;
                    tab.CloudSavedEditVersion = savedEditVersion;
                    tab.IsDirty = tab.EditVersion != savedEditVersion;
                    tab.CloudNotice = "This character now saves to Google Drive.";
                    cloud = null; // The tab now owns the workspace lease.
                    try { File.Delete(sidecarPath); }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        tab.CloudNotice += " The old session sidecar could not be removed: " + ex.Message;
                    }
                    return created;
                }
                catch (Exception ex)
                {
                    throw new IOException("The character reached Google Drive, but the local handoff failed. "
                        + "Your local save was kept. Open the uploaded character from Cloud Saves. " + ex.Message, ex);
                }
                finally { cloud?.Dispose(); }
            }
            finally { tab.FileSaveSemaphore.Release(); }
        }
        finally { tab.IsSaving = false; }
    }

    /// <summary>Returns the current draft to the local library without deleting the Drive file.</summary>
    public static async Task DisconnectAsync(CharacterTab tab, string charactersDirectory)
    {
        if (tab.CloudSession is not { } cloud) return;
        if (tab.IsSaving || tab.IsLoading || tab.CloudReloadFailed || tab.Character is null)
            throw new InvalidOperationException("Finish loading or saving the character before disconnecting it.");
        tab.IsSaving = true;
        long editVersion = tab.EditVersion;
        try
        {
            // This also works offline and preserves edits that have not reached Drive.
            await BuildService.SaveLocalDraftAsync(tab);
            await tab.FileSaveSemaphore.WaitAsync();
            try
            {
                Directory.CreateDirectory(charactersDirectory);
                string stem = Path.GetFileNameWithoutExtension(cloud.Metadata.FileName);
                foreach (char invalid in Path.GetInvalidFileNameChars()) stem = stem.Replace(invalid, '_');
                if (string.IsNullOrWhiteSpace(stem)) stem = "Character";
                string destination;
                FileStream characterStream;
                for (int suffix = 1; ; suffix++)
                {
                    destination = Path.Combine(charactersDirectory, stem + (suffix == 1 ? "" : $"_{suffix}") + ".dnd5e");
                    if (File.Exists(SessionStore.GetSidecarPath(destination))) continue;
                    try { characterStream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None); break; }
                    catch (IOException) when (File.Exists(destination)) { }
                }
                bool sidecarCreated = false;
                CharacterFileDiskStamp? stamp;
                try
                {
                    using (characterStream)
                    {
                        byte[] bytes = File.ReadAllBytes(tab.File.FilePath);
                        characterStream.Write(bytes);
                        characterStream.Flush(flushToDisk: true);
                    }
                    using (var sidecar = new FileStream(SessionStore.GetSidecarPath(destination), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        sidecarCreated = true;
                        sidecar.Write(File.ReadAllBytes(cloud.SessionPath));
                        sidecar.Flush(flushToDisk: true);
                    }
                    stamp = CharacterFileDiskStamp.Capture(destination);
                }
                catch
                {
                    File.Delete(destination);
                    if (sidecarCreated) File.Delete(SessionStore.GetSidecarPath(destination));
                    throw;
                }
                tab.File.FilePath = destination;
                tab.File.FileName = Path.GetFileName(destination);
                tab.File.RestoreKnownDiskStamp(stamp);
                tab.CloudSession = null;
                tab.CloudNotice = null;
                tab.IsDirty = tab.EditVersion != editVersion;
                cloud.Dispose();
            }
            finally { tab.FileSaveSemaphore.Release(); }
        }
        finally { tab.IsSaving = false; }
    }
}
