namespace Videoland4000.Persistence;

/// <summary>
/// Persists the local high score. Implementations must never throw: when
/// storage is unavailable or corrupted they fall back to in-memory values.
/// </summary>
public interface IHighScoreRepository
{
    Task<int> LoadAsync();

    Task SaveAsync(int highScore);
}
