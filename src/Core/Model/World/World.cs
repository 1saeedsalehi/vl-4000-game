namespace Videoland4000.Model.Simulation;

/// <summary>
/// The live simulation of one level or the boss arena. Pure C#, deterministic
/// given the same inputs and random source. Produces <see cref="WorldEvent"/>s
/// for the session to score and narrate, and sound cues for the renderer.
/// </summary>
public sealed class World
{
    private const double CacheBoostDuration = 8.0;
    private const double CacheBoostSlowFactor = 0.4;
    private const double CircuitBreakerDuration = 4.0;
    private const double CircuitBreakerRadius = 80.0;
    private const double FloatingTextRise = 18.0;

    private readonly IRandomSource _random;
    private readonly ScoringSpec _scoring;
    private int _keyCounter;

    public Level Level { get; }
    public Level ActiveLevel { get; private set; }
    public Player Player { get; }
    public List<Enemy> Enemies { get; } = new();
    public List<Collectible> Collectibles { get; } = new();
    public AuthTerminal? Terminal { get; private set; }
    public List<Checkpoint> Checkpoints { get; } = new();
    /// <summary>The most recently activated checkpoint (respawn point), if any.</summary>
    public Checkpoint? ActiveCheckpoint { get; private set; }
    /// <summary>The last checkpoint in level order (the one before the exit).</summary>
    public Checkpoint? Checkpoint => Checkpoints.Count > 0 ? Checkpoints[^1] : null;
    public ExitDoor? Exit { get; private set; }
    public List<RepairTerminal> RepairTerminals { get; } = new();
    public BossController? Boss { get; private set; }
    public List<Projectile> Projectiles { get; } = new();
    public List<Shockwave> Shockwaves { get; } = new();
    public List<Effect> Effects { get; } = new();
    public List<FloatingText> FloatingTexts { get; } = new();

    public Queue<WorldEvent> Events { get; } = new();
    public List<string> Sounds { get; } = new();
    public double Shake { get; set; }
    public double Flash { get; set; }
    public double Time { get; private set; }
    public double CameraX { get; private set; }
    public double CacheBoostTimer { get; private set; }
    public double FreezeDisplayTimer { get; private set; }
    public int TokensCollected { get; private set; }
    public int TokensRequired => Level.TokensRequired;
    public int CoffeeConsumed { get; private set; }
    public bool ExitUnlocked { get; private set; }
    public bool TerminalActivated => Terminal?.Activated ?? false;
    public bool CheckpointActivated => ActiveCheckpoint is not null;
    public bool StaticDirty { get; set; } = true;
    public int FloorTop => ActiveLevel.Height - PhysicsConfig.TileSize;

    public IRandomSource Random => _random;
    public ScoringSpec Scoring => _scoring;

    public World(Level level, IRandomSource random, int maxHealth, ScoringSpec scoring)
    {
        Level = level;
        ActiveLevel = level;
        _random = random;
        _scoring = scoring;
        Player = new Player(maxHealth);
        Player.Spawn(level.PlayerSpawn);
        SpawnEntities(level);
        Checkpoints.Sort((a, b) => a.CenterX.CompareTo(b.CenterX));
        SnapCamera();
    }

    // ----- Setup ------------------------------------------------------------

    private void SpawnEntities(Level level)
    {
        const int T = PhysicsConfig.TileSize;
        foreach (var spawn in level.Spawns)
        {
            var p = spawn.Position;
            switch (spawn.Kind)
            {
                case SpawnKind.Token:
                case SpawnKind.Coffee:
                case SpawnKind.CacheBoost:
                case SpawnKind.CircuitBreaker:
                case SpawnKind.Rollback:
                    Collectibles.Add(new Collectible
                    {
                        Key = NextKey("pickup"),
                        Id = spawn.Id,
                        Kind = spawn.Kind switch
                        {
                            SpawnKind.Token => CollectibleKind.Token,
                            SpawnKind.Coffee => CollectibleKind.Coffee,
                            SpawnKind.CacheBoost => CollectibleKind.CacheBoost,
                            SpawnKind.CircuitBreaker => CollectibleKind.CircuitBreaker,
                            _ => CollectibleKind.Rollback,
                        },
                        Origin = new Point(p.X, p.Y - T / 2.0),
                        Bounds = Rect.FromCenter(p.X, p.Y - T / 2.0, 12, 12),
                    });
                    break;
                case SpawnKind.Spinner:
                    SpawnSpinner(p, spawn.Override?.PatrolRange ?? 48, spawn.Override?.Speed ?? 26, fromBoss: false);
                    break;
                case SpawnKind.ExpiredToken:
                {
                    var boxW = (spawn.Override?.BoxWidth ?? 3) * T;
                    var boxH = (spawn.Override?.BoxHeight ?? 2) * T;
                    var box = new Rect(p.X - boxW / 2.0, p.Y - boxH, boxW, boxH);
                    var angle = _random.NextDouble() * Math.PI * 2;
                    var speed = spawn.Override?.Speed ?? 45;
                    Enemies.Add(new Enemy
                    {
                        Key = NextKey("enemy"),
                        Kind = EnemyKind.ExpiredToken,
                        Speed = speed,
                        Origin = p,
                        Box = box,
                        Bounds = Rect.FromCenter(p.X, p.Y - boxH / 2.0, 12, 12),
                        VelX = Math.Cos(angle) * speed,
                        VelY = Math.Sin(angle) * speed,
                        PhaseTimer = 3.0 + _random.NextDouble(),
                        SteerTimer = 1.2,
                    });
                    break;
                }
                case SpawnKind.Bug:
                    Enemies.Add(new Enemy
                    {
                        Key = NextKey("enemy"),
                        Kind = EnemyKind.Bug,
                        Speed = spawn.Override?.Speed ?? 40,
                        Origin = p,
                        Bounds = Rect.FromBottomCenter(p.X, p.Y, 12, 10),
                        FacingRight = false,
                    });
                    break;
                case SpawnKind.Terminal:
                    Terminal = new AuthTerminal { Key = "terminal", Bounds = Rect.FromBottomCenter(p.X, p.Y, 14, 32) };
                    break;
                case SpawnKind.Checkpoint:
                    Checkpoints.Add(new Checkpoint
                    {
                        Key = NextKey("checkpoint"),
                        Id = spawn.Id,
                        Bounds = Rect.FromBottomCenter(p.X, p.Y, 12, 32),
                        RespawnPoint = p,
                    });
                    break;
                case SpawnKind.Exit:
                    Exit = new ExitDoor { Key = "exit", Bounds = Rect.FromBottomCenter(p.X, p.Y, 20, 30) };
                    break;
                case SpawnKind.RepairIdentity:
                case SpawnKind.RepairContent:
                case SpawnKind.RepairStreaming:
                    RepairTerminals.Add(new RepairTerminal
                    {
                        Key = NextKey("repair"),
                        Service = spawn.Kind switch
                        {
                            SpawnKind.RepairIdentity => ServiceKind.Identity,
                            SpawnKind.RepairContent => ServiceKind.Content,
                            _ => ServiceKind.Streaming,
                        },
                        Bounds = Rect.FromBottomCenter(p.X, p.Y, 14, 32),
                    });
                    break;
                case SpawnKind.Boss:
                    Boss = new BossController(this, new Point(p.X, p.Y));
                    break;
            }
        }
    }

    public Enemy SpawnSpinner(Point feet, double patrolRange, double speed, bool fromBoss)
    {
        var enemy = new Enemy
        {
            Key = NextKey("enemy"),
            Kind = EnemyKind.Spinner,
            Speed = speed,
            Origin = feet,
            PatrolMin = Math.Max(8, feet.X - patrolRange),
            PatrolMax = Math.Min(ActiveLevel.Width - 8, feet.X + patrolRange),
            Bounds = Rect.FromBottomCenter(feet.X, feet.Y, 12, 12),
            VelX = speed,
            FacingRight = true,
            SpawnedByBoss = fromBoss,
            Bob = _random.NextDouble() * Math.PI * 2,
        };
        Enemies.Add(enemy);
        return enemy;
    }

    private string NextKey(string prefix) => $"{prefix}{_keyCounter++}";

    /// <summary>Restores checkpoint progress without duplicating collectibles or rewards.</summary>
    public void ApplyProgress(ProgressSnapshot progress)
    {
        foreach (var collectible in Collectibles)
        {
            if (progress.CollectedIds.Contains(collectible.Id))
            {
                collectible.Collected = true;
                if (collectible.Kind == CollectibleKind.Token)
                {
                    TokensCollected++;
                }
            }
        }

        CoffeeConsumed = progress.CoffeeConsumed;

        if (progress.TerminalActivated && Terminal is not null)
        {
            Terminal.Activated = true;
            UnlockExit(silent: true);
        }

        if (progress.CheckpointActivated && Checkpoints.Count > 0)
        {
            var target = Checkpoints.FirstOrDefault(c => c.Id == progress.CheckpointId) ?? Checkpoints[^1];
            foreach (var checkpoint in Checkpoints)
            {
                checkpoint.Activated = checkpoint.CenterX <= target.CenterX;
            }

            ActiveCheckpoint = target;
            Player.Spawn(target.RespawnPoint);
        }

        Checkpoints.Sort((a, b) => a.CenterX.CompareTo(b.CenterX));
        SnapCamera();
    }

    public void SnapCamera()
    {
        CameraX = CameraTarget();
    }

    // ----- Simulation -------------------------------------------------------

    /// <summary>Advances the world by one fixed step.</summary>
    public void Step(double dt, InputState input)
    {
        Time += dt;

        if (CacheBoostTimer > 0)
        {
            CacheBoostTimer = Math.Max(0, CacheBoostTimer - dt);
            if (CacheBoostTimer == 0)
            {
                Sounds.Add(SoundCue.PowerDown);
            }
        }

        if (FreezeDisplayTimer > 0)
        {
            FreezeDisplayTimer = Math.Max(0, FreezeDisplayTimer - dt);
        }

        UpdatePlayer(dt, input);
        UpdateEnemies(dt);
        Boss?.Update(dt, input);
        UpdateProjectiles(dt);
        ResolveInteractions(input);
        UpdateEffects(dt);
        UpdateCamera(dt);

        Shake = Math.Max(0, Shake - dt * 2.5);
        Flash = Math.Max(0, Flash - dt * 3);
    }

    private void UpdatePlayer(double dt, InputState input)
    {
        var p = Player;
        p.AnimTime += dt;

        if (p.Dead)
        {
            p.DeathTimer = Math.Max(0, p.DeathTimer - dt);
            p.Bounds = p.Bounds.Translate(0, -14 * dt);
            return;
        }

        p.PreviousBottom = p.Bounds.Bottom;
        p.InvulnerableTimer = Math.Max(0, p.InvulnerableTimer - dt);
        p.HurtTimer = Math.Max(0, p.HurtTimer - dt);
        p.InteractTimer = Math.Max(0, p.InteractTimer - dt);
        p.CheerTimer = Math.Max(0, p.CheerTimer - dt);

        var controllable = p.HurtTimer <= 0;

        double target = 0;
        if (controllable)
        {
            if (input.IsHeld(InputButtons.Left))
            {
                target -= PhysicsConfig.RunSpeed;
            }

            if (input.IsHeld(InputButtons.Right))
            {
                target += PhysicsConfig.RunSpeed;
            }
        }

        if (target != 0)
        {
            p.FacingRight = target > 0;
        }

        var accel = p.Grounded
            ? (target == 0 ? PhysicsConfig.GroundFriction : PhysicsConfig.GroundAcceleration)
            : PhysicsConfig.AirAcceleration;
        p.VelX = Physics.MoveToward(p.VelX, target, accel * dt);

        if (controllable && input.WasPressed(InputButtons.Jump))
        {
            p.JumpBufferTimer = PhysicsConfig.JumpBufferTime;
        }
        else
        {
            p.JumpBufferTimer = Math.Max(0, p.JumpBufferTimer - dt);
        }

        if (p.Grounded)
        {
            p.CoyoteTimer = PhysicsConfig.CoyoteTime;
            p.JumpCutApplied = false;
        }
        else
        {
            p.CoyoteTimer = Math.Max(0, p.CoyoteTimer - dt);
        }

        if (controllable && p.JumpBufferTimer > 0 && p.CoyoteTimer > 0)
        {
            p.VelY = -PhysicsConfig.JumpVelocity;
            p.JumpBufferTimer = 0;
            p.CoyoteTimer = 0;
            p.Grounded = false;
            Sounds.Add(SoundCue.Jump);
        }

        if (controllable && !p.Grounded && p.VelY < 0 && !p.JumpCutApplied && !input.IsHeld(InputButtons.Jump))
        {
            p.VelY *= PhysicsConfig.JumpCutFactor;
            p.JumpCutApplied = true;
        }

        p.VelY = Math.Min(p.VelY + PhysicsConfig.Gravity * dt, PhysicsConfig.MaxFallSpeed);

        var wasGrounded = p.Grounded;
        var fallSpeed = p.VelY;
        var result = Physics.Move(p.Bounds, p.VelX * dt, p.VelY * dt, ActiveLevel);
        p.Bounds = result.Bounds;

        if (result.HitWall)
        {
            p.VelX = 0;
        }

        if (result.HitCeiling && p.VelY < 0)
        {
            p.VelY = 0;
        }

        p.Grounded = result.Grounded;
        if (p.Grounded)
        {
            if (p.VelY > 0)
            {
                p.VelY = 0;
            }

            if (!wasGrounded && fallSpeed > 160)
            {
                Sounds.Add(SoundCue.Land);
            }

            if (!IsOverHazard(p.Bounds) && IsWellSupported(p.Bounds))
            {
                p.SampleSafePosition(dt);
            }
        }

        if (p.Bounds.Top > ActiveLevel.Height + 24)
        {
            HandleFall();
        }
    }

    /// <summary>True when both sides of the body stand on ground (not hanging over an edge).</summary>
    private bool IsWellSupported(Rect bounds)
    {
        var probeY = bounds.Bottom + 1;
        return ActiveLevel.HasGroundAt(bounds.Left + bounds.Width * 0.25, probeY)
            && ActiveLevel.HasGroundAt(bounds.Right - bounds.Width * 0.25, probeY);
    }

    private bool IsOverHazard(Rect bounds)
    {
        var probe = bounds.Inflate(2, 2);
        foreach (var hazard in ActiveLevel.Hazards)
        {
            if (probe.Overlaps(hazard))
            {
                return true;
            }
        }

        return false;
    }

    private void HandleFall()
    {
        var p = Player;
        Events.Enqueue(new WorldEvent(WorldEventKind.PlayerFell));

        if (!TryRollback())
        {
            var respawn = p.RecentSafePosition;
            DamagePlayer(p.CenterX, knockback: false, source: null);
            p.Teleport(respawn);
            p.Grounded = true;
        }

        SnapCamera();
    }

    /// <summary>
    /// Applies one damage event to the player. Returns false when no damage was
    /// taken (dead, invulnerable, or the Rollback power-up absorbed it).
    /// </summary>
    public bool DamagePlayer(double sourceX, bool knockback = true, EnemyKind? source = null)
    {
        var p = Player;
        if (p.Dead || p.IsInvulnerable || Boss is { Alive: false })
        {
            return false;
        }

        if (TryRollback())
        {
            return false;
        }

        p.Health = Math.Max(0, p.Health - 1);
        p.InvulnerableTimer = PhysicsConfig.InvulnerabilityTime;
        p.HurtTimer = PhysicsConfig.HurtTime;
        p.CheerTimer = 0;
        Flash = 0.5;
        Shake = Math.Max(Shake, 0.4);
        Sounds.Add(SoundCue.Hurt);

        if (knockback)
        {
            p.VelX = (p.CenterX < sourceX ? -1 : 1) * PhysicsConfig.KnockbackX;
            p.VelY = -PhysicsConfig.KnockbackY;
            p.Grounded = false;
        }

        Events.Enqueue(new WorldEvent(WorldEventKind.PlayerDamaged, p.Health, Enemy: source ?? EnemyKind.Spinner));

        if (p.Health == 0)
        {
            p.Dead = true;
            p.DeathTimer = PhysicsConfig.DeathDelay;
            p.VelX = 0;
            p.VelY = 0;
            Shake = 0.8;
            Sounds.Add(SoundCue.Death);
            Events.Enqueue(new WorldEvent(WorldEventKind.PlayerDied));
        }

        return true;
    }

    private bool TryRollback()
    {
        var p = Player;
        if (!p.RollbackArmed)
        {
            return false;
        }

        p.RollbackArmed = false;
        var target = p.RollbackPosition;
        p.Teleport(target);
        p.Grounded = true;
        p.InvulnerableTimer = PhysicsConfig.InvulnerabilityTime;
        Flash = 0.3;
        Sounds.Add(SoundCue.Rollback);
        AddEffect(EffectKind.Sparkle, new Point(target.X, target.Y - 8), 1.5);
        AddFloatingText("ROLLBACK", target.X, target.Y - 24, "#ff8a3b");
        Events.Enqueue(new WorldEvent(WorldEventKind.RollbackDeployed));
        SnapCamera();
        return true;
    }

    private void UpdateEnemies(double dt)
    {
        var speedFactor = CacheBoostTimer > 0 ? CacheBoostSlowFactor : 1.0;

        foreach (var enemy in Enemies)
        {
            if (!enemy.Alive)
            {
                continue;
            }

            enemy.AnimTime += dt * (enemy.Frozen ? 0 : speedFactor);

            if (enemy.Frozen)
            {
                enemy.FrozenTimer = Math.Max(0, enemy.FrozenTimer - dt);
                continue;
            }

            switch (enemy.Kind)
            {
                case EnemyKind.Spinner:
                    UpdateSpinner(enemy, dt * speedFactor);
                    break;
                case EnemyKind.ExpiredToken:
                    UpdateExpiredToken(enemy, dt, speedFactor);
                    break;
                case EnemyKind.Bug:
                    UpdateBug(enemy, dt, speedFactor);
                    break;
            }
        }

        Enemies.RemoveAll(e => !e.Alive);
    }

    private void UpdateSpinner(Enemy enemy, double dt)
    {
        // Pure patrol (no chasing): predictable, so the player can time a pass or a stomp.
        var direction = enemy.FacingRight ? 1 : -1;
        var x = enemy.CenterX + direction * enemy.Speed * dt;
        if (x <= enemy.PatrolMin)
        {
            x = enemy.PatrolMin;
            enemy.FacingRight = true;
        }
        else if (x >= enemy.PatrolMax)
        {
            x = enemy.PatrolMax;
            enemy.FacingRight = false;
        }

        var bob = Math.Sin(Time * 3 + enemy.Bob) * 3;
        enemy.Bounds = Rect.FromBottomCenter(x, enemy.Origin.Y + bob, enemy.Bounds.Width, enemy.Bounds.Height);
    }

    private void UpdateExpiredToken(Enemy enemy, double dt, double speedFactor)
    {
        enemy.PhaseTimer -= dt;
        if (enemy.PhaseTimer <= 0)
        {
            (enemy.Phase, enemy.PhaseTimer) = enemy.Phase switch
            {
                ExpiredPhase.Visible => (ExpiredPhase.Warning, 0.6),
                ExpiredPhase.Warning => (ExpiredPhase.Gone, 1.4),
                ExpiredPhase.Gone => (ExpiredPhase.Reappearing, 0.6),
                _ => (ExpiredPhase.Visible, 3.0),
            };
        }

        enemy.SteerTimer -= dt;
        if (enemy.SteerTimer <= 0)
        {
            enemy.SteerTimer = 1.2;
            var angle = Math.Atan2(enemy.VelY, enemy.VelX) + (_random.NextDouble() - 0.5) * Math.PI;
            enemy.VelX = Math.Cos(angle) * enemy.Speed;
            enemy.VelY = Math.Sin(angle) * enemy.Speed;
        }

        var moved = enemy.Bounds.Translate(enemy.VelX * dt * speedFactor, enemy.VelY * dt * speedFactor);
        var box = enemy.Box;

        if (moved.Left < box.Left)
        {
            moved = moved.WithPosition(box.Left, moved.Y);
            enemy.VelX = Math.Abs(enemy.VelX);
        }
        else if (moved.Right > box.Right)
        {
            moved = moved.WithPosition(box.Right - moved.Width, moved.Y);
            enemy.VelX = -Math.Abs(enemy.VelX);
        }

        if (moved.Top < box.Top)
        {
            moved = moved.WithPosition(moved.X, box.Top);
            enemy.VelY = Math.Abs(enemy.VelY);
        }
        else if (moved.Bottom > box.Bottom)
        {
            moved = moved.WithPosition(moved.X, box.Bottom - moved.Height);
            enemy.VelY = -Math.Abs(enemy.VelY);
        }

        enemy.Bounds = moved;
    }

    private void UpdateBug(Enemy enemy, double dt, double speedFactor)
    {
        enemy.VelX = (enemy.FacingRight ? 1 : -1) * enemy.Speed * speedFactor;
        enemy.VelY = Math.Min(enemy.VelY + PhysicsConfig.Gravity * dt, PhysicsConfig.MaxFallSpeed);

        var result = Physics.Move(enemy.Bounds, enemy.VelX * dt, enemy.VelY * dt, ActiveLevel);
        enemy.Bounds = result.Bounds;

        if (result.Grounded)
        {
            enemy.VelY = 0;
        }

        var turn = result.HitWall;
        if (result.Grounded)
        {
            var probeX = enemy.FacingRight ? enemy.Bounds.Right + 2 : enemy.Bounds.Left - 2;
            if (!ActiveLevel.HasGroundAt(probeX, enemy.Bounds.Bottom + 2))
            {
                turn = true;
            }
        }

        if (turn)
        {
            enemy.FacingRight = !enemy.FacingRight;
        }

        if (enemy.Bounds.Top > ActiveLevel.Height + 32)
        {
            enemy.Alive = false;
        }
    }

    private void UpdateProjectiles(double dt)
    {
        for (var i = Projectiles.Count - 1; i >= 0; i--)
        {
            var bullet = Projectiles[i];
            bullet.AnimTime += dt;
            bullet.Life -= dt;
            bullet.Bounds = bullet.Bounds.Translate(bullet.VelX * dt, bullet.VelY * dt);

            var b = bullet.Bounds;
            if (bullet.Life <= 0 || b.Right < -16 || b.Left > ActiveLevel.Width + 16 || b.Bottom < -16 || b.Top > ActiveLevel.Height + 16)
            {
                Projectiles.RemoveAt(i);
            }
        }

        for (var i = Shockwaves.Count - 1; i >= 0; i--)
        {
            var wave = Shockwaves[i];
            wave.AnimTime += dt;
            wave.Bounds = wave.Bounds.Translate(wave.VelX * dt, 0);
            if (wave.Bounds.Right < -8 || wave.Bounds.Left > ActiveLevel.Width + 8)
            {
                Shockwaves.RemoveAt(i);
            }
        }
    }

    private void ResolveInteractions(InputState input)
    {
        var p = Player;
        if (p.Dead)
        {
            return;
        }

        // Collectibles
        foreach (var collectible in Collectibles)
        {
            if (!collectible.Collected && collectible.Bounds.Overlaps(p.Bounds))
            {
                Collect(collectible);
            }
        }

        // Enemies: stomp or get hurt
        foreach (var enemy in Enemies)
        {
            if (!enemy.Tangible || !enemy.Bounds.Overlaps(p.Bounds))
            {
                continue;
            }

            if (p.VelY > 0 && p.PreviousBottom <= enemy.Bounds.Top + 4)
            {
                Stomp(enemy);
            }
            else
            {
                DamagePlayer(enemy.CenterX, knockback: true, source: enemy.Kind);
            }

            if (p.Dead)
            {
                return;
            }
        }

        // Hazards
        foreach (var hazard in ActiveLevel.Hazards)
        {
            if (hazard.Overlaps(p.Bounds))
            {
                DamagePlayer(hazard.CenterX, knockback: true, source: null);
                if (p.Dead)
                {
                    return;
                }

                break;
            }
        }

        // Boss projectiles / waves / body
        foreach (var bullet in Projectiles)
        {
            if (bullet.Bounds.Overlaps(p.Bounds) && DamagePlayer(bullet.CenterX, knockback: true, source: null))
            {
                Projectiles.Remove(bullet);
                break;
            }
        }

        foreach (var wave in Shockwaves)
        {
            if (wave.Bounds.Overlaps(p.Bounds))
            {
                DamagePlayer(wave.CenterX, knockback: true, source: null);
                break;
            }
        }

        if (p.Dead)
        {
            return;
        }

        Boss?.ResolvePlayerContact();

        // Interactions (edge-triggered)
        if (input.WasPressed(InputButtons.Interact))
        {
            if (Terminal is not null && Terminal.InteractZone.Overlaps(p.Bounds))
            {
                UseTerminal();
            }
            else if (Boss is not null)
            {
                Boss.TryInteract();
            }
        }

        if (Terminal is not null)
        {
            Terminal.DeniedTimer = Math.Max(0, Terminal.DeniedTimer - PhysicsConfig.FixedStep);
            Terminal.ActivatedTimer = Math.Max(0, Terminal.ActivatedTimer - PhysicsConfig.FixedStep);
        }

        foreach (var checkpoint in Checkpoints)
        {
            if (!checkpoint.Activated && checkpoint.Bounds.Overlaps(p.Bounds) && p.Grounded)
            {
                checkpoint.Activated = true;
                ActiveCheckpoint = checkpoint;
                Sounds.Add(SoundCue.Checkpoint);
                AddEffect(EffectKind.Sparkle, new Point(checkpoint.CenterX, checkpoint.Bounds.Top + 6), 1);
                AddFloatingText($"+{_scoring.Checkpoint}", checkpoint.CenterX, checkpoint.Bounds.Top - 4, "#3dff8a");
                Events.Enqueue(new WorldEvent(WorldEventKind.CheckpointActivated));
            }
        }

        if (Exit is not null)
        {
            Exit.LockedHintTimer = Math.Max(0, Exit.LockedHintTimer - PhysicsConfig.FixedStep);
            Exit.UnlockedTimer = Math.Max(0, Exit.UnlockedTimer - PhysicsConfig.FixedStep);

            if (Exit.Bounds.Overlaps(p.Bounds))
            {
                if (Exit.Unlocked && !Exit.Entered)
                {
                    Exit.Entered = true;
                    Sounds.Add(SoundCue.ExitEnter);
                    Events.Enqueue(new WorldEvent(WorldEventKind.ExitReached));
                }
                else if (!Exit.Unlocked && Exit.LockedHintTimer <= 0)
                {
                    Exit.LockedHintTimer = 3;
                    Sounds.Add(SoundCue.TerminalDenied);
                    Events.Enqueue(new WorldEvent(WorldEventKind.ExitLocked));
                }
            }
        }
    }

    private void Collect(Collectible collectible)
    {
        var p = Player;
        collectible.Collected = true;
        AddEffect(EffectKind.Sparkle, collectible.Origin, 1);

        switch (collectible.Kind)
        {
            case CollectibleKind.Token:
                TokensCollected++;
                Sounds.Add(SoundCue.Token);
                AddFloatingText($"+{_scoring.Token}", collectible.Origin.X, collectible.Origin.Y - 10, "#ffd43b");
                Events.Enqueue(new WorldEvent(WorldEventKind.TokenCollected, TokensCollected, CollectibleKind.Token));
                break;
            case CollectibleKind.Coffee:
                CoffeeConsumed++;
                p.Health = Math.Min(p.MaxHealth, p.Health + 1);
                Sounds.Add(SoundCue.Coffee);
                AddFloatingText($"+{_scoring.Coffee}", collectible.Origin.X, collectible.Origin.Y - 10, "#f6f2ff");
                Events.Enqueue(new WorldEvent(WorldEventKind.CoffeeCollected, p.Health, CollectibleKind.Coffee));
                break;
            case CollectibleKind.CacheBoost:
                CacheBoostTimer = CacheBoostDuration;
                Sounds.Add(SoundCue.PowerUp);
                AddFloatingText("CACHE HIT", collectible.Origin.X, collectible.Origin.Y - 10, "#35d6ff");
                Events.Enqueue(new WorldEvent(WorldEventKind.PowerUpCollected, 0, CollectibleKind.CacheBoost));
                break;
            case CollectibleKind.CircuitBreaker:
            {
                var frozen = 0;
                foreach (var enemy in Enemies)
                {
                    if (enemy.Alive && Point.DistanceBetween(enemy.Bounds.Center, collectible.Origin) <= CircuitBreakerRadius)
                    {
                        enemy.FrozenTimer = CircuitBreakerDuration;
                        frozen++;
                    }
                }

                FreezeDisplayTimer = CircuitBreakerDuration;
                Sounds.Add(SoundCue.PowerUp);
                AddFloatingText("BREAKER", collectible.Origin.X, collectible.Origin.Y - 10, "#ffd43b");
                Events.Enqueue(new WorldEvent(WorldEventKind.PowerUpCollected, frozen, CollectibleKind.CircuitBreaker));
                break;
            }
            case CollectibleKind.Rollback:
                p.RollbackArmed = true;
                Sounds.Add(SoundCue.PowerUp);
                AddFloatingText("ROLLBACK ARMED", collectible.Origin.X, collectible.Origin.Y - 10, "#ff8a3b");
                Events.Enqueue(new WorldEvent(WorldEventKind.PowerUpCollected, 0, CollectibleKind.Rollback));
                break;
        }
    }

    private void Stomp(Enemy enemy)
    {
        enemy.Alive = false;
        Player.VelY = -PhysicsConfig.StompBounceVelocity;
        Player.Grounded = false;
        Shake = Math.Max(Shake, 0.15);
        Sounds.Add(SoundCue.Stomp);
        AddEffect(EffectKind.Pop, enemy.Bounds.Center, 1);
        AddFloatingText($"+{_scoring.Enemy}", enemy.CenterX, enemy.Bounds.Top - 6, "#ff4fa3");
        Events.Enqueue(new WorldEvent(WorldEventKind.EnemyDefeated, 0, Enemy: enemy.Kind));
    }

    private void UseTerminal()
    {
        var terminal = Terminal!;
        Player.InteractTimer = 0.5;

        if (terminal.Activated)
        {
            Events.Enqueue(new WorldEvent(WorldEventKind.TerminalAlreadyActive));
            return;
        }

        if (TokensCollected >= TokensRequired)
        {
            terminal.Activated = true;
            terminal.ActivatedTimer = 2.5;
            Player.CheerTimer = 1.2;
            Sounds.Add(SoundCue.TerminalOk);
            AddEffect(EffectKind.Sparkle, new Point(terminal.CenterX, terminal.Bounds.Top + 8), 1.5);
            AddFloatingText($"+{_scoring.Objective}", terminal.CenterX, terminal.Bounds.Top - 4, "#3dff8a");
            Events.Enqueue(new WorldEvent(WorldEventKind.TerminalActivated));
            UnlockExit(silent: false);
        }
        else
        {
            terminal.DeniedTimer = 0.8;
            Sounds.Add(SoundCue.TerminalDenied);
            Events.Enqueue(new WorldEvent(WorldEventKind.TerminalDenied, TokensRequired - TokensCollected));
        }
    }

    private void UnlockExit(bool silent)
    {
        ExitUnlocked = true;
        if (Exit is not null)
        {
            Exit.Unlocked = true;
            if (!silent)
            {
                Exit.UnlockedTimer = 2;
                Sounds.Add(SoundCue.ExitOpen);
            }
        }
    }

    private void UpdateEffects(double dt)
    {
        for (var i = Effects.Count - 1; i >= 0; i--)
        {
            Effects[i].Timer += dt;
            if (Effects[i].Finished)
            {
                Effects.RemoveAt(i);
            }
        }

        for (var i = FloatingTexts.Count - 1; i >= 0; i--)
        {
            FloatingTexts[i].Timer += dt;
            if (FloatingTexts[i].Finished)
            {
                FloatingTexts.RemoveAt(i);
            }
        }
    }

    public void AddEffect(EffectKind kind, Point center, double scale)
    {
        if (Effects.Count >= 24)
        {
            Effects.RemoveAt(0);
        }

        Effects.Add(new Effect
        {
            Key = NextKey("fx"),
            Kind = kind,
            Scale = scale,
            Bounds = Rect.FromCenter(center.X, center.Y, 16, 16),
        });
    }

    public void AddFloatingText(string text, double x, double y, string color)
    {
        if (FloatingTexts.Count >= 12)
        {
            FloatingTexts.RemoveAt(0);
        }

        FloatingTexts.Add(new FloatingText { Key = NextKey("txt"), Text = text, X = x, Y = y, Color = color });
    }

    private double CameraTarget()
    {
        var p = Player;
        var maxX = Math.Max(0, ActiveLevel.Width - PhysicsConfig.ViewportWidth);
        var target = p.CenterX - PhysicsConfig.ViewportWidth / 2.0 + (p.FacingRight ? 12 : -12);
        return Math.Clamp(target, 0, maxX);
    }

    private void UpdateCamera(double dt)
    {
        var target = CameraTarget();
        CameraX += (target - CameraX) * (1 - Math.Exp(-6 * dt));
        if (Math.Abs(target - CameraX) < 0.05)
        {
            CameraX = target;
        }
    }

    /// <summary>Developer hook: collects an item as if the player touched it.</summary>
    public void ForceCollect(Collectible collectible)
    {
        if (!collectible.Collected)
        {
            Collect(collectible);
        }
    }

    /// <summary>Developer hook: moves the player (feet position) and snaps the camera.</summary>
    public void TeleportPlayer(Point feet)
    {
        Player.Teleport(feet);
        Player.Grounded = true;
        SnapCamera();
    }

    /// <summary>Switches between the primary and alternate arena geometry.</summary>
    public void ToggleLayout()
    {
        if (Level.Alternate is null)
        {
            return;
        }

        ActiveLevel = ReferenceEquals(ActiveLevel, Level) ? Level.Alternate : Level;
        StaticDirty = true;
    }

    // ----- Rendering --------------------------------------------------------

    public void Render(FrameSnapshot snapshot, bool includeStatic)
    {
        if (includeStatic)
        {
            snapshot.Level = BuildLevelSnapshot(ActiveLevel);
        }

        RenderStaticEntities(snapshot);
        RenderCollectibles(snapshot);
        RenderEnemies(snapshot);
        Boss?.Render(snapshot);
        RenderProjectiles(snapshot);
        RenderPlayer(snapshot);
        RenderEffects(snapshot);

        snapshot.Camera = new CameraState(Math.Round(CameraX), 0, Shake);
        snapshot.Flash = Flash;
    }

    public static LevelSnapshot BuildLevelSnapshot(Level level)
    {
        var snapshot = new LevelSnapshot { Width = level.Width, Height = level.Height, Theme = level.Theme };
        snapshot.Tiles.AddRange(level.Tiles);
        return snapshot;
    }

    private void RenderPlayer(FrameSnapshot snapshot)
    {
        var p = Player;
        string frame;
        if (p.Dead)
        {
            frame = Frames.Player.Dead;
        }
        else if (p.HurtTimer > 0)
        {
            frame = Frames.Player.Hurt;
        }
        else if (p.CheerTimer > 0)
        {
            frame = Frames.Player.Cheer((int)(p.AnimTime * 6));
        }
        else if (p.InteractTimer > 0)
        {
            frame = Frames.Player.Interact;
        }
        else if (!p.Grounded)
        {
            frame = p.VelY < 0 ? Frames.Player.Jump : Frames.Player.Fall;
        }
        else if (Math.Abs(p.VelX) > 8)
        {
            frame = Frames.Player.Run((int)(p.AnimTime * 10));
        }
        else
        {
            frame = Frames.Player.Idle((int)(p.AnimTime * 2));
        }

        double alpha = 1;
        if (p.Dead)
        {
            alpha = Math.Clamp(p.DeathTimer / PhysicsConfig.DeathDelay, 0, 1);
        }
        else if (p.IsInvulnerable)
        {
            alpha = (int)(p.AnimTime * 16) % 2 == 0 ? 1 : 0.3;
        }

        Sprite(snapshot, p.Key, frame, p.CenterX, p.Bounds.Bottom, RenderDepth.Player, flipX: !p.FacingRight, alpha: alpha);
    }

    private void RenderEnemies(FrameSnapshot snapshot)
    {
        var slowed = CacheBoostTimer > 0;
        foreach (var enemy in Enemies)
        {
            if (!enemy.Visible)
            {
                continue;
            }

            string frame;
            double alpha = 1;
            var flip = false;

            switch (enemy.Kind)
            {
                case EnemyKind.Spinner:
                    frame = Frames.Spinner.Spin((int)(enemy.AnimTime * 12));
                    break;
                case EnemyKind.Bug:
                    frame = Frames.Bug.Walk((int)(enemy.AnimTime * 8));
                    flip = !enemy.FacingRight;
                    break;
                default:
                    if (enemy.Phase == ExpiredPhase.Reappearing)
                    {
                        frame = Frames.Expired.Warn;
                        alpha = (int)(Time * 12) % 2 == 0 ? 0.8 : 0.35;
                    }
                    else
                    {
                        frame = Frames.Expired.Float((int)(enemy.AnimTime * 4));
                        if (enemy.Phase == ExpiredPhase.Warning)
                        {
                            alpha = (int)(Time * 16) % 2 == 0 ? 1 : 0.4;
                        }
                    }

                    break;
            }

            int? tint = enemy.Frozen ? 0x35d6ff : slowed ? 0x9ad8ff : null;
            Sprite(snapshot, enemy.Key, frame, enemy.CenterX, enemy.Bounds.Bottom + 2, RenderDepth.Enemies, flipX: flip, alpha: alpha, tint: tint);
        }
    }

    private void RenderCollectibles(FrameSnapshot snapshot)
    {
        var index = 0;
        foreach (var collectible in Collectibles)
        {
            index++;
            if (collectible.Collected)
            {
                continue;
            }

            var bob = Math.Sin(Time * 4 + index) * 2;
            var frame = collectible.Kind switch
            {
                CollectibleKind.Token => Frames.Pickups.Token((int)(Time * 8 + index)),
                CollectibleKind.Coffee => Frames.Pickups.Coffee((int)(Time * 4)),
                CollectibleKind.CacheBoost => Frames.Pickups.Cache((int)(Time * 6)),
                CollectibleKind.CircuitBreaker => Frames.Pickups.Breaker((int)(Time * 5)),
                _ => Frames.Pickups.Rollback((int)(Time * 5)),
            };

            Sprite(snapshot, collectible.Key, frame, collectible.Origin.X, collectible.Origin.Y + 8 + bob, RenderDepth.Collectibles);
        }
    }

    private void RenderStaticEntities(FrameSnapshot snapshot)
    {
        if (Terminal is not null)
        {
            var ready = TokensCollected >= TokensRequired;
            var frame = Terminal.DeniedTimer > 0 && (int)(Time * 10) % 2 == 0
                ? Frames.Terminal.Denied
                : Terminal.Activated
                    ? Frames.Terminal.Active((int)(Time * 4))
                    : Frames.Terminal.Locked((int)(Time * (ready ? 6 : 2)));
            Sprite(snapshot, Terminal.Key, frame, Terminal.CenterX, Terminal.Bounds.Bottom, RenderDepth.Interactables);

            if (!Terminal.Activated && !Player.Dead && Terminal.InteractZone.Inflate(14, 0).Overlaps(Player.Bounds))
            {
                Label(snapshot, "prompt", Terminal.CenterX, Terminal.Bounds.Top - 8, "PRESS E", ready ? "#3dff8a" : "#ffd43b", (int)(Time * 3) % 2 == 0 ? 1 : 0.7);
            }
        }

        foreach (var checkpoint in Checkpoints)
        {
            var frame = checkpoint.Activated ? Frames.Checkpoint.On((int)(Time * 4)) : Frames.Checkpoint.Off;
            Sprite(snapshot, checkpoint.Key, frame, checkpoint.CenterX, checkpoint.Bounds.Bottom, RenderDepth.Interactables);
        }

        if (Exit is not null)
        {
            var frame = Exit.Unlocked ? Frames.Exit.Open((int)(Time * 3)) : Frames.Exit.Locked((int)(Time * 2));
            Sprite(snapshot, Exit.Key, frame, Exit.CenterX, Exit.Bounds.Bottom, RenderDepth.Interactables);
        }

        foreach (var repair in RepairTerminals)
        {
            string frame;
            if (repair.FeedbackTimer > 0)
            {
                frame = repair.FeedbackOk ? Frames.Repair.Ok : Frames.Repair.Bad;
            }
            else if (repair.CooldownTimer > 0)
            {
                frame = Frames.Repair.Cooldown;
            }
            else
            {
                frame = Frames.Repair.Idle((int)(Time * 3));
            }

            // Label (8 px pixel font, centred) with its service icon inline on the left, placed in the
            // 11 px gap between the one-way platforms above and the terminal cabinet.
            var labelWidth = repair.Label.Length * 8;
            var labelCenterY = repair.Bounds.Top - 5.5;
            Sprite(snapshot, repair.Key, frame, repair.CenterX, repair.Bounds.Bottom, RenderDepth.Interactables);
            Sprite(snapshot, repair.IconKey, repair.IconFrame, repair.CenterX - labelWidth / 2.0 - 7, labelCenterY + 4, RenderDepth.Interactables);
            Label(snapshot, repair.LabelKey, repair.CenterX, labelCenterY, repair.Label, "#f6f2ff", 1);

            if (!Player.Dead && repair.InteractZone.Inflate(14, 0).Overlaps(Player.Bounds) && Boss is { Alive: true })
            {
                Label(snapshot, "prompt", repair.CenterX, repair.Bounds.Top - 22, "PRESS E", "#ffd43b", (int)(Time * 3) % 2 == 0 ? 1 : 0.7);
            }
        }
    }

    private void RenderProjectiles(FrameSnapshot snapshot)
    {
        foreach (var bullet in Projectiles)
        {
            Sprite(snapshot, bullet.Key, Frames.Effects.Bullet((int)(bullet.AnimTime * 12)), bullet.CenterX, bullet.CenterY, RenderDepth.Projectiles, originY: 0.5);
        }

        foreach (var wave in Shockwaves)
        {
            Sprite(snapshot, wave.Key, Frames.Effects.Wave((int)(wave.AnimTime * 10)), wave.CenterX, wave.Bounds.Bottom, RenderDepth.Projectiles, flipX: wave.VelX < 0);
        }
    }

    private void RenderEffects(FrameSnapshot snapshot)
    {
        foreach (var effect in Effects)
        {
            var frame = effect.Kind == EffectKind.Pop
                ? Frames.Effects.Pop(effect.FrameIndex)
                : Frames.Effects.Sparkle(effect.FrameIndex);
            Sprite(snapshot, effect.Key, frame, effect.CenterX, effect.CenterY, RenderDepth.Effects, scale: effect.Scale, originY: 0.5);
        }

        foreach (var text in FloatingTexts)
        {
            var t = text.Timer / text.Duration;
            Label(snapshot, text.Key, text.X, text.Y - t * FloatingTextRise, text.Text, text.Color, t < 0.6 ? 1 : 1 - (t - 0.6) / 0.4);
        }
    }

    internal static void Sprite(
        FrameSnapshot snapshot,
        string key,
        string frame,
        double x,
        double y,
        int depth,
        bool flipX = false,
        double alpha = 1,
        double scale = 1,
        int? tint = null,
        double originX = 0.5,
        double originY = 1,
        double rotation = 0)
    {
        var sprite = snapshot.AddSprite();
        sprite.Key = key;
        sprite.Frame = frame;
        sprite.X = Math.Round(x);
        sprite.Y = Math.Round(y);
        sprite.FlipX = flipX;
        sprite.Alpha = alpha;
        sprite.Scale = scale;
        sprite.Depth = depth;
        sprite.Tint = tint;
        sprite.OriginX = originX;
        sprite.OriginY = originY;
        sprite.Rotation = rotation;
    }

    internal static void Label(FrameSnapshot snapshot, string key, double x, double y, string text, string color, double alpha)
    {
        var label = snapshot.AddLabel();
        label.Key = key;
        label.X = Math.Round(x);
        label.Y = Math.Round(y);
        label.Text = text;
        label.Color = color;
        label.Alpha = alpha;
        label.Depth = RenderDepth.Labels;
        label.ScrollFactor = 1;
    }
}
