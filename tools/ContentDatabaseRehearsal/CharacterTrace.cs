using Builder.Core.Logging;

/// <summary>Bounded diagnostics: never copies character XML or portrait data into logs.</summary>
internal sealed class CharacterTrace(string path) : ILogger
{
    private int count;
    private string? previous;
    private int repeated;
    public HashSet<string> MissingGrants { get; } = new(StringComparer.Ordinal);
    public HashSet<string> LoadWarnings { get; } = new(StringComparer.Ordinal);
    public void Debug(string message, params object[] args) { }
    public void Info(string message, params object[] args)
    {
        if (message.StartsWith("Unregistering Element:") || message.Contains("unregister all remaining") || message.Contains("creating a new character"))
            Write(message, args);
    }
    public void Warning(string message, params object[] args)
    {
        string text = args.Length == 0 ? message : string.Format(message, args);
        if (text.StartsWith("Unable to find ", StringComparison.Ordinal) && text.Contains("that was set as grant"))
            MissingGrants.Add(text);
        if (text.Contains("unable to get element from character elements") || text.Contains("without all elements") ||
            text.Contains("ungranting:") || text.Contains("missing saved character elements after loading"))
            LoadWarnings.Add(text);
        Write(message, args);
    }
    public void Exception(Exception ex) => Write(ex.GetType().Name + ": " + ex.Message, []);
    private void Write(string message, object[] args)
    {
        string text = args.Length == 0 ? message : string.Format(message, args);
        if (text == previous)
        {
            repeated++;
            if (repeated % 10000 != 0) return;
            text += $" [repeated {repeated} times]";
        }
        else { previous = text; repeated = 0; }
        if (++count > 5000) return;
        File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + text + Environment.NewLine);
    }
}
