namespace Videoland4000.Infrastructure.Repositories;

/// <summary>
/// Stores the high score in window.localStorage. Any storage failure (private
/// mode, quota, corrupted value) silently falls back to an in-memory value.
/// </summary>
public class LocalStorageHighScoreRepository : IHighScoreRepository
{
    public const string StorageKey = "vl4000.highscore";

    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger _logger;
    private int _memoryFallback;

    public LocalStorageHighScoreRepository(IJSRuntime jsRuntime, ILogger<LocalStorageHighScoreRepository> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<int> LoadAsync()
    {
        try
        {
            var raw = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            var value = HighScoreValidator.Sanitize(raw);
            _memoryFallback = Math.Max(_memoryFallback, value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "High score storage unavailable, using in-memory value");
        }

        return _memoryFallback;
    }

    public async Task SaveAsync(int highScore)
    {
        _memoryFallback = HighScoreValidator.Sanitize(highScore);

        try
        {
            await _jsRuntime.InvokeVoidAsync(
                "localStorage.setItem",
                StorageKey,
                _memoryFallback.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "High score could not be persisted");
        }
    }
}
