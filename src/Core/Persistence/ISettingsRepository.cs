namespace Videoland4000.Persistence;

/// <summary>Persists <see cref="GameSettings"/>. Must never throw; falls back to defaults.</summary>
public interface ISettingsRepository
{
    Task<GameSettings> LoadAsync();

    Task SaveAsync(GameSettings settings);
}
