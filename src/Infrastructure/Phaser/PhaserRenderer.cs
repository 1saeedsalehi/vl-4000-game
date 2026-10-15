namespace Videoland4000.Infrastructure.Phaser;

/// <summary>
/// Owns the lifetime of the Phaser game instance. Create once per page;
/// dispose on navigation to avoid duplicate game loops.
/// </summary>
public sealed class PhaserRenderer : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private DotNetObjectReference<FrameTickHandler>? _tickRef;
    private bool _started;

    public int Width { get; }
    public int Height { get; }

    public PhaserRenderer(IJSRuntime jsRuntime, int width, int height)
    {
        _jsRuntime = jsRuntime;
        Width = width;
        Height = height;
    }

    /// <summary>Starts Phaser; completes when the scene is created and assets are loaded.</summary>
    public async Task StartAsync(
        string containerElementId,
        string textureUrl,
        string atlasUrl,
        Func<double, InputButtons, FrameSnapshot> tick,
        Func<string, string>? debug = null)
    {
        if (_started)
        {
            throw new InvalidOperationException("Phaser has already been started.");
        }

        _started = true;

        var ready = new TaskCompletionSource();
        var handler = new FrameTickHandler(tick, () => ready.TrySetResult(), debug);
        _tickRef = DotNetObjectReference.Create(handler);

        await _jsRuntime.InvokeVoidAsync(
            PhaserConstants.Functions.StartPhaser,
            containerElementId,
            Width,
            Height,
            textureUrl,
            atlasUrl,
            _tickRef);

        await ready.Task;

        if (debug is not null)
        {
            await _jsRuntime.InvokeVoidAsync(PhaserConstants.Functions.RegisterDebug, _tickRef);
        }
    }

    public ValueTask SetPausedAsync(bool paused) =>
        _jsRuntime.InvokeVoidAsync(PhaserConstants.Functions.SetGamePaused, paused);

    public ValueTask SetSoundEnabledAsync(bool enabled) =>
        _jsRuntime.InvokeVoidAsync(PhaserConstants.Functions.SetSoundEnabled, enabled);

    public ValueTask SetReducedMotionAsync(bool reduced) =>
        _jsRuntime.InvokeVoidAsync(PhaserConstants.Functions.SetReducedMotion, reduced);

    public ValueTask PlaySoundAsync(string cue) =>
        _jsRuntime.InvokeVoidAsync(PhaserConstants.Functions.PlaySound, cue);

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_started)
            {
                await _jsRuntime.InvokeVoidAsync(PhaserConstants.Functions.DestroyPhaser);
            }
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or TaskCanceledException or ObjectDisposedException)
        {
            // Page is going away or the JS side is already gone; nothing left to clean up there.
        }
        finally
        {
            _tickRef?.Dispose();
            _tickRef = null;
            _started = false;
        }
    }
}
