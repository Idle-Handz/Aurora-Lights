using System.Xml;
using Builder.Presentation;
using Builder.Presentation.Interfaces;
using Builder.Presentation.Services;
using Builder.Presentation.Utilities;

namespace Aurora.Reflections.Services;

public static partial class BuildService
{
    /// <summary>Exports the captured tab's current draft without saving or marking it clean.</summary>
    public static Task<XmlDocument> CaptureCharacterExportAsync(CharacterTab tab) =>
        WithExportDraftAsync(tab, () => SerializeExportDocument(tab));

    private static XmlDocument SerializeExportDocument(CharacterTab tab)
    {
        var document = new XmlDocument();
        using var stream = new MemoryStream(tab.File.SerializeCharacter(tab.Character!,
            document => ApplyPendingCustomFeatures(tab, document)));
        document.Load(stream);
        SidekickService.SynchronizeOwnerLevels(document, tab.Character!.Level);
        return document;
    }

    /// <summary>Best current engine state without blocking Android's synchronous stop callback.</summary>
    internal static XmlDocument? TryCaptureRecoveryDocument(CharacterTab tab)
    {
        using var context = CharacterContext.TryEnterActive(tab);
        if (context is null || tab.Character is null || tab.CloudReloadFailed || !tab.FileSaveSemaphore.Wait(0))
            return null;
        try
        {
            FlushPendingSnapshotEdits(tab);
            return SerializeExportDocument(tab);
        }
        finally { tab.FileSaveSemaphore.Release(); }
    }

    public static async Task ExportCharacterAsync(CharacterTab tab, string destination)
    {
        if (CharacterFileIdentity.RefersToSameFile(tab.File.FilePath, destination))
            throw new InvalidOperationException("Choose a different location to export a copy. Use Save to update the original character.");
        var document = await CaptureCharacterExportAsync(tab);
        await Task.Run(() => CharacterFileIo.SaveXmlDocumentAtomic(destination, document));
    }

    public static Task<FileInfo> ExportSheetAsync(CharacterTab tab, string destination, bool isPreview,
        ICharacterSheetGenerator? generator = null) =>
        WithExportDraftAsync(tab, () =>
            (generator ?? CharacterSheetGeneratorContext.Current
                ?? throw new InvalidOperationException("PDF export is unavailable."))
                .GenerateNewSheet(destination, isPreview));

    private static async Task<T> WithExportDraftAsync<T>(CharacterTab tab, Func<T> export)
    {
        using var context = await CharacterContext.EnterAsync(tab);
        await tab.FileSaveSemaphore.WaitAsync();
        try
        {
            if (tab.Character is null || tab.CloudReloadFailed)
                throw new InvalidOperationException("Load the character before exporting it.");
            FlushPendingSnapshotEdits(tab);
            EnsureMulticlassSelectionsRegistered();
            NormalizeSelectionState();
            return await Task.Run(export);
        }
        finally { tab.FileSaveSemaphore.Release(); }
    }
}
