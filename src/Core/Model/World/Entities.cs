namespace Videoland4000.Model.Simulation;

/// <summary>Base class for everything that lives in the world. Bounds is the collision box.</summary>
public abstract class Entity
{
    public string Key { get; init; } = string.Empty;
    public Rect Bounds { get; set; }
    public double AnimTime { get; set; }

    public double CenterX => Bounds.CenterX;
    public double CenterY => Bounds.CenterY;
    public Point Feet => Bounds.BottomCenter;
}

public sealed class Player : Entity
{
    private const int SafeHistoryLength = 8;
    private const double SafeSampleInterval = 0.25;

    private readonly Point[] _safeHistory = new Point[SafeHistoryLength];
    private int _safeCount;
    private int _safeHead;
    private double _safeSampleTimer;

    public double VelX { get; set; }
    public double VelY { get; set; }
    public bool Grounded { get; set; }
    public bool FacingRight { get; set; } = true;
    public int Health { get; set; }
    public int MaxHealth { get; }
    public double InvulnerableTimer { get; set; }
    public double HurtTimer { get; set; }
    public double CoyoteTimer { get; set; }
    public double JumpBufferTimer { get; set; }
    public double InteractTimer { get; set; }
    public double CheerTimer { get; set; }
    public bool JumpCutApplied { get; set; }
    public bool Dead { get; set; }
    public double DeathTimer { get; set; }
    public bool RollbackArmed { get; set; }
    public double PreviousBottom { get; set; }

    /// <summary>Most recent grounded, hazard-free feet position (pit respawn).</summary>
    public Point LastSafePosition { get; private set; }

    public bool IsInvulnerable => InvulnerableTimer > 0;

    public Player(int maxHealth)
    {
        Key = "player";
        MaxHealth = maxHealth;
        Health = maxHealth;
    }

    public void Spawn(Point feet)
    {
        Bounds = Rect.FromBottomCenter(feet.X, feet.Y, PhysicsConfig.PlayerWidth, PhysicsConfig.PlayerHeight);
        VelX = VelY = 0;
        Grounded = true;
        LastSafePosition = feet;
        PreviousBottom = Bounds.Bottom;
        _safeCount = 0;
        _safeHead = 0;
        _safeSampleTimer = 0;
        RecordSafe(feet, force: true);
    }

    public void Teleport(Point feet)
    {
        Bounds = Rect.FromBottomCenter(feet.X, feet.Y, PhysicsConfig.PlayerWidth, PhysicsConfig.PlayerHeight);
        VelX = VelY = 0;
        PreviousBottom = Bounds.Bottom;
    }

    public void SampleSafePosition(double dt)
    {
        LastSafePosition = Feet;
        _safeSampleTimer -= dt;
        if (_safeSampleTimer <= 0)
        {
            _safeSampleTimer = SafeSampleInterval;
            RecordSafe(Feet, force: false);
        }
    }

    /// <summary>Most recent history sample (a few pixels behind the edge): used to respawn after a fall.</summary>
    public Point RecentSafePosition
    {
        get
        {
            if (_safeCount == 0)
            {
                return LastSafePosition;
            }

            return _safeHistory[(_safeHead - 1 + SafeHistoryLength) % SafeHistoryLength];
        }
    }

    /// <summary>Grounded position from roughly two seconds ago (used by the Rollback power-up).</summary>
    public Point RollbackPosition
    {
        get
        {
            if (_safeCount == 0)
            {
                return LastSafePosition;
            }

            var oldest = (_safeHead - _safeCount + SafeHistoryLength) % SafeHistoryLength;
            return _safeHistory[oldest];
        }
    }

    private void RecordSafe(Point feet, bool force)
    {
        if (!force && _safeCount > 0)
        {
            var newest = _safeHistory[(_safeHead - 1 + SafeHistoryLength) % SafeHistoryLength];
            if (Point.DistanceBetween(newest, feet) < 4)
            {
                return; // standing still: keep the history meaningful
            }
        }

        _safeHistory[_safeHead] = feet;
        _safeHead = (_safeHead + 1) % SafeHistoryLength;
        _safeCount = Math.Min(_safeCount + 1, SafeHistoryLength);
    }
}

public enum ExpiredPhase
{
    Visible,
    Warning,
    Gone,
    Reappearing,
}

public sealed class Enemy : Entity
{
    public EnemyKind Kind { get; init; }
    public bool Alive { get; set; } = true;
    public double VelX { get; set; }
    public double VelY { get; set; }
    public bool FacingRight { get; set; }
    public double Speed { get; init; }
    public double FrozenTimer { get; set; }
    public Point Origin { get; init; }
    public double PatrolMin { get; init; }
    public double PatrolMax { get; init; }
    /// <summary>Movement box for the erratic Expired Token.</summary>
    public Rect Box { get; init; }
    public ExpiredPhase Phase { get; set; } = ExpiredPhase.Visible;
    public double PhaseTimer { get; set; }
    public double SteerTimer { get; set; }
    public bool SpawnedByBoss { get; init; }
    public double Bob { get; init; }

    public bool Frozen => FrozenTimer > 0;

    /// <summary>Whether the enemy can currently touch (damage) or be stomped by the player.</summary>
    public bool Tangible => Alive && (Kind != EnemyKind.ExpiredToken || Phase is ExpiredPhase.Visible or ExpiredPhase.Warning);

    public bool Visible => Alive && (Kind != EnemyKind.ExpiredToken || Phase != ExpiredPhase.Gone);
}

public sealed class Collectible : Entity
{
    public CollectibleKind Kind { get; init; }
    public string Id { get; init; } = string.Empty;
    public Point Origin { get; init; }
    public bool Collected { get; set; }
}

public sealed class AuthTerminal : Entity
{
    public bool Activated { get; set; }
    public double DeniedTimer { get; set; }
    public double ActivatedTimer { get; set; }

    public Rect InteractZone => Bounds.Inflate(10, 2);
}

public sealed class Checkpoint : Entity
{
    public string Id { get; init; } = string.Empty;
    public bool Activated { get; set; }
    public Point RespawnPoint { get; init; }
}

public sealed class ExitDoor : Entity
{
    public bool Unlocked { get; set; }
    public bool Entered { get; set; }
    public double LockedHintTimer { get; set; }
    public double UnlockedTimer { get; set; }
}

public sealed class RepairTerminal : Entity
{
    public ServiceKind Service { get; init; }
    public double CooldownTimer { get; set; }
    public double FeedbackTimer { get; set; }
    public bool FeedbackOk { get; set; }

    private string? _labelKey;
    private string? _iconKey;

    public Rect InteractZone => Bounds.Inflate(10, 2);

    public string LabelKey => _labelKey ??= Key + "lbl";

    public string IconKey => _iconKey ??= Key + "icon";

    public string Label => Service switch
    {
        ServiceKind.Identity => "IDENTITY",
        ServiceKind.Content => "CONTENT",
        _ => "STREAMING",
    };

    public string IconFrame => Service switch
    {
        ServiceKind.Identity => Frames.Icon.Identity,
        ServiceKind.Content => Frames.Icon.Content,
        _ => Frames.Icon.Streaming,
    };
}

public sealed class Projectile : Entity
{
    public double VelX { get; set; }
    public double VelY { get; set; }
    public double Life { get; set; } = 5;
}

public sealed class Shockwave : Entity
{
    public double VelX { get; set; }
}

public enum EffectKind
{
    Pop,
    Sparkle,
}

public sealed class Effect : Entity
{
    public EffectKind Kind { get; init; }
    public double Duration { get; init; } = 0.36;
    public double Timer { get; set; }
    public double Scale { get; init; } = 1;

    public bool Finished => Timer >= Duration;
    public int FrameIndex => Math.Clamp((int)(Timer / Duration * 3), 0, 2);
}

public sealed class FloatingText
{
    public string Key { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string Color { get; init; } = "#ffd43b";
    public double X { get; init; }
    public double Y { get; init; }
    public double Duration { get; init; } = 0.9;
    public double Timer { get; set; }

    public bool Finished => Timer >= Duration;
}
