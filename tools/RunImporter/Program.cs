using Aurora.Importer;

var contentDir = args.Length > 0 ? args[0]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                   "5e Character Builder", "custom");

var dbPath = args.Length > 1 ? args[1]
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                   "5e Character Builder", "aurora-elements.sqlite");

Console.WriteLine($"Content : {contentDir}");
Console.WriteLine($"Database: {dbPath}");
Console.WriteLine();

bool stale = AuroraContentImporter.IsStale(contentDir, dbPath);
Console.WriteLine(stale ? "Database is stale — running full import..." : "Database is current — running incremental import...");
Console.WriteLine();

var sw = System.Diagnostics.Stopwatch.StartNew();

var progress = new Progress<AuroraImportProgress>(p =>
{
    Console.Write($"\r  [{p.Phase,-10}]  {p.FilesScanned,4}/{p.FilesTotal,-4} files   ");
});

var result = AuroraContentImporter.Import(contentDir, dbPath, progress);
sw.Stop();

Console.WriteLine();
Console.WriteLine();

if (result.Success)
{
    Console.WriteLine($"Import succeeded in {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"  {result.Summary}");
}
else
{
    Console.WriteLine($"Import FAILED after {sw.Elapsed.TotalSeconds:F1}s");
    Console.WriteLine($"  Error: {result.ErrorMessage}");
    Environment.Exit(1);
}
