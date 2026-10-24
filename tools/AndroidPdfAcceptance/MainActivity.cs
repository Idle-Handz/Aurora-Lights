using Android.App;
using Android.OS;
using Android.Widget;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Aurora.Tests.Tests;

namespace Aurora.AndroidPdfAcceptance;

[Activity(Label = "Aurora PDF Acceptance", MainLauncher = true, Exported = true)]
public class MainActivity : Activity
{
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        var status = new TextView(this) { Text = "Running isolated PDF acceptance..." };
        status.SetTextColor(Android.Graphics.Color.Black);
        SetContentView(status);
        string directory = Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "pdf-acceptance");
        Directory.CreateDirectory(directory);
        System.Environment.SetEnvironmentVariable("AURORA_PDF_QA_DIRECTORY", directory);
        string? group = Intent?.GetStringExtra("group");
        bool skipSheets = Intent?.GetBooleanExtra("skipSheets", false) == true;
        _ = Task.Run(() =>
        {
            var results = new List<object>(); int passed = 0, failed = 0;
            try
            {
            Type[] types = [typeof(PdfExportAcceptanceTests), typeof(CharacterSheetFlatteningTests), typeof(PortablePdfFontsTests), typeof(PdfExportFilesTests)];
            foreach (var type in types.Where(t => group is null || t.Name == group))
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                if (!method.IsDefined(typeof(FactAttribute))) continue;
                if (skipSheets && method.Name == nameof(PdfExportAcceptanceTests.FullSheetPreservesSelectedPagesValuesAndFlattening)) continue;
                File.WriteAllText(Path.Combine(directory, "current-case.txt"), method.Name + " discovering data");
                var rows = new List<object?[]>();
                if (method.Name == nameof(PdfExportAcceptanceTests.FullSheetPreservesSelectedPagesValuesAndFlattening))
                    rows.AddRange(PdfExportAcceptanceTests.SheetModes());
                else
                {
                    foreach (var data in method.GetCustomAttributes<InlineDataAttribute>())
                        rows.AddRange(data.GetData(method));
                    if (rows.Count == 0) rows.Add([]);
                }
                foreach (var row in rows)
                {
                    string name = type.Name + "." + method.Name + "(" + string.Join(",", row) + ")";
                    string? error = null;
                    File.WriteAllText(Path.Combine(directory, "current-case.txt"), name);
                    var start = DateTime.UtcNow;
                    try { method.Invoke(Activator.CreateInstance(type), row); passed++; }
                    catch (Exception ex) { error = (ex.InnerException ?? ex).ToString(); failed++; }
                    results.Add(new { name, passed = error is null, error, seconds = (DateTime.UtcNow - start).TotalSeconds });
                    File.WriteAllText(Path.Combine(directory, "progress.json"), JsonSerializer.Serialize(new { passed, failed, results }));
                    RunOnUiThread(() => status.Text = $"PDF acceptance: {passed} passed, {failed} failed");
                }
            }
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(directory, "runner-error.txt"), ex.ToString());
                failed++;
            }
            var files = Directory.EnumerateFiles(directory, "*.pdf").Select(p => new { name = Path.GetFileName(p), bytes = new FileInfo(p).Length, sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray();
            File.WriteAllText(Path.Combine(directory, "results.json"), JsonSerializer.Serialize(new { passed, failed, files, results }, new JsonSerializerOptions { WriteIndented = true }));
            RunOnUiThread(() => status.Text = $"Complete: {passed} passed, {failed} failed. {files.Length} PDF files.");
        });
    }
}
