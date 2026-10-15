namespace Videoland4000.Messages.Events;

/// <summary>The game phase changed (menus, gameplay, pause, game over, victory).</summary>
public record PhaseChanged(GamePhase Phase, GamePhase Previous) : IEvent;

/// <summary>HUD values changed (published only on change, never every frame).</summary>
public record HudChanged(HudState Hud) : IEvent;

/// <summary>A humorous in-game message should be shown.</summary>
public record MessageShown(GameMessage Message) : IEvent;

/// <summary>Victory statistics are available (published right before the Victory phase).</summary>
public record VictoryAchieved(VictoryStats Stats) : IEvent;

/// <summary>Game over details (final score) are available.</summary>
public record GameEnded(int FinalScore, int HighScore, bool CheckpointAvailable) : IEvent;

/// <summary>Player-facing settings changed.</summary>
public record SettingsChanged(GameSettings Settings) : IEvent;

/// <summary>The game content (manifest, atlas, levels) finished loading.</summary>
public record ContentLoaded(GameManifest Manifest) : IEvent;

/// <summary>Loading the game content failed; the UI shows an error.</summary>
public record ContentFailed(string Error) : IEvent;
