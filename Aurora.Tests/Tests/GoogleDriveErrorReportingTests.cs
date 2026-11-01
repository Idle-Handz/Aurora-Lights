using System.Net;
using Aurora.Reflections.Services;

namespace Aurora.Tests.Tests;

public sealed class GoogleDriveErrorReportingTests
{
    [Fact]
    public async Task HttpClientTimeout_IsReportedAsTimeoutInsteadOfCancellation()
    {
        using var client = new HttpClient(new WaitingHandler()) { Timeout = TimeSpan.FromMilliseconds(50) };
        Exception exception = (await Record.ExceptionAsync(() => client.GetAsync("https://example.invalid/drive")))!;

        exception.Should().BeAssignableTo<OperationCanceledException>();
        GoogleDriveErrorReporting.IsRequestTimeout(exception, CancellationToken.None).Should().BeTrue();
        GoogleDriveErrorReporting.DescribeFailure(exception).Should().Contain(nameof(TimeoutException));
    }

    [Fact]
    public async Task ExplicitCancellation_IsNotReportedAsHttpTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new HttpClient(new WaitingHandler());
        Task<HttpResponseMessage> request = client.GetAsync("https://example.invalid/drive", cancellation.Token);
        cancellation.Cancel();
        Exception exception = (await Record.ExceptionAsync(() => request))!;

        exception.Should().BeAssignableTo<OperationCanceledException>();
        GoogleDriveErrorReporting.IsRequestTimeout(exception, cancellation.Token).Should().BeFalse();
    }

    [Fact]
    public void CancellationWinsWhenRequestTimeoutAndCallerCancellationOverlap()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = new TaskCanceledException("request stopped", new TimeoutException());

        GoogleDriveErrorReporting.IsRequestTimeout(exception, cancellation.Token).Should().BeFalse();
    }

    [Fact]
    public void NativeSignInCancellation_IsNotReportedAsHttpTimeout()
    {
        GoogleDriveErrorReporting.IsRequestTimeout(new OperationCanceledException(), CancellationToken.None)
            .Should().BeFalse();
    }

    [Fact]
    public void NativeSignInTimeout_RetainsItsSpecificErrorMessage()
    {
        GoogleDriveErrorReporting.IsRequestTimeout(new TimeoutException("Sign-in timed out"), CancellationToken.None)
            .Should().BeFalse();
    }

    [Fact]
    public void DiagnosticsKeepHttpStatusAndCausesWithoutSensitiveMessagesOrData()
    {
        var http = new HttpRequestException("access_token=private-token account@example.com",
            new IOException("<character>private character contents</character>"), HttpStatusCode.Forbidden);
        http.Data["secret"] = "private-refresh-token";

        string summary = GoogleDriveErrorReporting.DescribeFailure(new InvalidOperationException("private file path", http));

        summary.Should().Be("InvalidOperationException -> HttpRequestException (HTTP 403) (Unknown) -> IOException");
        summary.Should().NotContain("private").And.NotContain("account@example.com");
    }

    [Fact]
    public void DiagnosticsRetainNetworkFailureCategory()
    {
        var exception = new HttpRequestException(HttpRequestError.NameResolutionError, "private host name");

        GoogleDriveErrorReporting.DescribeFailure(exception)
            .Should().Be("HttpRequestException (NameResolutionError)");
    }

    [Theory]
    [InlineData(10, null)]
    [InlineData(8, "8: Unknown error [status=UNREGISTERED_ON_API_CONSOLE].")]
    public void UnregisteredAndroidBuild_ExplainsThePlaySigningCertificate(int status, string? message)
    {
        string result = GoogleDriveErrorReporting.AndroidAuthorizationFailureMessage(status, message);

        result.Should().Contain($"not configured for this Android build (code {status})")
            .And.Contain("Play app-signing certificate");
    }

    [Fact]
    public void OtherAndroidInternalErrors_AreNotMisdiagnosedAsMissingRegistration()
    {
        GoogleDriveErrorReporting.AndroidAuthorizationFailureMessage(8, "private provider payload")
            .Should().Be("Google Drive authorization failed (code 8). Check your connection and reconnect.");
    }

    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("The simulated request must end through cancellation.");
        }
    }
}
