using Android.Accounts;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.Identity;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Extensions;
using Builder.Presentation.Services.Storage;

namespace Aurora.App.Services;

/// <summary>Google's native authorization flow; no desktop redirect or embedded client secret.</summary>
internal static class AndroidGoogleDriveAuthorization
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static TaskCompletionSource<Intent>? _pending;
    private static Activity? _pendingActivity;
    private static string? _lastAccessToken;
    private static int _requestCode = 42000;

    public static async Task<string> AuthorizeAsync(string? accountEmail, bool interactive, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var activity = Platform.CurrentActivity
                    ?? throw new InvalidOperationException("Return to Aurora to connect Google Drive.");
                if (GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(activity) != ConnectionResult.Success)
                    throw new InvalidOperationException("Google Drive sign-in needs up-to-date Google Play services on this Android device.");

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromMinutes(3));
                var token = timeout.Token;
                // Explicit selection on first connection also allows switching after Disconnect.
                // Later requests are pinned to the verified account, including silent renewal.
                if (accountEmail is null)
                {
                    if (!interactive) throw new InvalidOperationException("Connect Google Drive in Cloud Saves first.");
                    using var chooser = AccountManager.NewChooseAccountIntent(null, null, ["com.google"], null, null, null, null);
                    using var selected = await ResolveAsync(activity,
                        code => activity.StartActivityForResult(chooser, code), token);
                    accountEmail = selected.GetStringExtra(AccountManager.KeyAccountName);
                    if (string.IsNullOrWhiteSpace(accountEmail))
                        throw new InvalidOperationException("Choose a Google account to connect Drive.");
                }

                using var account = new Account(accountEmail, "com.google");
                using var request = AuthorizationRequest.InvokeBuilder()
                    .SetAccount(account)!
                    .SetRequestedScopes(new List<Scope> { new(GoogleDriveAuthorization.Scope) })!
                    .Build();
                using var client = Identity.GetAuthorizationClient(activity);
                // Reconnect must recover from a revoked token that Play services still caches.
                if (interactive && _lastAccessToken is not null)
                {
                    using var clear = ClearTokenRequest.InvokeBuilder().SetToken(_lastAccessToken)!.Build();
                    await client.ClearToken(clear!).AsAsync().WaitAsync(token);
                    _lastAccessToken = null;
                }
                using var initial = await client.Authorize(request!).AsAsync<AuthorizationResult>().WaitAsync(token);
                AuthorizationResult result = initial;
                AuthorizationResult? resolved = null;
                try
                {
                    if (initial.HasResolution)
                    {
                        if (!interactive)
                            throw new InvalidOperationException("Google Drive access needs your permission. Choose Reconnect in Cloud Saves.");
                        using var intent = initial.PendingIntent
                            ?? throw new InvalidOperationException("Google could not open the Drive permission screen.");
                        using var data = await ResolveAsync(activity,
                            code => activity.StartIntentSenderForResult(intent.IntentSender, code, null, 0, 0, 0), token);
                        resolved = client.GetAuthorizationResultFromIntent(data);
                        result = resolved;
                    }
                    if (string.IsNullOrWhiteSpace(result.AccessToken)
                        || !result.GrantedScopes.Contains(GoogleDriveAuthorization.Scope))
                        throw new InvalidOperationException("Google Drive file access was not granted. Reconnect and allow file access.");
                    _lastAccessToken = result.AccessToken;
                    return result.AccessToken;
                }
                finally { resolved?.Dispose(); }
            });
        }
        catch (ApiException ex)
        {
            throw new InvalidOperationException(ex.StatusCode == 10
                ? "Google Drive is not configured for this Android build. Register its package name and signing certificate SHA-1 as an Android OAuth client in the same Google Cloud project as the desktop client."
                : $"Google Drive authorization failed (code {ex.StatusCode}). Check your connection and reconnect.");
        }
        finally { Gate.Release(); }
    }

    // Request codes are never reused during this process, so a late result after cancellation
    // cannot complete a newer sign-in. Access and completion happen on the main thread.
    private static async Task<Intent> ResolveAsync(Activity activity, Action<int> launch, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_requestCode == ushort.MaxValue)
            throw new InvalidOperationException("Restart Aurora before connecting Google Drive again.");
        int code = ++_requestCode;
        var completion = new TaskCompletionSource<Intent>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion;
        _pendingActivity = activity;
        try
        {
            launch(code);
            return await completion.Task.WaitAsync(ct);
        }
        finally
        {
            if (ReferenceEquals(_pending, completion))
            {
                _pending = null;
                _pendingActivity = null;
            }
        }
    }

    internal static bool OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != _requestCode || _pending is null) return false;
        if (resultCode == Result.Ok && data is not null) _pending.TrySetResult(new Intent(data));
        else _pending.TrySetCanceled();
        return true;
    }

    internal static void OnActivityDestroyed(Activity activity)
    {
        if (ReferenceEquals(_pendingActivity, activity)) _pending?.TrySetCanceled();
    }
}
