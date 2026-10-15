namespace Videoland4000.Model;

/// <summary>
/// Explicit top-level game state. All transitions go through
/// <see cref="Session.GameSession"/> so that pausing, dying, winning and
/// restarting can never leave stale gameplay logic running.
/// </summary>
public enum GamePhase
{
    /// <summary>Retro boot sequence is playing (UI driven).</summary>
    Booting,
    /// <summary>Title screen / main menu (attract scene renders in the canvas).</summary>
    MainMenu,
    /// <summary>Short level introduction card before gameplay starts.</summary>
    LevelIntro,
    /// <summary>Level gameplay is running.</summary>
    Playing,
    /// <summary>Gameplay (level or boss) is paused.</summary>
    Paused,
    /// <summary>Final boss encounter is running.</summary>
    BossEncounter,
    /// <summary>Boss defeated, victory screen is shown.</summary>
    Victory,
    /// <summary>Player health reached zero, game-over screen is shown.</summary>
    GameOver,
}
