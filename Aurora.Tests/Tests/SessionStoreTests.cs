using Aurora.App.Services;

namespace Aurora.Tests.Tests;

public sealed class SessionStoreTests : IDisposable
{
    private readonly string _dir =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"aurora-session-tests-{Guid.NewGuid():N}")).FullName;

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string CreateCharacterFile(string name = "Hero.dnd5e", string xml = "<character/>")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllText(path, xml);
        return path;
    }

    private static SessionState MakePopulatedState() => new()
    {
        CurrentHp          = 17,
        TempHp             = 5,
        DeathSaveSuccesses = 1,
        DeathSaveFailures  = 2,
        Inspiration        = true,
        Exhaustion         = 3,
        Conditions         = ["Prone", "Poisoned"],
        SpellSlotsUsed     = new Dictionary<int, int> { [1] = 2, [3] = 1 },
        CustomResources    =
        [
            new CustomResource { Id = "abc12345", Name = "Ki Points", Max = 5, Used = 2, ResetOn = ResetOn.ShortRest },
        ],
        AttackReminderWeaponIds          = ["main-weapon"],
        HiddenDefaultAttackReminderKeys  = ["default:claws:+4:1d6:5ft"],
        CustomAttackReminders =
        [
            new CustomAttackReminder { Id = "atk12345", Name = "Blowgun of Flame", Attack = "+6 vs AC", Damage = "1 piercing + 1d6 fire", Range = "25/100" },
        ],
    };

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        string characterPath = CreateCharacterFile();
        var state = MakePopulatedState();

        SessionStore.Save(characterPath, state).Should().BeTrue();
        var loaded = SessionStore.Load(characterPath, out bool corrupted);

        corrupted.Should().BeFalse();
        loaded.Should().BeEquivalentTo(state);
        File.Exists(SessionStore.GetSidecarPath(characterPath)).Should().BeTrue();
    }

    [Fact]
    public void Load_NoSidecarAndNoEmbeddedNode_ReturnsFreshStateWithoutSidecar()
    {
        string characterPath = CreateCharacterFile();

        var loaded = SessionStore.Load(characterPath, out bool corrupted);

        corrupted.Should().BeFalse();
        loaded.CurrentHp.Should().Be(-1); // "not yet initialised" sentinel
        File.Exists(SessionStore.GetSidecarPath(characterPath)).Should().BeFalse();
    }

    [Fact]
    public void Load_MigratesLegacyEmbeddedSessionNodeToSidecar()
    {
        string characterPath = CreateCharacterFile(xml: """
            <character>
              <build/>
              <session>
                <currenthp>9</currenthp>
                <temphp>2</temphp>
                <deathsave-successes>1</deathsave-successes>
                <deathsave-failures>0</deathsave-failures>
                <inspiration>true</inspiration>
                <exhaustion>1</exhaustion>
                <conditions><condition>Prone</condition></conditions>
                <spellslots><slot level="2" used="1" /></spellslots>
                <resources><resource id="r1" name="Rage" max="3" used="1" reset="LongRest" /></resources>
                <attack-reminders>
                  <weapon identifier="main-weapon" />
                  <hidden-default key="default:bite" />
                </attack-reminders>
              </session>
            </character>
            """);

        var loaded = SessionStore.Load(characterPath, out bool corrupted);

        corrupted.Should().BeFalse();
        loaded.CurrentHp.Should().Be(9);
        loaded.TempHp.Should().Be(2);
        loaded.DeathSaveSuccesses.Should().Be(1);
        loaded.Inspiration.Should().BeTrue();
        loaded.Exhaustion.Should().Be(1);
        loaded.Conditions.Should().BeEquivalentTo(["Prone"]);
        loaded.SpellSlotsUsed.Should().BeEquivalentTo(new Dictionary<int, int> { [2] = 1 });
        loaded.CustomResources.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(
                new CustomResource { Id = "r1", Name = "Rage", Max = 3, Used = 1, ResetOn = ResetOn.LongRest });
        loaded.AttackReminderWeaponIds.Should().BeEquivalentTo(["main-weapon"]);
        loaded.HiddenDefaultAttackReminderKeys.Should().BeEquivalentTo(["default:bite"]);

        // Migration must have written the sidecar so the node's data survives the next full save.
        File.Exists(SessionStore.GetSidecarPath(characterPath)).Should().BeTrue();
    }

    [Fact]
    public void Load_PrefersSidecarOverStaleEmbeddedNode()
    {
        string characterPath = CreateCharacterFile(xml: """
            <character>
              <session><currenthp>1</currenthp></session>
            </character>
            """);
        SessionStore.Save(characterPath, new SessionState { CurrentHp = 42 });

        var loaded = SessionStore.Load(characterPath, out bool corrupted);

        corrupted.Should().BeFalse();
        loaded.CurrentHp.Should().Be(42);
    }

    [Fact]
    public void Load_CorruptSidecar_ReturnsFreshStateAndFlagsCorruption()
    {
        string characterPath = CreateCharacterFile();
        File.WriteAllText(SessionStore.GetSidecarPath(characterPath), "{ not valid json");

        var loaded = SessionStore.Load(characterPath, out bool corrupted);

        corrupted.Should().BeTrue();
        loaded.CurrentHp.Should().Be(-1);
    }

    [Fact]
    public void Load_CorruptEmbeddedNode_ReturnsFreshStateAndFlagsCorruption()
    {
        string characterPath = CreateCharacterFile(xml: "<character><session><currenthp>");

        var loaded = SessionStore.Load(characterPath, out bool corrupted);

        corrupted.Should().BeTrue();
        loaded.CurrentHp.Should().Be(-1);
    }

    [Fact]
    public void Save_WhenCharacterFileMissing_ReturnsFalseAndWritesNothing()
    {
        string characterPath = Path.Combine(_dir, "missing.dnd5e");

        SessionStore.Save(characterPath, MakePopulatedState()).Should().BeFalse();
        File.Exists(SessionStore.GetSidecarPath(characterPath)).Should().BeFalse();
    }

    [Fact]
    public void Delete_RemovesSidecar()
    {
        string characterPath = CreateCharacterFile();
        SessionStore.Save(characterPath, MakePopulatedState());

        SessionStore.Delete(characterPath);

        File.Exists(SessionStore.GetSidecarPath(characterPath)).Should().BeFalse();
    }

    [Fact]
    public void MoveAlongside_MovesSidecarWithRenamedCharacterFile()
    {
        string oldPath = CreateCharacterFile("Old Name.dnd5e");
        SessionStore.Save(oldPath, new SessionState { CurrentHp = 13 });

        // Simulate the rename flow: move the .dnd5e, then bring the sidecar along.
        string newPath = Path.Combine(_dir, "New Name.dnd5e");
        File.Move(oldPath, newPath);
        SessionStore.MoveAlongside(oldPath, newPath);

        File.Exists(SessionStore.GetSidecarPath(oldPath)).Should().BeFalse();
        SessionStore.Load(newPath, out bool corrupted).CurrentHp.Should().Be(13);
        corrupted.Should().BeFalse();
    }

    [Fact]
    public void MoveAlongside_NoSidecar_IsANoOp()
    {
        string oldPath = CreateCharacterFile("Old.dnd5e");
        string newPath = Path.Combine(_dir, "New.dnd5e");
        File.Move(oldPath, newPath);

        SessionStore.MoveAlongside(oldPath, newPath);

        File.Exists(SessionStore.GetSidecarPath(newPath)).Should().BeFalse();
    }
}
