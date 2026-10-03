namespace Aurora.App.Services;

// The actual selection/spell handlers are linked from MauiContextStubs.cs.
// Its unrelated platform launcher must never open external files during rehearsal.
internal sealed record ReadOnlyFile(string Path);
internal sealed record OpenFileRequest(string Name, ReadOnlyFile File);
internal sealed class Launcher
{
    public static Launcher Default { get; } = new();
    public Task OpenAsync(object target) => throw new NotSupportedException("External launching is disabled in rehearsal.");
}
