namespace Videoland4000.Model.Session;

/// <summary>Gameplay wiring for <see cref="GameSession"/>: world lifecycle, scoring, messages, HUD.</summary>
public sealed partial class GameSession
{
    private const double IntroDuration = 3.5;

    private World? _world;
    private AttractScene? _attract;
    private Level? _levelGeometry;
    private Level? _arenaGeometry;
    private ProgressSnapshot? _checkpoint;
    private int _score;
    private double _accumulator;
    private double _introTimer;
    private int _levelId;
    private bool _attractDirty = true;
    private bool _inArena;
    private int _lastBossPhase;
    private int _levelTokensCollected;
    private bool _explainedTrafficWave;
    private bool _explainedArenaShift;

    public int Score => _score;
    public bool CheckpointAvailable => _checkpoint is not null;

    /// <summary>The live world (null in menus). Exposed for tests and diagnostics.</summary>
    public World? World => _world;

    public Level LevelGeometry => _levelGeometry ??= LevelParser.Parse(Content.Level);
    public Level ArenaGeometry => _arenaGeometry ??= LevelParser.Parse(Content.BossArena);

    private int MaxHealth => Content.Manifest.Spec.Player.MaxHealth;
    private ScoringSpec Scoring => Content.Manifest.Spec.Scoring;

    // ----- Phase entry points ----------------------------------------------

    private void EnterMainMenu()
    {
        _world = null;
        _inArena = false;
        _attract ??= new AttractScene();
        _attractDirty = true;
        _levelId++;
        ChangePhase(GamePhase.MainMenu);
        RefreshHud();
    }

    private void BeginNewRun()
    {
        _score = 0;
        _checkpoint = null;
        CreateLevelWorld(progress: null);
        _introTimer = IntroDuration;
        ChangePhase(GamePhase.LevelIntro);
    }

    private void StartLevelPlay()
    {
        _suppressEdgesOnce = true;
        _pendingPressed = InputButtons.None;
        ChangePhase(GamePhase.Playing);
        Say(GameTexts.LevelStart());
    }

    private void RetryFromCheckpointOrStart()
    {
        if (_checkpoint is { } checkpoint)
        {
            _score = checkpoint.Score;
            if (checkpoint.InBossArena)
            {
                _levelTokensCollected = Math.Min(Content.Level.TokensRequired, checkpoint.CollectedIds.Count(id => id.StartsWith("Token:", StringComparison.Ordinal)));
                CreateArenaWorld(checkpoint);
                ChangePhase(GamePhase.BossEncounter);
            }
            else
            {
                CreateLevelWorld(checkpoint);
                ChangePhase(GamePhase.Playing);
            }
        }
        else
        {
            _score = 0;
            CreateLevelWorld(progress: null);
            ChangePhase(GamePhase.Playing);
        }
    }

    private void CreateLevelWorld(ProgressSnapshot? progress)
    {
        _inArena = false;
        _world = new World(LevelGeometry, Random, MaxHealth, Scoring);
        if (progress is not null)
        {
            _world.ApplyProgress(progress);
        }

        _accumulator = 0;
        RefreshHud();
    }

    private void CreateArenaWorld(ProgressSnapshot progress)
    {
        _inArena = true;
        _explainedTrafficWave = false;
        _explainedArenaShift = false;
        _world = new World(ArenaGeometry, Random, MaxHealth, Scoring);
        _world.ApplyProgress(progress);
        _lastBossPhase = 1;
        _accumulator = 0;
        RefreshHud();
    }

    private void EnterBossArena()
    {
        _levelTokensCollected = _world?.TokensCollected ?? 0;
        _checkpoint = CaptureProgress(inBossArena: true);
        CreateArenaWorld(_checkpoint);
        ChangePhase(GamePhase.BossEncounter);
        Say(GameTexts.PrimeTimeWarning());
    }

    private ProgressSnapshot CaptureProgress(bool inBossArena)
    {
        var world = _world!;
        var collected = world.Collectibles.Where(c => c.Collected).Select(c => c.Id).ToHashSet();
        if (_checkpoint is not null)
        {
            collected.UnionWith(_checkpoint.CollectedIds);
        }

        return new ProgressSnapshot(
            _score,
            collected,
            world.CoffeeConsumed,
            world.TerminalActivated || (_checkpoint?.TerminalActivated ?? false),
            world.CheckpointActivated || (_checkpoint?.CheckpointActivated ?? false),
            inBossArena,
            world.ActiveCheckpoint?.Id ?? _checkpoint?.CheckpointId);
    }

    private void EndGame()
    {
        UpdateHighScore(_score);
        ChangePhase(GamePhase.GameOver);
        GameEnded?.Invoke(_score, HighScore, CheckpointAvailable);
    }

    private void Win()
    {
        AddScore(Scoring.Boss);
        var isNewHighScore = _score > HighScore;
        UpdateHighScore(_score);

        var coffee = _world?.CoffeeConsumed ?? 0;
        var stats = new VictoryStats(
            ViewersOnline: 1_000_000 + Random.Next(0, 999_999),
            IncidentsResolved: Random.Next(17, 99),
            CoffeeConsumed: coffee + Random.Next(11, 18),
            FinalScore: _score,
            HighScore: HighScore,
            IsNewHighScore: isNewHighScore);

        LastVictory = stats;
        RefreshHud();
        Say(GameTexts.DeploymentSuccessful());
        VictoryAchieved?.Invoke(stats);
        ChangePhase(GamePhase.Victory);
    }

    // ----- Per frame --------------------------------------------------------

    private void Advance(double deltaSeconds, InputState input)
    {
        switch (Phase)
        {
            case GamePhase.MainMenu:
            case GamePhase.Booting:
                _attract?.Update(deltaSeconds);
                break;
            case GamePhase.LevelIntro:
                _introTimer -= deltaSeconds;
                if (_introTimer <= 0)
                {
                    StartLevelPlay();
                }

                break;
            case GamePhase.Playing:
            case GamePhase.BossEncounter:
                StepWorld(deltaSeconds, input);
                break;
        }
    }

    private void StepWorld(double deltaSeconds, InputState input)
    {
        var world = _world;
        if (world is null)
        {
            return;
        }

        _accumulator += deltaSeconds;
        var first = true;

        while (_accumulator >= PhysicsConfig.FixedStep)
        {
            _accumulator -= PhysicsConfig.FixedStep;
            world.Step(PhysicsConfig.FixedStep, first ? input : new InputState(input.Held, InputButtons.None));
            if (first)
            {
                _pendingPressed = InputButtons.None; // edges consumed by this step
                first = false;
            }

            DrainEvents(world);

            if (Phase is not (GamePhase.Playing or GamePhase.BossEncounter))
            {
                _accumulator = 0;
                break;
            }

            if (world.Player.Dead && world.Player.DeathTimer <= 0)
            {
                EndGame();
                _accumulator = 0;
                break;
            }
        }

        RefreshHud();
    }

    private void DrainEvents(World world)
    {
        while (world.Events.TryDequeue(out var worldEvent))
        {
            HandleWorldEvent(world, worldEvent);
        }
    }

    private void HandleWorldEvent(World world, WorldEvent e)
    {
        switch (e.Kind)
        {
            case WorldEventKind.TokenCollected:
                AddScore(Scoring.Token);
                break;
            case WorldEventKind.CoffeeCollected:
                AddScore(Scoring.Coffee);
                break;
            case WorldEventKind.PowerUpCollected:
                Say(e.Pickup switch
                {
                    CollectibleKind.CacheBoost => GameTexts.CacheHit(),
                    CollectibleKind.CircuitBreaker => GameTexts.BreakerTripped(e.Value),
                    _ => GameTexts.RollbackArmed(),
                });
                break;
            case WorldEventKind.EnemyDefeated:
                AddScore(Scoring.Enemy);
                break;
            case WorldEventKind.PlayerDamaged:
                if (e.Enemy == EnemyKind.ExpiredToken)
                {
                    Say(GameTexts.TokenExpired());
                }

                break;
            case WorldEventKind.PlayerDied:
                Say(GameTexts.Death());
                break;
            case WorldEventKind.PlayerFell:
                Say(GameTexts.ConnectionLost());
                break;
            case WorldEventKind.RollbackDeployed:
                Say(GameTexts.RollbackDeployed());
                break;
            case WorldEventKind.TerminalActivated:
                AddScore(Scoring.Objective);
                Say(GameTexts.Authenticating());
                Say(GameTexts.RootCause());
                break;
            case WorldEventKind.TerminalDenied:
                Say(GameTexts.AccessDenied(world.TokensCollected, world.TokensRequired));
                break;
            case WorldEventKind.TerminalAlreadyActive:
                Say(GameTexts.AlreadyAuthenticated());
                break;
            case WorldEventKind.CheckpointActivated:
                AddScore(Scoring.Checkpoint);
                _checkpoint = CaptureProgress(inBossArena: false);
                Say(GameTexts.CheckpointSaved());
                break;
            case WorldEventKind.ExitReached:
                EnterBossArena();
                break;
            case WorldEventKind.ExitLocked:
                Say(GameTexts.ExitLocked());
                break;
            case WorldEventKind.BossIntroFinished:
                Say(GameTexts.BossIntro());
                break;
            case WorldEventKind.BossPhaseChanged:
                if (e.Value != _lastBossPhase)
                {
                    _lastBossPhase = e.Value;
                    Say(e.Value == 2 ? GameTexts.TrafficSpike() : GameTexts.InfiniteLoop());
                }

                break;
            case WorldEventKind.BossStunExpired:
                Say(GameTexts.StunExpired());
                break;
            case WorldEventKind.BossHit:
                Say(GameTexts.BossHit(e.Value));
                break;
            case WorldEventKind.BossDefeated:
                Win();
                break;
            case WorldEventKind.RepairCorrect:
                Say(GameTexts.RepairCorrect(BossController.ServiceName(e.Service)));
                break;
            case WorldEventKind.RepairWrong:
                Say(GameTexts.RepairWrong(BossController.ServiceName(e.Service)));
                break;
            case WorldEventKind.RepairCooldown:
                Say(GameTexts.RepairUnavailable());
                break;
            case WorldEventKind.ArenaShifted:
                if (!_explainedArenaShift)
                {
                    _explainedArenaShift = true;
                    Say(GameTexts.ArenaShifted());
                }

                break;
            case WorldEventKind.TrafficWaveIncoming:
                if (!_explainedTrafficWave)
                {
                    _explainedTrafficWave = true;
                    Say(GameTexts.TrafficWave());
                }

                break;
        }
    }

    private void AddScore(int points)
    {
        _score = Math.Min(HighScoreValidator.MaxScore, _score + Math.Max(0, points));
    }

    // ----- Developer hook ------------------------------------------------------

    /// <summary>
    /// Developer/test commands (the host only wires them when the page is opened
    /// with <c>?dev</c>). They never grant score directly: they move the player or
    /// trigger the same world interactions a player would. Returns a status line.
    /// </summary>
    public string ApplyDebugCommand(string command)
    {
        var world = _world;
        if (world is null)
        {
            return "no world (not playing)";
        }

        switch (command.Trim().ToLowerInvariant())
        {
            case "tokens":
                foreach (var token in world.Collectibles.Where(c => c.Kind == CollectibleKind.Token && !c.Collected))
                {
                    if (world.TokensCollected >= world.TokensRequired)
                    {
                        break;
                    }

                    world.ForceCollect(token);
                }

                return $"tokens {world.TokensCollected}/{world.TokensRequired}";
            case "terminal":
                if (world.Terminal is null) return "no terminal";
                world.TeleportPlayer(new Point(world.Terminal.CenterX, world.Terminal.Bounds.Bottom));
                return "at terminal";
            case "checkpoint":
                if (world.Checkpoint is null) return "no checkpoint";
                world.TeleportPlayer(world.Checkpoint.RespawnPoint);
                return "at last checkpoint";
            case "checkpoint1":
                if (world.Checkpoints.Count == 0) return "no checkpoint";
                world.TeleportPlayer(world.Checkpoints[0].RespawnPoint);
                return "at first checkpoint";
            case "exit":
                if (world.Exit is null) return "no exit";
                world.TeleportPlayer(new Point(world.Exit.CenterX - 4, world.Exit.Bounds.Bottom));
                return "at exit";
            case "repair":
                if (world.Boss is null) return "no boss";
                var target = world.RepairTerminals.FirstOrDefault(t => t.Service == world.Boss.Diagnostic);
                if (target is null) return "no matching terminal";
                world.TeleportPlayer(new Point(target.CenterX, target.Bounds.Bottom));
                return $"at {target.Label} terminal";
            case "stun":
                if (world.Boss is null) return "no boss";
                world.Boss.ForceStun();
                return $"boss {world.Boss.State}";
            case "boss-touch":
                if (world.Boss is null) return "no boss";
                world.TeleportPlayer(new Point(world.Boss.Center.X, world.Boss.Center.Y + 8));
                return "touching boss";
            case "heal":
                world.Player.Health = world.Player.MaxHealth;
                return $"health {world.Player.Health}";
            case "hurt":
                world.Player.InvulnerableTimer = 0;
                world.DamagePlayer(world.Player.CenterX + 1, knockback: false);
                return $"health {world.Player.Health}";
            case "state":
                return $"{Phase} score={_score} tokens={world.TokensCollected}/{world.TokensRequired} health={world.Player.Health} " +
                       $"terminal={world.TerminalActivated} exit={world.ExitUnlocked} checkpoint={world.CheckpointActivated} " +
                       $"boss={(world.Boss is null ? "-" : $"{world.Boss.State}/{world.Boss.Health}/{world.Boss.Diagnostic}")} " +
                       $"player=({world.Player.Feet.X:0},{world.Player.Feet.Y:0})";
            default:
                return "unknown command: tokens|terminal|checkpoint|exit|repair|stun|boss-touch|heal|hurt|state";
        }
    }

    // ----- HUD ----------------------------------------------------------------

    private void RefreshHud()
    {
        var world = _world;
        var player = world?.Player;
        var boss = world?.Boss;

        string objective;
        string status;

        if (world is null)
        {
            objective = string.Empty;
            status = string.Empty;
        }
        else if (_inArena && boss is not null)
        {
            objective = "FIX THE INFINITE SPINNER";
            status = boss.State switch
            {
                BossState.Intro => "INCIDENT IN PROGRESS",
                BossState.Attacking => boss.DiagnosticText,
                BossState.Stunned => "SPINNER STUNNED. JUMP INTO IT!",
                BossState.HitReact => "DIRECT HIT",
                _ => "RESOLVING INCIDENT...",
            };
        }
        else if (world.TokensCollected < world.TokensRequired)
        {
            objective = $"COLLECT TOKENS ({world.TokensCollected:00}/{world.TokensRequired:00})";
            status = "AUTHENTICATION UNSTABLE";
        }
        else if (!world.TerminalActivated)
        {
            objective = "RESTORE AUTHENTICATION: PRESS E AT THE RED TERMINAL";
            status = "TOKENS READY";
        }
        else
        {
            objective = "REACH THE EXIT DOOR";
            status = "SESSION RESTORED";
        }

        var hud = new HudState
        {
            Score = _score,
            HighScore = world is null ? HighScore : Math.Max(HighScore, _score),
            Tokens = _inArena ? _levelTokensCollected : world?.TokensCollected ?? 0,
            TokensRequired = _inArena ? Content.Level.TokensRequired : world?.TokensRequired ?? Content.Level.TokensRequired,
            Health = player?.Health ?? MaxHealth,
            MaxHealth = MaxHealth,
            Coffee = world?.CoffeeConsumed ?? 0,
            LevelName = _inArena ? "FINAL: PRIME TIME" : Content.Level.Name,
            Objective = objective,
            Status = status,
            BossActive = _inArena && boss is not null,
            BossHealth = boss?.Health ?? 0,
            BossMaxHealth = BossController.MaxHealth,
            BossPhase = boss?.Phase ?? 0,
            BossDiagnostic = boss is { State: BossState.Attacking } ? boss.DiagnosticText : string.Empty,
            CacheBoostSeconds = Math.Ceiling(world?.CacheBoostTimer ?? 0),
            FreezeSeconds = Math.Ceiling(world?.FreezeDisplayTimer ?? 0),
            RollbackArmed = player?.RollbackArmed ?? false,
        };

        PublishHud(hud);
    }

    // ----- Rendering ----------------------------------------------------------

    private void Render(FrameSnapshot snapshot)
    {
        var world = _world;
        if (world is not null)
        {
            var includeStatic = world.StaticDirty;
            if (includeStatic)
            {
                _levelId++;
                world.StaticDirty = false;
            }

            world.Render(snapshot, includeStatic);
            snapshot.Sounds.AddRange(world.Sounds);
            world.Sounds.Clear();
        }
        else if (_attract is not null)
        {
            _attract.Render(snapshot, _attractDirty);
            _attractDirty = false;
        }
        else
        {
            snapshot.Camera = new CameraState(0, 0, 0);
        }

        snapshot.LevelId = _levelId;
        snapshot.Music = Phase switch
        {
            GamePhase.MainMenu => "title",
            GamePhase.LevelIntro or GamePhase.Playing => "level",
            GamePhase.BossEncounter => "boss",
            GamePhase.Paused => _inArena ? "boss" : "level",
            GamePhase.Victory => "victory",
            _ => null,
        };
    }
}
