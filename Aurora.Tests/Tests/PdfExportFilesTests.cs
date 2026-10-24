using Aurora.App.Services;

namespace Aurora.Tests.Tests;

public sealed class PdfExportFilesTests
{
    [Fact]
    public void PathsAreUniqueSafeAndCleanupOnlyExpiredOwnedDirectories()
    {
        string cache = Path.Combine(Path.GetTempPath(), "AuroraPdfCache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cache);
        try
        {
            string first = PdfExportFiles.CreatePath(cache, "../Hero: \\ name?");
            string second = PdfExportFiles.CreatePath(cache, "../Hero: \\ name?");
            first.Should().NotBe(second);
            string longName = PdfExportFiles.CreatePath(cache, new string('漢', 200) + "😀");
            System.Text.Encoding.UTF8.GetByteCount(Path.GetFileName(longName)).Should().BeLessThanOrEqualTo(184);
            Path.GetDirectoryName(first).Should().StartWith(Path.Combine(cache, "pdf-exports"));
            Path.GetFileName(first).Should().NotContainAny("/", "\\", ":", "?");
            File.WriteAllText(first, "recent PDF");
            string owned = Path.Combine(cache, "pdf-exports", Guid.NewGuid().ToString("N"));
            string other = Path.Combine(cache, "pdf-exports", "unrelated");
            Directory.CreateDirectory(owned); Directory.CreateDirectory(other);
            File.WriteAllText(Path.Combine(owned, "old.pdf"), "old");
            File.WriteAllText(Path.Combine(other, "keep"), "keep");
            Directory.SetLastWriteTimeUtc(owned, DateTime.UtcNow.AddDays(-3));
            Directory.SetLastWriteTimeUtc(other, DateTime.UtcNow.AddDays(-3));
            PdfExportFiles.CreatePath(cache, "");
            Directory.Exists(owned).Should().BeFalse();
            File.ReadAllText(first).Should().Be("recent PDF");
            File.ReadAllText(Path.Combine(other, "keep")).Should().Be("keep");
            PdfExportFiles.CreatePath(cache, "...").Should().EndWith("Character.pdf");
        }
        finally { Directory.Delete(cache, true); }
    }
}
