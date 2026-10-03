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
