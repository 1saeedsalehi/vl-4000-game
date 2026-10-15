namespace Videoland4000.Model.Simulation;

public enum WorldEventKind
{
    TokenCollected,
    CoffeeCollected,
    PowerUpCollected,
    EnemyDefeated,
    PlayerDamaged,
    PlayerDied,
    PlayerFell,
    RollbackDeployed,
    TerminalActivated,
    TerminalDenied,
    TerminalAlreadyActive,
    CheckpointActivated,
    ExitReached,
    ExitLocked,
    BossIntroFinished,
    BossPhaseChanged,
    BossStunned,
    BossStunExpired,
    BossHit,
    BossDefeated,
    RepairCorrect,
    RepairWrong,
    RepairCooldown,
    ArenaShifted,
    TrafficWaveIncoming,
}

public enum CollectibleKind
{
    Token,
    Coffee,
    CacheBoost,
    CircuitBreaker,
    Rollback,
}

public enum EnemyKind
{
    Spinner,
    ExpiredToken,
    Bug,
}

public enum ServiceKind
{
    Identity,
    Content,
    Streaming,
}

/// <summary>Something noteworthy that happened during a simulation step. Drained by the session.</summary>
public readonly record struct WorldEvent(
    WorldEventKind Kind,
    int Value = 0,
    CollectibleKind Pickup = CollectibleKind.Token,
    EnemyKind Enemy = EnemyKind.Spinner,
    ServiceKind Service = ServiceKind.Identity);
