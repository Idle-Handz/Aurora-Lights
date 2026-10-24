using Android.App;
using Android.Content;
using Android.Provider;

namespace Aurora.App.Services;

/// <summary>Save a completed export through Android's user-selected document provider.</summary>
internal static class AndroidDocumentSaver
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static TaskCompletionSource<Android.Net.Uri?>? _pending;
    private static Activity? _activity;
    private static int _requestCode = 44000;

    public static async Task<bool> SaveAsync(string sourcePath, string name, string mimeType)
    {
        await Gate.WaitAsync();
        try
        {
            var uri = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var activity = Platform.CurrentActivity
                    ?? throw new InvalidOperationException("Return to Aurora before saving the PDF.");
                var completion = new TaskCompletionSource<Android.Net.Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
                _pending = completion;
                _activity = activity;
                int code = ++_requestCode;
                try
                {
                    using var intent = new Intent(Intent.ActionCreateDocument);
                    intent.AddCategory(Intent.CategoryOpenable);
                    intent.SetType(mimeType);
                    intent.PutExtra(Intent.ExtraTitle, Path.GetFileName(name));
                    activity.StartActivityForResult(intent, code);
                    return await completion.Task;
                }
                finally { _pending = null; _activity = null; }
            });
            if (uri is null) return false;
            try
            {
                await Task.Run(async () =>
                {
                    using var input = File.OpenRead(sourcePath);
                    using var output = Android.App.Application.Context.ContentResolver!.OpenOutputStream(uri, "w")
                        ?? throw new IOException("The selected location could not open the PDF for writing.");
                    await input.CopyToAsync(output);
                    await output.FlushAsync();
                });
                return true;
            }
            catch
            {
                // ACTION_CREATE_DOCUMENT creates a new file; remove only our incomplete result.
                try { DocumentsContract.DeleteDocument(Android.App.Application.Context.ContentResolver!, uri); }
                catch (Exception cleanup) { DebugLogService.Instance.LogException(cleanup, "Incomplete PDF save cleanup"); }
                throw;
            }
            finally { uri.Dispose(); }
        }
        finally { Gate.Release(); }
    }

    internal static void OnActivityResult(int requestCode, Result result, Intent? data)
    {
        if (_pending is null || requestCode != _requestCode) return;
        _pending.TrySetResult(result == Result.Ok ? data?.Data : null);
    }

    internal static void OnActivityDestroyed(Activity activity)
    {
        if (ReferenceEquals(_activity, activity)) _pending?.TrySetResult(null);
    }
}
