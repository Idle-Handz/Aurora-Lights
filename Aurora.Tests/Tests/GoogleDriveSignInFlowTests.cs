using Aurora.Reflections.Services;

namespace Aurora.Tests.Tests;

public sealed class GoogleDriveSignInFlowTests
{
    [Fact]
    public async Task AuthorizationWaitsForAnExplicitContinue()
    {
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var authorizations = 0;
        var connection = GoogleDriveSignInFlow.RunAsync(_ => decision.Task, _ =>
        {
            authorizations++;
            return Task.CompletedTask;
        });

        connection.IsCompleted.Should().BeFalse();
        authorizations.Should().Be(0);
        decision.SetResult(true);
        (await connection).Should().BeTrue();
        authorizations.Should().Be(1);
    }

    [Fact]
    public async Task DismissingTheNoticeLeavesAnExistingConnectionUntouched()
    {
        var connectedAccount = "previous-account";
        var result = await GoogleDriveSignInFlow.RunAsync(_ => Task.FromResult(false), _ =>
        {
            connectedAccount = "new-account";
            return Task.CompletedTask;
        });

        result.Should().BeFalse();
        connectedAccount.Should().Be("previous-account");
    }

    [Fact]
    public async Task CancellationWhileNoticeIsOpenCannotAuthorizeEvenIfContinueArrivesLater()
    {
        using var cancellation = new CancellationTokenSource();
        var decision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var authorizations = 0;
        var connection = GoogleDriveSignInFlow.RunAsync(_ => decision.Task, _ =>
        {
            authorizations++;
            return Task.CompletedTask;
        }, cancellation.Token);

        cancellation.Cancel();
        Func<Task> awaitConnection = () => connection;
        await awaitConnection.Should().ThrowAsync<OperationCanceledException>();
        decision.SetResult(true);
        authorizations.Should().Be(0);
    }

    [Fact]
    public async Task CancellationAtContinueIsCheckedBeforeStartingGoogle()
    {
        using var cancellation = new CancellationTokenSource();
        var authorizations = 0;
        Func<Task> connect = () => GoogleDriveSignInFlow.RunAsync(_ =>
        {
            cancellation.Cancel();
            return Task.FromResult(true);
        }, _ =>
        {
            authorizations++;
            return Task.CompletedTask;
        }, cancellation.Token);

        await connect.Should().ThrowAsync<OperationCanceledException>();
        authorizations.Should().Be(0);
    }

    [Fact]
    public async Task FailedGoogleAuthorizationDoesNotReturnSuccess()
    {
        Func<Task> connect = () => GoogleDriveSignInFlow.RunAsync(_ => Task.FromResult(true),
            _ => Task.FromException(new InvalidOperationException("Google refused authorization")));

        await connect.Should().ThrowAsync<InvalidOperationException>().WithMessage("Google refused authorization");
    }
}
