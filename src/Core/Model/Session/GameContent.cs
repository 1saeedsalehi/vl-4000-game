namespace Videoland4000.Model.Session;

/// <summary>Everything loaded from the game disk that the session needs to run.</summary>
public sealed record GameContent(GameManifest Manifest, LevelSpec Level, LevelSpec BossArena);
