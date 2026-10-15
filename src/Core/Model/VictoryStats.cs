namespace Videoland4000.Model;

/// <summary>
/// Fictional flavour statistics shown on the victory screen. They are
/// randomised for replayability and are NOT real metrics of any kind.
/// </summary>
public sealed record VictoryStats(
    int ViewersOnline,
    int IncidentsResolved,
    int CoffeeConsumed,
    int FinalScore,
    int HighScore,
    bool IsNewHighScore);
