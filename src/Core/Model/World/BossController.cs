namespace Videoland4000.Model.Simulation;

public enum BossState
{
    Intro,
    Attacking,
    Stunned,
    HitReact,
    Dead,
}

public enum BossAttack
{
    None,
    Aimed,
    Spread,
    Burst,
    Sides,
}

/// <summary>
/// The Infinite Loading Spinner. The boss is invulnerable while spinning; the
/// player must read the diagnostic line, activate the matching repair terminal
/// to stun it, then touch it to deal damage. Three phases by remaining health.
/// </summary>
public sealed class BossController
{
    public const int MaxHealth = 4;
    public const double StunDuration = 5.0;
    public const double IntroDuration = 2.2;
    public const double HitReactDuration = 0.7;
    public const double DeathDuration = 1.8;
    public const double ChargeDuration = 0.5;
    public const double WaveWarningDuration = 0.9;
    public const double ShiftWarningDuration = 0.6;
    public const int MaxBossSpinners = 2;
    public const int MaxBossSpinnersTotal = 4;

    private readonly World _world;
    private readonly Point _spawn;
    private readonly double _arenaCenterX;

    private double _attackTimer;
    private double _spawnTimer;
    private double _waveTimer;
    private double _patternTimer;
    private int _patternStep;
    private int _spawnedTotal;
    private double _chargeTimer;
    private BossAttack _pendingAttack;
    private double _waveWarningTimer;
    private double _shiftWarningTimer;
    private double _stunDescent;
    private ServiceKind? _lastDiagnostic;
    private string? _diagnosticText;
    private (int Phase, ServiceKind Service) _diagnosticTextKey;

    private static readonly double[] PatternThresholds = { 0.0, 1.6, 3.2, 4.6 };

    public BossState State { get; private set; } = BossState.Intro;
    public double StateTimer { get; private set; } = IntroDuration;
    public int Health { get; private set; } = MaxHealth;
    public ServiceKind Diagnostic { get; private set; }
    public double HoverTime { get; private set; }
    public Point Center { get; private set; }
    public double AnimTime { get; private set; }
    public bool Charging => _chargeTimer > 0;
    public bool WaveWarning => _waveWarningTimer > 0;
    public bool ShiftWarning => _shiftWarningTimer > 0;
    public bool Alive => State != BossState.Dead;
    public bool Defeated { get; private set; }

    public int Phase => Health >= 3 ? 1 : Health == 2 ? 2 : 3;

    public int Scale => Phase == 3 ? 3 : 2;

    /// <summary>Collision box (smaller than the visual ring so hits feel fair).</summary>
    public Rect Bounds => Rect.FromCenter(Center.X, Center.Y, 20 * Scale, 20 * Scale);

    public BossController(World world, Point spawn)
    {
        _world = world;
        _spawn = spawn;
        _arenaCenterX = world.Level.Width / 2.0;
        Center = new Point(spawn.X, -40);
        Diagnostic = PickDiagnostic();
        _attackTimer = 2.0;
        _waveTimer = 5.0;
        _spawnTimer = 3.0;
        world.Sounds.Add(SoundCue.BossAppear);
    }

    public string DiagnosticText
    {
        get
        {
            var key = (Phase, Diagnostic);
            if (_diagnosticText is null || _diagnosticTextKey != key)
            {
                _diagnosticTextKey = key;
                _diagnosticText = Phase switch
                {
                    1 => $"DIAGNOSTIC: {ServiceName(Diagnostic)} SERVICE DEGRADED",
                    2 => $"ALERT: {ServiceName(Diagnostic)} SERVICE TIMING OUT",
                    _ => $"LOGS: CPU OK. DISK OK. DNS OK. {ServiceName(Diagnostic)} SERVICE FAILING.",
                };
            }

            return _diagnosticText;
        }
    }

    public static string ServiceName(ServiceKind service) => service switch
    {
        ServiceKind.Identity => "IDENTITY",
        ServiceKind.Content => "CONTENT",
        _ => "STREAMING",
    };

    private double HoverY => Scale == 3 ? 50 : 56;

    private double StunY => _world.FloorTop - 16 * Scale - 6;

    // ----- Update -----------------------------------------------------------

    public void Update(double dt, InputState input)
    {
        AnimTime += dt;
        StateTimer = Math.Max(0, StateTimer - dt);

        foreach (var terminal in _world.RepairTerminals)
        {
            terminal.CooldownTimer = Math.Max(0, terminal.CooldownTimer - dt);
            terminal.FeedbackTimer = Math.Max(0, terminal.FeedbackTimer - dt);
            terminal.AnimTime += dt;
        }

        switch (State)
        {
            case BossState.Intro:
                UpdateIntro(dt);
                break;
            case BossState.Attacking:
                UpdateAttacking(dt);
                break;
            case BossState.Stunned:
                UpdateStunned(dt);
                break;
            case BossState.HitReact:
                if (StateTimer <= 0)
                {
                    AfterHit();
                }

                break;
            case BossState.Dead:
                UpdateDead(dt);
                break;
        }
    }

    private void UpdateIntro(double dt)
    {
        var progress = 1 - StateTimer / IntroDuration;
        var eased = 1 - Math.Pow(1 - progress, 3);
        Center = new Point(_spawn.X, -40 + (HoverY + 40) * eased);

        if (StateTimer <= 0)
        {
            State = BossState.Attacking;
            HoverTime = 0;
            _world.Events.Enqueue(new WorldEvent(WorldEventKind.BossIntroFinished));
        }
    }

    private void UpdateAttacking(double dt)
    {
        HoverTime += dt;
        var speed = Phase switch { 1 => 0.6, 2 => 1.0, _ => 1.4 };
        var amplitude = Phase == 3 ? 70 : 90;
        var x = _arenaCenterX + Math.Sin(HoverTime * speed) * amplitude;
        var y = HoverY + Math.Sin(HoverTime * 2.2) * 4;
        Center = new Point(x, y);

        if (_chargeTimer > 0)
        {
            _chargeTimer -= dt;
            if (_chargeTimer <= 0)
            {
                Fire(_pendingAttack);
                _pendingAttack = BossAttack.None;
            }
        }

        if (_waveWarningTimer > 0)
        {
            _waveWarningTimer -= dt;
            if (_waveWarningTimer <= 0)
            {
                LaunchWaves();
            }
        }

        if (_shiftWarningTimer > 0)
        {
            _shiftWarningTimer -= dt;
            if (_shiftWarningTimer <= 0)
            {
                _world.ToggleLayout();
                _world.Shake = Math.Max(_world.Shake, 0.5);
                _world.Sounds.Add(SoundCue.ArenaShift);
                _world.Events.Enqueue(new WorldEvent(WorldEventKind.ArenaShifted));
            }
        }

        switch (Phase)
        {
            case 1:
                _attackTimer -= dt;
                if (_attackTimer <= 0)
                {
                    _attackTimer = 2.2;
                    BeginCharge(BossAttack.Aimed);
                }

                break;
            case 2:
                _attackTimer -= dt;
                if (_attackTimer <= 0)
                {
                    _attackTimer = 1.7;
                    BeginCharge(BossAttack.Spread);
                }

                _spawnTimer -= dt;
                if (_spawnTimer <= 0)
                {
                    _spawnTimer = 6.0;
                    SpawnBufferingEnemy();
                }

                _waveTimer -= dt;
                if (_waveTimer <= 0 && _waveWarningTimer <= 0)
                {
                    _waveTimer = 7.0;
                    BeginWaveWarning();
                }

                break;
            default:
                UpdateInfiniteLoop(dt);
                break;
        }
    }

    /// <summary>Phase 3: a fixed, recognisable repeating pattern (while(true) { buffer(); }).</summary>
    private void UpdateInfiniteLoop(double dt)
    {
        _patternTimer += dt;
        if (_patternStep < PatternThresholds.Length && _patternTimer >= PatternThresholds[_patternStep])
        {
            switch (_patternStep)
            {
                case 0:
                    BeginCharge(BossAttack.Sides);
                    break;
                case 1:
                    BeginWaveWarning();
                    break;
                case 2:
                    if (_world.Level.Alternate is not null)
                    {
                        _shiftWarningTimer = ShiftWarningDuration;
                        _world.Sounds.Add(SoundCue.Warning);
                    }

                    break;
                case 3:
                    BeginCharge(BossAttack.Spread);
                    break;
            }

            _patternStep++;
        }

        if (_patternTimer >= 6.2)
        {
            _patternTimer = 0;
            _patternStep = 0;
        }
    }

    private void UpdateStunned(double dt)
    {
        _stunDescent = Math.Min(1, _stunDescent + dt * 2.5);
        var y = HoverY + (StunY - HoverY) * _stunDescent;
        Center = new Point(Center.X, y + Math.Sin(AnimTime * 10) * 1);

        if (StateTimer <= 0)
        {
            State = BossState.Attacking;
            Diagnostic = PickDiagnostic();
            _world.Events.Enqueue(new WorldEvent(WorldEventKind.BossStunExpired));
            _world.Sounds.Add(SoundCue.Warning);
        }
    }

    private void UpdateDead(double dt)
    {
        Center = new Point(Center.X + Math.Sin(AnimTime * 40) * 1.5, Center.Y + dt * 6);
        if ((int)(AnimTime * 8) % 2 == 0 && _world.Effects.Count < 20)
        {
            var offset = new Point((_world.Random.NextDouble() - 0.5) * 24 * Scale, (_world.Random.NextDouble() - 0.5) * 24 * Scale);
            _world.AddEffect(EffectKind.Pop, Center + offset, 1 + _world.Random.NextDouble());
        }

        if (StateTimer <= 0 && !Defeated)
        {
            Defeated = true;
            _world.Events.Enqueue(new WorldEvent(WorldEventKind.BossDefeated));
        }
    }

    // ----- Attacks ----------------------------------------------------------

    private void BeginCharge(BossAttack attack)
    {
        if (_chargeTimer > 0)
        {
            return;
        }

        _pendingAttack = attack;
        _chargeTimer = ChargeDuration;
        _world.Sounds.Add(SoundCue.BossCharge);
    }

    private void Fire(BossAttack attack)
    {
        var speed = Phase switch { 1 => 75, 2 => 90, _ => 100 };
        var player = _world.Player;
        var toPlayer = (player.Bounds.Center - Center).Normalized();
        if (toPlayer.Length < 0.5)
        {
            toPlayer = new Point(0, 1);
        }

        switch (attack)
        {
            case BossAttack.Aimed:
                SpawnBullet(toPlayer, speed);
                break;
            case BossAttack.Spread:
                SpawnBullet(Rotate(toPlayer, -0.35), speed);
                SpawnBullet(toPlayer, speed);
                SpawnBullet(Rotate(toPlayer, 0.35), speed);
                break;
            case BossAttack.Burst:
                SpawnBullet(toPlayer, speed + 20);
                SpawnBullet(Rotate(toPlayer, -0.6), speed);
                SpawnBullet(Rotate(toPlayer, 0.6), speed);
                break;
            case BossAttack.Sides:
                SpawnBullet(new Point(-0.8, 0.6).Normalized(), speed);
                SpawnBullet(new Point(0.8, 0.6).Normalized(), speed);
                SpawnBullet(toPlayer, speed);
                break;
        }

        _world.Sounds.Add(SoundCue.BossShoot);
    }

    private static Point Rotate(Point v, double angle)
    {
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        return new Point(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
    }

    private void SpawnBullet(Point direction, double speed)
    {
        if (_world.Projectiles.Count >= 16)
        {
            return;
        }

        _world.Projectiles.Add(new Projectile
        {
            Key = $"bullet{_bulletCounter++}",
            Bounds = Rect.FromCenter(Center.X, Center.Y, 6, 6),
            VelX = direction.X * speed,
            VelY = direction.Y * speed,
            Life = 5,
        });
    }

    private int _bulletCounter;
    private int _waveCounter;

    private void BeginWaveWarning()
    {
        if (_waveWarningTimer > 0)
        {
            return;
        }

        _waveWarningTimer = WaveWarningDuration;
        _world.Sounds.Add(SoundCue.Warning);
        _world.Events.Enqueue(new WorldEvent(WorldEventKind.TrafficWaveIncoming));
    }

    private void LaunchWaves()
    {
        var floor = _world.FloorTop;
        foreach (var direction in new[] { -1, 1 })
        {
            _world.Shockwaves.Add(new Shockwave
            {
                Key = $"wave{_waveCounter++}",
                Bounds = new Rect(Center.X - 7 + direction * 10, floor - 12, 14, 12),
                VelX = direction * 110,
            });
        }

        _world.Shake = Math.Max(_world.Shake, 0.35);
        _world.Sounds.Add(SoundCue.Wave);
    }

    private void SpawnBufferingEnemy()
    {
        var alive = _world.Enemies.Count(e => e.Alive && e.SpawnedByBoss);
        if (alive >= MaxBossSpinners || _spawnedTotal >= MaxBossSpinnersTotal)
        {
            return;
        }

        _spawnedTotal++;
        var feet = new Point(Center.X, _world.FloorTop - 44);
        _world.SpawnSpinner(feet, patrolRange: 140, speed: 32, fromBoss: true);
        _world.AddEffect(EffectKind.Pop, new Point(feet.X, feet.Y - 6), 1);
        _world.Sounds.Add(SoundCue.Warning);
    }

    // ----- Interaction ------------------------------------------------------

    /// <summary>Player pressed Interact; activates a repair terminal when in reach. Returns true when handled.</summary>
    public bool TryInteract()
    {
        var player = _world.Player;
        RepairTerminal? terminal = null;
        foreach (var candidate in _world.RepairTerminals)
        {
            if (candidate.InteractZone.Overlaps(player.Bounds))
            {
                terminal = candidate;
                break;
            }
        }

        if (terminal is null)
        {
            return false;
        }

        player.InteractTimer = 0.5;

        if (terminal.CooldownTimer > 0 || State != BossState.Attacking)
        {
            _world.Sounds.Add(SoundCue.TerminalDenied);
            _world.Events.Enqueue(new WorldEvent(WorldEventKind.RepairCooldown, Service: terminal.Service));
            return true;
        }

        if (terminal.Service == Diagnostic)
        {
            Stun();
            terminal.FeedbackTimer = 1.2;
            terminal.FeedbackOk = true;
            foreach (var other in _world.RepairTerminals)
            {
                other.CooldownTimer = Math.Max(other.CooldownTimer, 1.0);
            }

            _world.Sounds.Add(SoundCue.TerminalOk);
            _world.Events.Enqueue(new WorldEvent(WorldEventKind.RepairCorrect, Service: terminal.Service));
        }
        else
        {
            terminal.CooldownTimer = 2.5;
            terminal.FeedbackTimer = 0.8;
            terminal.FeedbackOk = false;
            _world.Sounds.Add(SoundCue.TerminalDenied);
            _world.Events.Enqueue(new WorldEvent(WorldEventKind.RepairWrong, Service: terminal.Service));
            BeginCharge(BossAttack.Burst);
        }

        return true;
    }

    /// <summary>Developer hook: stuns the boss as if the correct terminal was used.</summary>
    public void ForceStun()
    {
        if (State == BossState.Attacking)
        {
            Stun();
        }
    }

    private void Stun()
    {
        State = BossState.Stunned;
        StateTimer = StunDuration;
        _stunDescent = 0;
        _chargeTimer = 0;
        _pendingAttack = BossAttack.None;
        _waveWarningTimer = 0;
        _shiftWarningTimer = 0;
        _world.Shake = Math.Max(_world.Shake, 0.5);
        _world.Sounds.Add(SoundCue.BossStun);
        _world.Events.Enqueue(new WorldEvent(WorldEventKind.BossStunned));
    }

    /// <summary>Resolves body contact between the boss and the player for the current step.</summary>
    public void ResolvePlayerContact()
    {
        var player = _world.Player;
        if (player.Dead || !Bounds.Overlaps(player.Bounds))
        {
            return;
        }

        switch (State)
        {
            case BossState.Attacking:
            case BossState.HitReact:
                _world.DamagePlayer(Center.X, knockback: true, source: EnemyKind.Spinner);
                break;
            case BossState.Stunned:
                TakeHit();
                break;
        }
    }

    private void TakeHit()
    {
        var player = _world.Player;
        Health--;
        State = BossState.HitReact;
        StateTimer = HitReactDuration;
        player.VelY = -PhysicsConfig.StompBounceVelocity;
        player.Grounded = false;
        // The final hit protects the player for the whole death animation: a won fight can never end in a game over.
        player.InvulnerableTimer = Math.Max(player.InvulnerableTimer, Health <= 0 ? HitReactDuration + DeathDuration + 1.0 : 1.0);
        _world.Shake = 1.0;
        _world.Flash = 0.4;
        _world.Sounds.Add(SoundCue.BossHit);
        _world.AddEffect(EffectKind.Pop, Center, 2);
        _world.AddFloatingText("HIT!", Center.X, Center.Y - 12 * Scale, "#ff4fa3");
        _world.Events.Enqueue(new WorldEvent(WorldEventKind.BossHit, Health));
    }

    private void AfterHit()
    {
        if (Health <= 0)
        {
            State = BossState.Dead;
            StateTimer = DeathDuration;
            _world.Projectiles.Clear();
            _world.Shockwaves.Clear();

            // The incident is over: everything the spinner spawned pops with it (no score for these).
            foreach (var enemy in _world.Enemies)
            {
                if (enemy.Alive && enemy.SpawnedByBoss)
                {
                    enemy.Alive = false;
                    _world.AddEffect(EffectKind.Pop, enemy.Bounds.Center, 1);
                }
            }

            _world.Sounds.Add(SoundCue.BossDefeat);
            _world.Shake = 1.0;
            return;
        }

        var previousPhase = Health + 1 >= 3 ? 1 : Health + 1 == 2 ? 2 : 3;
        State = BossState.Attacking;
        Diagnostic = PickDiagnostic();
        _attackTimer = 1.5;
        _waveTimer = 3.0;
        _spawnTimer = 2.0;
        _patternTimer = 0;
        _patternStep = 0;

        if (Phase != previousPhase)
        {
            _world.Events.Enqueue(new WorldEvent(WorldEventKind.BossPhaseChanged, Phase));
            _world.Sounds.Add(SoundCue.BossAppear);
        }
    }

    private ServiceKind PickDiagnostic()
    {
        var options = new List<ServiceKind> { ServiceKind.Identity, ServiceKind.Content, ServiceKind.Streaming };
        if (_lastDiagnostic is { } last)
        {
            options.Remove(last);
        }

        var picked = options[_world.Random.Next(0, options.Count)];
        _lastDiagnostic = picked;
        return picked;
    }

    // ----- Rendering --------------------------------------------------------

    public void Render(FrameSnapshot snapshot)
    {
        string frame;
        double alpha = 1;
        switch (State)
        {
            case BossState.Stunned:
                frame = Frames.Boss.Stun((int)(AnimTime * 3));
                break;
            case BossState.HitReact:
                frame = (int)(AnimTime * 20) % 2 == 0 ? Frames.Boss.Hit : Frames.Boss.Spin((int)(AnimTime * 10));
                break;
            case BossState.Dead:
                frame = Frames.Boss.Dead;
                alpha = Math.Clamp(StateTimer / DeathDuration, 0.2, 1);
                break;
            default:
                frame = Charging && (int)(AnimTime * 12) % 2 == 0
                    ? Frames.Boss.Charge((int)(AnimTime * 6))
                    : Frames.Boss.Spin((int)(AnimTime * (8 + Phase * 4)));
                break;
        }

        World.Sprite(snapshot, "boss", frame, Center.X, Center.Y, RenderDepth.Boss, alpha: alpha, scale: Scale, originY: 0.5);

        if (WaveWarning)
        {
            var floor = _world.FloorTop;
            for (var i = 0; i < 10; i++)
            {
                World.Sprite(snapshot, $"warn{i}", Frames.Effects.Warn((int)(AnimTime * 8)), 16 + i * 32, floor, RenderDepth.Effects, alpha: 0.9);
            }
        }

        if (ShiftWarning)
        {
            var i = 0;
            foreach (var platform in _world.ActiveLevel.OneWayPlatforms)
            {
                World.Sprite(snapshot, $"shift{i++}", Frames.Effects.Warn((int)(AnimTime * 10)), platform.CenterX, platform.Top - 2, RenderDepth.Effects, alpha: 0.9);
            }
        }
    }
}
