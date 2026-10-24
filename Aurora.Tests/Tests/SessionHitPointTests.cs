using Aurora.Reflections.Services;

namespace Aurora.Tests.Tests;

public sealed class SessionHitPointTests
{
    [Fact]
    public void TemporaryHpDoesNotIncreaseHealingLimitOrChangeActualHpWhenRemoved()
    {
        var state = new SessionState { CurrentHp = 7 };
        state.SetTemporaryHp(5);
        state.AdjustCurrentHp(100, 10);
        state.CurrentHp.Should().Be(10);
        state.TempHp.Should().Be(5);
        state.SetTemporaryHp(0);
        state.CurrentHp.Should().Be(10);
        state.AdjustTemporaryHp(3);
        state.CurrentHp.Should().Be(10);
        state.AdjustTemporaryHp(-100);
        state.TempHp.Should().Be(0);
        state.CurrentHp.Should().Be(10);
    }

    [Fact]
    public void ManualHpControlsKeepZeroAndClampWithoutIntegerOverflow()
    {
        var state = new SessionState { CurrentHp = 0, TempHp = 5 };
        state.AdjustCurrentHp(-1, 10);
        state.CurrentHp.Should().Be(0);
        state.TempHp.Should().Be(5);
        state.SetCurrentHp(int.MaxValue, 10);
        state.AdjustCurrentHp(int.MaxValue, 10);
        state.CurrentHp.Should().Be(10);
        state.AdjustCurrentHp(int.MinValue, 10);
        state.CurrentHp.Should().Be(0);
        state.SetTemporaryHp(int.MaxValue);
        state.AdjustTemporaryHp(1);
        state.TempHp.Should().Be(int.MaxValue);
    }

    [Fact]
    public void ActualAndTemporaryHpSurviveSidecarReloadAsSeparateValues()
    {
        string root = Path.Combine(Path.GetTempPath(), "Aurora.Tests", "hp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string character = Path.Combine(root, "Hero.dnd5e");
        File.WriteAllText(character, "<character />");
        try
        {
            var state = new SessionState { CurrentHp = 7 };
            state.SetTemporaryHp(5);
            state.AdjustCurrentHp(100, 10);
            SessionStore.SaveRequired(character, state);
            var loaded = SessionStore.Load(character);
            loaded.InitializeIfNew(10);
            loaded.CurrentHp.Should().Be(10);
            loaded.TempHp.Should().Be(5);
            loaded.SetTemporaryHp(0);
            SessionStore.SaveRequired(character, loaded);
            SessionStore.Load(character).CurrentHp.Should().Be(10);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void LoadingOldOverMaximumHpNormalizesActualHpAndPreservesTemporaryHp()
    {
        var state = new SessionState { CurrentHp = 15, TempHp = 5 };
        state.InitializeIfNew(10);
        state.CurrentHp.Should().Be(10);
        state.TempHp.Should().Be(5);
        state.SetCurrentHp(0, 10);
        state.InitializeIfNew(10);
        state.CurrentHp.Should().Be(0);
        state.LongRest(10);
        state.CurrentHp.Should().Be(10);
        state.TempHp.Should().Be(0);
    }
}
