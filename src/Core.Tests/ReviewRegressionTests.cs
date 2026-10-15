using Videoland4000.Core.Tests.Support;

namespace Videoland4000.Core.Tests;

/// <summary>Regression tests for defects found by the independent review.</summary>
public class ReviewRegressionTests
{
    [Fact]
    public void JumpPressedOnAFrameShorterThanAFixedStepIsNotLost()
    {
        var session = TestContent.StartPlaying();
        SessionDriver.Seconds(session, 0.3);
        var world = session.World!;
        var floor = world.Player.Bounds.Bottom;

        // Two 4 ms frames (240 Hz): neither reaches the 8.33 ms fixed step, the press edge lands on the second.
        session.Tick(0.004, InputButtons.None);
        session.Tick(0.004, InputButtons.Jump);
        for (var i = 0; i < 6; i++)
        {
            session.Tick(1.0 / 60.0, InputButtons.Jump);
        }

        Assert.True(world.Player.Bounds.Bottom < floor - 4, $"player should be airborne (bottom {world.Player.Bounds.Bottom}, floor {floor})");
    }

    [Fact]
    public void InteractPressedOnAShortFrameStillUsesTheTerminal()
    {
        var session = TestContent.StartPlaying();
        var world = session.World!;
        var messages = new List<GameMessage>();
        session.MessageShown += messages.Add;

        world.Player.Teleport(new Point(world.Terminal!.CenterX, 164));
        SessionDriver.Frames(session, 2);
        session.Tick(0.004, InputButtons.None);
        session.Tick(0.004, InputButtons.Interact);
        SessionDriver.Frames(session, 3, InputButtons.Interact);

        Assert.Contains(messages, m => m.Lines[0].StartsWith("ACCESS DENIED", StringComparison.Ordinal));
    }

    [Fact]
    public void PressesMadeOutsideGameplayDoNotFireWhenGameplayStarts()
    {
        var session = TestContent.NewSession();
        session.CompleteBoot();
        session.StartNewGame();
        SessionDriver.Frames(session, 5, InputButtons.Jump); // Space held during the intro
        session.SkipIntro();
        SessionDriver.Frames(session, 6, InputButtons.Jump);

        var world = session.World!;
        Assert.True(world.Player.Grounded, "the key that skipped the intro must not also jump");

        // A genuinely new press afterwards still works.
        SessionDriver.Frames(session, 2, InputButtons.None);
        SessionDriver.Frames(session, 6, InputButtons.Jump);
        Assert.False(world.Player.Grounded);
    }

    [Fact]
    public void BossDeathPopsItsSpinnersAndProtectsThePlayer()
    {
        var world = TestContent.NewWorld(TestContent.Arena(), seed: 3);
        var driver = new Driver();
        driver.Seconds(world, BossController.IntroDuration + 0.2);
        var boss = world.Boss!;
        var spinner = world.SpawnSpinner(new Point(160, world.FloorTop - 44), 100, 30, fromBoss: true);

        for (var hit = 0; hit < BossController.MaxHealth; hit++)
        {
            for (var i = 0; i < 120 * 5 && boss.State != BossState.Attacking; i++)
            {
                driver.Step(world);
            }

            world.Player.InvulnerableTimer = 10;
            var terminal = world.RepairTerminals.Single(t => t.Service == boss.Diagnostic);
            world.Player.Teleport(new Point(terminal.CenterX, world.FloorTop));
            driver.Step(world, steps: 2);
            driver.Press(world, InputButtons.Interact, holdSteps: 2);
            driver.Release(world);
            driver.Seconds(world, 0.6);
            world.Player.Teleport(new Point(boss.Center.X, boss.Center.Y + 8));
            world.Player.InvulnerableTimer = 0;
            driver.Step(world);
        }

        Assert.Equal(0, boss.Health);
        driver.Seconds(world, BossController.HitReactDuration + 0.05);
        Assert.Equal(BossState.Dead, boss.State);
        Assert.False(spinner.Alive, "boss-spawned spinners pop with the boss");

        world.Player.InvulnerableTimer = 0;
        var healthBefore = world.Player.Health;
        Assert.False(world.DamagePlayer(0), "nothing can hurt the player once the incident is over");
        Assert.Equal(healthBefore, world.Player.Health);

        driver.Seconds(world, BossController.DeathDuration + 0.2);
        Assert.Contains(Driver.Drain(world), e => e.Kind == WorldEventKind.BossDefeated);
        Assert.False(world.Player.Dead);
    }

    [Fact]
    public void TyingTheHighScoreIsNotReportedAsANewHighScore()
    {
        var session = TestContent.StartPlaying(highScore: 3250, seed: 5);
        VictoryStats? stats = null;
        session.VictoryAchieved += s => stats = s;

        Playthrough.ReachBossArena(session);
        Playthrough.DefeatBoss(session);

        Assert.NotNull(stats);
        Assert.Equal(3250, stats!.FinalScore);
        Assert.False(stats.IsNewHighScore);

        var better = TestContent.StartPlaying(highScore: 100, seed: 5);
        VictoryStats? betterStats = null;
        better.VictoryAchieved += s => betterStats = s;
        Playthrough.ReachBossArena(better);
        Playthrough.DefeatBoss(better);
        Assert.True(betterStats!.IsNewHighScore);
    }

    [Fact]
    public void AbandoningARunFromTheMenuDoesNotCountAsAHighScore()
    {
        var session = TestContent.StartPlaying();
        var saved = new List<int>();
        session.HighScoreChanged += saved.Add;
        var world = session.World!;

        var token = world.Collectibles.First(c => c.Kind == CollectibleKind.Token);
        world.Player.Teleport(new Point(token.Origin.X, token.Origin.Y + 6));
        SessionDriver.Frames(session, 3);
        Assert.True(session.Score > 0);

        session.TogglePause();
        session.ReturnToMenu();

        Assert.Equal(0, session.Hud.HighScore);
        Assert.Empty(saved);
    }

    [Fact]
    public void RetryingInsideTheArenaShowsTheIncidentBannerOnce()
    {
        var session = TestContent.StartPlaying(seed: 5);
        Playthrough.ReachBossArena(session);
        var arena = session.World!;
        var messages = new List<GameMessage>();
        session.MessageShown += messages.Add;

        for (var i = 0; i < 3; i++)
        {
            arena.Player.InvulnerableTimer = 0;
            arena.DamagePlayer(0, knockback: false);
        }

        SessionDriver.Seconds(session, PhysicsConfig.DeathDelay + 0.3);
        Assert.Equal(GamePhase.GameOver, session.Phase);
        messages.Clear();

        Assert.True(session.Retry());
        Assert.Equal(GamePhase.BossEncounter, session.Phase);
        SessionDriver.Seconds(session, BossController.IntroDuration + 0.5);

        Assert.Equal(1, messages.Count(m => m.Lines[0].StartsWith("INCIDENT:", StringComparison.Ordinal)));
    }

    [Fact]
    public void PhaseThreeExplainsWavesAndDriftOnlyOnce()
    {
        var session = TestContent.StartPlaying(seed: 5);
        Playthrough.ReachBossArena(session);
        var arena = session.World!;
        var boss = arena.Boss!;
        var messages = new List<GameMessage>();
        session.MessageShown += messages.Add;

        SessionDriver.Seconds(session, BossController.IntroDuration + 0.2);
        arena.Player.InvulnerableTimer = 10000;
        for (var hit = 0; hit < 3; hit++)
        {
            for (var i = 0; i < 60 * 6 && boss.State != BossState.Attacking; i++)
            {
                session.Tick(SessionDriver.FrameSeconds, InputButtons.None);
            }

            var terminal = arena.RepairTerminals.Single(t => t.Service == boss.Diagnostic);
            arena.Player.Teleport(new Point(terminal.CenterX, arena.FloorTop));
            SessionDriver.Frames(session, 2);
            SessionDriver.Press(session, InputButtons.Interact);
            SessionDriver.Seconds(session, 0.6);
            arena.Player.Teleport(new Point(boss.Center.X, boss.Center.Y + 8));
            SessionDriver.Frames(session, 1);
            SessionDriver.Seconds(session, BossController.HitReactDuration + 0.1);
        }

        Assert.Equal(3, boss.Phase);
        arena.Player.Teleport(new Point(160, arena.FloorTop));
        messages.Clear();
        SessionDriver.Seconds(session, 20);

        Assert.Equal(1, messages.Count(m => m.Lines[0] == "TRAFFIC WAVE INCOMING"));
        Assert.Equal(1, messages.Count(m => m.Lines[0] == "INFRASTRUCTURE DRIFT"));
    }

    [Fact]
    public void DyingAfterTheMidLevelCheckpointRetriesFromThere()
    {
        var session = TestContent.StartPlaying(seed: 4);
        var world = session.World!;
        var mid = world.Checkpoints[0];
        world.Player.Teleport(mid.RespawnPoint);
        SessionDriver.Frames(session, 3);
        Assert.True(session.CheckpointAvailable);
        var checkpointScore = session.Score;

        for (var i = 0; i < 3; i++)
        {
            world.Player.InvulnerableTimer = 0;
            world.DamagePlayer(0, knockback: false);
        }

        SessionDriver.Seconds(session, PhysicsConfig.DeathDelay + 0.3);
        Assert.True(session.Retry());
        var restored = session.World!;
        Assert.Equal(mid.RespawnPoint.X, restored.Player.Feet.X, 0.01);
        Assert.True(restored.Checkpoints[0].Activated);
        Assert.False(restored.Checkpoints[1].Activated);
        Assert.Equal(checkpointScore, session.Score);
    }

    [Fact]
    public void SnapshotPoolingNeverLeaksStaleSpriteState()
    {
        var session = TestContent.StartPlaying();
        var first = session.Tick(SessionDriver.FrameSeconds, InputButtons.None);
        var keys = first.Sprites.Select(s => s.Key).ToList();

        // Pause: the next snapshot renders the same world, so pooled objects are reused with identical content.
        session.TogglePause();
        var second = session.Tick(SessionDriver.FrameSeconds, InputButtons.None);

        Assert.Equal(keys, second.Sprites.Select(s => s.Key).ToList());
        Assert.All(second.Sprites, s => Assert.False(string.IsNullOrEmpty(s.Frame)));
        Assert.Equal(second.Sprites.Count, second.Sprites.Select(s => s.Key).Distinct().Count());
    }
}
