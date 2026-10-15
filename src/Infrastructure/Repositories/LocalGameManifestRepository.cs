namespace Videoland4000.Infrastructure.Repositories;

/// <summary>Loads games/diskNNN/game.yaml files from the web root.</summary>
public class LocalGameManifestRepository : IGameManifestRepository
{
    private readonly HttpClient _client;
    private readonly ILogger _logger;

    public LocalGameManifestRepository(HttpClient client, ILogger<LocalGameManifestRepository> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<GameManifest>> LoadGameManifestsAsync()
    {
        List<GameManifest> result = new();

        var diskNumber = 1;
        while (true)
        {
            var manifest = await TryLoadGameManifestAsync(diskNumber++);
            if (manifest is null)
            {
                break;
            }

            result.Add(manifest);
        }

        _logger.LogDebug("Found {Count} game(s)", result.Count);
        return result.AsReadOnly();
    }

    public async Task<GameManifest?> TryLoadGameManifestAsync(int diskNumber)
    {
        var basePath = $"games/disk{diskNumber:000}/";

        try
        {
            var response = await _client.GetAsync($"{basePath}game.yaml?v={DateTime.UtcNow.Ticks}");
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var manifest = YamlLoader.Deserializer.Deserialize<GameManifest>(content);
            manifest.BasePath = basePath;
            manifest.DiskNumber = diskNumber;
            return manifest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading game {DiskNumber}", diskNumber);
            return null;
        }
    }
}
