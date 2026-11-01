using Android.Accounts;
using Android.App;
using Android.Content;
using Android.Gms.Auth.Api.Identity;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Extensions;
using Builder.Presentation.Services.Storage;
using OperationCanceledException = System.OperationCanceledException;

namespace Aurora.Reflections.Services;

/// <summary>Google's native authorization flow; no desktop redirect or embedded client secret.</summary>
internal static class AndroidGoogleDriveAuthorization
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static TaskCompletionSource<Intent>? _pending;
    private static Activity? _pendingActivity;
    private static bool _pendingAuthorization;
    private static string? _lastAccessToken;
    private static int _requestCode = 42000;

    public static async Task<string> AuthorizeAsync(string? accountEmail, bool interactive, CancellationToken ct)
    {
        await Gate.WaitAsync(ct);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        try
        {
            return await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var activity = Platform.CurrentActivity
                    ?? throw new InvalidOperationException("Return to Aurora to connect Google Drive.");
                if (GoogleApiAvailability.Instance.IsGooglePlayServicesAvailable(activity) != ConnectionResult.Success)
                    throw new InvalidOperationException("Google Drive sign-in needs up-to-date Google Play services on this Android device.");

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
                            code => activity.StartIntentSenderForResult(intent.IntentSender, code, null, 0, 0, 0), token,
                            authorizationResult: true);
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
            // Log only the status, never the returned intent, account, or token.
            DebugLogService.Instance.Warn($"Google Drive Android authorization failed (code {ex.StatusCode}).");
            if (ex.StatusCode == 16) // CommonStatusCodes.CANCELED
                throw new OperationCanceledException("Google Drive sign-in was cancelled.", ex, ct);
            throw new InvalidOperationException(
                GoogleDriveErrorReporting.AndroidAuthorizationFailureMessage(ex.StatusCode, ex.Message));
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            DebugLogService.Instance.Warn("Google Drive Android sign-in timed out after three minutes.");
            throw new TimeoutException("Google Drive sign-in timed out. Please try connecting again.", ex);
        }
        finally { Gate.Release(); }
    }

    // Request codes are never reused during this process, so a late result after cancellation
    // cannot complete a newer sign-in. Access and completion happen on the main thread.
    private static async Task<Intent> ResolveAsync(Activity activity, Action<int> launch, CancellationToken ct,
        bool authorizationResult = false)
    {
        ct.ThrowIfCancellationRequested();
        if (_requestCode == ushort.MaxValue)
            throw new InvalidOperationException("Restart Aurora before connecting Google Drive again.");
        int code = ++_requestCode;
        var completion = new TaskCompletionSource<Intent>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending = completion;
        _pendingActivity = activity;
        _pendingAuthorization = authorizationResult;
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
                _pendingAuthorization = false;
            }
        }
    }

    internal static bool OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        if (requestCode != _requestCode || _pending is null) return false;
        DebugLogService.Instance.Info($"Google Drive Android {(_pendingAuthorization ? "permission" : "account")} screen returned result {(int)resultCode}; response data present: {data is not null}.");
        // Google's permission screen can return an error in a non-OK intent. Let
        // GetAuthorizationResultFromIntent decode it instead of losing the status.
        if (data is not null && (_pendingAuthorization || resultCode == Result.Ok))
            _pending.TrySetResult(new Intent(data));
        else if (resultCode == Result.Canceled)
            _pending.TrySetCanceled();
        else
            _pending.TrySetException(new InvalidOperationException("Google Drive sign-in returned no usable response. Please try connecting again."));
        return true;
    }

    internal static void OnActivityDestroyed(Activity activity)
    {
        if (ReferenceEquals(_pendingActivity, activity) && _pending?.TrySetException(new InvalidOperationException(
                "Google Drive sign-in was interrupted when Android closed Aurora's screen. Return to Aurora and try again.")) == true)
        {
            DebugLogService.Instance.Warn("Google Drive Android sign-in was interrupted because its activity was destroyed.");
        }
    }
}
