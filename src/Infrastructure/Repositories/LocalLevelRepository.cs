namespace Videoland4000.Infrastructure.Repositories;

/// <summary>Loads level YAML files relative to the game disk folder.</summary>
public class LocalLevelRepository : ILevelRepository
{
    private readonly HttpClient _client;

    public LocalLevelRepository(HttpClient client)
    {
        _client = client;
    }

    public async Task<LevelSpec> LoadLevelAsync(GameManifest manifest, string relativePath)
    {
        var content = await _client.GetStringAsync(
            $"{manifest.BasePath}{relativePath}?v={DateTime.UtcNow.Ticks}");

        return YamlLoader.Deserializer.Deserialize<LevelSpec>(content);
    }
}
