using System.Xml;
using Builder.Presentation.Utilities;

namespace Aurora.Tests.Tests;

public sealed class CharacterFileIoTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"aurora_fileio_{Guid.NewGuid():N}");

    public CharacterFileIoTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    [Fact]
    public void LoadXmlDocument_AllowsAnotherSharedWriterHandle()
    {
        string path = WriteCharacterXml("shared-read.dnd5e", "<character><build /></character>");

        using var otherAppHandle = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        XmlDocument doc = CharacterFileIo.LoadXmlDocument(path);

        doc.DocumentElement?.Name.Should().Be("character");
    }

    [Fact]
    public async Task SaveXmlDocumentAtomic_RetriesUntilOpenHandleIsReleased()
    {
        string path = WriteCharacterXml("retry-save.dnd5e", "<character><old /></character>");
        using var otherAppHandle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        XmlDocument replacement = CreateDocument("<character><new /></character>");

        Task saveTask = Task.Run(() => CharacterFileIo.SaveXmlDocumentAtomic(path, replacement));
        await Task.Delay(200);
        await otherAppHandle.DisposeAsync();

        await saveTask;

        CharacterFileIo.LoadXmlDocument(path).DocumentElement?["new"]
            .Should().NotBeNull();
    }

    [Fact]
    public void HasChangedSince_DetectsExternalEdits()
    {
        string path = WriteCharacterXml("changed.dnd5e", "<character><build /></character>");
        CharacterFileDiskStamp? stamp = CharacterFileDiskStamp.Capture(path);

        File.AppendAllText(path, " ");

        CharacterFileIo.HasChangedSince(path, stamp).Should().BeTrue();
    }

    private string WriteCharacterXml(string fileName, string xml)
    {
        string path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, xml);
        return path;
    }

    private static XmlDocument CreateDocument(string xml)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xml);
        return doc;
    }
}
