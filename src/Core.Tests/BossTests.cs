using Videoland4000.Core.Tests.Support;

namespace Videoland4000.Core.Tests;

public class BossTests
{
    private static (World World, Driver Driver) ArenaAfterIntro(int seed = 3)
    {
        var world = TestContent.NewWorld(TestContent.Arena(), seed);
        var driver = new Driver();
        driver.Seconds(world, BossController.IntroDuration + 0.2);
        Assert.Equal(BossState.Attacking, world.Boss!.State);
        Driver.Drain(world);
        return (world, driver);
    }

    private static void UseTerminal(World world, Driver driver, ServiceKind service)
    {
        var terminal = world.RepairTerminals.Single(t => t.Service == service);
        world.Player.Teleport(new Point(terminal.CenterX, world.FloorTop));
        driver.Step(world, steps: 2);
        driver.Press(world, InputButtons.Interact, holdSteps: 2);
        driver.Release(world);
    }

    private static ServiceKind WrongService(BossController boss) =>
        Enum.GetValues<ServiceKind>().First(s => s != boss.Diagnostic);

    [Fact]
    public void BossStartsWithAnIntroAndFullHealth()
    {
        var world = TestContent.NewWorld(TestContent.Arena());
        var boss = world.Boss!;

        Assert.Equal(BossState.Intro, boss.State);
        Assert.Equal(BossController.MaxHealth, boss.Health);
        Assert.Equal(1, boss.Phase);
        Assert.Equal(3, world.RepairTerminals.Count);
    }

    [Fact]
    public void TouchingTheSpinningBossHurtsThePlayerNotTheBoss()
    {
        var (world, driver) = ArenaAfterIntro();
        var boss = world.Boss!;

        world.Player.Teleport(new Point(boss.Center.X, boss.Center.Y + 8));
        driver.Step(world);

        Assert.Equal(BossController.MaxHealth, boss.Health);
        Assert.Equal(2, world.Player.Health);
    }

    [Fact]
    public void WrongRepairTerminalDoesNotStunAndGoesOnCooldown()
    {
        var (world, driver) = ArenaAfterIntro();
        var boss = world.Boss!;
        var wrong = WrongService(boss);

        UseTerminal(world, driver, wrong);

        Assert.Equal(BossState.Attacking, boss.State);
        Assert.True(world.RepairTerminals.Single(t => t.Service == wrong).CooldownTimer > 0);
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.RepairWrong && e.Service == wrong);
    }

    [Fact]
    public void CorrectRepairTerminalStunsTheBoss()
    {
        var (world, driver) = ArenaAfterIntro();
        var boss = world.Boss!;

        UseTerminal(world, driver, boss.Diagnostic);

        Assert.Equal(BossState.Stunned, boss.State);
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.RepairCorrect);
    }

    [Fact]
    public void StunnedBossTakesDamageOnContactAndRecoversWithANewDiagnostic()
    {
        var (world, driver) = ArenaAfterIntro();
        var boss = world.Boss!;
        var previousDiagnostic = boss.Diagnostic;

        UseTerminal(world, driver, boss.Diagnostic);
        driver.Seconds(world, 0.6); // let it descend
        world.Player.Teleport(new Point(boss.Center.X, boss.Center.Y + 8));
        driver.Step(world);

        Assert.Equal(BossController.MaxHealth - 1, boss.Health);
        Assert.Equal(BossState.HitReact, boss.State);
        Assert.Equal(3, world.Player.Health);
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.BossHit);

        driver.Seconds(world, BossController.HitReactDuration + 0.1);
        Assert.Equal(BossState.Attacking, boss.State);
        Assert.NotEqual(previousDiagnostic, boss.Diagnostic);
    }

    [Fact]
    public void StunExpiresWithoutAHit()
    {
        var (world, driver) = ArenaAfterIntro();
        var boss = world.Boss!;

        UseTerminal(world, driver, boss.Diagnostic);
        world.Player.Teleport(new Point(20, world.FloorTop));
        driver.Seconds(world, BossController.StunDuration + 0.2);

        Assert.Equal(BossState.Attacking, boss.State);
        Assert.Equal(BossController.MaxHealth, boss.Health);
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.BossStunExpired);
    }

    [Fact]
    public void RepeatedlyMashingInteractNeverDamagesTheBoss()
    {
        var (world, driver) = ArenaAfterIntro();
        var boss = world.Boss!;

        for (var i = 0; i < 40; i++)
        {
            var terminal = world.RepairTerminals[i % 3];
            world.Player.Teleport(new Point(terminal.CenterX, world.FloorTop));
            driver.Press(world, InputButtons.Interact, holdSteps: 1);
            driver.Release(world);
        }

        Assert.Equal(BossController.MaxHealth, boss.Health);
    }

    [Fact]
    public void BossFiresTelegraphedProjectilesInPhaseOne()
    {
        var (world, driver) = ArenaAfterIntro();
        world.Player.Teleport(new Point(40, world.FloorTop));

        var sawCharge = false;
        for (var i = 0; i < 120 * 4 && world.Projectiles.Count == 0; i++)
        {
            driver.Step(world);
            sawCharge |= world.Boss!.Charging;
        }

        Assert.True(sawCharge, "attacks are telegraphed");
        Assert.NotEmpty(world.Projectiles);
        Assert.True(world.Projectiles.Count <= 16);
    }

    [Fact]
    public void FullFightProgressesThroughAllPhasesAndEndsInDefeat()
    {
        var (world, driver) = ArenaAfterIntro(seed: 7);
        var boss = world.Boss!;
        var phasesSeen = new HashSet<int> { boss.Phase };
        var events = new List<WorldEvent>();

        for (var hit = 0; hit < BossController.MaxHealth; hit++)
        {
            // wait until the boss is attacking (and not mid-charge) before repairing
            for (var i = 0; i < 120 * 5 && boss.State != BossState.Attacking; i++)
            {
                driver.Step(world);
            }

            Assert.Equal(BossState.Attacking, boss.State);
            world.Player.InvulnerableTimer = 10; // test harness: ignore incidental projectile damage
            UseTerminal(world, driver, boss.Diagnostic);
            Assert.Equal(BossState.Stunned, boss.State);

            driver.Seconds(world, 0.6);
            world.Player.Teleport(new Point(boss.Center.X, boss.Center.Y + 8));
            world.Player.InvulnerableTimer = 0;
            driver.Step(world);
            events.AddRange(Driver.Drain(world));
            Assert.Equal(BossController.MaxHealth - hit - 1, boss.Health);

            driver.Seconds(world, BossController.HitReactDuration + 0.05);
            phasesSeen.Add(boss.Phase);
            events.AddRange(Driver.Drain(world));
        }

        Assert.Equal(BossState.Dead, boss.State);
        driver.Seconds(world, BossController.DeathDuration + 0.2);
        events.AddRange(Driver.Drain(world));

        Assert.Equal(new HashSet<int> { 1, 2, 3 }, phasesSeen);
        Assert.Single(events, e => e.Kind == WorldEventKind.BossDefeated);
        Assert.Contains(events, e => e.Kind == WorldEventKind.BossPhaseChanged && e.Value == 2);
        Assert.Contains(events, e => e.Kind == WorldEventKind.BossPhaseChanged && e.Value == 3);
    }

    [Fact]
    public void PhaseThreeShiftsTheArenaLayoutWithAWarning()
    {
        var (world, driver) = ArenaAfterIntro(seed: 11);
        var boss = world.Boss!;

        // Bring the boss to its final phase quickly.
        for (var hit = 0; hit < 3; hit++)
        {
            for (var i = 0; i < 120 * 5 && boss.State != BossState.Attacking; i++)
            {
                driver.Step(world);
            }

            world.Player.InvulnerableTimer = 10;
            UseTerminal(world, driver, boss.Diagnostic);
            driver.Seconds(world, 0.6);
            world.Player.Teleport(new Point(boss.Center.X, boss.Center.Y + 8));
            world.Player.InvulnerableTimer = 0;
            driver.Step(world);
            driver.Seconds(world, BossController.HitReactDuration + 0.05);
        }

        Assert.Equal(3, boss.Phase);
        world.Player.InvulnerableTimer = 100;
        world.Player.Teleport(new Point(160, world.FloorTop));
        Driver.Drain(world);

        var initialLayout = world.ActiveLevel;
        var sawWarning = false;
        for (var i = 0; i < 120 * 8 && ReferenceEquals(world.ActiveLevel, initialLayout); i++)
        {
            driver.Step(world);
            sawWarning |= boss.ShiftWarning;
        }

        Assert.True(sawWarning, "arena shift is telegraphed");
        Assert.False(ReferenceEquals(world.ActiveLevel, initialLayout));
        Assert.True(world.StaticDirty);
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.ArenaShifted);
    }
}
