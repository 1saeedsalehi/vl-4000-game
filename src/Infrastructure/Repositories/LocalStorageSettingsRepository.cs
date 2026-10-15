namespace Videoland4000.Infrastructure.Repositories;

/// <summary>Persists player settings in localStorage; never throws.</summary>
public class LocalStorageSettingsRepository : ISettingsRepository
{
    public const string StorageKey = "vl4000.settings";

    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;
    private GameSettings _memoryFallback = GameSettings.Default;

    public LocalStorageSettingsRepository(IJSRuntime jsRuntime, ILogger<LocalStorageSettingsRepository> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<GameSettings> LoadAsync()
    {
        try
        {
            var raw = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var stored = JsonSerializer.Deserialize<StoredSettings>(raw);
                if (stored is not null)
                {
                    _memoryFallback = _memoryFallback with
                    {
                        SoundEnabled = stored.Sound ?? true,
                        CrtEnabled = stored.Crt ?? true,
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Settings storage unavailable, using defaults");
        }

        return _memoryFallback;
    }

    public async Task SaveAsync(GameSettings settings)
    {
        _memoryFallback = settings;

        try
        {
            var raw = JsonSerializer.Serialize(new StoredSettings
            {
                Sound = settings.SoundEnabled,
                Crt = settings.CrtEnabled,
            });

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, raw);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Settings could not be persisted");
        }
    }

    private sealed class StoredSettings
    {
        public bool? Sound { get; set; }
        public bool? Crt { get; set; }
    }
}
