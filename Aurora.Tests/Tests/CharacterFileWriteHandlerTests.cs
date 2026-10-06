using Aurora.App.Services;
using Builder.Presentation.Models;
using MudBlazor;

namespace Aurora.Tests.Tests;

public sealed class CharacterFileWriteHandlerTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"aurora_write_handler_{Guid.NewGuid():N}");

    public CharacterFileWriteHandlerTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { }
    }

    /// <summary>Everything the handler reports or does, in the order it happened.</summary>
    private sealed class Recorder
    {
        public List<string> Events { get; } = [];
        public List<(string Message, Severity Severity, int Milliseconds)> Notes { get; } = [];
        public List<(Exception Exception, string Context)> Logs { get; } = [];
        public List<CharacterTab> Reloaded { get; } = [];
        public Func<CharacterTab, Task>? OnReload { get; set; }

        public CharacterFileWriteHandler Handler() => new(
            (message, severity, milliseconds) =>
            {
                Notes.Add((message, severity, milliseconds));
                Events.Add("note");
            },
            (exception, context) =>
            {
                Logs.Add((exception, context));
                Events.Add("log");
            },
            async tab =>
            {
                Reloaded.Add(tab);
                Events.Add("reload");
                if (OnReload is not null)
                    await OnReload(tab);
            });
    }

    private static CharacterFileWrite Write(Func<bool> write, string? logContext = null) => new(
        Operation: "Test write",
        Write: write,
        FailureMessage: "The change was made in memory, but could not be written yet.",
        ExternalChangeMessage: "The file changed on disk. Reloaded instead of overwriting.",
        ReloadFailedAction: "making more equipment changes",
        LogContext: logContext ?? "Test.Context");

    private CharacterTab NewTab(string name = "tab.dnd5e")
    {
        string path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "<character><build /></character>");
        var file = new CharacterFile(path);
        file.RefreshKnownDiskStamp();
        return new CharacterTab(file);
    }

    private static void ChangeOnDisk(CharacterTab tab) => File.AppendAllText(tab.File.FilePath, " ");

    [Fact]
    public async Task A_successful_write_says_nothing_and_lets_the_caller_carry_on()
    {
        var recorder = new Recorder();
        int writes = 0;

        bool carryOn = await recorder.Handler().WriteAsync(NewTab(), Write(() => { writes++; return true; }));

        carryOn.Should().BeTrue();
        writes.Should().Be(1);
        recorder.Notes.Should().BeEmpty();
        recorder.Reloaded.Should().BeEmpty();
        recorder.Logs.Should().BeEmpty();
    }

    [Fact]
    public async Task A_write_that_reports_failure_keeps_the_change_in_memory_and_warns_with_the_reason()
    {
        var recorder = new Recorder();

        bool carryOn = await recorder.Handler().WriteAsync(NewTab(), Write(() => false));

        carryOn.Should().BeTrue("the change is still in memory, so the page keeps working");
        var note = recorder.Notes.Should().ContainSingle().Subject;
        note.Severity.Should().Be(Severity.Warning);
        note.Milliseconds.Should().Be(7000);
        note.Message.Should().StartWith("The change was made in memory, but could not be written yet.")
            .And.Contain("Test write could not be written to the character file");
        recorder.Reloaded.Should().BeEmpty();
        recorder.Logs.Should().BeEmpty("there was no exception to log");
    }

    [Fact]
    public async Task A_write_that_throws_is_logged_against_the_pages_context_and_warned_about()
    {
        var recorder = new Recorder();
        var boom = new IOException("disk is full");

        bool carryOn = await recorder.Handler().WriteAsync(
            NewTab(), Write(() => throw boom, logContext: "Equipment.PersistCharacterFile"));

        carryOn.Should().BeTrue();
        recorder.Logs.Should().ContainSingle().Which.Should().Be((boom, "Equipment.PersistCharacterFile"));
        recorder.Notes.Should().ContainSingle().Which.Message.Should().Contain("disk is full");
        recorder.Reloaded.Should().BeEmpty();
    }

    [Fact]
    public async Task A_file_that_changed_on_disk_is_not_overwritten_and_the_character_is_reloaded()
    {
        var recorder = new Recorder();
        var tab = NewTab();
        ChangeOnDisk(tab);
        bool writerCalled = false;
        bool afterReloadCalled = false;

        bool carryOn = await recorder.Handler().WriteAsync(
            tab,
            Write(() => { writerCalled = true; return true; }),
            afterReload: () => { afterReloadCalled = true; recorder.Events.Add("after"); return Task.CompletedTask; });

        carryOn.Should().BeFalse("the character was reloaded, so the caller must stop and show it");
        writerCalled.Should().BeFalse("a stale file must never be overwritten");
        recorder.Reloaded.Should().ContainSingle().Which.Should().BeSameAs(tab);
        afterReloadCalled.Should().BeTrue();
        var note = recorder.Notes.Should().ContainSingle().Subject;
        note.Message.Should().Be("The file changed on disk. Reloaded instead of overwriting.");
        note.Severity.Should().Be(Severity.Warning);
        recorder.Events.Should().Equal("note", "reload", "after");
    }

    [Fact]
    public async Task A_reload_that_fails_tells_the_user_to_reopen_the_character_and_skips_the_refresh()
    {
        var recorder = new Recorder { OnReload = _ => throw new InvalidOperationException("cannot read file") };
        var tab = NewTab();
        ChangeOnDisk(tab);
        bool afterReloadCalled = false;

        bool carryOn = await recorder.Handler().WriteAsync(
            tab,
            Write(() => true, logContext: "Shop.Persist"),
            afterReload: () => { afterReloadCalled = true; return Task.CompletedTask; });

        carryOn.Should().BeFalse();
        afterReloadCalled.Should().BeFalse("there is no reloaded character to show");
        recorder.Logs.Should().ContainSingle().Which.Context.Should().Be("Shop.Persist.Reload");
        recorder.Notes.Should().HaveCount(2);
        var error = recorder.Notes[1];
        error.Severity.Should().Be(Severity.Error);
        error.Milliseconds.Should().Be(9000);
        error.Message.Should().EndWith("Close and reopen the character before making more equipment changes.");
    }

    [Fact]
    public async Task A_page_refresh_that_throws_after_a_good_reload_is_reported_like_a_failed_reload()
    {
        var recorder = new Recorder();
        var tab = NewTab();
        ChangeOnDisk(tab);

        bool carryOn = await recorder.Handler().WriteAsync(
            tab, Write(() => true), afterReload: () => throw new InvalidOperationException("view broke"));

        carryOn.Should().BeFalse();
        recorder.Notes.Last().Severity.Should().Be(Severity.Error);
        recorder.Logs.Should().ContainSingle().Which.Exception.Message.Should().Be("view broke");
    }

    [Fact]
    public async Task The_write_gate_is_released_whichever_way_the_write_ends()
    {
        var recorder = new Recorder();
        var tab = NewTab();

        await recorder.Handler().WriteAsync(tab, Write(() => throw new IOException("first fails")));
        ChangeOnDisk(tab);
        await recorder.Handler().WriteAsync(tab, Write(() => true));
        tab.File.RefreshKnownDiskStamp();
        bool carryOn = await recorder.Handler().WriteAsync(tab, Write(() => true));

        carryOn.Should().BeTrue("a failed or reloaded write must not leave the file's gate held");
        tab.FileSaveSemaphore.CurrentCount.Should().Be(1);
    }

    [Fact]
    public void Constructing_without_a_reporter_is_refused()
    {
        Action noNotify = () => _ = new CharacterFileWriteHandler(null!, (_, _) => { });
        Action noLog = () => _ = new CharacterFileWriteHandler((_, _, _) => { }, null!);

        noNotify.Should().Throw<ArgumentNullException>();
        noLog.Should().Throw<ArgumentNullException>();
    }
}
