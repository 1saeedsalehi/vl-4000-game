namespace Videoland4000.Model;

/// <summary>
/// Everything the HUD needs to render. Published as <see cref="HudChanged"/>
/// only when at least one value differs from the previous frame, so the DOM
/// is never updated every frame.
/// </summary>
public sealed record HudState
{
    public int Score { get; init; }
    public int HighScore { get; init; }
    public int Tokens { get; init; }
    public int TokensRequired { get; init; }
    public int Health { get; init; }
    public int MaxHealth { get; init; }
    public int Coffee { get; init; }
    public string LevelName { get; init; } = string.Empty;
    public string Objective { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool BossActive { get; init; }
    public int BossHealth { get; init; }
    public int BossMaxHealth { get; init; }
    public int BossPhase { get; init; }
    public string BossDiagnostic { get; init; } = string.Empty;
    public double CacheBoostSeconds { get; init; }
    public double FreezeSeconds { get; init; }
    public bool RollbackArmed { get; init; }

    public static readonly HudState Empty = new();
}
