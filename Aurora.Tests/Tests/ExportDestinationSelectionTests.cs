using Aurora.App.Services;

namespace Aurora.Tests.Tests;

public sealed class ExportDestinationSelectionTests
{
    [Theory]
    [InlineData(ExistingExportChoice.Cancel)]
    [InlineData(ExistingExportChoice.Replace)]
    public async Task ExistingFileRequiresAnExplicitReplacementDecision(ExistingExportChoice choice)
    {
        string folder = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "export-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string file = Path.Combine(folder, "Hero.pdf");
            await File.WriteAllTextAsync(file, "Keep existing export");
            int confirmations = 0;
            string? selected = await ExportDestinationSelection.ChooseAsync(folder, "Hero.pdf",
                (name, error) => Task.FromResult<string?>(name), path =>
                {
                    confirmations++;
                    path.Should().Be(file);
                    return Task.FromResult(choice);
                });
            confirmations.Should().Be(1);
            selected.Should().Be(choice == ExistingExportChoice.Replace ? file : null);
            (await File.ReadAllTextAsync(file)).Should().Be("Keep existing export");
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public async Task ChangeNameReturnsADifferentDestinationAndPreservesExtension()
    {
        var names = new Queue<string?>(["Hero.pdf", "Hero revised"]);
        var errors = new List<string?>();
        string? selected = await ExportDestinationSelection.ChooseAsync(Path.GetTempPath(), "Hero.pdf",
            (_, error) => { errors.Add(error); return Task.FromResult(names.Dequeue()); },
            _ => Task.FromResult(ExistingExportChoice.ChangeName),
            file => Path.GetFileName(file) == "Hero.pdf");
        Path.GetFileName(selected).Should().Be("Hero revised.pdf");
        errors.Should().HaveCount(2);
        errors[1].Should().NotBeNull();
    }

    [Theory]
    [InlineData("../other.pdf")]
    [InlineData("other/folder.pdf")]
    [InlineData("other\\folder.pdf")]
    [InlineData("wrong.txt")]
    [InlineData("")]
    public async Task InvalidOrWrongFormatNamesAreRetriedBeforeReturningAPath(string invalid)
    {
        var names = new Queue<string?>([invalid, "Valid.pdf"]);
        int count = 0;
        string? selected = await ExportDestinationSelection.ChooseAsync(Path.GetTempPath(), "Hero.pdf",
            (_, error) => { if (count++ > 0) error.Should().NotBeNull(); return Task.FromResult(names.Dequeue()); },
            _ => throw new Exception("No overwrite expected"), _ => false);
        Path.GetFileName(selected).Should().Be("Valid.pdf");
        count.Should().Be(2);
    }

    [Fact]
    public async Task CancellingNameEntryDoesNotChooseAnyFile()
    {
        (await ExportDestinationSelection.ChooseAsync(Path.GetTempPath(), "Hero.dnd5e",
            (_, _) => Task.FromResult<string?>(null), _ => throw new Exception("No overwrite expected")))
            .Should().BeNull();
    }
}
