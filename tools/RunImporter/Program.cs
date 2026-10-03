using Aurora.App.Services;
using Aurora.Content;

// Developer utility: import a content folder into a database exactly as the app does.
var contentDir = args.Length > 0 ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                   "5e Character Builder", "custom");

var dbPath = args.Length > 1 ? args[1]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                   "5e Character Builder", "aurora-elements.sqlite");

Console.WriteLine($"Content : {contentDir}");
Console.WriteLine($"Database: {dbPath}");
Console.WriteLine();

Console.WriteLine(ContentDatabaseReader.IsStale([contentDir], dbPath)
    ? "Database needs an import."
    : "Database is current; importing anyway.");
Console.WriteLine();

var sw = System.Diagnostics.Stopwatch.StartNew();
var progress = new Progress<ContentImportProgress>(p =>
    Console.Write("\r  [" + p.Phase.ToString().PadRight(10) + "]  "
        + p.Completed.ToString().PadLeft(6) + "/" + p.Total.ToString().PadRight(6) + "   "));

try
{
    var result = await ContentImport.ImportAsync(contentDir, dbPath, progress,
        onDiagnostic: Console.Error.WriteLine);
    sw.Stop();
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"Import succeeded in {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"  {result.ElementsWritten} elements from {result.FilesChanged} changed file(s), "
        + $"{result.FilesUnchanged} unchanged.");
}
catch (Exception error)
{
    sw.Stop();
    Console.WriteLine();
    Console.WriteLine();
    Console.WriteLine($"Import FAILED after {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"  {error.Message}");
    Environment.Exit(1);
}
