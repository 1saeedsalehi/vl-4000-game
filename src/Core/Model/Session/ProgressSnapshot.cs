namespace Videoland4000.Model.Session;

/// <summary>
/// Objective progress saved when a checkpoint is activated (or the exit is
/// reached). Restoring it never duplicates collectibles or re-awards points:
/// collected items stay collected and the score reverts to the saved value.
/// </summary>
public sealed record ProgressSnapshot(
    int Score,
    IReadOnlySet<string> CollectedIds,
    int CoffeeConsumed,
    bool TerminalActivated,
    bool CheckpointActivated,
    bool InBossArena,
    string? CheckpointId = null);
