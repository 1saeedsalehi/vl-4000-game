namespace Videoland4000.BlazorUI.Services;

/// <summary>
/// Holds the current <see cref="GameSettings"/>, persists changes and
/// publishes <see cref="SettingsChanged"/> so the UI, renderer and audio stay in sync.
/// </summary>
public sealed class GameSettingsService
{
    private readonly ISettingsRepository _repository;
    private readonly IMediator _mediator;

    public GameSettings Current { get; private set; } = GameSettings.Default;

    public GameSettingsService(ISettingsRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task LoadAsync(bool prefersReducedMotion)
    {
        var stored = await _repository.LoadAsync();
        Current = stored with { ReducedMotion = prefersReducedMotion };
        await _mediator.PublishAsync(new SettingsChanged(Current));
    }

    public Task ToggleSoundAsync() => UpdateAsync(Current with { SoundEnabled = !Current.SoundEnabled });

    public Task ToggleCrtAsync() => UpdateAsync(Current with { CrtEnabled = !Current.CrtEnabled });

    public async Task UpdateAsync(GameSettings settings)
    {
        Current = settings;
        await _repository.SaveAsync(settings);
        await _mediator.PublishAsync(new SettingsChanged(settings));
    }
}
