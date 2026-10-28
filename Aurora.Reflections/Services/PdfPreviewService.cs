using System.Net;

namespace Aurora.Reflections.Services;

/// <summary>
/// Runs a lightweight localhost HTTP server so the MAUI WebView can load a
/// generated PDF. WebView2 renders application/pdf responses using its built-in
/// PDF viewer, giving true in-app preview without any external viewer.
/// </summary>
public sealed class PdfPreviewService : IDisposable
{
    private readonly HttpListener _listener;
    private readonly int _port;
    private byte[]? _pdfBytes;

    public PdfPreviewService()
    {
        _port    = GetFreePort();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{_port}/");
        _listener.Start();
        _ = Task.Run(ListenLoop);
    }

    /// <summary>
    /// Stores the PDF bytes and returns the URL to load them from.
    /// Append a cache-buster timestamp so WebView2 doesn't serve a stale copy.
    /// </summary>
    public string Serve(byte[] pdfBytes)
    {
        _pdfBytes = pdfBytes;
        return $"http://localhost:{_port}/preview.pdf?t={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
    }

    private async Task ListenLoop()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch (Exception ex)
            {
                if (_listener.IsListening)
                    DebugLogService.Instance.LogException(ex, "PdfPreviewService.ListenLoop");
                break;
            }

            try
            {
                if (_pdfBytes is { } bytes)
                {
                    ctx.Response.ContentType        = "application/pdf";
                    ctx.Response.ContentLength64    = bytes.Length;
                    ctx.Response.Headers["Access-Control-Allow-Origin"] = "*";
                    await ctx.Response.OutputStream.WriteAsync(bytes);
                }
                else
                {
                    ctx.Response.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                DebugLogService.Instance.LogException(ex, "PdfPreviewService.ListenLoop.Request");
            }
            finally
            {
                try { ctx.Response.Close(); } catch { }
            }
        }
    }

    public void Dispose()
    {
        try { _listener.Stop(); } catch { }
    }

    private static int GetFreePort()
    {
        using var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        l.Start();
        return ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
    }
}
