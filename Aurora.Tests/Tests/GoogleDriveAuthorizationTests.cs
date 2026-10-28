using Builder.Presentation.Services.Storage;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace Aurora.Tests.Tests;

public sealed class GoogleDriveAuthorizationTests
{
    [Fact]
    public void ValidCallback_ReturnsCode() =>
        GoogleDriveAuthorization.ValidateCallback(new Uri("http://127.0.0.1:1234/?state=expected&code=abc"),
            "http://127.0.0.1:1234/", "expected").Should().Be("abc");

    [Theory]
    [InlineData("http://127.0.0.1:1234/?state=wrong&code=abc")]
    [InlineData("http://127.0.0.1:1234/?state=expected&state=expected&code=abc")]
    [InlineData("http://127.0.0.1:1234/?state=expected&code=a&code=b")]
    [InlineData("http://127.0.0.1:1234/?state=expected&error=access_denied")]
    [InlineData("http://127.0.0.1:1235/?state=expected&code=abc")]
    [InlineData("http://example.com:1234/?state=expected&code=abc")]
    [InlineData("http://127.0.0.1:1234/unexpected?state=expected&code=abc")]
    public void InvalidCallback_IsRejected(string url)
    {
        Action callback = () => GoogleDriveAuthorization.ValidateCallback(new Uri(url), "http://127.0.0.1:1234/", "expected");
        callback.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DesktopFlow_UsesPkceAndValidatesAccountBeforeReplacingCredential(bool wrongAccount)
    {
        var tokens = new TokenStore();
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        using var callbackHttp = new HttpClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var authorization = new GoogleDriveAuthorization(http, tokens, new("test-client", "test-client-secret"));
        Task<HttpResponseMessage>? callback = null;
        bool accountValidated = false;
        async Task SignIn() => await authorization.SignInAsync(uri =>
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            query["code_challenge_method"].Should().Be("S256");
            query["scope"].Should().Be(GoogleDriveAuthorization.Scope);
            handler.ExpectedChallenge = query["code_challenge"]!;
            callback = callbackHttp.GetAsync(query["redirect_uri"] + "?state=" +
                Uri.EscapeDataString(query["state"]!) + "&code=test-code", timeout.Token);
            return Task.CompletedTask;
        }, timeout.Token, (token, _) =>
        {
            token.Should().Be("new-access");
            tokens.Writes.Should().Be(0);
            if (wrongAccount) throw new InvalidOperationException("wrong account");
            accountValidated = true;
            return Task.CompletedTask;
        });
        {
            if (wrongAccount)
            {
                await ((Func<Task>)SignIn).Should().ThrowAsync<InvalidOperationException>().WithMessage("wrong account");
                tokens.Writes.Should().Be(0);
                tokens.Value.Should().Be("previous-refresh");
            }
            else
            {
                await SignIn();
                accountValidated.Should().BeTrue();
                tokens.Value.Should().Be("new-refresh");
                (await authorization.GetAccessTokenAsync()).Should().Be("new-access");
                handler.Requests.Should().Be(1);
            }
        }
        if (callback is not null) (await callback).Dispose();
    }

    [Fact]
    public async Task DesktopFlow_AllowsLoginAndSecondFactorBeyondThreeMinutes()
    {
        var clock = new SignInClock();
        var tokens = new TokenStore();
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        using var callbackHttp = new HttpClient();
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var authorization = new GoogleDriveAuthorization(http, tokens, new("test-client", null))
            { SignInTimeProvider = clock };
        Task<HttpResponseMessage>? callback = null;

        await authorization.SignInAsync(uri =>
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            handler.ExpectedChallenge = query["code_challenge"]!;
            clock.Advance(TimeSpan.FromMinutes(4));
            callback = callbackHttp.GetAsync(query["redirect_uri"] + "?state=" +
                Uri.EscapeDataString(query["state"]!) + "&code=test-code", guard.Token);
            return Task.CompletedTask;
        }, guard.Token);

        using var response = await callback!;
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tokens.Value.Should().Be("new-refresh");
        handler.Requests.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DesktopFlow_ExpiryOrCancelClosesListenerPreservesCredentialsAndAllowsRetry(bool cancelByUser)
    {
        var clock = new SignInClock();
        var tokens = new TokenStore();
        using var handler = new TokenHandler();
        using var http = new HttpClient(handler);
        using var callbackHttp = new HttpClient();
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(guard.Token);
        var authorization = new GoogleDriveAuthorization(http, tokens, new("test-client", null))
            { SignInTimeProvider = clock };
        string? expiredRedirect = null;
        Func<Task> signIn = () => authorization.SignInAsync(uri =>
        {
            expiredRedirect = HttpUtility.ParseQueryString(uri.Query)["redirect_uri"];
            if (cancelByUser) caller.Cancel();
            else clock.Advance(TimeSpan.FromMinutes(11));
            return Task.CompletedTask;
        }, caller.Token);

        if (cancelByUser)
            await signIn.Should().ThrowAsync<OperationCanceledException>();
        else
            await signIn.Should().ThrowAsync<TimeoutException>()
                .WithMessage("*timed out*Connect Google Drive*try again*");
        tokens.Writes.Should().Be(0);
        tokens.Value.Should().Be("previous-refresh");
        handler.Requests.Should().Be(0);
        Func<Task> lateCallback = async () =>
        {
            using var response = await callbackHttp.GetAsync(expiredRedirect, guard.Token);
        };
        await lateCallback.Should().ThrowAsync<HttpRequestException>();

        Task<HttpResponseMessage>? callback = null;
        await authorization.SignInAsync(uri =>
        {
            var query = HttpUtility.ParseQueryString(uri.Query);
            handler.ExpectedChallenge = query["code_challenge"]!;
            callback = callbackHttp.GetAsync(query["redirect_uri"] + "?state=" +
                Uri.EscapeDataString(query["state"]!) + "&code=test-code", guard.Token);
            return Task.CompletedTask;
        }, guard.Token);
        using var retryResponse = await callback!;
        retryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        tokens.Value.Should().Be("new-refresh");
        tokens.Writes.Should().Be(1);
    }

    // Advance only the OAuth deadline; the real loopback socket still exercises cleanup and retry.
    private sealed class SignInClock : TimeProvider
    {
        private TimeSpan _elapsed;
        private readonly List<Deadline> _deadlines = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            period.Should().Be(Timeout.InfiniteTimeSpan);
            var deadline = new Deadline(this, callback, state);
            deadline.Change(dueTime, period);
            _deadlines.Add(deadline);
            return deadline;
        }
        public void Advance(TimeSpan amount)
        {
            _elapsed += amount;
            foreach (var deadline in _deadlines.ToArray()) deadline.FireIfDue();
        }
        private sealed class Deadline(SignInClock clock, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan? _due;
            private bool _disposed;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                if (_disposed) return false;
                _due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._elapsed + dueTime;
                return true;
            }
            public void FireIfDue()
            {
                if (_disposed || _due is null || _due > clock._elapsed) return;
                _due = null;
                callback(state);
            }
            public void Dispose() => _disposed = true;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }

    private sealed class TokenStore : IGoogleDriveTokenStore
    {
        public int Writes { get; private set; }
        public string Value { get; private set; } = "previous-refresh";
        public Task<string?> ReadAsync(string key) => Task.FromResult<string?>(Value);
        public Task WriteAsync(string key, string value) { Writes++; Value = value; return Task.CompletedTask; }
        public void Remove(string key) { }
    }

    private sealed class TokenHandler : HttpMessageHandler
    {
        public string ExpectedChallenge { get; set; } = "";
        public int Requests { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            request.RequestUri!.AbsoluteUri.Should().Be("https://oauth2.googleapis.com/token");
            var form = HttpUtility.ParseQueryString(await request.Content!.ReadAsStringAsync(ct));
            form["code"].Should().Be("test-code");
            string challenge = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"]!)))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            challenge.Should().Be(ExpectedChallenge);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600}""")
            };
        }
    }
}
