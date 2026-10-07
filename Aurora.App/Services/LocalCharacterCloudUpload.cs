using Builder.Presentation.Services.Storage;

namespace Aurora.App.Services;

/// <summary>Uploads an app-owned character without handing its private path to a file picker.</summary>
public static class LocalCharacterCloudUpload
{
    public static async Task<CharacterDocumentMetadata> UploadAsync(
        ICharacterDocumentStore store, CharacterTab tab, CancellationToken cancellationToken = default)
    {
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
            byte[] payload;
            await tab.FileSaveSemaphore.WaitAsync(cancellationToken);
            try
            {
                tab.File.EnsureNoExternalFileChanges();
                byte[] character = await File.ReadAllBytesAsync(tab.File.FilePath, cancellationToken);
                byte[] session = await File.ReadAllBytesAsync(SessionStore.GetSidecarPath(tab.File.FilePath), cancellationToken);
                payload = await Task.Run(() =>
                {
                    var packed = CloudCharacterSession.Pack(character, session);
                    CloudCharacterSession.ValidateCharacter(packed);
                    return packed;
                }, cancellationToken);
            }
            finally { tab.FileSaveSemaphore.Release(); }

            cancellationToken.ThrowIfCancellationRequested();
            return await store.CreateAsync(Path.GetFileName(tab.File.FilePath), payload,
                cancellationToken: cancellationToken);
        }
        finally { tab.IsSaving = false; }
    }
}
