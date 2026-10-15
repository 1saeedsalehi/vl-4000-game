using Videoland4000.Core.Tests.Support;

namespace Videoland4000.Core.Tests;

public class SessionTests
{
    [Fact]
    public void StartsBootingAndFollowsTheMenuFlow()
    {
        var session = TestContent.NewSession(highScore: 8400);
        var phases = new List<GamePhase>();
        session.PhaseChanged += (phase, _) => phases.Add(phase);

        Assert.Equal(GamePhase.Booting, session.Phase);
        Assert.Equal(8400, session.Hud.HighScore);

        Assert.False(session.StartNewGame(), "cannot start before the boot finished");
        Assert.True(session.CompleteBoot());
        Assert.False(session.CompleteBoot(), "boot completes once");
        Assert.True(session.StartNewGame());
        Assert.Equal(GamePhase.LevelIntro, session.Phase);
        Assert.True(session.SkipIntro());
        Assert.Equal(GamePhase.Playing, session.Phase);

        Assert.Equal(new[] { GamePhase.MainMenu, GamePhase.LevelIntro, GamePhase.Playing }, phases);
    }

    [Fact]
    public void LevelIntroAdvancesAutomatically()
    {
        var session = TestContent.NewSession();
        session.CompleteBoot();
        session.StartNewGame();

        SessionDriver.Seconds(session, 4.0);

        Assert.Equal(GamePhase.Playing, session.Phase);
    }

    [Fact]
    public void PauseFreezesTheWorldAndResumeContinues()
    {
        var session = TestContent.StartPlaying();
        SessionDriver.Seconds(session, 0.5);
        var world = session.World!;

        Assert.True(session.TogglePause());
        Assert.Equal(GamePhase.Paused, session.Phase);
        var frozenTime = world.Time;
        var frozenX = world.Player.Bounds.X;

        SessionDriver.Seconds(session, 1.0, InputButtons.Right);
        Assert.Equal(frozenTime, world.Time);
        Assert.Equal(frozenX, world.Player.Bounds.X);
        Assert.False(session.Tick(SessionDriver.FrameSeconds, InputButtons.None).Running);

        Assert.True(session.TogglePause());
        Assert.Equal(GamePhase.Playing, session.Phase);
        SessionDriver.Seconds(session, 0.5, InputButtons.Right);
        Assert.True(world.Time > frozenTime);
        Assert.True(world.Player.Bounds.X > frozenX);
    }

    [Fact]
    public void PauseIsOnlyPossibleDuringGameplay()
    {
        var session = TestContent.NewSession();
        Assert.False(session.TogglePause());
        session.CompleteBoot();
        Assert.False(session.TogglePause());
    }

    [Fact]
    public void GameOverStopsGameplayAndRetryRestartsCleanly()
    {
        var session = TestContent.StartPlaying();
        var world = session.World!;
        var ended = new List<(int Score, int High, bool Checkpoint)>();
        session.GameEnded += (score, high, checkpoint) => ended.Add((score, high, checkpoint));

        SessionDriver.Seconds(session, 0.3);
        for (var i = 0; i < 3; i++)
        {
            world.DamagePlayer(0, knockback: false);
            world.Player.InvulnerableTimer = 0;
        }

        Assert.True(world.Player.Dead);
        SessionDriver.Seconds(session, PhysicsConfig.DeathDelay + 0.3);

        Assert.Equal(GamePhase.GameOver, session.Phase);
        Assert.Single(ended);
        Assert.False(ended[0].Checkpoint);

        var timeAtGameOver = world.Time;
        SessionDriver.Seconds(session, 1.0, InputButtons.Right);
        Assert.Equal(timeAtGameOver, world.Time);

        Assert.False(session.StartNewGame(), "StartNewGame is only valid from the menu");
        Assert.True(session.Retry());
        Assert.Equal(GamePhase.Playing, session.Phase);
        Assert.NotSame(world, session.World);
        Assert.Equal(3, session.World!.Player.Health);
        Assert.Equal(0, session.Score);
        Assert.Equal(0, session.World.TokensCollected);
        Assert.False(session.Retry(), "retry is only valid from game over");
    }

    [Fact]
    public void ReturnToMenuWorksFromPauseGameOverAndVictoryOnly()
    {
        var session = TestContent.StartPlaying();
        Assert.False(session.ReturnToMenu());

        session.TogglePause();
        Assert.True(session.ReturnToMenu());
        Assert.Equal(GamePhase.MainMenu, session.Phase);
        Assert.Null(session.World);

        var snapshot = session.Tick(SessionDriver.FrameSeconds, InputButtons.None);
        Assert.Equal("title", snapshot.Music);
        Assert.Contains(snapshot.Sprites, s => s.Key == "attract-player");
    }

    [Fact]
    public void ScoringAwardsConfiguredPointsOnce()
    {
        var session = TestContent.StartPlaying();
        var world = session.World!;
        var scoring = session.Content.Manifest.Spec.Scoring;

        var token = world.Collectibles.First(c => c.Kind == CollectibleKind.Token);
        world.Player.Teleport(new Point(token.Origin.X, token.Origin.Y + 6));
        SessionDriver.Frames(session, 3);
        Assert.Equal(scoring.Token, session.Score);
        SessionDriver.Seconds(session, 0.5);
        Assert.Equal(scoring.Token, session.Score);

        var coffee = world.Collectibles.First(c => c.Kind == CollectibleKind.Coffee);
        world.Player.Teleport(new Point(coffee.Origin.X, coffee.Origin.Y + 6));
        SessionDriver.Frames(session, 3);
        Assert.Equal(scoring.Token + scoring.Coffee, session.Score);
        Assert.Equal(1, session.Hud.Coffee);
    }

    [Fact]
    public void FullRunReachesTheBossAndVictoryUpdatesTheHighScore()
    {
        var session = TestContent.StartPlaying(highScore: 100, seed: 5);
        var world = session.World!;
        var scoring = session.Content.Manifest.Spec.Scoring;
        VictoryStats? stats = null;
        var highScores = new List<int>();
        session.VictoryAchieved += s => stats = s;
        session.HighScoreChanged += h => highScores.Add(h);

        Playthrough.ReachBossArena(session);
        var expectedScore = scoring.Token * world.TokensRequired + scoring.Objective + scoring.Checkpoint;
        Assert.Equal(expectedScore, session.Score);
        Assert.True(session.Hud.BossActive);
        Assert.Equal(world.TokensRequired, session.Hud.Tokens);

        Playthrough.DefeatBoss(session);

        Assert.NotNull(stats);
        Assert.Equal(expectedScore + scoring.Boss, session.Score);
        Assert.Equal(session.Score, stats!.FinalScore);
        Assert.True(stats.IsNewHighScore);
        Assert.Equal(session.Score, session.HighScore);
        Assert.Contains(session.Score, highScores);
        Assert.InRange(stats.ViewersOnline, 1_000_000, 1_999_999);

        // Victory freezes gameplay; menu return works; a new game starts clean.
        var arena = session.World!;
        var time = arena.Time;
        SessionDriver.Seconds(session, 1.0, InputButtons.Right);
        Assert.Equal(time, arena.Time);
        Assert.True(session.ReturnToMenu());
        Assert.True(session.StartNewGame());
        Assert.Equal(0, session.Score);
        Assert.False(session.CheckpointAvailable);
    }

    [Fact]
    public void RetryAfterCheckpointRestoresProgressWithoutDuplicates()
    {
        var session = TestContent.StartPlaying(seed: 2);
        var world = session.World!;
        var scoring = session.Content.Manifest.Spec.Scoring;

        foreach (var token in world.Collectibles.Where(c => c.Kind == CollectibleKind.Token).Take(world.TokensRequired))
        {
            world.Player.Teleport(new Point(token.Origin.X, token.Origin.Y + 6));
            SessionDriver.Frames(session, 2);
        }

        world.Player.Teleport(new Point(world.Terminal!.CenterX, 164));
        world.Player.InvulnerableTimer = 100;
        SessionDriver.Frames(session, 2);
        SessionDriver.Press(session, InputButtons.Interact);
        world.Player.Teleport(new Point(world.Checkpoint!.CenterX, 164));
        SessionDriver.Frames(session, 3);
        var checkpointScore = session.Score;
        Assert.Equal(scoring.Token * world.TokensRequired + scoring.Objective + scoring.Checkpoint, checkpointScore);

        // Earn more, then die.
        var bonus = world.Collectibles.First(c => c.Kind == CollectibleKind.Token && !c.Collected);
        world.Player.Teleport(new Point(bonus.Origin.X, bonus.Origin.Y + 6));
        SessionDriver.Frames(session, 2);
        Assert.Equal(checkpointScore + scoring.Token, session.Score);

        world.Player.InvulnerableTimer = 0;
        for (var i = 0; i < 3; i++)
        {
            world.DamagePlayer(0, knockback: false);
            world.Player.InvulnerableTimer = 0;
        }

        SessionDriver.Seconds(session, PhysicsConfig.DeathDelay + 0.3);
        Assert.Equal(GamePhase.GameOver, session.Phase);
        Assert.True(session.CheckpointAvailable);

        Assert.True(session.Retry());
        var restored = session.World!;
        Assert.Equal(GamePhase.Playing, session.Phase);
        Assert.Equal(checkpointScore, session.Score);
        Assert.Equal(restored.TokensRequired, restored.TokensCollected);
        Assert.True(restored.TerminalActivated);
        Assert.True(restored.ExitUnlocked);
        Assert.True(restored.CheckpointActivated);
        Assert.Equal(3, restored.Player.Health);
        Assert.Equal(restored.Checkpoint!.RespawnPoint.X, restored.Player.Feet.X, 0.01);
        Assert.False(restored.Collectibles.Single(c => c.Id == bonus.Id).Collected, "items collected after the checkpoint come back");

        SessionDriver.Seconds(session, 1.0);
        Assert.Equal(checkpointScore, session.Score);
    }

    [Fact]
    public void HudIsPublishedOnlyWhenValuesChange()
    {
        var session = TestContent.StartPlaying();
        var updates = 0;
        session.HudChanged += _ => updates++;

        // Idle on the floor: nothing in the HUD changes.
        SessionDriver.Seconds(session, 0.5);
        SessionDriver.Seconds(session, 2.0);

        Assert.True(updates <= 2, $"expected almost no HUD updates while idle, got {updates}");
    }

    [Fact]
    public void SnapshotCarriesStaticLevelOnlyWhenItChanges()
    {
        var session = TestContent.StartPlaying();

        var first = session.Tick(SessionDriver.FrameSeconds, InputButtons.None);
        Assert.NotNull(first.Level);
        Assert.Equal("dungeon", first.Level!.Theme);
        Assert.True(first.Running);
        Assert.Equal("level", first.Music);
        var levelId = first.LevelId;

        var second = session.Tick(SessionDriver.FrameSeconds, InputButtons.None);
        Assert.Null(second.Level);
        Assert.Equal(levelId, second.LevelId);

        var keys = second.Sprites.Select(s => s.Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
        Assert.Contains(second.Sprites, s => s.Key == "player");
    }

    [Fact]
    public void SnapshotSerializesWithTheDocumentedShortNames()
    {
        var session = TestContent.StartPlaying();
        var snapshot = session.Tick(SessionDriver.FrameSeconds, InputButtons.Right);

        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        Assert.Contains("\"cam\":{\"x\":", json);
        Assert.Contains("\"sprites\":[{\"k\":", json);
        Assert.Contains("\"fx\":[", json);
        Assert.Contains("\"levelId\":", json);
        Assert.Contains("\"tiles\":[{\"f\":", json);
    }

    [Fact]
    public void InputEdgeDetectionIgnoresHeldKeys()
    {
        var state = InputState.FromHeld(InputButtons.Jump | InputButtons.Right, InputButtons.Jump);
        Assert.True(state.IsHeld(InputButtons.Jump));
        Assert.False(state.WasPressed(InputButtons.Jump));
        Assert.True(state.WasPressed(InputButtons.Right));
    }
}
