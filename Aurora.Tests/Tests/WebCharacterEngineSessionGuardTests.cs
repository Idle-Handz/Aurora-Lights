using Aurora.Web.Services;

namespace Aurora.Tests.Tests;

public sealed class WebCharacterEngineSessionGuardTests
{
    [Fact]
    public void Acquire_RejectsAnotherSessionUntilOwnerReleases()
    {
        var guard = new WebCharacterEngineSessionGuard();
        Guid firstSession = Guid.NewGuid();
        Guid secondSession = Guid.NewGuid();

        guard.Acquire(firstSession).Should().BeTrue();
        guard.Acquire(firstSession).Should().BeFalse();

        Action acquireSecond = () => guard.Acquire(secondSession);
        acquireSecond.Should().Throw<InvalidOperationException>()
            .WithMessage("Another browser session*");

        guard.Release(firstSession);
        guard.Acquire(secondSession).Should().BeTrue();
    }

    [Fact]
    public void Release_FromAnotherSession_DoesNotReleaseOwner()
    {
        var guard = new WebCharacterEngineSessionGuard();
        Guid firstSession = Guid.NewGuid();
        Guid secondSession = Guid.NewGuid();
        guard.Acquire(firstSession);

        guard.Release(secondSession);

        Action acquireSecond = () => guard.Acquire(secondSession);
        acquireSecond.Should().Throw<InvalidOperationException>();
    }
}
