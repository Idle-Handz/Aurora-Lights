using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using System.Security.Cryptography;
using System.Text.Json;
using iTextSharp.text.pdf;

namespace Aurora.AndroidPdfAcceptance;

[Activity(Label = "Aurora PDF Test Receiver", Exported = true)]
[IntentFilter(new[] { Intent.ActionView, Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "application/pdf")]
public class PdfReceiveActivity : Activity
{
    protected override void OnCreate(Bundle? state)
    {
        base.OnCreate(state);
        string directory = Path.Combine(GetExternalFilesDir(null)!.AbsolutePath, "received");
        Directory.CreateDirectory(directory);
        string action = Intent?.Action == Android.Content.Intent.ActionSend ? "share" : "open";
#pragma warning disable CA1422, CS0618
        var uri = Intent?.Data ?? Intent?.GetParcelableExtra(Android.Content.Intent.ExtraStream) as Android.Net.Uri;
#pragma warning restore CA1422, CS0618
        try
        {
            using var input = ContentResolver!.OpenInputStream(uri!)!;
            string path = Path.Combine(directory, action + ".pdf");
            using (var output = File.Create(path)) input.CopyTo(output);
            using var pdf = new PdfReader(path);
            var record = new { action, pages = pdf.NumberOfPages, fields = pdf.AcroFields.Fields.Count,
                sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), uriScheme = uri!.Scheme,
                readPermissionWorked = true };
            File.WriteAllText(Path.Combine(directory, action + ".json"), JsonSerializer.Serialize(record));
            SetContentView(new TextView(this) { Text = $"Received {action} PDF: {pdf.NumberOfPages} pages, {pdf.AcroFields.Fields.Count} editable fields. Read permission verified." });
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(directory, action + "-error.txt"), ex.ToString());
            SetContentView(new TextView(this) { Text = "PDF handoff failed: " + ex.Message });
        }
    }
}
